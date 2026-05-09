using Aom.Core.Presets;
using Aom.Core.Runtime;
using Aom.Core.Tuning;
using Xunit;

namespace Aom.Core.Tests;

public sealed class TuneModeRuntimePlannerTests
{
    private readonly TuneModeRuntimePlanner planner = new();

    [Fact]
    public void CreatePlan_ReturnsNullWhenTuneModeDisabled()
    {
        var plan = planner.CreatePlan(new TuneModeState(), PresetCatalog.Default, new RuntimeViewState());

        Assert.Null(plan);
    }

    [Fact]
    public void CreatePlan_ConfiguresCustomViewAndGunCenter()
    {
        var plan = planner.CreatePlan(
            new TuneModeState { IsEnabled = true, SelectedModeName = "isCustomView" },
            PresetCatalog.FindById("lagg")!,
            new RuntimeViewState { YOffset = 1.25 });

        Assert.NotNull(plan);
        Assert.True(plan!.ViewState.IsCustomView);
        Assert.True(plan.ViewState.IsGunViewAtCenter);
        Assert.Equal(1.25, plan.ViewState.YOffset, 6);
    }

    [Fact]
    public void CreatePlan_UsesManualYawForAutoMode()
    {
        var preset = PresetCatalog.FindById("lagg")!;
        var plan = planner.CreatePlan(
            new TuneModeState { IsEnabled = true, SelectedModeName = "isAuto" },
            preset,
            new RuntimeViewState());

        Assert.NotNull(plan);
        Assert.Equal(preset.GetValueOrDefault("manual_yaw"), plan!.TrackIrPose.Yaw, 6);
    }
}