using Aom.Core.Bindings;

namespace Aom.Core.Runtime;

public sealed class RuntimeViewStateReducer
{
    public RuntimeViewState Apply(RuntimeViewState currentState, RuntimeActionSnapshot actions)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(actions);

        var nextState = currentState with
        {
            IsSideView = actions.IsActive(BindingActionIds.SideViewHold),
            IsCustomView = actions.IsActive(BindingActionIds.CustomViewHold),
        };

        if (actions.WasPressed(BindingActionIds.GunViewToggle))
        {
            nextState = nextState with { IsGunViewAtCenter = !nextState.IsGunViewAtCenter };
        }

        if (actions.WasPressed(BindingActionIds.HeadCenter))
        {
            nextState = SetHeadState(nextState, isCenter: true, isHigh: false, isHighest: false, isDynamic: false);
        }

        if (actions.WasPressed(BindingActionIds.HeadHigh))
        {
            nextState = SetHeadState(nextState, isCenter: false, isHigh: true, isHighest: false, isDynamic: false);
        }

        if (actions.WasPressed(BindingActionIds.HeadHighest))
        {
            nextState = SetHeadState(nextState, isCenter: false, isHigh: false, isHighest: true, isDynamic: false);
        }

        if (actions.WasPressed(BindingActionIds.HeadDynamic))
        {
            nextState = SetHeadState(nextState, isCenter: false, isHigh: false, isHighest: false, isDynamic: true);
        }

        if (actions.WasPressed(BindingActionIds.ZoomIn))
        {
            nextState = nextState with { IsZoomIn = true, IsZoomOut = false };
        }

        if (actions.WasPressed(BindingActionIds.ZoomOut))
        {
            nextState = nextState with { IsZoomIn = false, IsZoomOut = true };
        }

        if (actions.WasPressed(BindingActionIds.ZoomCenter))
        {
            nextState = nextState with { IsZoomIn = false, IsZoomOut = false };
        }

        if (actions.IsActive(BindingActionIds.CenterAll))
        {
            nextState = nextState with
            {
                CenterPendingFrameTimer = 5,
                IsHeadCenter = false,
                IsHeadHigh = false,
                IsHeadHighest = false,
                IsHeadDynamic = false,
                IsZoomIn = false,
                IsZoomOut = false,
                IsSideView = false,
            };
        }

        return nextState;
    }

    private static RuntimeViewState SetHeadState(RuntimeViewState currentState, bool isCenter, bool isHigh, bool isHighest, bool isDynamic) =>
        currentState with
        {
            IsHeadCenter = isCenter,
            IsHeadHigh = isHigh,
            IsHeadHighest = isHighest,
            IsHeadDynamic = isDynamic,
        };
}