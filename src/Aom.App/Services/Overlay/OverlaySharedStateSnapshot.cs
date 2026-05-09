namespace Aom.App.Services.Overlay;

public sealed record OverlaySharedStateSnapshot(
    bool IsVisible,
    string CurrentPresetDisplayName,
    string LiveTrackIrStatus,
    string UdpStreamingStatus,
    string OutputPoseSummary,
    string RuntimeStateSummary,
    string TrackIrRateSummary,
    string UdpRateSummary,
    DateTimeOffset UpdatedAtUtc);