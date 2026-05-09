namespace Aom.App.Services.Ai;

public static class AiPartnerScreenshotCaptureGate
{
    public static bool ShouldStartCapture(
        bool isEnabled,
        bool isCaptureInProgress,
        bool isAiTurnInFlight,
        int cadenceSeconds,
        DateTimeOffset nowUtc,
        DateTimeOffset lastAttemptedAtUtc)
    {
        if (!isEnabled || isCaptureInProgress || isAiTurnInFlight)
        {
            return false;
        }

        var cadence = TimeSpan.FromSeconds(Math.Max(1, cadenceSeconds));
        return lastAttemptedAtUtc == DateTimeOffset.MinValue || nowUtc - lastAttemptedAtUtc >= cadence;
    }
}