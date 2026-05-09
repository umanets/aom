namespace Aom.App.Services.Ai;

public sealed record AiPartnerTurnRequest(
    string Briefing,
    string TelemetrySummary,
    byte[] AudioWaveBytes,
    AiPartnerScreenshotFrame? Screenshot,
    string ScreenshotStatusMessage);