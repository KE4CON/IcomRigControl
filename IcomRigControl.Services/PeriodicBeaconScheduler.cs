namespace IcomRigControl.Services;

/// <summary>
/// Fires a given async action repeatedly at a configured interval, until
/// stopped. Used to drive automatic periodic APRS beaconing without
/// coupling the timing logic to the UI ViewModel — see CLAUDE.md Phase 10.
/// A single failed beacon attempt never stops future scheduled attempts,
/// matching this project's never-crash-the-loop pattern used throughout
/// (EmmcomBridge, RadioInfoUdpBroadcaster, etc.).
/// </summary>
public class PeriodicBeaconScheduler
{
    private readonly Func<Task> _beaconAction;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsRunning { get; private set; }

    public PeriodicBeaconScheduler(Func<Task> beaconAction)
    {
        _beaconAction = beaconAction;
    }

    public void Start(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) return;
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _loopTask = Task.Run(() => LoopAsync(interval, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        IsRunning = false;
    }

    private async Task LoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await _beaconAction();
                }
                catch
                {
                    // Never let one failed beacon attempt kill future
                    // scheduled attempts — a transient audio device or
                    // radio comms error shouldn't silently end all
                    // future automatic beaconing.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown via Stop() — nothing to do.
        }
    }
}