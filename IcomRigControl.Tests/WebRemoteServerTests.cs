using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class WebSocketHandshakeTests
{
    [Fact]
    public void ComputeAcceptKey_MatchesRfc6455Example()
    {
        // The worked example from RFC 6455 section 1.3.
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=",
            WebSocketHandshake.ComputeAcceptKey("dGhlIHNhbXBsZSBub25jZQ=="));
    }
}

public class WebRemoteServerTests
{
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    [Fact]
    public async Task ServesThePage_AndStreamsState_AndAcceptsCommands()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        int port = FreePort();
        var server = new WebRemoteServer(rig, token: null, port);
        server.Start();
        try
        {
            // HTTP: the page is served.
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            string page = await http.GetStringAsync($"http://127.0.0.1:{port}/");
            Assert.Contains("IcomRigControl Remote", page);

            // WebSocket: we get a live state message.
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), cts.Token);

            string first = await ReceiveTextAsync(ws, cts.Token);
            Assert.Contains("\"type\":\"state\"", first);

            // Command: set frequency, and confirm the rig received it.
            await SendTextAsync(ws, "{\"cmd\":\"freq\",\"hz\":14074000}", cts.Token);

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (rig.FrequencyHz != 14_074_000 && DateTime.UtcNow < deadline)
                await Task.Delay(50, cts.Token);
            Assert.Equal(14_074_000, rig.FrequencyHz);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task RejectsWebSocket_WhenTokenIsWrong()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        int port = FreePort();
        var server = new WebRemoteServer(rig, token: "secret", port);
        server.Start();
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws?token=wrong"), cts.Token);

            string msg = await ReceiveTextAsync(ws, cts.Token);
            Assert.Contains("unauthorized", msg);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[8192];
        var sb = new StringBuilder();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(buf, ct);
            sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
        }
        while (!r.EndOfMessage);
        return sb.ToString();
    }

    private static Task SendTextAsync(ClientWebSocket ws, string text, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);
}
