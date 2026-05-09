using Aom.Core.Bindings;
using Aom.Core.Runtime;
using Xunit;

namespace Aom.Core.Tests;

public sealed class RuntimeViewStateReducerTests
{
    private readonly RuntimeViewStateReducer reducer = new();

    [Fact]
    public void Apply_HoldActionsMirrorSideAndCustomView()
    {
        var nextState = reducer.Apply(
            new RuntimeViewState(),
            new RuntimeActionSnapshot(activeActionIds: new[] { BindingActionIds.SideViewHold, BindingActionIds.CustomViewHold }));

        Assert.True(nextState.IsSideView);
        Assert.True(nextState.IsCustomView);

        var clearedState = reducer.Apply(nextState, new RuntimeActionSnapshot());

        Assert.False(clearedState.IsSideView);
        Assert.False(clearedState.IsCustomView);
    }

    [Fact]
    public void Apply_PressActionsToggleGunViewAndHeadModes()
    {
        var nextState = reducer.Apply(
            new RuntimeViewState(),
            new RuntimeActionSnapshot(pressedActionIds: new[] { BindingActionIds.GunViewToggle, BindingActionIds.HeadHigh }));

        Assert.True(nextState.IsGunViewAtCenter);
        Assert.True(nextState.IsHeadHigh);
        Assert.False(nextState.IsHeadCenter);
        Assert.False(nextState.IsHeadDynamic);

        var toggledState = reducer.Apply(
            nextState,
            new RuntimeActionSnapshot(pressedActionIds: new[] { BindingActionIds.GunViewToggle, BindingActionIds.HeadDynamic }));

        Assert.False(toggledState.IsGunViewAtCenter);
        Assert.True(toggledState.IsHeadDynamic);
        Assert.False(toggledState.IsHeadHigh);
    }

    [Fact]
    public void Apply_CenterAllResetsZoomHeadAndSideView()
    {
        var currentState = new RuntimeViewState
        {
            CenterPendingFrameTimer = null,
            IsSideView = true,
            IsZoomIn = true,
            IsHeadHighest = true,
        };

        var nextState = reducer.Apply(
            currentState,
            new RuntimeActionSnapshot(activeActionIds: new[] { BindingActionIds.CenterAll }));

        Assert.False(nextState.IsSideView);
        Assert.False(nextState.IsZoomIn);
        Assert.False(nextState.IsZoomOut);
        Assert.Equal(5, nextState.CenterPendingFrameTimer);
        Assert.False(nextState.IsHeadCenter);
        Assert.False(nextState.IsHeadHigh);
        Assert.False(nextState.IsHeadHighest);
        Assert.False(nextState.IsHeadDynamic);
    }
}