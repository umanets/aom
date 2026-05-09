namespace Aom.Core.Runtime;

public sealed record FlapAutomationState
{
    public bool IsFlapOpen { get; init; }

    public FlapOperation ActiveOperation { get; init; }

    public DateTimeOffset? OperationStartedAt { get; init; }

    public DateTimeOffset LastReminderAt { get; init; }
}