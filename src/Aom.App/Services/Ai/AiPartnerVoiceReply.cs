namespace Aom.App.Services.Ai;

public sealed record AiPartnerVoiceReply(
	string PilotTranscript,
	string AiReply,
	byte[]? AiSpeechAudioBytes = null,
	string? AiSpeechAudioFormat = null,
	string? AiSpeechErrorMessage = null);