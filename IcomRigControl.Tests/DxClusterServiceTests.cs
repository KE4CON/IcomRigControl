using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class DxClusterServiceTests
{
    [Fact]
    public async Task PostSpotAsync_SendsDxCommandInClusterFormat()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var stream = server.GetStream();
            var buf = new byte[256];
            int n = await stream.ReadAsync(buf);
            received.TrySetResult(Encoding.ASCII.GetString(buf, 0, n).Trim());
        });

        var service = new DxClusterService("KE4CON");
        try
        {
            await service.ConnectAsync("127.0.0.1", port);
            await service.PostSpotAsync(14074.0, "JA1XYZ", "FT8 nice sig");

            var line = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("DX 14074.0 JA1XYZ FT8 nice sig", line);
        }
        finally
        {
            await service.DisconnectAsync();
            listener.Stop();
            try { await serverTask; } catch { }
        }
    }

    [Fact]
    public async Task Connect_SendsLoginCallsign_AndRaisesSpotReceived()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // Server side: accept, prompt for a call, capture what the client sends,
        // then push one spot line.
        var loginReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using var server = await listener.AcceptTcpClientAsync();
            var stream = server.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes("Please enter your call: "));

            var buf = new byte[256];
            int n = await stream.ReadAsync(buf);
            loginReceived.TrySetResult(Encoding.ASCII.GetString(buf, 0, n).Trim());

            await stream.WriteAsync(Encoding.ASCII.GetBytes("DX de W3LPL: 14074.0 K1ABC FT8 -12 dB 1305Z\r\n"));
            await Task.Delay(500); // keep the connection open briefly
        });

        var service = new DxClusterService("KE4CON");
        var spotReceived = new TaskCompletionSource<DxSpot>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.SpotReceived += (_, spot) => spotReceived.TrySetResult(spot);

        try
        {
            await service.ConnectAsync("127.0.0.1", port);

            // Generous timeout: this crosses a real TCP round-trip and a read loop.
            var completed = await Task.WhenAny(spotReceived.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(completed == spotReceived.Task, "Timed out waiting for a spot from the cluster service.");

            var spot = await spotReceived.Task;
            Assert.Equal("K1ABC", spot.DxCallsign);
            Assert.Equal(14_074_000, spot.FrequencyHz);

            // The service must have sent our login callsign when prompted.
            var login = await loginReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("KE4CON", login);
        }
        finally
        {
            await service.DisconnectAsync();
            listener.Stop();
            try { await serverTask; } catch { /* ignore teardown races */ }
        }
    }
}
