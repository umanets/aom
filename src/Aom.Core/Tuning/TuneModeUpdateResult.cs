using Aom.Core.Presets;

namespace Aom.Core.Tuning;

public sealed record TuneModeUpdateResult(TuneModeState NextState, CameraPreset NextPreset, bool RequestCenterSync, string? StatusMessage);