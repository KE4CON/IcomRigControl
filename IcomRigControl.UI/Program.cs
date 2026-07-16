using Avalonia;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace IcomRigControl.UI;

sealed class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (Array.IndexOf(args, "--headless-server") >= 0)
        {
            // The project is built as OutputType=WinExe (no console window
            // by default, since this is normally a GUI app). Attach to the
            // launching terminal's console so Console.WriteLine output from
            // headless mode is actually visible, on Windows specifically —
            // this is a no-op (and harmless) on macOS/Linux, where console
            // I/O works normally regardless of OutputType.
            if (OperatingSystem.IsWindows())
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
            }

            // Phase 9: headless remote-control server mode. Skips Avalonia
            // entirely — intended for running on a Pi next to the radio with
            // no display attached, serving CivTcpServer to remote clients.
            HeadlessServer.RunAsync(args).GetAwaiter().GetResult();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}