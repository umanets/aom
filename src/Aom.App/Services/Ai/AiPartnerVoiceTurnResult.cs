namespace Aom.App.Services.Ai;

public sealed record AiPartnerVoiceTurnResult(
    bool Succeeded,
    string StatusMessage,
    string ScreenshotStatusMessage,
    AiPartnerScreenshotFrame? Screenshot,
    string? PilotTranscript = null,
    string? AiReply = null,
    byte[]? AiSpeechAudioBytes = null,
    string? AiSpeechAudioFormat = null)
{
    public static AiPartnerVoiceTurnResult Success(
        string pilotTranscript,
        string aiReply,
        byte[]? aiSpeechAudioBytes,
        string? aiSpeechAudioFormat,
        string? aiSpeechErrorMessage,
        string screenshotStatusMessage,
        AiPartnerScreenshotFrame? screenshot)
    {
        var statusMessage = aiSpeechAudioBytes is { Length: > 0 }
            ? "AI reply ready. OpenAI voice queued."
            : string.IsNullOrWhiteSpace(aiSpeechErrorMessage)
                ? "AI reply ready. Local voice queued."
                : "AI reply ready. OpenAI voice unavailable, local voice queued.";

        return new AiPartnerVoiceTurnResult(
            Succeeded: true,
            StatusMessage: statusMessage,
            ScreenshotStatusMessage: screenshotStatusMessage,
            Screenshot: screenshot,
            PilotTranscript: pilotTranscript,
            AiReply: aiReply,
            AiSpeechAudioBytes: aiSpeechAudioBytes,
            AiSpeechAudioFormat: aiSpeechAudioFormat);
    }

    public static AiPartnerVoiceTurnResult Failure(
        string statusMessage,
        string screenshotStatusMessage,
        AiPartnerScreenshotFrame? screenshot,
        string? pilotTranscript = null)
    {
        return new AiPartnerVoiceTurnResult(
            Succeeded: false,
            StatusMessage: statusMessage,
            ScreenshotStatusMessage: screenshotStatusMessage,
            Screenshot: screenshot,
            PilotTranscript: pilotTranscript,
            AiReply: null);
    }
}