using Aom.Core.Runtime;

namespace Aom.App.Services.Il2;

public static class Il2TelemetryFlapMonitor
{
    private const float FlapsOpenThreshold = 0.01f;

    public static bool IsFlapOpen(float flapsPosition)
    {
        var clamped = Math.Clamp(flapsPosition, 0f, 1f);
        return clamped > FlapsOpenThreshold;
    }

    public static bool HasFreshFlapTelemetry(float? flapsPosition, DateTimeOffset lastFrameAt, DateTimeOffset sampledAt, TimeSpan staleThreshold)
    {
        if (flapsPosition is null || lastFrameAt == DateTimeOffset.MinValue)
        {
            return false;
        }

        return sampledAt - lastFrameAt <= staleThreshold;
    }

    public static FlapAutomationState CreateTrackedState(FlapAutomationState currentState, float flapsPosition)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        return currentState with
        {
            IsFlapOpen = IsFlapOpen(flapsPosition),
            ActiveOperation = FlapOperation.None,
            OperationStartedAt = null,
        };
    }
}