using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using IcomRigControl.RigModel;

namespace IcomRigControl.Services;

/// <summary>
/// Serves the mobile web remote (WebRemotePage) and a WebSocket that streams live
/// radio state to the browser and accepts tuning / mode / PTT commands. Built on a
/// plain TcpListener plus the framework's WebSocket.CreateFromStream — no web
/// framework dependency, so it stays light enough for the headless Pi and works
/// identically on every platform. Consumes only the Transceiver (Layer 2), so it
/// respects the same rules as every other service — including TransmitInhibited,
/// since PTT goes through Transceiver.SetPttAsync. See CLAUDE.md web remote.
/// </summary>
public sealed class WebRemoteServer : IAsyncDisposable
{
    private readonly Transceiver _rig;
    private readonly string? _token;
    private readonly int _port;

    // RX audio: capture at 44.1 kHz and downsample by this factor for the browser
    // (voice audio needs little bandwidth, and a phone over Wi-Fi appreciates it).
    private const int CaptureRateHz = 44100;
    private const int AudioDecimation = 4;              // 44100 / 4 = 11025 Hz
    private const int AudioRateHz = CaptureRateHz / AudioDecimation;

    private readonly Func<IAudioCapture>? _captureFactory;
    private readonly string? _captureDevice;
    private readonly object _audioLock = new();
    private readonly List<ChannelWriter<byte[]>> _audioSinks = new();
    private IAudioCapture? _capture;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning { get; private set; }
    public int Port => _port;

    /// <param name="captureFactory">Supplies an IAudioCapture for RX audio streaming
    /// (usually AudioDevices.CreateCapture). Null disables audio (control-only).</param>
    /// <param name="captureDevice">Optional capture device name (e.g. an ALSA device).</param>
    public WebRemoteServer(Transceiver rig, string? token, int port = 8080,
        Func<IAudioCapture>? captureFactory = null, string? captureDevice = null)
    {
        _rig = rig;
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
        _port = port;
        _captureFactory = captureFactory;
        _captureDevice = captureDevice;
    }

