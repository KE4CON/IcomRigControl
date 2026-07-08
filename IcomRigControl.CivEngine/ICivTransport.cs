namespace IcomRigControl.CivEngine;

/// <summary>
/// Abstraction over the physical connection to the radio (USB serial, network, etc.)
/// so CivEngine and RigModel never depend on System.IO.Ports directly.
/// This is what allows unit testing without real hardware attached.
/// </summary>
public interface ICivTransport : IAsyncDisposable
{
    bool IsOpen { get; }

    event EventHandler<byte[]>? DataReceived;

    Task OpenAsync(CancellationToken ct = default);
    Task CloseAsync();
    Task WriteAsync(byte[] data, CancellationToken ct = default);
}
