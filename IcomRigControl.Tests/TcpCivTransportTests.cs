using System.Net;
using System.Net.Sockets;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using Xunit;

namespace IcomRigControl.Tests;

public class TcpCivTransportTests
{
    [Fact]
    public async Task OpenAsync_CorrectToken_ConnectsSuccessfully()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();
        await Task.Delay(100);

        var transport = new TcpCivTransport("127.0.0.1", freePort, "correct-token");
        await transport.OpenAsync();

        Assert.True(transport.IsOpen);

        await transport.CloseAsync();
        server.Stop();
    }

    [Fact]
    public async Task OpenAsync_WrongToken_ThrowsAndDoesNotOpen()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();
        await Task.Delay(100);

        var transport = new TcpCivTransport("127.0.0.1", freePort, "wrong-token");

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.OpenAsync());
        Assert.False(transport.IsOpen);

        server.Stop();
    }

    [Fact]
    public async Task WriteAsync_SendsBytesToServer_WhichForwardsToRadio()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();
        await Task.Delay(100);

        var transport = new TcpCivTransport("127.0.0.1", freePort, "correct-token");
        await transport.OpenAsync();

        byte[] civFrame = { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0xFD };
        await transport.WriteAsync(civFrame);

        // Poll briefly rather than a single fixed delay, since this relays
        // through CivTcpServer's background accept/relay loop -- more
        // resilient to timing variance under system load than one fixed wait.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !radioTransport.WrittenFrames.Any(f => f.SequenceEqual(civFrame)))
        {
            await Task.Delay(50);
        }

        Assert.Contains(radioTransport.WrittenFrames, f => f.SequenceEqual(civFrame));

        await transport.CloseAsync();
        server.Stop();
    }

    [Fact]
    public async Task RadioDataReceived_IsRelayedToClientAsDataReceivedEvent()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();
        await Task.Delay(100);

        var transport = new TcpCivTransport("127.0.0.1", freePort, "correct-token");
        await transport.OpenAsync();

        byte[]? received = null;
        var tcs = new TaskCompletionSource();
        transport.DataReceived += (_, data) =>
        {
            received = data;
            tcs.TrySetResult();
        };

        byte[] simulatedRadioReply = { 0xFE, 0xFE, 0xE0, 0x94, 0x00, 0x14, 0x07, 0x40, 0x00, 0xFD };
        radioTransport.SimulateIncoming(simulatedRadioReply);

        // This relays across two background tasks and a real TCP round-trip
        // (radio transport -> CivTcpServer -> network -> TcpCivTransport's
        // read loop), so give it a generous timeout under system load rather
        // than a tight one -- a slow CI/test-runner moment here reflects
        // system contention, not a real bug in the relay logic itself.
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(completed == tcs.Task, "Timed out waiting for DataReceived to fire — the relay chain (radio transport -> CivTcpServer -> network -> TcpCivTransport) did not complete within 10 seconds.");

        Assert.NotNull(received);
        Assert.Equal(simulatedRadioReply, received);

        await transport.CloseAsync();
        server.Stop();
    }

    [Fact]
    public async Task CloseAsync_SetsIsOpenFalse()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();
        await Task.Delay(100);

        var transport = new TcpCivTransport("127.0.0.1", freePort, "correct-token");
        await transport.OpenAsync();
        await transport.CloseAsync();

        Assert.False(transport.IsOpen);

        server.Stop();
    }

    private static int GetFreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        int port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }
}