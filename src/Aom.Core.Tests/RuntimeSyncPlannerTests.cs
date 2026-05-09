using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class RuntimeSyncPlannerTests
{
    private readonly RuntimeSyncPlanner planner = new();

    [Fact]
    public void Apply_NoTimerProducesNormalFrame()
    {
        var state = new RuntimeViewState();

        var result = planner.Apply(state);

        Assert.False(result.IsSyncFrame);
        Assert.False(result.SendGlobalCenterPulse);
        Assert.Equal(state, result.NextState);
        Assert.Equal(default, result.Pose);
    }

    [Fact]
    public void Apply_CountsDownAndPulsesAtZero()
    {
        var currentState = new RuntimeViewState { CenterPendingFrameTimer = 1 };

        var first = planner.Apply(currentState);

        Assert.True(first.IsSyncFrame);
        Assert.False(first.SendGlobalCenterPulse);
        Assert.Equal(0, first.NextState.CenterPendingFrameTimer);

        var second = planner.Apply(first.NextState);

        Assert.True(second.IsSyncFrame);
        Assert.True(second.SendGlobalCenterPulse);
        Assert.Equal(-1, second.NextState.CenterPendingFrameTimer);
    }

    [Fact]
    public void Apply_ClearsTimerAfterTailFrames()
    {
        var result = planner.Apply(new RuntimeViewState { CenterPendingFrameTimer = -5 });

        Assert.True(result.IsSyncFrame);
        Assert.False(result.SendGlobalCenterPulse);
        Assert.Null(result.NextState.CenterPendingFrameTimer);
    }
}