    public void Start()
    {
        Stop();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _ = AcceptLoopAsync(_listener, _cts.Token);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct); }
            catch { break; }
            _ = HandleClientAsync(client, ct); // one task per connection
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var (requestLine, headers) = await ReadRequestHeadAsync(stream, ct);
                if (requestLine is null) return;

                string[] parts = requestLine.Split(' ');
                string path = parts.Length >= 2 ? parts[1] : "/";

                bool wantsWs = headers.TryGetValue("upgrade", out var up) &&
                               up.Contains("websocket", StringComparison.OrdinalIgnoreCase);

                if (wantsWs && path.StartsWith("/ws", StringComparison.Ordinal))
                    await HandleWebSocketAsync(stream, path, headers, ct);
                else if (parts.Length >= 1 && parts[0] == "GET" && (path == "/" || path.StartsWith("/index")))
                    await WriteHttpAsync(stream, "200 OK", "text/html; charset=utf-8", WebRemotePage.Html, ct);
                else
                    await WriteHttpAsync(stream, "404 Not Found", "text/plain", "Not found", ct);
            }
            catch { /* connection error — just drop it */ }
        }
    }

    // Reads request line + headers, stopping exactly at the blank line so any
    // following WebSocket bytes stay in the stream for CreateFromStream.
    private static async Task<(string? requestLine, Dictionary<string, string> headers)> ReadRequestHeadAsync(
        NetworkStream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var one = new byte[1];
        int total = 0;
        while (total < 16384)
        {
            int n = await stream.ReadAsync(one, ct);
            if (n <= 0) break;
            sb.Append((char)one[0]);
            total++;
            if (sb.Length >= 4 && sb[^1] == '\n' && sb[^2] == '\r' && sb[^3] == '\n' && sb[^4] == '\r')
                break;
        }

        var lines = sb.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return (null, new());
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            int c = lines[i].IndexOf(':');
            if (c > 0) headers[lines[i][..c].Trim()] = lines[i][(c + 1)..].Trim();
        }
        return (lines[0], headers);
    }

    private async Task HandleWebSocketAsync(NetworkStream stream, string path,
        Dictionary<string, string> headers, CancellationToken ct)
    {
        if (!headers.TryGetValue("sec-websocket-key", out var key)) return;

        string accept = WebSocketHandshake.ComputeAcceptKey(key);
        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
        byte[] respBytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(respBytes, ct);

        using var ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null,
            keepAliveInterval: TimeSpan.FromSeconds(30));

        // Token check happens after upgrade so the browser can prompt and reconnect.
        if (_token is not null && QueryToken(path) != _token)
        {
            await SendTextAsync(ws, "{\"type\":\"unauthorized\"}", ct);
            await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "token", ct);
            return;
        }

        // All sends (state, scope, RX audio, close reply) go through one gate — a
        // WebSocket allows only one outstanding send at a time. Both loops are local
        // so they can share this session's audio channel.
        using var sendLock = new SemaphoreSlim(1, 1);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Per-session RX-audio queue; DropOldest keeps latency bounded if the phone
        // can't keep up rather than growing without limit.
        var audioCh = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(48) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        bool audioOn = false;

        async Task SendLoop()
        {
            int[]? lastWave = null;
            var lastState = DateTime.MinValue;
            try
            {
                while (ws.State == WebSocketState.Open && !linked.IsCancellationRequested)
                {
                    await sendLock.WaitAsync(linked.Token);
                    try
                    {
                        // State + scope at ~5 Hz.
                        if ((DateTime.UtcNow - lastState).TotalMilliseconds >= 200)
                        {
                            lastState = DateTime.UtcNow;
                            await SendTextAsync(ws, BuildStateJson(), linked.Token);
                            int[] wave = _rig.LastWaveform;
                            if (wave.Length > 0 && !ReferenceEquals(wave, lastWave))
                            {
                                lastWave = wave;
                                await SendTextAsync(ws, BuildScopeJson(wave), linked.Token);
                            }
                        }
                        // Drain any queued RX-audio frames promptly (low latency).
                        while (audioCh.Reader.TryRead(out byte[]? frame))
                            await ws.SendAsync(frame, WebSocketMessageType.Binary, true, linked.Token);
                    }
                    finally { sendLock.Release(); }
                    await Task.Delay(25, linked.Token); // ~40 Hz service tick
                }
            }
            catch { /* socket closed / cancelled */ }
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            var msg = new List<byte>();
            try
            {
                while (ws.State == WebSocketState.Open && !linked.IsCancellationRequested)
                {
                    msg.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(buffer, linked.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await sendLock.WaitAsync(CancellationToken.None);
                            try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
                            catch { }
                            finally { sendLock.Release(); }
                            return;
                        }
                        msg.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                    }
                    while (!result.EndOfMessage);

                    // Intercept the audio on/off command; everything else drives the rig.
                    string text = Encoding.UTF8.GetString(msg.ToArray());
                    if (TryReadAudioToggle(text, out bool wantAudio))
                    {
                        if (wantAudio && !audioOn) { audioOn = true; EnableAudio(audioCh.Writer); }
                        else if (!wantAudio && audioOn) { audioOn = false; DisableAudio(audioCh.Writer); }
                    }
                    else
                    {
                        await DispatchCommandAsync(text);
                    }
                }
            }
            catch { /* socket closed / cancelled */ }
        }

        try
        {
            var recv = ReceiveLoop();
            var send = SendLoop();
            await Task.WhenAny(recv, send);
            linked.Cancel();
            try { await Task.WhenAll(recv, send); } catch { }
        }
        finally
        {
            if (audioOn) DisableAudio(audioCh.Writer);
        }
    }

    private static bool TryReadAudioToggle(string json, out bool on)
    {
        on = false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("cmd", out var c) || c.GetString() != "audio") return false;
            on = doc.RootElement.TryGetProperty("on", out var o) && o.GetBoolean();
            return true;
        }
        catch { return false; }
    }

    // ── RX audio: capture the radio's receive audio and fan PCM frames out to every
    //    listening browser session. Capture starts on the first listener, stops on
    //    the last. Called under no lock by sessions; guarded by _audioLock. ──
    private void EnableAudio(ChannelWriter<byte[]> sink)
    {
        if (_captureFactory is null) return;
        lock (_audioLock)
        {
            _audioSinks.Add(sink);
            if (_capture is null)
            {
                _capture = _captureFactory();
                _capture.SamplesCaptured += OnCaptureSamples;
                try { _capture.Start(CaptureRateHz, _captureDevice); }
                catch { /* no device — leave sinks registered but silent */ }
            }
        }
    }

    private void DisableAudio(ChannelWriter<byte[]> sink)
    {
        lock (_audioLock)
        {
            _audioSinks.Remove(sink);
            if (_audioSinks.Count == 0 && _capture is not null)
            {
                _capture.SamplesCaptured -= OnCaptureSamples;
                try { _capture.Stop(); } catch { }
                _capture = null;
            }
        }
    }

    private void OnCaptureSamples(object? sender, short[] samples)
    {
        // Downsample by simple averaging, then pack little-endian 16-bit PCM.
        int outCount = samples.Length / AudioDecimation;
        if (outCount == 0) return;
        var bytes = new byte[outCount * 2];
        for (int i = 0; i < outCount; i++)
        {
            int sum = 0;
            for (int j = 0; j < AudioDecimation; j++) sum += samples[i * AudioDecimation + j];
            short v = (short)(sum / AudioDecimation);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        lock (_audioLock)
            foreach (var sink in _audioSinks) sink.TryWrite(bytes);
    }

    private async Task DispatchCommandAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("cmd", out var cmdEl)) return;
            string cmd = cmdEl.GetString() ?? "";

            switch (cmd)
            {
                case "tune":
                    long delta = root.GetProperty("delta").GetInt64();
                    await _rig.SetFrequencyAsync(Math.Max(0, _rig.FrequencyHz + delta));
                    break;
                case "freq":
                    long hz = root.GetProperty("hz").GetInt64();
                    await _rig.SetFrequencyAsync(Math.Max(0, hz));
                    break;
                case "mode":
                    string? mode = root.GetProperty("mode").GetString();
                    if (!string.IsNullOrWhiteSpace(mode)) await _rig.SetModeAsync(mode);
                    break;
                case "ptt":
                    bool on = root.GetProperty("on").GetBoolean();
                    await _rig.SetPttAsync(on); // honors TransmitInhibited internally
                    break;
            }
        }
        catch { /* bad command — ignore, never crash the session */ }
    }

    private string BuildStateJson()
    {
        var state = new
        {
            type = "state",
            connected = _rig.IsConnected,
            freq = _rig.FrequencyHz,
            mode = _rig.Mode,
            ptt = _rig.PttActive,
            inhibited = _rig.TransmitInhibited,
            s = _rig.SMeterS,
            sdbm = _rig.SMeterDbm,
            power = _rig.RfPowerPercent,
            swr = _rig.SwrRatio,
            alc = _rig.AlcLevel,
            volts = _rig.SupplyVoltage,
            amps = _rig.CurrentDraw,
            audioRate = AudioRateHz,
            audioAvailable = _captureFactory is not null,
        };
        return JsonSerializer.Serialize(state);
    }

    private string BuildScopeJson(int[] wave) => JsonSerializer.Serialize(new
    {
        type = "scope",
        center = _rig.FrequencyHz,
        span = _rig.CurrentSpanHz,
        data = wave,
    });

    private static async Task SendTextAsync(WebSocket ws, string text, CancellationToken ct)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task WriteHttpAsync(NetworkStream stream, string status, string contentType,
        string body, CancellationToken ct)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string head =
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    private static string? QueryToken(string path)
    {
        int q = path.IndexOf('?');
        if (q < 0) return null;
        foreach (string pair in path[(q + 1)..].Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq] == "token")
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return null;
    }

    /// The http:// URLs a phone/tablet on the same network can open — every IPv4
    /// address of this machine, for display in the desktop UI.
    public static List<string> GetLanUrls(int port)
    {
        var urls = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        urls.Add($"http://{ip.Address}:{port}");
                }
            }
        }
        catch { /* enumeration not available — return what we have */ }
        return urls;
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        lock (_audioLock)
        {
            _audioSinks.Clear();
            if (_capture is not null)
            {
                _capture.SamplesCaptured -= OnCaptureSamples;
                try { _capture.Stop(); } catch { }
                _capture = null;
            }
        }
        _cts?.Dispose();
        _cts = null;
        _listener = null;
        IsRunning = false;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
