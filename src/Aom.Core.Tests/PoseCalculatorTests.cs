using Aom.Core.Presets;
using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class PoseCalculatorTests
{
    private readonly PoseCalculator calculator = new();

    [Fact]
    public void GetValueOrDefault_ReturnsZeroForMissingParameter()
    {
        var preset = PresetCatalog.Default;

        Assert.Equal(0, preset.GetValueOrDefault("does-not-exist"));
    }

    [Fact]
    public void ComputeGamePose_ReturnsCustomViewPreset_WhenCustomViewIsEnabled()
    {
        var preset = PresetCatalog.All.Single(preset => preset.Id == "lagg");

        var result = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState { IsCustomView = true },
            new HeadPose(Yaw: 25, Pitch: 5, Roll: 1, X: 1, Y: 2, Z: 3),
            stickY: 0);

        Assert.Equal(-2.5, result.Pose.Yaw, 3);
        Assert.Equal(-1.0, result.Pose.Pitch, 3);
        Assert.Equal(0.0, result.Pose.X, 3);
        Assert.Equal(-4.45, result.Pose.Y, 3);
        Assert.Equal(8.45, result.Pose.Z, 3);
    }

    [Fact]
    public void ComputeGamePose_MapsDynamicHeadY_FromStickAxis()
    {
        var preset = PresetCatalog.All.Single(preset => preset.Id == "lagg");

        var result = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState { IsHeadDynamic = true },
            new HeadPose(Yaw: 0, Pitch: 0, Roll: 0, X: 0, Y: 0.2, Z: 0),
            stickY: 500);

        Assert.Equal(0.375, result.Pose.Y, 3);
        Assert.Equal(0.2, result.NextYOffset, 3);
    }

    [Fact]
    public void ComputeGamePose_AppliesAutoOffsets_WhenYawPassesCornerThreshold()
    {
        var preset = PresetCatalog.All.Single(preset => preset.Id == "lagg");

        var result = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState(),
            new HeadPose(Yaw: 85, Pitch: 2, Roll: 1, X: 0, Y: 0, Z: 0),
            stickY: 0);

        Assert.Equal(8.5, result.Pose.Yaw, 3);
        Assert.Equal(-0.0652, result.Pose.Y, 4);
        Assert.Equal(3.26, result.Pose.Z, 3);
    }

    [Fact]
    public void ComputeGamePose_AppliesManualSideViewShift_WhenSideViewIsEnabled()
    {
        var preset = PresetCatalog.All.Single(preset => preset.Id == "lagg");

        var result = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState { IsSideView = true },
            new HeadPose(Yaw: 50, Pitch: 0, Roll: 0, X: 0, Y: 0, Z: 0),
            stickY: 0);

        Assert.Equal(1.6, result.Pose.X, 3);
    }

    [Fact]
    public void ComputeGamePose_ConvergesToSameBackwardZForBothZoomStates()
    {
        var preset = PresetCatalog.All.Single(preset => preset.Id == "yakodin");
        var trackIrPose = new HeadPose(Yaw: 140, Pitch: 0, Roll: 0, X: 0, Y: 0, Z: 0);
        var expectedBackwardZ = preset.GetValueOrDefault("deltaZ1") + preset.GetValueOrDefault("deltaZ2_2");

        var zoomInResult = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState { IsZoomIn = true },
            trackIrPose,
            stickY: 0);

        var zoomOutResult = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState { IsZoomOut = true },
            trackIrPose,
            stickY: 0);

        Assert.Equal(expectedBackwardZ, zoomInResult.Pose.Z, 3);
        Assert.Equal(expectedBackwardZ, zoomOutResult.Pose.Z, 3);
    }

    [Fact]
    public void ComputeGamePose_UsesRuntimeTrackIrInputProfileParameters()
    {
        var preset = PresetCatalog.Default;

        var result = calculator.ComputeGamePose(
            preset,
            new RuntimeViewState
            {
                TrackIrYawScale = 0.2,
                TrackIrPitchScale = 0.25,
                TrackIrXDeadZone = 0.25,
                TrackIrYEngageYawThreshold = 20,
            },
            new HeadPose(Yaw: 30, Pitch: 4, Roll: 0, X: 0.4, Y: 0.5, Z: 0),
            stickY: 0);

        Assert.Equal(6.0, result.Pose.Yaw, 3);
        Assert.Equal(1.0, result.Pose.Pitch, 3);
        Assert.Equal(0.4, result.Pose.X, 3);
        Assert.Equal(0.5, result.Pose.Y, 3);
    }
}