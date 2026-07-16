using System.Net.Sockets;
using IcomRigControl.CivEngine;

namespace IcomRigControl.RigModel;

/// <summary>
/// ICivTransport implementation that connects to a remote CivTcpServer
/// (Phase 9) instead of a local serial port. From Transceiver's perspective
/// this is indistinguishable from SerialCivTransport — the rest of the
/// application (dashboard, meters, all Services integrations) works
/// unchanged whether the radio is local or remote.
/// </summary>
public class TcpCivTransport : ICivTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _authToken;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readCts;
    private Task? _readLoopTask;

    public bool IsOpen { get; private set; }
    public event EventHandler<byte[]>? DataReceived;

    public TcpCivTransport(string host, int port, string authToken)
    {
        _host = host;
        _port = port;
        _authToken = authToken;
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, ct);
        _stream = _client.GetStream();

        // Authenticate before considering the connection open.
        var authRequest = CivNetworkProtocol.BuildAuthRequest(_authToken);
        await _stream.WriteAsync(authRequest, ct);

        var responseBuffer = new byte[64];
        int bytesRead = await _stream.ReadAsync(responseBuffer, ct);
        var response = responseBuffer[..bytesRead];

        if (!CivNetworkProtocol.IsAuthSuccess(response))
        {
            _client.Close();
            _client = null;
            _stream = null;
            throw new InvalidOperationException("Remote CI-V server rejected authentication token.");
        }

        IsOpen = true;
        _readCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
    }

    public Task CloseAsync()
    {
        _readCts?.Cancel();
        _stream?.Close();
        _client?.Close();
        IsOpen = false;
        return Task.CompletedTask;
    }

    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        if (_stream == null || !IsOpen)
        {
            throw new InvalidOperationException("Transport is not open.");
        }

        await _stream.WriteAsync(data, ct);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_stream == null) return;

        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int bytesRead = await _stream.ReadAsync(buffer, ct);
                if (bytesRead == 0) break; // server closed the connection

                DataReceived?.Invoke(this, buffer[..bytesRead]);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // Transient read error — keep the loop alive rather than
                // silently dying, per this project's never-crash pattern.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}