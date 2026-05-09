namespace Aom.App.Services.Ai;

public static class AiPartnerPromptBuilder
{
    private static readonly string[] LocationPromptMarkers =
    {
        "where",
        "location",
        "position",
        "locate",
        "map",
        "grid",
        "sector",
        "где",
        "район",
        "квадрат",
        "сектор",
        "карте",
        "карта",
        "позици",
        "местополож",
        "локац",
        "ориентир",
    };

    public static string SystemPrompt =>
        "You are an AI wingman for offline IL-2 Great Battles flights. " +
        "Always reply in Russian using concise practical radio style. Keep replies under three short sentences. " +
        "Use the player's briefing, current telemetry, and screenshot if available. " +
        "Do not invent precise facts when the image or telemetry is unclear. " +
        "Prioritize immediate tactical guidance, threats, navigation, energy, and aircraft state awareness. " +
        "When reference theater maps are attached and the pilot asks for current position, identify the most likely theater or area, name visible landmarks if any, and state confidence as high, medium, or low. If uncertain, say so explicitly.";

    public static bool ShouldAttachMapReferences(string pilotTranscript)
    {
        if (string.IsNullOrWhiteSpace(pilotTranscript))
        {
            return false;
        }

        return LocationPromptMarkers.Any(marker => pilotTranscript.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildUserPrompt(
        string? briefing,
        string? telemetrySummary,
        string pilotTranscript,
        AiPartnerScreenshotFrame? screenshot,
        IReadOnlyList<AiPartnerReferenceImage>? mapReferences)
    {
        var normalizedBriefing = string.IsNullOrWhiteSpace(briefing)
            ? "No mission briefing provided."
            : briefing.Trim();

        var normalizedTelemetry = string.IsNullOrWhiteSpace(telemetrySummary)
            ? "Telemetry unavailable."
            : telemetrySummary.Trim();

        var normalizedTranscript = string.IsNullOrWhiteSpace(pilotTranscript)
            ? "[no transcription]"
            : pilotTranscript.Trim();

        var screenshotSummary = screenshot is null
            ? "No screenshot attached."
            : $"Screenshot attached from \"{screenshot.WindowTitle}\" at {screenshot.CapturedAtUtc.ToLocalTime():HH:mm:ss}, size {screenshot.Width}x{screenshot.Height}.";

        var mapReferenceSummary = mapReferences is { Count: > 0 }
            ? $"Reference theater maps attached:{Environment.NewLine}{string.Join(", ", mapReferences.Select(reference => reference.Label))}"
            : "No reference theater maps attached.";

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            $"Mission briefing:{Environment.NewLine}{normalizedBriefing}",
            $"Live telemetry:{Environment.NewLine}{normalizedTelemetry}",
            $"Player said:{Environment.NewLine}{normalizedTranscript}",
            screenshotSummary,
            mapReferenceSummary,
            "Answer as the wingman on the radio in Russian. If the pilot asks for location and reference maps are attached, estimate the theater, approximate area or grid if possible, the key landmarks you relied on, and confidence." );
    }
}