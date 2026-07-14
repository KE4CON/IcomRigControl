using System;
using System.IO;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class QsoLoggerTests
{
    [Fact]
    public async Task LogQso_AddsRecordToLog()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        await tx.SetFrequencyAsync(14_074_000);
        await tx.SetModeAsync("USB");

        var logger = new QsoLogger(tx);
        logger.LogQso("W1AW", "59", "59");

        Assert.Single(logger.Qsos);
    }

    [Fact]
    public async Task LogQso_AutoFillsFrequencyModeAndBandFromTransceiver()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        await tx.SetFrequencyAsync(14_074_000);
        await tx.SetModeAsync("USB");

        var logger = new QsoLogger(tx);
        logger.LogQso("W1AW", "59", "59");

        var qso = logger.Qsos[0];
        Assert.Equal(14.074, qso.FrequencyMHz, precision: 3);
        Assert.Equal("USB", qso.Mode);
        Assert.Equal("20M", qso.Band);
    }

    [Fact]
    public async Task LogQso_SetsCallsignUppercase()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var logger = new QsoLogger(tx);
        logger.LogQso("w1aw", "59", "59");

        Assert.Equal("W1AW", logger.Qsos[0].Callsign);
    }

    [Theory]
    [InlineData(1_800_000, "160M")]
    [InlineData(3_573_000, "80M")]
    [InlineData(7_074_000, "40M")]
    [InlineData(14_074_000, "20M")]
    [InlineData(21_074_000, "15M")]
    [InlineData(28_074_000, "10M")]
    [InlineData(50_313_000, "6M")]
    [InlineData(146_520_000, "2M")]
    public async Task LogQso_MapsFrequencyToCorrectBand(long hz, string expectedBand)
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        await tx.SetFrequencyAsync(hz);

        var logger = new QsoLogger(tx);
        logger.LogQso("TEST", "59", "59");

        Assert.Equal(expectedBand, logger.Qsos[0].Band);
    }

    [Fact]
    public async Task ExportToAdif_WritesAllLoggedQsos()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var logger = new QsoLogger(tx);
        logger.LogQso("W1AW", "59", "59");
        logger.LogQso("K1ABC", "59", "57");

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".adi");
        try
        {
            logger.ExportToAdif(tempFile);
            var content = File.ReadAllText(tempFile);

            Assert.Contains("W1AW", content);
            Assert.Contains("K1ABC", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ClearLog_RemovesAllQsos()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var logger = new QsoLogger(tx);
        logger.LogQso("W1AW", "59", "59");
        logger.ClearLog();

        Assert.Empty(logger.Qsos);
    }
}