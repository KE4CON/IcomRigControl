using System;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class SchedulerTests
{
    private static readonly ScheduledTask Task = new("1", "14:30", "Frequency", "14074000");

    [Fact]
    public void IsDue_WhenTimeMatches_AndNotYetFired()
    {
        var now = new DateTime(2026, 8, 15, 14, 30, 5, DateTimeKind.Utc);
        Assert.True(Scheduler.IsDue(Task, now, default));
    }

    [Fact]
    public void NotDue_IfAlreadyFiredThisMinute()
    {
        var now = new DateTime(2026, 8, 15, 14, 30, 45, DateTimeKind.Utc);
        var fired = new DateTime(2026, 8, 15, 14, 30, 5, DateTimeKind.Utc);
        Assert.False(Scheduler.IsDue(Task, now, fired));
    }

    [Fact]
    public void NotDue_AtADifferentMinute()
    {
        var now = new DateTime(2026, 8, 15, 14, 31, 0, DateTimeKind.Utc);
        Assert.False(Scheduler.IsDue(Task, now, default));
    }

    [Fact]
    public void NotDue_WhenDisabled()
    {
        var disabled = Task with { Enabled = false };
        var now = new DateTime(2026, 8, 15, 14, 30, 5, DateTimeKind.Utc);
        Assert.False(Scheduler.IsDue(disabled, now, default));
    }

    [Fact]
    public void FiresAgain_TheNextDay()
    {
        var today = new DateTime(2026, 8, 15, 14, 30, 5, DateTimeKind.Utc);
        var tomorrow = new DateTime(2026, 8, 16, 14, 30, 5, DateTimeKind.Utc);
        Assert.False(Scheduler.IsDue(Task, today, today));       // already fired today
        Assert.True(Scheduler.IsDue(Task, tomorrow, today));     // new day -> due again
    }
}
