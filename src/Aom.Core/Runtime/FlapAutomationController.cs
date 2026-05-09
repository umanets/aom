namespace Aom.Core.Runtime;

public sealed class FlapAutomationController
{
    private readonly TimeSpan openDuration;
    private readonly TimeSpan closeDuration;
    private readonly TimeSpan reminderInterval;

    public FlapAutomationController(TimeSpan? openDuration = null, TimeSpan? closeDuration = null, TimeSpan? reminderInterval = null)
    {
        this.openDuration = openDuration ?? TimeSpan.FromSeconds(3);
        this.closeDuration = closeDuration ?? TimeSpan.FromSeconds(4);
        this.reminderInterval = reminderInterval ?? TimeSpan.FromSeconds(2);
    }

    public FlapAutomationResult Apply(FlapAutomationState currentState, DateTimeOffset now, bool openRequested, bool closeRequested)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        var nextState = currentState;
        var output = FlapAutomationOutput.None;

        if (openRequested)
        {
            if (currentState.ActiveOperation == FlapOperation.Closing)
            {
                output = output with { ReleaseCloseKeys = true };
            }

            if (currentState.ActiveOperation == FlapOperation.Closing || !currentState.IsFlapOpen)
            {
                nextState = new FlapAutomationState
                {
                    IsFlapOpen = true,
                    ActiveOperation = FlapOperation.Opening,
                    OperationStartedAt = now,
                    LastReminderAt = now,
                };
                output = output with { PressOpenKeys = true };
            }
        }
        else if (closeRequested)
        {
            if (currentState.ActiveOperation == FlapOperation.Opening)
            {
                output = output with { ReleaseOpenKeys = true };
            }

            nextState = new FlapAutomationState
            {
                IsFlapOpen = false,
                ActiveOperation = FlapOperation.Closing,
                OperationStartedAt = now,
                LastReminderAt = currentState.LastReminderAt,
            };
            output = output with { PressCloseKeys = true };
        }

        if (nextState.OperationStartedAt is null)
        {
            return BuildResult(nextState, output, now);
        }

        var elapsed = now - nextState.OperationStartedAt.Value;

        if (nextState.ActiveOperation == FlapOperation.Opening && elapsed >= openDuration)
        {
            nextState = nextState with
            {
                ActiveOperation = FlapOperation.None,
                OperationStartedAt = null,
            };
            output = output with { ReleaseOpenKeys = true };
        }
        else if (nextState.ActiveOperation == FlapOperation.Closing && elapsed >= closeDuration)
        {
            nextState = nextState with
            {
                ActiveOperation = FlapOperation.None,
                OperationStartedAt = null,
            };
            output = output with { ReleaseCloseKeys = true };
        }

        return BuildResult(nextState, output, now);
    }

    public FlapAutomationResult ObserveFlapState(FlapAutomationState currentState, DateTimeOffset now, bool flapsOpen)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        FlapAutomationState nextState;
        if (flapsOpen)
        {
            nextState = currentState.IsFlapOpen
                ? currentState with
                {
                    ActiveOperation = FlapOperation.None,
                    OperationStartedAt = null,
                }
                : new FlapAutomationState
                {
                    IsFlapOpen = true,
                    ActiveOperation = FlapOperation.None,
                    OperationStartedAt = null,
                    LastReminderAt = now,
                };
        }
        else
        {
            nextState = currentState with
            {
                IsFlapOpen = false,
                ActiveOperation = FlapOperation.None,
                OperationStartedAt = null,
            };
        }

        return BuildResult(nextState, FlapAutomationOutput.None, now);
    }

    private FlapAutomationResult BuildResult(FlapAutomationState state, FlapAutomationOutput output, DateTimeOffset now)
    {
        var shouldSpeakReminder =
            state.IsFlapOpen &&
            now - state.LastReminderAt >= reminderInterval;

        if (shouldSpeakReminder)
        {
            state = state with { LastReminderAt = now };
        }

        return new FlapAutomationResult(state, output, shouldSpeakReminder);
    }
}