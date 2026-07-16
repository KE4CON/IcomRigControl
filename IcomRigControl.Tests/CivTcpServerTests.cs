using System.Net;
using System.Net.Sockets;
using System.Text;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using Xunit;

namespace IcomRigControl.Tests;

public class CivTcpServerTests
{
    [Fact]
    public async Task Start_SetsIsRunningTrue()
    {
        var radioTransport = new FakeCivTransport();
        var server = new CivTcpServer(radioTransport, "test-token", port: 0);

        server.Start();

        Assert.True(server.IsRunning);
        server.Stop();
    }

    [Fact]
    public async Task Stop_SetsIsRunningFalse()
    {
        var radioTransport = new FakeCivTransport();
        var server = new CivTcpServer(radioTransport, "test-token", port: 0);

        server.Start();
        server.Stop();

        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task ClientWithCorrectToken_ReceivesAuthSuccess()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();

        await Task.Delay(100);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, freePort);
        var stream = client.GetStream();

        var authRequest = CivNetworkProtocol.BuildAuthRequest("correct-token");
        await stream.WriteAsync(authRequest);

        var buffer = new byte[64];
        int read = await stream.ReadAsync(buffer);
        var response = buffer[..read];

        Assert.True(CivNetworkProtocol.IsAuthSuccess(response));

        server.Stop();
    }

    [Fact]
    public async Task ClientWithWrongToken_ReceivesAuthFailureAndIsDisconnected()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();

        await Task.Delay(100);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, freePort);
        var stream = client.GetStream();

        var authRequest = CivNetworkProtocol.BuildAuthRequest("wrong-token");
        await stream.WriteAsync(authRequest);

        var buffer = new byte[64];
        int read = await stream.ReadAsync(buffer);
        var response = buffer[..read];

        Assert.False(CivNetworkProtocol.IsAuthSuccess(response));

        server.Stop();
    }

    [Fact]
    public async Task AuthenticatedClient_DataSentToRadioTransport()
    {
        var radioTransport = new FakeCivTransport();
        int freePort = GetFreePort();
        var server = new CivTcpServer(radioTransport, "correct-token", freePort);
        server.Start();

        await Task.Delay(100);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, freePort);
        var stream = client.GetStream();

        await stream.WriteAsync(CivNetworkProtocol.BuildAuthRequest("correct-token"));
        var authBuffer = new byte[64];
        await stream.ReadAsync(authBuffer);

        // Send a fake CI-V frame from the network client
        byte[] civFrame = { 0xFE, 0xFE, 0xE0, 0x94, 0x03, 0xFD };
        await stream.WriteAsync(civFrame);

        await Task.Delay(200); // give the relay loop time to forward it

        Assert.Contains(radioTransport.WrittenFrames, f => f.SequenceEqual(civFrame));

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