using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class FlapAutomationControllerTests
{
    private readonly FlapAutomationController controller = new(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2));
    private readonly DateTimeOffset start = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_OpenRequestStartsTimedOpenSequence()
    {
        var first = controller.Apply(new FlapAutomationState(), start, openRequested: true, closeRequested: false);

        Assert.True(first.NextState.IsFlapOpen);
        Assert.Equal(FlapOperation.Opening, first.NextState.ActiveOperation);
        Assert.True(first.Output.PressOpenKeys);
        Assert.False(first.Output.ReleaseOpenKeys);

        var second = controller.Apply(first.NextState, start.AddSeconds(3), openRequested: false, closeRequested: false);

        Assert.True(second.NextState.IsFlapOpen);
        Assert.Equal(FlapOperation.None, second.NextState.ActiveOperation);
        Assert.True(second.Output.ReleaseOpenKeys);
    }

    [Fact]
    public void Apply_CloseRequestStartsTimedCloseSequence()
    {
        var current = new FlapAutomationState
        {
            IsFlapOpen = true,
            ActiveOperation = FlapOperation.None,
        };

        var first = controller.Apply(current, start, openRequested: false, closeRequested: true);

        Assert.False(first.NextState.IsFlapOpen);
        Assert.Equal(FlapOperation.Closing, first.NextState.ActiveOperation);
        Assert.True(first.Output.PressCloseKeys);

        var second = controller.Apply(first.NextState, start.AddSeconds(4), openRequested: false, closeRequested: false);

        Assert.False(second.NextState.IsFlapOpen);
        Assert.Equal(FlapOperation.None, second.NextState.ActiveOperation);
        Assert.True(second.Output.ReleaseCloseKeys);
    }

    [Fact]
    public void Apply_OpenWhileClosingReleasesCloseAndStartsOpen()
    {
        var current = new FlapAutomationState
        {
            IsFlapOpen = false,
            ActiveOperation = FlapOperation.Closing,
            OperationStartedAt = start,
        };

        var result = controller.Apply(current, start.AddSeconds(1), openRequested: true, closeRequested: false);

        Assert.True(result.NextState.IsFlapOpen);
        Assert.Equal(FlapOperation.Opening, result.NextState.ActiveOperation);
        Assert.True(result.Output.ReleaseCloseKeys);
        Assert.True(result.Output.PressOpenKeys);
    }

    [Fact]
    public void Apply_OpenFlapsSpeaksReminderEveryTwoSeconds()
    {
        var first = controller.Apply(new FlapAutomationState(), start, openRequested: true, closeRequested: false);

        Assert.False(first.ShouldSpeakReminder);

        var second = controller.Apply(first.NextState, start.AddSeconds(1), openRequested: false, closeRequested: false);

        Assert.False(second.ShouldSpeakReminder);

        var third = controller.Apply(second.NextState, start.AddSeconds(2), openRequested: false, closeRequested: false);

        Assert.True(third.ShouldSpeakReminder);
        Assert.Equal(start.AddSeconds(2), third.NextState.LastReminderAt);
    }

    [Fact]
    public void Apply_ClosedFlapsDoNotSpeakReminder()
    {
        var current = new FlapAutomationState
        {
            IsFlapOpen = true,
            LastReminderAt = start,
        };

        var result = controller.Apply(current, start.AddSeconds(3), openRequested: false, closeRequested: true);

        Assert.False(result.ShouldSpeakReminder);
    }

    [Fact]
    public void ObserveFlapState_TelemetryOpenSpeaksReminderEveryTwoSeconds()
    {
        var first = controller.ObserveFlapState(new FlapAutomationState(), start, flapsOpen: true);

        Assert.False(first.ShouldSpeakReminder);

        var second = controller.ObserveFlapState(first.NextState, start.AddSeconds(1), flapsOpen: true);

        Assert.False(second.ShouldSpeakReminder);

        var third = controller.ObserveFlapState(second.NextState, start.AddSeconds(2), flapsOpen: true);

        Assert.True(third.ShouldSpeakReminder);
        Assert.Equal(start.AddSeconds(2), third.NextState.LastReminderAt);
    }

    [Fact]
    public void ObserveFlapState_ClosedTelemetryDoesNotSpeakReminder()
    {
        var current = new FlapAutomationState
        {
            IsFlapOpen = true,
            LastReminderAt = start,
        };

        var result = controller.ObserveFlapState(current, start.AddSeconds(3), flapsOpen: false);

        Assert.False(result.NextState.IsFlapOpen);
        Assert.False(result.ShouldSpeakReminder);
    }
}