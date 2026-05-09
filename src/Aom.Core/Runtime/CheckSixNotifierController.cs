namespace Aom.Core.Runtime;

public sealed class CheckSixNotifierController
{
    private readonly TimeSpan speechInterval;
    private readonly double optimalMinSpeedKilometersPerHour;
    private readonly double optimalMaxSpeedKilometersPerHour;
    private readonly double dangerMaxSpeedKilometersPerHour;

    public CheckSixNotifierController(
        double optimalMinSpeedKilometersPerHour = 250,
        double optimalMaxSpeedKilometersPerHour = 270,
        double dangerMaxSpeedKilometersPerHour = 400,
        TimeSpan? speechInterval = null)
    {
        this.optimalMinSpeedKilometersPerHour = optimalMinSpeedKilometersPerHour;
        this.optimalMaxSpeedKilometersPerHour = optimalMaxSpeedKilometersPerHour;
        this.dangerMaxSpeedKilometersPerHour = dangerMaxSpeedKilometersPerHour;
        this.speechInterval = speechInterval ?? TimeSpan.FromSeconds(2);
    }

    public CheckSixNotifierResult Apply(CheckSixNotifierState currentState, DateTimeOffset now, double? speedKilometersPerHour, bool toggleRequested)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        var nextState = currentState;
        var toggled = false;

        if (toggleRequested)
        {
            nextState = nextState with
            {
                IsActivated = !nextState.IsActivated,
                LastSpeechAt = now,
            };
            toggled = true;
        }

        var cue = ResolveCue(speedKilometersPerHour);

        var shouldNotify =
            nextState.IsActivated &&
            cue != CheckSixSpeedCue.None &&
            now - nextState.LastSpeechAt >= speechInterval;

        if (shouldNotify)
        {
            nextState = nextState with { LastSpeechAt = now };
        }

        return new CheckSixNotifierResult(nextState, shouldNotify, toggled, cue);
    }

    private CheckSixSpeedCue ResolveCue(double? speedKilometersPerHour)
    {
        if (speedKilometersPerHour is null)
        {
            return CheckSixSpeedCue.None;
        }

        if (speedKilometersPerHour.Value < optimalMinSpeedKilometersPerHour)
        {
            return CheckSixSpeedCue.Low;
        }

        if (speedKilometersPerHour.Value <= optimalMaxSpeedKilometersPerHour)
        {
            return CheckSixSpeedCue.Optimal;
        }

        if (speedKilometersPerHour.Value < dangerMaxSpeedKilometersPerHour)
        {
            return CheckSixSpeedCue.Danger;
        }

        return CheckSixSpeedCue.None;
    }
}