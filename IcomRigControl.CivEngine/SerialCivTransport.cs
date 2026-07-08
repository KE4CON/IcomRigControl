using System.IO.Ports;

namespace IcomRigControl.CivEngine;

/// <summary>
/// ICivTransport implementation using a physical USB/serial CI-V connection.
/// Wraps System.IO.Ports.SerialPort.
/// </summary>
public class SerialCivTransport : ICivTransport
{
    private readonly string _portName;
    private readonly int _baudRate;
    private SerialPort? _port;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public bool IsOpen => _port?.IsOpen ?? false;

    public event EventHandler<byte[]>? DataReceived;

    public SerialCivTransport(string portName, int baudRate = 115200)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public Task OpenAsync(CancellationToken ct = default)
    {
        _port = new SerialPort(_portName, _baudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        _port.Open();

        _readCts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task CloseAsync()
    {
        if (_readCts != null)
        {
            await _readCts.CancelAsync();
            if (_readTask != null)
            {
                try { await _readTask; } catch (OperationCanceledException) { }
            }
        }

        if (_port is { IsOpen: true })
        {
            _port.Close();
        }
        _port?.Dispose();
        _port = null;
    }

    public Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        if (_port is not { IsOpen: true })
            throw new InvalidOperationException("Transport is not open.");

        _port.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[256];

        while (!ct.IsCancellationRequested && _port is { IsOpen: true })
        {
            try
            {
                int bytesRead = _port.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    var received = new byte[bytesRead];
                    Array.Copy(buffer, received, bytesRead);
                    DataReceived?.Invoke(this, received);
                }
            }
            catch (TimeoutException)
            {
                // Expected — no data available within ReadTimeout. Loop again.
            }
            catch (InvalidOperationException)
            {
                // Port was closed out from under us — exit cleanly.
                break;
            }

            await Task.Delay(10, ct).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}
