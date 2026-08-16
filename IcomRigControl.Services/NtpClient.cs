using System.Net.Sockets;

namespace IcomRigControl.Services;

/// <summary>
/// A tiny SNTP client to check the PC clock against internet time. The app's clock
/// always follows the operating-system clock; this just tells the operator how far
/// that clock is off "real" time (handy for time-critical modes like FT8). It sends
/// one UDP packet to an NTP server and compares the returned time to the PC clock.
/// The packet parsing is separated out so it's unit-testable without a network.
/// See CLAUDE.md clock accuracy.
/// </summary>
public static class NtpClient
{
    private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// Returns how far the PC clock is from internet time (positive = PC is behind /
    /// internet time is ahead), or null if the server couldn't be reached.
    public static async Task<TimeSpan?> GetOffsetAsync(string server = "pool.ntp.org", int timeoutMs = 3000)
    {
        try
        {
            var request = new byte[48];
            request[0] = 0x1B; // LI = 0, Version = 3, Mode = 3 (client)

            using var udp = new UdpClient();
            await udp.SendAsync(request, request.Length, server, 123);

            var receiveTask = udp.ReceiveAsync();
            if (await Task.WhenAny(receiveTask, Task.Delay(timeoutMs)) != receiveTask)
                return null; // timed out

            DateTime localNow = DateTime.UtcNow;
            DateTime serverTime = ParseTransmitTimestamp(receiveTask.Result.Buffer);
            return serverTime - localNow;
        }
        catch
        {
            return null; // no network / blocked / bad response
        }
    }

    /// Parses the Transmit Timestamp (bytes 40..47) of an NTP response into UTC.
    public static DateTime ParseTransmitTimestamp(byte[] data)
    {
        ulong seconds = ((ulong)data[40] << 24) | ((ulong)data[41] << 16) | ((ulong)data[42] << 8) | data[43];
        ulong fraction = ((ulong)data[44] << 24) | ((ulong)data[45] << 16) | ((ulong)data[46] << 8) | data[47];
        double milliseconds = seconds * 1000.0 + fraction * 1000.0 / 0x100000000L;
        return NtpEpoch.AddMilliseconds(milliseconds);
    }
}
