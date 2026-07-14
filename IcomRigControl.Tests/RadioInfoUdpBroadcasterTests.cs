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

public class RadioInfoUdpBroadcasterTests
{
    [Fact]
    public async Task Start_SetsIsRunningTrue()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var broadcaster = new RadioInfoUdpBroadcaster(tx);
        broadcaster.Start();

        Assert.True(broadcaster.IsRunning);
        broadcaster.Stop();
    }

    [Fact]
    public async Task Stop_SetsIsRunningFalse()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        var broadcaster = new RadioInfoUdpBroadcaster(tx);
        broadcaster.Start();
        broadcaster.Stop();

        Assert.False(broadcaster.IsRunning);
    }

    [Fact]
    public void AddDestination_AddsToDestinationList()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        var broadcaster = new RadioInfoUdpBroadcaster(tx);
        broadcaster.AddDestination("127.0.0.1", 12060);

        Assert.Single(broadcaster.Destinations);
    }

    [Fact]
    public void RemoveDestination_RemovesFromDestinationList()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);

        var broadcaster = new RadioInfoUdpBroadcaster(tx);
        broadcaster.AddDestination("127.0.0.1", 12060);
        broadcaster.RemoveDestination("127.0.0.1", 12060);

        Assert.Empty(broadcaster.Destinations);
    }

    [Fact]
    public async Task FrequencyChanged_WhileRunning_SendsPacketToDestination()
    {
        var transport = new FakeCivTransport();
        var tx = new Transceiver(transport, RadioModel.IC7300);
        await tx.ConnectAsync();

        int freePort = GetFreeUdpPort();
        using var receiver = new UdpClient(freePort);
        var receiveTask = receiver.ReceiveAsync();

        var broadcaster = new RadioInfoUdpBroadcaster(tx);
        broadcaster.AddDestination("127.0.0.1", freePort);
        broadcaster.Start();

        await tx.SetFrequencyAsync(14_074_000);

        var completed = await Task.WhenAny(receiveTask, Task.Delay(2000));
        broadcaster.Stop();

        Assert.Same(receiveTask, completed);
        var xml = Encoding.UTF8.GetString(receiveTask.Result.Buffer);
        Assert.Contains("RadioInfo", xml);
        Assert.Contains("14074000", xml);
    }

    [Fact]
    public void GenerateRadioInfoXml_IncludesFrequencyModeAndPtt()
    {
        var xml = RadioInfoUdpBroadcaster.GenerateRadioInfoXml(14_074_000, "USB", false);

        Assert.Contains("<RadioInfo>", xml);
        Assert.Contains("<Freq>14074000</Freq>", xml);
        Assert.Contains("<Mode>USB</Mode>", xml);
        Assert.Contains("<IsTransmitting>False</IsTransmitting>", xml);
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new UdpClient(0);
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }
}