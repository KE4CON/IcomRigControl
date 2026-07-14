using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class ContactUdpListenerTests
{
    private const string SampleContactXml = @"<contactinfo>
  <call>W1AW</call>
  <band>20</band>
  <mode>USB</mode>
  <rxfreq>14074000</rxfreq>
  <timestamp>2026-07-14 20:30:00</timestamp>
  <snt>59</snt>
  <rcv>59</rcv>
</contactinfo>";

    [Fact]
    public async Task Start_SetsIsRunningTrue()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        var qsoLogger = new QsoLogger(tx);

        var listener = new ContactUdpListener(qsoLogger, port: 0); // port 0 = OS picks a free port
        listener.Start();

        Assert.True(listener.IsRunning);
        listener.Stop();
    }

    [Fact]
    public async Task Stop_SetsIsRunningFalse()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        var qsoLogger = new QsoLogger(tx);

        var listener = new ContactUdpListener(qsoLogger, port: 0);
        listener.Start();
        listener.Stop();

        Assert.False(listener.IsRunning);
    }

    [Fact]
    public async Task ReceivingValidContactPacket_AddsQsoToLogger()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        var qsoLogger = new QsoLogger(tx);

        // Bind to an actual free loopback port so we can send a real UDP packet to it
        int freePort = GetFreeUdpPort();
        var listener = new ContactUdpListener(qsoLogger, freePort);
        listener.Start();

        await Task.Delay(100); // give the listener a moment to bind

        using var sender = new UdpClient();
        var bytes = Encoding.UTF8.GetBytes(SampleContactXml);
        await sender.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, freePort));

        await Task.Delay(300); // give the listener time to receive and process

        listener.Stop();

        Assert.Single(qsoLogger.Qsos);
        Assert.Equal("W1AW", qsoLogger.Qsos[0].Callsign);
    }

    [Fact]
    public async Task ReceivingMalformedPacket_DoesNotCrashListener()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();
        var qsoLogger = new QsoLogger(tx);

        int freePort = GetFreeUdpPort();
        var listener = new ContactUdpListener(qsoLogger, freePort);
        listener.Start();

        await Task.Delay(100);

        using var sender = new UdpClient();
        var bytes = Encoding.UTF8.GetBytes("not valid xml <<<");
        await sender.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, freePort));

        await Task.Delay(300);

        Assert.True(listener.IsRunning); // still running, didn't crash
        Assert.Empty(qsoLogger.Qsos); // malformed packet was ignored, not logged

        listener.Stop();
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new UdpClient(0);
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }
}