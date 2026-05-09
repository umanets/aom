namespace Aom.Core.Runtime;

public sealed record CheckSixNotifierState
{
    public bool IsActivated { get; init; }

    public DateTimeOffset LastSpeechAt { get; init; }

    public DateTimeOffset LastYawBeyondThresholdAt { get; init; }
}