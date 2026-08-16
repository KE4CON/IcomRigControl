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

        bool webMode = !string.IsNullOrWhiteSpace(GetArgValue(args, "--webport"));

        // Raw-relay mode needs --tcpport + --token; web mode (--webport) does not.
        if (string.IsNullOrWhiteSpace(serialPort) ||
            (!webMode && (string.IsNullOrWhiteSpace(tcpPortStr) || string.IsNullOrWhiteSpace(authToken))))
        {
            Console.WriteLine("Usage (raw CI-V relay): IcomRigControl.UI --headless-server --port <comport> --tcpport <port> --token <authtoken> [--model IC7300|IC7300MK2|IC705] [--audioport <udpport> [--audiocapture <alsadev>] [--audioout <alsadev>]]");
            Console.WriteLine("Usage (phone/tablet web UI): IcomRigControl.UI --headless-server --port <comport> --webport <port> [--webtoken <token>] [--model IC7300|IC7300MK2|IC705]");
            Console.WriteLine("Example: IcomRigControl.UI --headless-server --port /dev/ttyUSB0 --tcpport 7300 --token mysecret123");
            Console.WriteLine("Web example: IcomRigControl.UI --headless-server --port /dev/ttyUSB0 --webport 8080 --webtoken mysecret123");
            return;
        }

        int tcpPort = 0;
        if (!webMode && !int.TryParse(tcpPortStr, out tcpPort))
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

        // Web mode: if --webport is given, serve the mobile web UI. This needs a
        // Transceiver that OWNS the serial port (it polls for state), so it cannot
        // share the port with the raw-CI-V relay (CivTcpServer). Web mode therefore
        // replaces the raw relay — pick one per Pi (a CI-V multiplexer to run both
        // at once is future work). See CLAUDE.md web remote.
        string? webPortStr = GetArgValue(args, "--webport");
        if (!string.IsNullOrWhiteSpace(webPortStr) && int.TryParse(webPortStr, out int webPort))
        {
            string? webToken = GetArgValue(args, "--webtoken") ?? authToken;
            var rig = new Transceiver(serialTransport, model);
            await rig.ConnectAsync();
            rig.StartPolling(TimeSpan.FromMilliseconds(500));
            await rig.StartScopeAsync(TimeSpan.FromMilliseconds(300)); // feed the browser waterfall

            // The meter poll doesn't read frequency/mode; refresh them so the
            // browser shows the real dial (and reflects front-panel tuning).
            var freqCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!freqCts.IsCancellationRequested)
                {
                    try { await rig.RefreshFrequencyAndModeAsync(freqCts.Token); } catch { }
                    try { await Task.Delay(700, freqCts.Token); } catch { break; }
                }
            });

            // --audiocapture names the ALSA device for the radio's RX audio (browser Listen).
            string? webAudioDevice = GetArgValue(args, "--audiocapture");
            var webServer = new WebRemoteServer(rig, webToken, webPort,
                captureFactory: AudioDevices.CreateCapture, captureDevice: webAudioDevice);
            webServer.Start();
            Console.WriteLine($"  Web remote on http://<this-Pi-IP>:{webPort} (token {(string.IsNullOrWhiteSpace(webToken) ? "none" : "set")}).");
            foreach (string url in WebRemoteServer.GetLanUrls(webPort))
                Console.WriteLine($"    {url}");
            Console.WriteLine("  (Web mode owns the serial port; the raw CI-V TCP relay is not started.)");
            Console.WriteLine("  Server running. Press Ctrl+C to stop.");

            var webExit = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; webExit.TrySetResult(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => webExit.TrySetResult();
            await webExit.Task;

            Console.WriteLine("Shutting down...");
            freqCts.Cancel();
            await webServer.DisposeAsync();
            await rig.DisposeAsync();
            await serialTransport.CloseAsync();
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