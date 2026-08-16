using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class IdReminderTimerTests
{
    [Fact]
    public void Elapses_Once_AfterTheInterval_ThenLatchesUntilAcknowledged()
    {
        var t = new IdReminderTimer(intervalSeconds: 3);
        int fired = 0;
        t.Elapsed += () => fired++;

        t.Tick(); Assert.False(t.ReminderDue); // 2 left
        t.Tick(); Assert.False(t.ReminderDue); // 1 left
        t.Tick();                               // 0 -> due
        Assert.True(t.ReminderDue);
        Assert.Equal(1, fired);

        t.Tick(); t.Tick();                     // stays latched, no re-fire
        Assert.True(t.ReminderDue);
        Assert.Equal(1, fired);

        t.Acknowledge();                        // operator identified
        Assert.False(t.ReminderDue);
        Assert.Equal(3, t.SecondsRemaining);
    }

    [Fact]
    public void Display_FormatsMinutesAndSeconds()
    {
        var t = new IdReminderTimer(intervalSeconds: 600);
        Assert.Equal("10:00", t.Display);
        t.Tick();
        Assert.Equal("09:59", t.Display);
    }
}
