namespace IcomRigControl.Services;

/// <summary>
/// A scheduled station action: at a UTC time each day, do something (change
/// frequency/mode, send a beacon, start/stop recording, power on/off). Time is
/// "HH:mm" UTC. Used for scheduled nets and unattended (Pi) operation.
/// </summary>
public record ScheduledTask(string Id, string TimeUtc, string Action, string Parameter, bool Enabled = true);

/// <summary>
/// Fires ScheduledTasks when their UTC time arrives. Checks a few times a minute and
/// raises TaskDue once per task per minute (tracked so a task fires exactly once even
/// though the timer ticks several times within its minute). The due-decision is a pure
/// function so it's unit-testable. Action dispatch lives in the UI (which owns the
/// services). See CLAUDE.md scheduler.
/// </summary>
public sealed class Scheduler : IDisposable
{
    private readonly object _lock = new();
    private List<ScheduledTask> _tasks;
    private readonly Dictionary<string, DateTime> _lastFired = new();
    private readonly System.Threading.Timer _timer;

    public event Action<ScheduledTask>? TaskDue;

    public Scheduler(IEnumerable<ScheduledTask>? tasks = null)
    {
        _tasks = tasks?.ToList() ?? new List<ScheduledTask>();
        _timer = new System.Threading.Timer(_ => Check(DateTime.UtcNow), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(20));
    }

    public void SetTasks(IEnumerable<ScheduledTask> tasks)
    {
        lock (_lock) _tasks = tasks.ToList();
    }

    private void Check(DateTime nowUtc)
    {
        List<ScheduledTask> due = new();
        lock (_lock)
        {
            foreach (var t in _tasks)
            {
                if (IsDue(t, nowUtc, _lastFired.GetValueOrDefault(t.Id)))
                {
                    _lastFired[t.Id] = nowUtc;
                    due.Add(t);
                }
            }
        }
        foreach (var t in due) TaskDue?.Invoke(t);
    }

    /// True when the task should fire now: it's enabled, the current UTC hour:minute
    /// matches its time, and it hasn't already fired within this same minute.
    public static bool IsDue(ScheduledTask task, DateTime nowUtc, DateTime lastFired)
    {
        if (!task.Enabled) return false;
        if (!TimeOnly.TryParse(task.TimeUtc, out var t)) return false;

        bool sameMinute = nowUtc.Hour == t.Hour && nowUtc.Minute == t.Minute;
        bool firedThisMinute = lastFired != default &&
                               lastFired.Date == nowUtc.Date &&
                               lastFired.Hour == nowUtc.Hour &&
                               lastFired.Minute == nowUtc.Minute;
        return sameMinute && !firedThisMinute;
    }

    public void Dispose() => _timer.Dispose();
}
