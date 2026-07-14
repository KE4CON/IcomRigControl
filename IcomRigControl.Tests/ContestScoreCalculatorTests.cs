using System;
using System.Collections.Generic;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class ContestScoreCalculatorTests
{
    [Fact]
    public void CalculateScore_EmptyLog_ReturnsZero()
    {
        var fd = ContestCatalog.FieldDay;
        var result = ContestScoreCalculator.CalculateScore(fd, new List<QsoRecord>());

        Assert.Equal(0, result.TotalPoints);
        Assert.Equal(0, result.QsoCount);
    }

    [Fact]
    public void CalculateScore_SinglePhoneQso_OnePoint()
    {
        var fd = ContestCatalog.FieldDay;
        var qsos = new List<QsoRecord>
        {
            new("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59",
                ContestExchangeSent: "3A GA", ContestExchangeReceived: "5A OH")
        };

        var result = ContestScoreCalculator.CalculateScore(fd, qsos);

        Assert.Equal(1, result.TotalPoints);
        Assert.Equal(1, result.QsoCount);
    }

    [Fact]
    public void CalculateScore_SingleCwQso_TwoPoints()
    {
        var fd = ContestCatalog.FieldDay;
        var qsos = new List<QsoRecord>
        {
            new("W1AW", 14.074, "20M", "CW", DateTime.UtcNow, "599", "599",
                ContestExchangeSent: "3A GA", ContestExchangeReceived: "5A OH")
        };

        var result = ContestScoreCalculator.CalculateScore(fd, qsos);

        Assert.Equal(2, result.TotalPoints);
    }

    [Fact]
    public void CalculateScore_MixedModes_SumsCorrectly()
    {
        var fd = ContestCatalog.FieldDay;
        var qsos = new List<QsoRecord>
        {
            new("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59"),   // 1 pt
            new("K1ABC", 7.035, "40M", "CW", DateTime.UtcNow, "599", "599"), // 2 pt
            new("N0CALL", 14.074, "20M", "FT8", DateTime.UtcNow, "+05", "-03"), // 2 pt
        };

        var result = ContestScoreCalculator.CalculateScore(fd, qsos);

        Assert.Equal(5, result.TotalPoints);
        Assert.Equal(3, result.QsoCount);
    }

    [Fact]
    public void CalculateScore_TracksSectionsWorkedAsMultiplierCandidates()
    {
        var fd = ContestCatalog.FieldDay;
        var qsos = new List<QsoRecord>
        {
            new("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59",
                ContestExchangeSent: "3A GA", ContestExchangeReceived: "OH"),
            new("K1ABC", 7.035, "40M", "CW", DateTime.UtcNow, "599", "599",
                ContestExchangeSent: "3A GA", ContestExchangeReceived: "OH"), // same section, not new
            new("N0CALL", 14.074, "20M", "FT8", DateTime.UtcNow, "+05", "-03",
                ContestExchangeSent: "3A GA", ContestExchangeReceived: "TX"), // new section
        };

        var result = ContestScoreCalculator.CalculateScore(fd, qsos);

        Assert.Equal(2, result.SectionsWorked.Count);
        Assert.Contains("OH", result.SectionsWorked);
        Assert.Contains("TX", result.SectionsWorked);
    }

    [Fact]
    public void CalculateScore_ExchangeWithoutSection_DoesNotCrashOrCountAsSection()
    {
        var fd = ContestCatalog.FieldDay;
        var qsos = new List<QsoRecord>
        {
            new("W1AW", 14.074, "20M", "USB", DateTime.UtcNow, "59", "59") // no exchange entered
        };

        var result = ContestScoreCalculator.CalculateScore(fd, qsos);

        Assert.Empty(result.SectionsWorked);
        Assert.Equal(1, result.TotalPoints);
    }
}