using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IcomRigControl.Services;

/// <summary>
/// Listens on a UDP port for Contact XML packets broadcast by N1MM Logger+,
/// WSJT-X, or HRD Logbook whenever a QSO is logged there, and mirrors each
/// valid contact into a QsoLogger. This is the receive half of the shared
/// N1MM/WSJT-X/HRD protocol integration (CLAUDE.md Phase 8f, Direction 2) and
/// exists specifically to fulfil the Core Design Principle: QSOs logged in
/// any external program still end up in this project's resilient local log,
/// without manual re-entry and without depending on the external program
/// staying up.
/// </summary>
public class ContactUdpListener
{
    private readonly QsoLogger _qsoLogger;
    private readonly int _port;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }

    public ContactUdpListener(QsoLogger qsoLogger, int port)
    {
        _qsoLogger = qsoLogger;
        _port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        _udpClient = new UdpClient(_port);
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
        IsRunning = false;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                string xml = Encoding.UTF8.GetString(result.Buffer);

                var qso = ContactPacketParser.Parse(xml);
                if (qso != null)
                {
                    MirrorIntoLogger(qso);
                }
                // Malformed/unrecognized packets are silently ignored (Parse
                // returns null) — never crash the listener over a bad packet.
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                // Client was closed via Stop() while a receive was pending — normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                // Keep listening — a single bad packet or transient socket error
                // should never take down the whole listener.
            }
        }
    }

    /// Re-logs a QSO received over UDP into the local QsoLogger. Since
    /// QsoLogger.LogQso() auto-fills frequency/mode from the live Transceiver
    /// (which would overwrite the contact's own frequency/mode with whatever
    /// the local radio is doing right now), we bypass that auto-fill here by
    /// directly constructing and persisting the record as received.
    private void MirrorIntoLogger(QsoRecord received)
    {
        _qsoLogger.LogReceivedQso(received);
    }
}