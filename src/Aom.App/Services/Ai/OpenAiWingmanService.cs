using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Aom.App.Services.Ai;

public sealed class OpenAiWingmanService : IDisposable
{
    public const string TranscriptionModel = "gpt-4o-mini-transcribe";
    public const string ResponseModel = "gpt-5.4-nano-2026-03-17";
    public const string SpeechModel = "gpt-4o-mini-tts";
    public const string SpeechVoice = "alloy";
    public const string SpeechResponseFormat = "mp3";

    private readonly HttpClient httpClient;
    private readonly bool disposeHttpClient;
    private readonly AiPartnerMapReferenceCatalog aiPartnerMapReferenceCatalog = new();

    public OpenAiWingmanService(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
        };
        disposeHttpClient = httpClient is null;
    }

    public async Task<AiPartnerVoiceReply> ExecuteTurnAsync(string apiKey, AiPartnerTurnRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(request);

        var pilotTranscript = await TranscribeAsync(apiKey, request.AudioWaveBytes, cancellationToken);
        if (string.IsNullOrWhiteSpace(pilotTranscript))
        {
            throw new InvalidOperationException("OpenAI returned an empty transcription.");
        }

        var aiReply = await RequestWingmanReplyAsync(apiKey, request, pilotTranscript, cancellationToken);
        if (string.IsNullOrWhiteSpace(aiReply))
        {
            throw new InvalidOperationException("OpenAI returned an empty reply.");
        }

        byte[]? aiSpeechAudioBytes = null;
        string? aiSpeechErrorMessage = null;
        try
        {
            aiSpeechAudioBytes = await RequestWingmanSpeechAsync(apiKey, aiReply.Trim(), cancellationToken);
        }
        catch (Exception exception)
        {
            aiSpeechErrorMessage = exception.Message;
        }

        return new AiPartnerVoiceReply(
            pilotTranscript.Trim(),
            aiReply.Trim(),
            aiSpeechAudioBytes,
            aiSpeechAudioBytes is null ? null : SpeechResponseFormat,
            aiSpeechErrorMessage);
    }

    public void Dispose()
    {
        if (disposeHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private async Task<string> TranscribeAsync(string apiKey, byte[] audioWaveBytes, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(new StringContent(TranscriptionModel), "model");

        var audioContent = new ByteArrayContent(audioWaveBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        multipartContent.Add(audioContent, "file", "ptt.wav");

        requestMessage.Content = multipartContent;

        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI transcription failed: {(int)response.StatusCode} {TrimForError(responseBody)}");
        }

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task<string> RequestWingmanReplyAsync(string apiKey, AiPartnerTurnRequest request, string pilotTranscript, CancellationToken cancellationToken)
    {
        var mapReferences = AiPartnerPromptBuilder.ShouldAttachMapReferences(pilotTranscript)
            ? aiPartnerMapReferenceCatalog.GetReferenceMaps()
            : Array.Empty<AiPartnerReferenceImage>();

        var userContent = new List<object>
        {
            new
            {
                type = "text",
                text = AiPartnerPromptBuilder.BuildUserPrompt(request.Briefing, request.TelemetrySummary, pilotTranscript, request.Screenshot, mapReferences),
            },
        };

        if (request.Screenshot is not null)
        {
            userContent.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:{request.Screenshot.MediaType};base64,{Convert.ToBase64String(request.Screenshot.ImageBytes)}",
                    detail = "high",
                },
            });
        }

        foreach (var mapReference in mapReferences)
        {
            userContent.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:{mapReference.MediaType};base64,{Convert.ToBase64String(mapReference.ImageBytes)}",
                    detail = "low",
                },
            });
        }

        var payload = new
        {
            model = ResponseModel,
            max_completion_tokens = 120,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = AiPartnerPromptBuilder.SystemPrompt,
                },
                new
                {
                    role = "user",
                    content = userContent,
                },
            },
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI reply failed: {(int)response.StatusCode} {TrimForError(responseBody)}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var message = choices[0].GetProperty("message");
        return message.TryGetProperty("content", out var content)
            ? ExtractText(content)
            : string.Empty;
    }

    private async Task<byte[]> RequestWingmanSpeechAsync(string apiKey, string aiReply, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = SpeechModel,
            voice = SpeechVoice,
            input = aiReply,
            response_format = SpeechResponseFormat,
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "audio/speech");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI speech failed: {(int)response.StatusCode} {TrimForError(responseBody)}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string ExtractText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Object && content.TryGetProperty("text", out var objectText))
        {
            return objectText.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Append(item.GetString());
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var text))
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }

    private static string TrimForError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return "No response body.";
        }

        var normalized = responseBody.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 320
            ? normalized
            : normalized[..320];
    }
}