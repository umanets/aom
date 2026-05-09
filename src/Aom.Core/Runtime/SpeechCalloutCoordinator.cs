namespace Aom.Core.Runtime;

public static class SpeechCalloutCoordinator
{
    public static SpeechCalloutDecision Select(SpeechCalloutState currentState, bool flapReminderDue, CheckSixSpeedCue speedCue)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        var speedCallout = ToSpeechCallout(speedCue);
        var nextCallout = SpeechCallout.None;

        if (flapReminderDue && speedCallout != SpeechCallout.None)
        {
            nextCallout = currentState.LastIssuedCallout == SpeechCallout.Flaps
                ? speedCallout
                : SpeechCallout.Flaps;
        }
        else if (flapReminderDue)
        {
            nextCallout = SpeechCallout.Flaps;
        }
        else if (speedCallout != SpeechCallout.None)
        {
            nextCallout = speedCallout;
        }

        var nextState = nextCallout == SpeechCallout.None
            ? currentState
            : currentState with { LastIssuedCallout = nextCallout };

        return new SpeechCalloutDecision(nextState, nextCallout);
    }

    private static SpeechCallout ToSpeechCallout(CheckSixSpeedCue cue) => cue switch
    {
        CheckSixSpeedCue.Low => SpeechCallout.CheckSixSpeedLow,
        CheckSixSpeedCue.Optimal => SpeechCallout.CheckSixSpeedOptimal,
        CheckSixSpeedCue.Danger => SpeechCallout.CheckSixSpeedDanger,
        _ => SpeechCallout.None,
    };
}