using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class AdifWriterTests
{
    private static QsoRecord SampleQso() => new(
        Callsign: "W1AW",
        FrequencyMHz: 14.074,
        Band: "20M",
        Mode: "USB",
        DateTimeUtc: new DateTime(2026, 7, 14, 18, 30, 0, DateTimeKind.Utc),
        RstSent: "59",
        RstReceived: "59",
        Name: "Hiram",
        GridSquare: "FN31",
        Notes: "Great signal"
    );

    [Fact]
    public void FormatQso_IncludesCallsignField()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.Contains("<CALL:4>W1AW", line);
    }

    [Fact]
    public void FormatQso_IncludesBandField()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.Contains("<BAND:3>20M", line);
    }

    [Fact]
    public void FormatQso_IncludesModeField()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.Contains("<MODE:3>USB", line);
    }

    [Fact]
    public void FormatQso_IncludesDateInAdifFormat()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.Contains("<QSO_DATE:8>20260714", line);
    }

    [Fact]
    public void FormatQso_IncludesTimeInAdifFormat()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.Contains("<TIME_ON:4>1830", line);
    }

    [Fact]
    public void FormatQso_IncludesFrequency()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.Contains("<FREQ:6>14.074", line);
    }

    [Fact]
    public void FormatQso_EndsWithEndOfRecordMarker()
    {
        var line = AdifWriter.FormatQso(SampleQso());
        Assert.EndsWith("<EOR>", line.TrimEnd());
    }

    [Fact]
    public void FormatQso_OmitsOptionalFieldsWhenNull()
    {
        var minimal = new QsoRecord(
            Callsign: "N0CALL",
            FrequencyMHz: 7.074,
            Band: "40M",
            Mode: "USB",
            DateTimeUtc: DateTime.UtcNow,
            RstSent: "59",
            RstReceived: "59",
            Name: null,
            GridSquare: null,
            Notes: null
        );
        var line = AdifWriter.FormatQso(minimal);
        Assert.DoesNotContain("<NAME:", line);
        Assert.DoesNotContain("<GRIDSQUARE:", line);
        Assert.DoesNotContain("<NOTES:", line);
    }

    [Fact]
    public void GenerateHeader_ContainsAdifVersionAndProgramId()
    {
        var header = AdifWriter.GenerateHeader();
        Assert.Contains("<ADIF_VER:", header);
        Assert.Contains("<PROGRAMID:", header);
        Assert.Contains("<EOH>", header);
    }

    [Fact]
    public void WriteToFile_CreatesFileWithHeaderAndOneQso()
    {
        var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".adi");
        try
        {
            AdifWriter.WriteToFile(tempFile, new[] { SampleQso() });

            var content = System.IO.File.ReadAllText(tempFile);
            Assert.Contains("<EOH>", content);
            Assert.Contains("W1AW", content);
            Assert.Contains("<EOR>", content);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
        }
    }
}