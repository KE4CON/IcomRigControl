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
    public async Task Transmit_KeysPtt_FeedsMicAudio_AndUnkeys()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        int port = FreePort();
        var txOut = new RecordingStreamOutput();
        var server = new WebRemoteServer(rig, token: "secret", port, txOutputFactory: () => txOut); // TX needs auth
        server.Start();
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws?token=secret"), cts.Token);

            await SendTextAsync(ws, "{\"cmd\":\"tx\",\"on\":true}", cts.Token);
            await WaitUntil(() => rig.PttActive, TimeSpan.FromSeconds(5));
            Assert.True(rig.PttActive, "PTT should be keyed while transmitting");

            // Send a mic PCM frame; it should reach the radio's TX output.
            var frame = new byte[400];
            await ws.SendAsync(frame, WebSocketMessageType.Binary, true, cts.Token);
            await WaitUntil(() => txOut.SamplesWritten > 0, TimeSpan.FromSeconds(5));
            Assert.True(txOut.SamplesWritten > 0, "mic audio should be written to the radio's TX output");

            await SendTextAsync(ws, "{\"cmd\":\"tx\",\"on\":false}", cts.Token);
            await WaitUntil(() => !rig.PttActive, TimeSpan.FromSeconds(5));
            Assert.False(rig.PttActive, "PTT should be released when transmit stops");

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Transmit_IsBlocked_WhenTransmitInhibited()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300) { TransmitInhibited = true };
        int port = FreePort();
        var server = new WebRemoteServer(rig, token: "secret", port, txOutputFactory: () => new RecordingStreamOutput());
        server.Start();
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws?token=secret"), cts.Token);

            await SendTextAsync(ws, "{\"cmd\":\"tx\",\"on\":true}", cts.Token);
            await Task.Delay(700, cts.Token);
            Assert.False(rig.PttActive, "TX-inhibit must block keying from the web remote");
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Https_ServesPage_AndWebSocketOverTls()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        int port = FreePort();
        var server = new WebRemoteServer(rig, token: null, port, useHttps: true);
        server.Start();
        Assert.Equal("https", server.Scheme);
        try
        {
            // HTTPS page (accept the self-signed cert for the test).
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            string page = await http.GetStringAsync($"https://127.0.0.1:{port}/");
            Assert.Contains("IcomRigControl Remote", page);

            // WSS over the same TLS endpoint.
            using var ws = new ClientWebSocket();
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"wss://127.0.0.1:{port}/ws"), cts.Token);
            string first = await ReceiveTextAsync(ws, cts.Token);
            Assert.Contains("\"type\":\"state\"", first);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task NoToken_MonitoringWorks_ButTransmitIsRefused()
    {
        // Audit CRITICAL fix: an open (no-token) server must never key the radio.
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        int port = FreePort();
        var server = new WebRemoteServer(rig, token: null, port,
            txOutputFactory: () => new RecordingStreamOutput());
        server.Start();
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), cts.Token);

            // Tuning (monitoring) still works without a token.
            await SendTextAsync(ws, "{\"cmd\":\"freq\",\"hz\":14074000}", cts.Token);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (rig.FrequencyHz != 14_074_000 && DateTime.UtcNow < deadline) await Task.Delay(25, cts.Token);
            Assert.Equal(14_074_000, rig.FrequencyHz);

            // But keying is refused: PTT-on and TX-toggle must NOT key the radio.
            await SendTextAsync(ws, "{\"cmd\":\"ptt\",\"on\":true}", cts.Token);
            await SendTextAsync(ws, "{\"cmd\":\"tx\",\"on\":true}", cts.Token);
            await Task.Delay(700, cts.Token);
            Assert.False(rig.PttActive, "an unauthenticated (no-token) client must not be able to key the transmitter");
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

    private static async Task WaitUntil(Func<bool> cond, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!cond() && DateTime.UtcNow < deadline) await Task.Delay(25);
    }

    private sealed class RecordingStreamOutput : IAudioStreamOutput
    {
        public int SamplesWritten { get; private set; }
        public bool Running { get; private set; }
        public void Start(int sampleRateHz, string? deviceName = null) => Running = true;
        public void Write(short[] pcmFrame) => SamplesWritten += pcmFrame.Length;
        public void Stop() => Running = false;
    }
}
