namespace Aom.Core.Runtime;

public sealed class RuntimeSyncPlanner
{
    public RuntimeSyncResult Apply(RuntimeViewState currentState)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        var timer = currentState.CenterPendingFrameTimer;
        if (timer is null)
        {
            return new RuntimeSyncResult(false, false, currentState, default);
        }

        if (timer == 0)
        {
            return new RuntimeSyncResult(true, true, currentState with { CenterPendingFrameTimer = -1 }, default);
        }

        if (timer == -5)
        {
            return new RuntimeSyncResult(true, false, currentState with { CenterPendingFrameTimer = null }, default);
        }

        return new RuntimeSyncResult(true, false, currentState with { CenterPendingFrameTimer = timer - 1 }, default);
    }
}