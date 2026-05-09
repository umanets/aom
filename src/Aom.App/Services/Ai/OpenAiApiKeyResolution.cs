namespace Aom.App.Services.Ai;

public sealed record OpenAiApiKeyResolution(string? ApiKey, string SourceDescription)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}