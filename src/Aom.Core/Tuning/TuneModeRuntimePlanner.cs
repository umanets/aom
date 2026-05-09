using Aom.Core.Presets;
using Aom.Core.Runtime;

namespace Aom.Core.Tuning;

public sealed class TuneModeRuntimePlanner
{
    public TuneModeRuntimePlan? CreatePlan(TuneModeState state, CameraPreset preset, RuntimeViewState baseState)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(baseState);

        if (!state.IsEnabled)
        {
            return null;
        }

        var modeName = state.SelectedModeName;

        var runtimeState = baseState with
        {
            IsSideView = modeName == "isManualShiftX",
            IsHeadCenter = modeName == "isCenter",
            IsHeadHigh = modeName == "isManualShiftY1",
            IsHeadHighest = modeName == "isManualShiftY2",
            IsHeadDynamic = modeName is "isDynamicYLow" or "isDynamicYHigh",
            IsZoomIn = modeName == "isZoomIn",
            IsZoomOut = modeName is "isZoomOut" or "isCenter" or "isManualShiftY1" or "isManualShiftY2",
            IsCustomView = modeName == "isCustomView",
            IsGunViewAtCenter = true,
        };

        var yaw = modeName == "isAuto" ? preset.GetValueOrDefault("manual_yaw") : 0d;
        var stickY = modeName == "isDynamicYHigh" ? 1000d : 0d;
        var pose = new HeadPose(yaw, 0, 0, 0, 0, 0);

        return new TuneModeRuntimePlan(runtimeState, pose, stickY);
    }
}