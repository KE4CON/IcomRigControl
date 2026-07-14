using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake ITqslProcessRunner that simulates TQSL's behavior without actually
/// launching a process, so LotwBridge can be tested without TQSL installed.
/// </summary>
public class FakeTqslProcessRunner : ITqslProcessRunner
{
    public bool ShouldSucceed { get; set; } = true;
    public string SignedFileContent { get; set; } = "SIGNED_ADIF_CONTENT";
    public string? LastInputPath { get; private set; }
    public int CallCount { get; private set; }

    public Task<TqslResult> SignAdifFileAsync(string adifFilePath, string outputPath)
    {
        CallCount++;
        LastInputPath = adifFilePath;

        if (!ShouldSucceed)
        {
            return Task.FromResult(new TqslResult(false, "TQSL signing failed: station location not found"));
        }

        System.IO.File.WriteAllText(outputPath, SignedFileContent);
        return Task.FromResult(new TqslResult(true, null));
    }
}

public class LotwBridgeTests
{
    [Fact]
    public async Task Upload_CallsSignAdifFile()
    {
        var tempAdif = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".adi");
        System.IO.File.WriteAllText(tempAdif, "dummy adif content");

        var runner = new FakeTqslProcessRunner();
        var handler = new FakeHttpResponseHandler("Upload successful");
        var httpClient = new HttpClient(handler);
        var bridge = new LotwBridge(runner, httpClient);

        var result = await bridge.UploadAsync(tempAdif);

        Assert.True(result.Success);
        Assert.Equal(1, runner.CallCount);

        System.IO.File.Delete(tempAdif);
    }

    [Fact]
    public async Task Upload_SigningFails_ReturnsFailureWithoutPosting()
    {
        var tempAdif = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".adi");
        System.IO.File.WriteAllText(tempAdif, "dummy adif content");

        var runner = new FakeTqslProcessRunner { ShouldSucceed = false };
        var handler = new FakeHttpResponseHandler("");
        var httpClient = new HttpClient(handler);
        var bridge = new LotwBridge(runner, httpClient);

        var result = await bridge.UploadAsync(tempAdif);

        Assert.False(result.Success);
        Assert.Contains("station location", result.Message ?? "");

        System.IO.File.Delete(tempAdif);
    }

    [Fact]
    public async Task Upload_NetworkFailure_ReturnsFailureWithoutThrowing()
    {
        var tempAdif = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".adi");
        System.IO.File.WriteAllText(tempAdif, "dummy adif content");

        var runner = new FakeTqslProcessRunner();
        var handler = new FakeHttpResponseHandler("", HttpStatusCode.ServiceUnavailable);
        var httpClient = new HttpClient(handler);
        var bridge = new LotwBridge(runner, httpClient);

        var result = await bridge.UploadAsync(tempAdif);

        Assert.False(result.Success);

        System.IO.File.Delete(tempAdif);
    }

    [Fact]
    public async Task Upload_MissingFile_ReturnsFailureWithoutThrowing()
    {
        var runner = new FakeTqslProcessRunner();
        var httpClient = new HttpClient(new FakeHttpResponseHandler(""));
        var bridge = new LotwBridge(runner, httpClient);

        var result = await bridge.UploadAsync(@"C:\this\path\does\not\exist.adi");

        Assert.False(result.Success);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task Download_ParsesReturnedAdifIntoQsoRecords()
    {
        const string sampleAdifResponse = @"<APP_LoTW_EOH>
<call:4>W1AW<band:3>20M<mode:3>USB<qso_date:8>20260701<time_on:4>1830<app_lotw_qsl_rcvd:1>Y<eor>
<call:5>K1ABC<band:3>40M<mode:2>CW<qso_date:8>20260702<time_on:4>0900<app_lotw_qsl_rcvd:1>Y<eor>";

        var runner = new FakeTqslProcessRunner();
        var handler = new FakeHttpResponseHandler(sampleAdifResponse);
        var httpClient = new HttpClient(handler);
        var bridge = new LotwBridge(runner, httpClient);

        var results = await bridge.DownloadConfirmedQsosAsync(new DateTime(2026, 6, 1));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Callsign == "W1AW");
        Assert.Contains(results, r => r.Callsign == "K1ABC");
    }

    [Fact]
    public async Task Download_NetworkFailure_ReturnsEmptyListWithoutThrowing()
    {
        var runner = new FakeTqslProcessRunner();
        var handler = new FakeHttpResponseHandler("", HttpStatusCode.ServiceUnavailable);
        var httpClient = new HttpClient(handler);
        var bridge = new LotwBridge(runner, httpClient);

        var results = await bridge.DownloadConfirmedQsosAsync(new DateTime(2026, 6, 1));

        Assert.Empty(results);
    }
}