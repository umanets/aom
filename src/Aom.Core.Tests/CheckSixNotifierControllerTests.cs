using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class CheckSixNotifierControllerTests
{
    private readonly CheckSixNotifierController controller = new();
    private readonly DateTimeOffset start = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_ToggleEnablesNotifierAndResetsSpeechTimer()
    {
        var state = new CheckSixNotifierState
        {
            LastSpeechAt = start,
        };

        var result = controller.Apply(state, start.AddSeconds(1), speedKilometersPerHour: 260, toggleRequested: true);

        Assert.True(result.Toggled);
        Assert.True(result.NextState.IsActivated);
        Assert.False(result.ShouldNotify);
        Assert.Equal(start.AddSeconds(1), result.NextState.LastSpeechAt);
        Assert.Equal(CheckSixSpeedCue.Optimal, result.Cue);
    }

    [Fact]
    public void Apply_NotifiesOptimalSpeedEveryTwoSecondsWhenActivated()
    {
        var state = new CheckSixNotifierState
        {
            IsActivated = true,
            LastSpeechAt = start,
        };

        var result = controller.Apply(state, start.AddSeconds(2), speedKilometersPerHour: 260, toggleRequested: false);

        Assert.True(result.ShouldNotify);
        Assert.Equal(start.AddSeconds(2), result.NextState.LastSpeechAt);
        Assert.Equal(CheckSixSpeedCue.Optimal, result.Cue);
    }

    [Fact]
    public void Apply_UsesLowCueBelowOptimalBand()
    {
        var state = new CheckSixNotifierState
        {
            IsActivated = true,
            LastSpeechAt = start,
        };

        var result = controller.Apply(state, start.AddSeconds(2), speedKilometersPerHour: 240, toggleRequested: false);

        Assert.True(result.ShouldNotify);
        Assert.Equal(CheckSixSpeedCue.Low, result.Cue);
    }

    [Fact]
    public void Apply_UsesDangerCueAboveOptimalBand()
    {
        var state = new CheckSixNotifierState
        {
            IsActivated = true,
            LastSpeechAt = start,
        };

        var result = controller.Apply(state, start.AddSeconds(2), speedKilometersPerHour: 300, toggleRequested: false);

        Assert.True(result.ShouldNotify);
        Assert.Equal(CheckSixSpeedCue.Danger, result.Cue);
    }

    [Fact]
    public void Apply_DoesNotNotifyOutsideConfiguredBands()
    {
        var state = new CheckSixNotifierState
        {
            IsActivated = true,
            LastSpeechAt = start,
        };

        var result = controller.Apply(state, start.AddSeconds(2), speedKilometersPerHour: 405, toggleRequested: false);

        Assert.False(result.ShouldNotify);
        Assert.Equal(CheckSixSpeedCue.None, result.Cue);
    }
}