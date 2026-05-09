namespace Aom.App.Services.TrackIr;

public sealed record TrackIrInputProfile(
    string Id,
    string DisplayName,
    double AutoCornerStart,
    double AutoCornerEnd,
    double AutoCornerXEnd,
    double YawScale,
    double PitchScale,
    double XDeadZone,
    double YEngageYawThreshold);