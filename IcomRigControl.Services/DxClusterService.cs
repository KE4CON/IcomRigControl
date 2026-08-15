using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace IcomRigControl.Services;

/// <summary>
/// Connects to a DX cluster node over telnet, logs in with the operator's
/// callsign, and raises SpotReceived for each parsed DX spot. Resilient: network
/// errors are recorded in LastError and surfaced via StatusChanged, never thrown
/// back to the caller (per CLAUDE.md's never-crash rule).
/// </summary>
public class DxClusterService
{
    private readonly string _loginCallsign;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _loginSent;

    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler<DxSpot>? SpotReceived;
    public event EventHandler<string>? StatusChanged;

    public DxClusterService(string loginCallsign)
    {
        _loginCallsign = loginCallsign;
    }

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        await DisconnectAsync();
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, ct);
            _stream = _client.GetStream();
            IsConnected = true;
            _loginSent = false;
            LastError = null;
            StatusChanged?.Invoke(this, $"Connected to {host}:{port}.");

            _cts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            IsConnected = false;
            StatusChanged?.Invoke(this, $"Connect failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_readTask != null)
        {
            try { await _readTask; } catch { /* expected on cancel */ }
        }
        _stream?.Close();
        _client?.Close();
        _cts?.Dispose();
        _cts = null;
        _stream = null;
        _client = null;
        _readTask = null;
        if (IsConnected)
        {
            IsConnected = false;
            StatusChanged?.Invoke(this, "Disconnected.");
        }
    }

    /// Post (announce) a DX spot to the cluster: "DX &lt;freq kHz&gt; &lt;callsign&gt; &lt;comment&gt;".
    /// The cluster distributes it to everyone connected.
    public Task PostSpotAsync(double frequencyKHz, string callsign, string comment, CancellationToken ct = default)
    {
        string cmd = $"DX {frequencyKHz.ToString("0.0", CultureInfo.InvariantCulture)} {callsign}";
        if (!string.IsNullOrWhiteSpace(comment)) cmd += " " + comment.Trim();
        return SendAsync(cmd, ct);
    }

    /// Send a raw command line to the cluster (e.g. "sh/dx", "set/filter").
    public async Task SendAsync(string command, CancellationToken ct = default)
    {
        var stream = _stream;
        if (stream == null) return;
        try
        {
            var bytes = Encoding.ASCII.GetBytes(command + "\r\n");
            await stream.WriteAsync(bytes, ct);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var line = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested && _stream != null)
            {
                int read;
                try { read = await _stream.ReadAsync(buffer, ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { LastError = ex.Message; break; }

                if (read == 0) break; // server closed the connection

                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];
                    if (b == (byte)'\n' || b == (byte)'\r')
                    {
                        if (line.Length > 0)
                        {
                            HandleLine(line.ToString());
                            line.Clear();
                        }
                    }
                    else if (b >= 0x20 && b < 0x7F)
                    {
                        // Printable ASCII only — skips telnet IAC/control bytes.
                        line.Append((char)b);
                    }
                }

                // A login prompt often arrives without a trailing newline, so also
                // check the partial buffer. Clear it after responding so the
                // consumed prompt text doesn't get prepended to the next line.
                if (!_loginSent && line.Length > 0 && LooksLikeLoginPrompt(line.ToString()))
                {
                    await SendLoginAsync(ct);
                    line.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            if (IsConnected)
            {
                IsConnected = false;
                StatusChanged?.Invoke(this, "Connection closed.");
            }
        }
    }

    private void HandleLine(string text)
    {
        // A valid spot is a spot regardless of anything else; parse it first.
        var spot = DxSpotParser.Parse(text);
        if (spot != null)
        {
            SpotReceived?.Invoke(this, spot);
            return;
        }

        if (!_loginSent && LooksLikeLoginPrompt(text))
            _ = SendLoginAsync(CancellationToken.None);
    }

    private static bool LooksLikeLoginPrompt(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("login") || t.Contains("call");
    }

    private async Task SendLoginAsync(CancellationToken ct)
    {
        _loginSent = true;
        await SendAsync(_loginCallsign, ct);
        StatusChanged?.Invoke(this, $"Logged in as {_loginCallsign}.");
    }
}
