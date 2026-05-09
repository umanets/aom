namespace Aom.Core.Runtime;

public sealed record RuntimeViewState
{
    public int? CenterPendingFrameTimer { get; init; }

    public bool IsSideView { get; init; }

    public bool IsHeadCenter { get; init; }

    public bool IsHeadHigh { get; init; }

    public bool IsHeadHighest { get; init; }

    public bool IsHeadDynamic { get; init; }

    public bool IsZoomIn { get; init; }

    public bool IsZoomOut { get; init; }

    public bool IsCustomView { get; init; }

    public bool IsGunViewAtCenter { get; init; }

    public double AutoCornerStart { get; init; } = 30.0;

    public double AutoCornerEnd { get; init; } = 140.0;

    public double AutoCornerXEnd { get; init; } = 140.0;

    public double TrackIrYawScale { get; init; } = 0.1;

    public double TrackIrPitchScale { get; init; } = 1.0;

    public double TrackIrXDeadZone { get; init; } = 1.0;

    public double TrackIrYEngageYawThreshold { get; init; } = 45.0;

    public double YOffset { get; init; }
}