using Aom.Core.Bindings;
using Aom.Core.Presets;
using Aom.Core.Runtime;

namespace Aom.Core.Tuning;

public sealed class TuneModeController
{
    public TuneModeUpdateResult Apply(TuneModeState currentState, CameraPreset currentPreset, RuntimeActionSnapshot actions, RuntimeViewState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(currentPreset);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(runtimeState);

        var nextState = currentState;
        var nextPreset = currentPreset;
        var requestCenterSync = false;
        string? statusMessage = null;

        if (actions.WasPressed(BindingActionIds.ToggleTuneMode))
        {
            nextState = nextState.IsEnabled
                ? new TuneModeState()
                : new TuneModeState { IsEnabled = true };

            statusMessage = nextState.IsEnabled ? "TuneMode: Please select tune mode." : "Game mode.";
        }

        if (!nextState.IsEnabled)
        {
            return new TuneModeUpdateResult(nextState, nextPreset, false, statusMessage);
        }

        if (actions.WasPressed(BindingActionIds.CycleTuneMode))
        {
            var hadSelection = !string.IsNullOrWhiteSpace(nextState.SelectedModeName);
            nextState = nextState with { SelectedModeName = TuneModeCatalog.GetNextName(nextState.SelectedModeName) };
            requestCenterSync = hadSelection;
            statusMessage = TuneModeCatalog.GetDisplayName(nextState.SelectedModeName) ?? nextState.SelectedModeName;
        }

        if (string.IsNullOrWhiteSpace(nextState.SelectedModeName))
        {
            return new TuneModeUpdateResult(nextState, nextPreset, requestCenterSync, statusMessage);
        }

        nextPreset = ApplyAdjustments(nextPreset, nextState.SelectedModeName, actions, runtimeState);
        return new TuneModeUpdateResult(nextState, nextPreset, requestCenterSync, statusMessage);
    }

    private static CameraPreset ApplyAdjustments(CameraPreset currentPreset, string modeName, RuntimeActionSnapshot actions, RuntimeViewState runtimeState)
    {
        var result = currentPreset;
        var xyzDelta = modeName is "isAuto" or "isCenter" ? 0.01 : 0.05;

        result = ApplyAdjustment(result, modeName, TuneSubject.Yaw, GetNetDelta(actions, BindingActionIds.TuneAdjustYawPositive, BindingActionIds.TuneAdjustYawNegative, 0.5), runtimeState);
        result = ApplyAdjustment(result, modeName, TuneSubject.Pitch, GetNetDelta(actions, BindingActionIds.TuneAdjustPitchPositive, BindingActionIds.TuneAdjustPitchNegative, 0.5), runtimeState);
        result = ApplyAdjustment(result, modeName, TuneSubject.X, GetNetDelta(actions, BindingActionIds.TuneAdjustXPositive, BindingActionIds.TuneAdjustXNegative, xyzDelta), runtimeState);
        result = ApplyAdjustment(result, modeName, TuneSubject.Y, GetNetDelta(actions, BindingActionIds.TuneAdjustYPositive, BindingActionIds.TuneAdjustYNegative, xyzDelta), runtimeState);
        result = ApplyAdjustment(result, modeName, TuneSubject.Z, GetNetDelta(actions, BindingActionIds.TuneAdjustZPositive, BindingActionIds.TuneAdjustZNegative, xyzDelta), runtimeState);

        return result;
    }

    private static CameraPreset ApplyAdjustment(CameraPreset preset, string modeName, TuneSubject subject, double delta, RuntimeViewState runtimeState)
    {
        if (Math.Abs(delta) < double.Epsilon)
        {
            return preset;
        }

        var parameterName = ResolveParameterName(modeName, subject);
        if (parameterName is null)
        {
            return preset;
        }

        var nextValue = preset.GetValueOrDefault(parameterName) + delta;
        nextValue = ApplyMapper(modeName, subject, nextValue, delta, runtimeState);

        return preset.WithParameterValue(parameterName, nextValue);
    }

    private static string? ResolveParameterName(string modeName, TuneSubject subject) => (modeName, subject) switch
    {
        ("isAuto", TuneSubject.Yaw) => "manual_yaw",
        ("isAuto", TuneSubject.X) => "deltaX1",
        ("isAuto", TuneSubject.Y) => "deltaY1",
        ("isAuto", TuneSubject.Z) => "deltaZ1",
        ("isZoomIn", TuneSubject.Z) => "deltaZ2_1",
        ("isZoomOut", TuneSubject.Z) => "deltaZ2_2",
        ("isCenter", TuneSubject.X) => "deltaX0",
        ("isCenter", TuneSubject.Y) => "deltaY0",
        ("isManualShiftX", TuneSubject.X) => "deltaX2_1",
        ("isManualShiftY1", TuneSubject.Y) => "deltaY2_1",
        ("isManualShiftY2", TuneSubject.Y) => "deltaY2_2",
        ("isDynamicYLow", TuneSubject.Y) => "deltaYLow",
        ("isDynamicYHigh", TuneSubject.Y) => "deltaYHigh",
        ("isCustomView", TuneSubject.Yaw) => "syaw",
        ("isCustomView", TuneSubject.Pitch) => "spitch",
        ("isCustomView", TuneSubject.X) => "deltaX2_4",
        ("isCustomView", TuneSubject.Y) => "deltaY2_4",
        ("isCustomView", TuneSubject.Z) => "deltaZ2_4",
        _ => null,
    };

    private static double ApplyMapper(string modeName, TuneSubject subject, double value, double delta, RuntimeViewState runtimeState)
    {
        if (modeName == "isAuto" && subject == TuneSubject.Yaw)
        {
            return Math.Max(-runtimeState.AutoCornerEnd, Math.Min(-runtimeState.AutoCornerStart, value));
        }

        if (modeName == "isAuto" && subject is TuneSubject.X or TuneSubject.Y or TuneSubject.Z)
        {
            if (delta > 0 && value > -0.0004)
            {
                return Math.Max(0.0004, value);
            }

            if (delta < 0 && value < 0.0004)
            {
                return Math.Min(-0.0004, value);
            }
        }

        return value;
    }

    private static double GetNetDelta(RuntimeActionSnapshot actions, string positiveActionId, string negativeActionId, double magnitude)
    {
        var delta = 0d;

        if (actions.IsActive(positiveActionId))
        {
            delta += magnitude;
        }

        if (actions.IsActive(negativeActionId))
        {
            delta -= magnitude;
        }

        return delta;
    }

    private enum TuneSubject
    {
        X,
        Y,
        Z,
        Yaw,
        Pitch,
    }
}