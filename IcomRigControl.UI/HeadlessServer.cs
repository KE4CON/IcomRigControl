using System;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;

namespace IcomRigControl.UI;

/// <summary>
/// Headless remote-control server entry point (Phase 9). Connects to a real
/// radio via SerialCivTransport and serves it over TCP via CivTcpServer, with
/// no UI — intended for running on a Raspberry Pi next to the radio.
///
/// Usage: IcomRigControl.UI --headless-server --port [comport] --tcpport
/// [port] --token [authtoken] --model [IC7300|IC7300MK2]
/// </summary>
public static class HeadlessServer
{
    public static async Task RunAsync(string[] args)
    {
        string? serialPort = GetArgValue(args, "--port");
        string? tcpPortStr = GetArgValue(args, "--tcpport");
        string? authToken = GetArgValue(args, "--token");
        string? modelStr = GetArgValue(args, "--model");

        if (string.IsNullOrWhiteSpace(serialPort) ||
            string.IsNullOrWhiteSpace(tcpPortStr) ||
            string.IsNullOrWhiteSpace(authToken))
        {
            Console.WriteLine("Usage: IcomRigControl.UI --headless-server --port <comport> --tcpport <port> --token <authtoken> [--model IC7300|IC7300MK2]");
            Console.WriteLine("Example: IcomRigControl.UI --headless-server --port /dev/ttyUSB0 --tcpport 7300 --token mysecret123");
            return;
        }

        if (!int.TryParse(tcpPortStr, out int tcpPort))
        {
            Console.WriteLine($"Invalid --tcpport value: {tcpPortStr}");
            return;
        }

        var model = modelStr?.Equals("IC7300MK2", StringComparison.OrdinalIgnoreCase) == true
            ? RadioModel.IC7300MK2
            : RadioModel.IC7300;

        Console.WriteLine($"IcomRigControl headless server starting...");
        Console.WriteLine($"  Radio: {model} on {serialPort}");
        Console.WriteLine($"  Listening on TCP port {tcpPort}");

        var serialTransport = new SerialCivTransport(serialPort);

        try
        {
            await serialTransport.OpenAsync();
            Console.WriteLine("  Radio connected successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: Failed to open serial port {serialPort}: {ex.Message}");
            return;
        }

        var server = new CivTcpServer(serialTransport, authToken, tcpPort);
        server.Start();

        Console.WriteLine("  Server running. Press Ctrl+C to stop.");

        // Keep the process alive until Ctrl+C or process termination.
        var exitSignal = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitSignal.TrySetResult();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => exitSignal.TrySetResult();

        await exitSignal.Task;

        Console.WriteLine("Shutting down...");
        server.Stop();
        await serialTransport.CloseAsync();
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        int index = Array.IndexOf(args, flag);
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }
        return null;
    }
}