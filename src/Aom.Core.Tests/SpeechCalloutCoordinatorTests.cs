using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class SpeechCalloutCoordinatorTests
{
    [Fact]
    public void Select_ReturnsFlapsWhenOnlyFlapsAreDue()
    {
        var result = SpeechCalloutCoordinator.Select(new SpeechCalloutState(), flapReminderDue: true, speedCue: CheckSixSpeedCue.None);

        Assert.Equal(SpeechCallout.Flaps, result.Callout);
    }

    [Fact]
    public void Select_ReturnsSpeedWhenOnlySpeedCueIsDue()
    {
        var result = SpeechCalloutCoordinator.Select(new SpeechCalloutState(), flapReminderDue: false, speedCue: CheckSixSpeedCue.Optimal);

        Assert.Equal(SpeechCallout.CheckSixSpeedOptimal, result.Callout);
    }

    [Fact]
    public void Select_AlternatesWhenFlapsAndSpeedAreBothDue()
    {
        var first = SpeechCalloutCoordinator.Select(new SpeechCalloutState(), flapReminderDue: true, speedCue: CheckSixSpeedCue.Danger);
        var second = SpeechCalloutCoordinator.Select(first.NextState, flapReminderDue: true, speedCue: CheckSixSpeedCue.Danger);

        Assert.Equal(SpeechCallout.Flaps, first.Callout);
        Assert.Equal(SpeechCallout.CheckSixSpeedDanger, second.Callout);
    }
}