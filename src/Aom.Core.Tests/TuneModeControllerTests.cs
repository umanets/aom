using Aom.Core.Bindings;
using Aom.Core.Presets;
using Aom.Core.Runtime;
using Aom.Core.Tuning;
using Xunit;

namespace Aom.Core.Tests;

public sealed class TuneModeControllerTests
{
    private readonly TuneModeController controller = new();

    [Fact]
    public void Apply_ToggleAndCycleEnableTuneModeAndAdvanceModes()
    {
        var preset = PresetCatalog.FindById("lagg")!;
        var runtimeState = new RuntimeViewState();

        var toggled = controller.Apply(
            new TuneModeState(),
            preset,
            new RuntimeActionSnapshot(pressedActionIds: new[] { BindingActionIds.ToggleTuneMode }),
            runtimeState);

        Assert.True(toggled.NextState.IsEnabled);
        Assert.Null(toggled.NextState.SelectedModeName);

        var firstMode = controller.Apply(
            toggled.NextState,
            preset,
            new RuntimeActionSnapshot(pressedActionIds: new[] { BindingActionIds.CycleTuneMode }),
            runtimeState);

        Assert.Equal("isAuto", firstMode.NextState.SelectedModeName);
        Assert.Equal("Auto", firstMode.StatusMessage);
        Assert.False(firstMode.RequestCenterSync);

        var secondMode = controller.Apply(
            firstMode.NextState,
            preset,
            new RuntimeActionSnapshot(pressedActionIds: new[] { BindingActionIds.CycleTuneMode }),
            runtimeState);

        Assert.Equal("isZoomIn", secondMode.NextState.SelectedModeName);
        Assert.Equal("Zoom In", secondMode.StatusMessage);
        Assert.True(secondMode.RequestCenterSync);
    }

    [Fact]
    public void Apply_AdjustsMappedCenterParameter()
    {
        var preset = PresetCatalog.FindById("lagg")!;

        var result = controller.Apply(
            new TuneModeState { IsEnabled = true, SelectedModeName = "isCenter" },
            preset,
            new RuntimeActionSnapshot(activeActionIds: new[] { BindingActionIds.TuneAdjustYPositive }),
            new RuntimeViewState());

        Assert.Equal(preset.GetValueOrDefault("deltaY0") + 0.01, result.NextPreset.GetValueOrDefault("deltaY0"), 6);
    }

    [Fact]
    public void Apply_ClampsAutoYawToCornerRange()
    {
        var preset = PresetCatalog.Default;

        var result = controller.Apply(
            new TuneModeState { IsEnabled = true, SelectedModeName = "isAuto" },
            preset,
            new RuntimeActionSnapshot(activeActionIds: new[] { BindingActionIds.TuneAdjustYawPositive }),
            new RuntimeViewState { AutoCornerStart = 30, AutoCornerEnd = 140 });

        Assert.Equal(-30, result.NextPreset.GetValueOrDefault("manual_yaw"), 6);
    }
}