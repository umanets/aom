using Aom.Core.Presets;

namespace Aom.Core.Runtime;

public sealed class PoseCalculator
{
    public GamePoseResult ComputeGamePose(CameraPreset preset, RuntimeViewState viewState, HeadPose trackIrPose, double stickY)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(viewState);

        var deltaZ =
            (preset.GetValueOrDefault("deltaZ2_1") * ToFlag(viewState.IsZoomIn)) +
            (preset.GetValueOrDefault("deltaZ2_2") * ToFlag(viewState.IsZoomOut));
        var (autoX, autoY, autoZ) = ComputeAutoOffsets(preset, viewState, trackIrPose.Yaw, deltaZ);
        var deltaX = ComputeManualX(preset, viewState, trackIrPose.Yaw);
        var deltaY = ComputeManualY(preset, viewState, stickY);

        if (viewState.IsCustomView)
        {
            return new GamePoseResult(
                new HeadPose(
                    Yaw: preset.GetValueOrDefault("syaw"),
                    Pitch: preset.GetValueOrDefault("spitch"),
                    Roll: 0,
                    X: preset.GetValueOrDefault("deltaX2_4"),
                    Y: preset.GetValueOrDefault("deltaY2_4"),
                    Z: preset.GetValueOrDefault("deltaZ2_4")),
                viewState.YOffset);
        }

        return ComputeFakePose(preset, viewState, trackIrPose, autoX, autoY, autoZ, deltaX, deltaY, deltaZ);
    }

    private static GamePoseResult ComputeFakePose(
        CameraPreset preset,
        RuntimeViewState viewState,
        HeadPose trackIrPose,
        double autoX,
        double autoY,
        double autoZ,
        double deltaX,
        double deltaY,
        double deltaZ)
    {
        var isYOn = Math.Abs(trackIrPose.Yaw) >= viewState.TrackIrYEngageYawThreshold;
        var fakeYaw = trackIrPose.Yaw * viewState.TrackIrYawScale;
        var fakePitch = trackIrPose.Pitch * viewState.TrackIrPitchScale;

        var fakeTempX = Math.Abs(autoX) < double.Epsilon ? trackIrPose.X : autoX;
        var xDirection = ApplyDeadZone(fakeTempX, viewState.TrackIrXDeadZone);
        var fakeX = xDirection + deltaX + (preset.GetValueOrDefault("deltaX0") * ToFlag(viewState.IsGunViewAtCenter));

        var fakeY =
            (trackIrPose.Y * ToFlag(isYOn)) +
            deltaY +
            autoY +
            (preset.GetValueOrDefault("deltaY0") * ToFlag(viewState.IsGunViewAtCenter)) -
            (viewState.YOffset * ToFlag(isYOn));

        var fakeZ = trackIrPose.Z + deltaZ + autoZ;
        var nextYOffset = isYOn ? viewState.YOffset : trackIrPose.Y;

        return new GamePoseResult(new HeadPose(fakeYaw, fakePitch, 0, fakeX, fakeY, fakeZ), nextYOffset);
    }

    private static (double X, double Y, double Z) ComputeAutoOffsets(CameraPreset preset, RuntimeViewState viewState, double yaw, double deltaZ)
    {
        if (Math.Abs(yaw) <= viewState.AutoCornerStart)
        {
            return (0, 0, 0);
        }

        var autoZAtBackwardLimit = ComputeAutoZAtBackwardLimit(preset, viewState, deltaZ);

        return (
            MapRange(yaw, viewState.AutoCornerStart, viewState.AutoCornerEnd, 0, preset.GetValueOrDefault("deltaX1")),
            MapRange(Math.Abs(yaw), viewState.AutoCornerStart, viewState.AutoCornerEnd, 0, preset.GetValueOrDefault("deltaY1")),
            MapRange(Math.Abs(yaw), viewState.AutoCornerStart, viewState.AutoCornerEnd, 0, autoZAtBackwardLimit));
    }

    private static double ComputeAutoZAtBackwardLimit(CameraPreset preset, RuntimeViewState viewState, double deltaZ)
    {
        var baseAutoZ = preset.GetValueOrDefault("deltaZ1");
        if (!viewState.IsZoomIn && !viewState.IsZoomOut)
        {
            return baseAutoZ;
        }

        var closestZoomZ = Math.Max(0d, Math.Max(preset.GetValueOrDefault("deltaZ2_1"), preset.GetValueOrDefault("deltaZ2_2")));
        return (baseAutoZ + closestZoomZ) - deltaZ;
    }

    private static double ComputeManualX(CameraPreset preset, RuntimeViewState viewState, double yaw)
    {
        if (!viewState.IsSideView)
        {
            return 0;
        }

        var mirrorBoundary = Math.Abs(viewState.AutoCornerXEnd);
        var x = preset.GetValueOrDefault("deltaX2_1");

        if (-mirrorBoundary <= yaw && yaw <= 0)
        {
            return -Math.Abs(x);
        }

        if (0 < yaw && yaw < mirrorBoundary)
        {
            return Math.Abs(x);
        }

        if (yaw < -mirrorBoundary)
        {
            return Math.Abs(x * 3);
        }

        if (mirrorBoundary <= yaw)
        {
            return -Math.Abs(x * 3);
        }

        return 0;
    }

    private static double ComputeManualY(CameraPreset preset, RuntimeViewState viewState, double stickY)
    {
        if (viewState.IsHeadCenter)
        {
            return preset.GetValueOrDefault("deltaY0");
        }

        if (viewState.IsHeadHigh)
        {
            return preset.GetValueOrDefault("deltaY2_1");
        }

        if (viewState.IsHeadHighest)
        {
            return preset.GetValueOrDefault("deltaY2_2");
        }

        if (viewState.IsHeadDynamic)
        {
            return MapRange(stickY, 0, 1000, preset.GetValueOrDefault("deltaYLow"), preset.GetValueOrDefault("deltaYHigh"));
        }

        return 0;
    }

    private static double MapRange(double value, double inputMin, double inputMax, double outputMin, double outputMax)
    {
        if (Math.Abs(inputMax - inputMin) < double.Epsilon)
        {
            return outputMin;
        }

        var normalized = (value - inputMin) / (inputMax - inputMin);
        return outputMin + (normalized * (outputMax - outputMin));
    }

    private static double ApplyDeadZone(double value, double threshold) => Math.Abs(value) >= threshold ? value : 0;

    private static double ToFlag(bool value) => value ? 1d : 0d;
}