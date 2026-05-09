namespace Aom.App.Services.Il2;

public sealed record Il2TelemetrySnapshot(
    uint Tick,
    float? EasMetersPerSecond,
    float? FlapsPosition,
    DateTimeOffset ReceivedAtUtc)
{
    public double? EasKilometersPerHour => EasMetersPerSecond is null
        ? null
        : EasMetersPerSecond.Value * 3.6d;

    public bool HasAnyValue => EasMetersPerSecond is not null || FlapsPosition is not null;
}