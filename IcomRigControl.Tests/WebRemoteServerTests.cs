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
    public async Task StreamsRxAudio_AsBinaryFrames_WhenRequested()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        int port = FreePort();
        var capture = new PushCapture();
        var server = new WebRemoteServer(rig, token: null, port, captureFactory: () => capture);
        server.Start();
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), cts.Token);

            await SendTextAsync(ws, "{\"cmd\":\"audio\",\"on\":true}", cts.Token);

            // Expect a binary PCM frame to arrive (text state frames are skipped).
            byte[]? audio = null;
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (audio is null && DateTime.UtcNow < deadline)
            {
                var (isBinary, data) = await ReceiveAnyAsync(ws, cts.Token);
                if (isBinary) audio = data;
            }
            Assert.NotNull(audio);
            Assert.True(audio!.Length > 0 && audio.Length % 2 == 0, "PCM frame should be non-empty 16-bit samples");
            Assert.True(capture.Started, "capture should have started for the listener");

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

    private static async Task<(bool isBinary, byte[] data)> ReceiveAnyAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[16384];
        var ms = new System.Collections.Generic.List<byte>();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(buf, ct);
            ms.AddRange(new ArraySegment<byte>(buf, 0, r.Count));
        }
        while (!r.EndOfMessage);
        return (r.MessageType == WebSocketMessageType.Binary, ms.ToArray());
    }

    // A fake capture that pushes PCM buffers on a timer once started, so the audio
    // fan-out path can be exercised without a sound card.
    private sealed class PushCapture : IAudioCapture
    {
        private CancellationTokenSource? _cts;
        public bool Started { get; private set; }
        public bool IsCapturing => _cts is not null;
        public event EventHandler<short[]>? SamplesCaptured;
        public System.Collections.Generic.List<string> GetAvailableDevices() => new();

        public void Start(int sampleRateHz, string? deviceName = null)
        {
            Started = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = Task.Run(async () =>
            {
                var buf = new short[4096];
                for (int i = 0; i < buf.Length; i++) buf[i] = (short)(i % 100);
                while (!ct.IsCancellationRequested)
                {
                    SamplesCaptured?.Invoke(this, buf);
                    try { await Task.Delay(30, ct); } catch { break; }
                }
            });
        }

        public void Stop() { _cts?.Cancel(); _cts = null; }
    }
}
