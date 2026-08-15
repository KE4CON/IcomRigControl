using System;
using System.Threading.Tasks;
using IcomRigControl.CivEngine;
using IcomRigControl.RigModel;
using IcomRigControl.Services;

namespace IcomRigControl.UI;

/// <summary>
/// Headless remote-control server entry point (Phase 9). Connects to a real
/// radio via SerialCivTransport and serves it over TCP via CivTcpServer, with
/// no UI — intended for running on a Raspberry Pi next to the radio.
///
/// Usage: IcomRigControl.UI --headless-server --port [comport] --tcpport
/// [port] --token [authtoken] --model [IC7300|IC7300MK2|IC705]
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
            Console.WriteLine("Usage: IcomRigControl.UI --headless-server --port <comport> --tcpport <port> --token <authtoken> [--model IC7300|IC7300MK2|IC705] [--audioport <udpport> [--audiocapture <alsadev>] [--audioout <alsadev>]]");
            Console.WriteLine("Example: IcomRigControl.UI --headless-server --port /dev/ttyUSB0 --tcpport 7300 --token mysecret123");
            Console.WriteLine("With audio: ... --audioport 7301 --audiocapture plughw:1,0 --audioout plughw:1,0");
            return;
        }

        if (!int.TryParse(tcpPortStr, out int tcpPort))
        {
            Console.WriteLine($"Invalid --tcpport value: {tcpPortStr}");
            return;
        }

        var model = Enum.TryParse<RadioModel>(modelStr, ignoreCase: true, out var m)
            ? m
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

        // Phase 12: optionally also serve real-time audio. --audioport enables it;
        // --audiocapture / --audioout name the ALSA devices for the radio's RX
        // audio (input) and TX audio (output). The link runs in server mode
        // (learns the client's address) and always streams the radio's RX audio.
        RemoteAudioLink? audioLink = null;
        string? audioPortStr = GetArgValue(args, "--audioport");
        if (!string.IsNullOrWhiteSpace(audioPortStr) && int.TryParse(audioPortStr, out int audioPort))
        {
            string? captureDevice = GetArgValue(args, "--audiocapture");
            string? outputDevice = GetArgValue(args, "--audioout");
            try
            {
                audioLink = new RemoteAudioLink(AudioDevices.CreateCapture(), AudioDevices.CreateStreamOutput())
                {
                    SendEnabled = true // always stream the radio's RX audio to the client
                };
                audioLink.StartServer(audioPort, captureDevice, outputDevice);
                Console.WriteLine($"  Audio server on UDP port {audioPort} (capture={captureDevice ?? "default"}, out={outputDevice ?? "default"}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  WARNING: audio server failed to start: {ex.Message}");
                audioLink = null;
            }
        }

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
        if (audioLink is not null) await audioLink.DisposeAsync();
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