namespace IcomRigControl.Services;

/// <summary>
/// A station-identification reminder: US FCC rules (§97.119) require identifying at
/// least every 10 minutes during a communication (and at the end). This counts down
/// the interval; when it reaches zero it latches a "you must identify" state (and
/// raises Elapsed) that stays up until the operator acknowledges — you can't miss it.
/// Pure logic (driven by one Tick per second) so it's fully testable. See CLAUDE.md
/// ID timer.
/// </summary>
public sealed class IdReminderTimer
{
    private int _intervalSeconds;

    public IdReminderTimer(int intervalSeconds = 600)
    {
        _intervalSeconds = Math.Max(1, intervalSeconds);
        Reset();
    }

    /// The reminder interval in seconds (default 600 = 10 minutes). Changing it resets.
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set { _intervalSeconds = Math.Max(1, value); Reset(); }
    }

    public int SecondsRemaining { get; private set; }

    /// True once the interval has elapsed and the operator hasn't acknowledged yet.
    public bool ReminderDue { get; private set; }

    /// Raised the moment the interval elapses (edge, once per cycle).
    public event Action? Elapsed;

    /// Advance one second. Call once per second while the reminder is enabled.
    public void Tick()
    {
        if (SecondsRemaining > 0) SecondsRemaining--;
        if (SecondsRemaining == 0 && !ReminderDue)
        {
            ReminderDue = true;
            Elapsed?.Invoke();
        }
    }

    /// The operator identified — restart the countdown.
    public void Acknowledge() => Reset();

    public void Reset()
    {
        SecondsRemaining = _intervalSeconds;
        ReminderDue = false;
    }

    /// "MM:SS" for display.
    public string Display => $"{SecondsRemaining / 60:00}:{SecondsRemaining % 60:00}";
}
