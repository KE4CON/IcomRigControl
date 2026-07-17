using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class PeriodicBeaconSchedulerTests
{
    [Fact]
    public void Start_SetsIsRunningTrue()
    {
        int callCount = 0;
        var scheduler = new PeriodicBeaconScheduler(() =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        scheduler.Start(TimeSpan.FromMilliseconds(50));

        Assert.True(scheduler.IsRunning);
        scheduler.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunningFalse()
    {
        var scheduler = new PeriodicBeaconScheduler(() => Task.CompletedTask);

        scheduler.Start(TimeSpan.FromMilliseconds(50));
        scheduler.Stop();

        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public async Task Start_CallsBeaconActionRepeatedlyAtInterval()
    {
        int callCount = 0;
        var scheduler = new PeriodicBeaconScheduler(() =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        scheduler.Start(TimeSpan.FromMilliseconds(50));
        await Task.Delay(220); // should allow roughly 3-4 firings
        scheduler.Stop();

        Assert.True(callCount >= 2, $"Expected at least 2 calls, got {callCount}");
    }

    [Fact]
    public void Start_ZeroOrNegativeInterval_DoesNotStart()
    {
        var scheduler = new PeriodicBeaconScheduler(() => Task.CompletedTask);

        scheduler.Start(TimeSpan.Zero);

        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public async Task Stop_StopsFurtherFirings()
    {
        int callCount = 0;
        var scheduler = new PeriodicBeaconScheduler(() =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        scheduler.Start(TimeSpan.FromMilliseconds(50));
        await Task.Delay(120);
        scheduler.Stop();

        int countAtStop = callCount;
        await Task.Delay(150); // give it time to fire again if it wrongly kept running

        Assert.Equal(countAtStop, callCount);
    }

    [Fact]
    public async Task Start_IfBeaconActionThrows_SchedulerKeepsRunning()
    {
        // A single failed beacon attempt (e.g. a transient audio device
        // error) should not silently kill future scheduled attempts —
        // matches this project's never-crash-the-loop pattern used
        // throughout (EmmcomBridge, RadioInfoUdpBroadcaster, etc.).
        int callCount = 0;
        var scheduler = new PeriodicBeaconScheduler(() =>
        {
            callCount++;
            if (callCount == 1) throw new InvalidOperationException("Simulated failure.");
            return Task.CompletedTask;
        });

        scheduler.Start(TimeSpan.FromMilliseconds(50));
        await Task.Delay(220);
        scheduler.Stop();

        Assert.True(callCount >= 2, $"Expected the scheduler to keep running after a failure, got {callCount} calls");
    }
}