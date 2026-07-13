using System;
using System.Net.Http;
using System.Threading.Tasks;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using IcomRigControl.CivEngine;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake HttpMessageHandler that captures outgoing requests instead of
/// sending them over the network, so EmmcomBridge can be tested without
/// a real server.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    public string? LastRequestBody { get; private set; }
    public string? LastRequestUrl { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, System.Threading.CancellationToken ct)
    {
        RequestCount++;
        LastRequestUrl = request.RequestUri?.ToString();
        if (request.Content != null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(ct);
        }
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
    }
}

public class EmmcomBridgeTests
{
    [Fact]
    public async Task Start_SetsIsRunningTrue()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var bridge = new EmmcomBridge(tx, httpClient, "http://localhost:9000/api/rigstatus");

        bridge.Start();

        Assert.True(bridge.IsRunning);
        bridge.Stop();
    }

    [Fact]
    public async Task MeterUpdated_WhileRunning_PostsJsonToEndpoint()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var bridge = new EmmcomBridge(tx, httpClient, "http://localhost:9000/api/rigstatus");
        bridge.Start();

        tx.StartPolling(TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        tx.StopPolling();

        // give any in-flight async POST a moment to complete
        await Task.Delay(100);

        Assert.True(handler.RequestCount > 0);
        Assert.Equal("http://localhost:9000/api/rigstatus", handler.LastRequestUrl);
        Assert.Contains("frequencyHz", handler.LastRequestBody);

        bridge.Stop();
    }

    [Fact]
    public async Task Stop_UnsubscribesFromMeterUpdated()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var bridge = new EmmcomBridge(tx, httpClient, "http://localhost:9000/api/rigstatus");
        bridge.Start();
        bridge.Stop();

        Assert.False(bridge.IsRunning);

        int countBefore = handler.RequestCount;

        tx.StartPolling(TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        tx.StopPolling();
        await Task.Delay(100);

        Assert.Equal(countBefore, handler.RequestCount);
    }

    [Fact]
    public async Task FailedPost_DoesNotThrow_AndKeepsRunning()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        // A handler pointed at an invalid URL to simulate a network failure
        var httpClient = new HttpClient(new FakeHttpMessageHandler());
        var bridge = new EmmcomBridge(tx, httpClient, "http://this-host-does-not-exist.invalid/api");
        bridge.Start();

        tx.StartPolling(TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        tx.StopPolling();

        // Should not throw, should still report running
        Assert.True(bridge.IsRunning);
        bridge.Stop();
    }
}