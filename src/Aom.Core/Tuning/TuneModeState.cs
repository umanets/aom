namespace Aom.Core.Tuning;

public sealed record TuneModeState
{
    public bool IsEnabled { get; init; }

    public string? SelectedModeName { get; init; }
}