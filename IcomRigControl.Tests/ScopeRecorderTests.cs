using IcomRigControl.RigModel;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

public class ScopeRecorderTests
{
    [Fact]
    public void BuffersFrames_AndKeepsMostRecentUpToCapacity()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        using var rec = new ScopeRecorder(rig, maxFrames: 3);

        for (int i = 0; i < 5; i++)
            rec.Add(new[] { i, i, i });

        var frames = rec.GetFrames();
        Assert.Equal(3, frames.Count);   // capped
        Assert.Equal(2, frames[0][0]);   // oldest kept is frame #2
        Assert.Equal(4, frames[^1][0]);  // newest is frame #4
    }

    [Fact]
    public void IgnoresEmptyFrames_AndClearsCleanly()
    {
        var rig = new Transceiver(new FakeCivTransport(), RadioModel.IC7300);
        using var rec = new ScopeRecorder(rig, maxFrames: 10);
        rec.Add(new int[0]);
        Assert.Equal(0, rec.FrameCount);
        rec.Add(new[] { 1, 2, 3 });
        Assert.Equal(1, rec.FrameCount);
        rec.Clear();
        Assert.Equal(0, rec.FrameCount);
    }
}
