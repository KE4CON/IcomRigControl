using System.Net;
using System.Net.Sockets;
using IcomRigControl.CivEngine;

namespace IcomRigControl.RigModel;

/// <summary>
/// TCP server for Phase 9 remote CI-V control. Wraps a real ICivTransport
/// already connected to a physical radio, listens for network clients, and
/// relays raw CI-V bytes bidirectionally after a successful token auth
/// handshake (see CivNetworkProtocol). Intended to run on a headless Pi
/// sitting next to the radio, reachable over LAN, VPN, or 44Net/AMPRNet.
/// </summary>
public class CivTcpServer
{
    private readonly ICivTransport _radioTransport;
    private readonly string _authToken;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    public bool IsRunning { get; private set; }

    public CivTcpServer(ICivTransport radioTransport, string authToken, int port)
    {
        _radioTransport = radioTransport;
        _authToken = authToken;
        _port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _listener?.Stop();
        IsRunning = false;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
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
                // Never let one bad accept take down the whole listener.
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverCt)
    {
        using var _ = client;
        try
        {
            var stream = client.GetStream();

            // Step 1: require a valid auth handshake before relaying anything.
            var authBuffer = new byte[256];
            int authBytesRead = await stream.ReadAsync(authBuffer, serverCt);
            var authRequest = authBuffer[..authBytesRead];

            if (!CivNetworkProtocol.TryParseAuthRequest(authRequest, out string? providedToken) ||
                !CivNetworkProtocol.ValidateToken(_authToken, providedToken!))
            {
                await stream.WriteAsync(CivNetworkProtocol.AuthFailureResponse, serverCt);
                return;
            }

            await stream.WriteAsync(CivNetworkProtocol.AuthSuccessResponse, serverCt);

            // Radio data events fire back-to-back from the radio read-loop
            // thread; a NetworkStream forbids overlapping writes, so serialize
            // every write to this client through a per-connection gate. Without
            // it a burst (e.g. a scope sweep) makes the second WriteAsync throw,
            // and the swallowed exception silently drops that chunk of CI-V data.
            var clientWriteGate = new SemaphoreSlim(1, 1);

            // Step 2: relay bytes from the network client to the real radio.
            EventHandler<byte[]>? radioDataHandler = null;
            radioDataHandler = async (_, data) =>
            {
                try
                {
                    await clientWriteGate.WaitAsync(serverCt);
                    try
                    {
                        await stream.WriteAsync(data, serverCt);
                    }
                    finally
                    {
                        clientWriteGate.Release();
                    }
                }
                catch
                {
                    // Client likely disconnected — the outer loop below will
                    // detect this and clean up.
                }
            };
            _radioTransport.DataReceived += radioDataHandler;

            try
            {
                var relayBuffer = new byte[4096];
                while (!serverCt.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(relayBuffer, serverCt);
                    if (bytesRead == 0) break; // client disconnected

                    await _radioTransport.WriteAsync(relayBuffer[..bytesRead], serverCt);
                }
            }
            finally
            {
                _radioTransport.DataReceived -= radioDataHandler;
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutting down or client disconnected — normal.
        }
        catch
        {
            // Never let one client's error crash the server for other clients.
        }
    }
}