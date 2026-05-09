namespace Aom.App.Services.Overlay;

public readonly record struct OverlaySharedStateFrame(long Sequence, OverlaySharedStateSnapshot Snapshot);