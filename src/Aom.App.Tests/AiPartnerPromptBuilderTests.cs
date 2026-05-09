using Aom.App.Services.Ai;
using Xunit;

namespace Aom.App.Tests;

public sealed class AiPartnerPromptBuilderTests
{
    [Fact]
    public void ShouldAttachMapReferences_ReturnsTrueForLocationQueries()
    {
        Assert.True(AiPartnerPromptBuilder.ShouldAttachMapReferences("Где мы сейчас на карте?"));
        Assert.True(AiPartnerPromptBuilder.ShouldAttachMapReferences("Can you locate our position?"));
    }

    [Fact]
    public void ShouldAttachMapReferences_ReturnsFalseForRegularRadioTraffic()
    {
        Assert.False(AiPartnerPromptBuilder.ShouldAttachMapReferences("Проверка связи, как слышно?"));
    }

    [Fact]
    public void SystemPrompt_RequiresRussianReplies()
    {
        Assert.Contains("Always reply in Russian", AiPartnerPromptBuilder.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserPrompt_IncludesBriefingTelemetryTranscriptAndScreenshotSummary()
    {
        var screenshot = new AiPartnerScreenshotFrame(
            ImageBytes: new byte[] { 1, 2, 3 },
            MediaType: "image/jpeg",
            Width: 1920,
            Height: 1080,
            CapturedAtUtc: new DateTimeOffset(2026, 4, 30, 10, 11, 12, TimeSpan.Zero),
            WindowTitle: "IL-2 Sturmovik Great Battles");

        var prompt = AiPartnerPromptBuilder.BuildUserPrompt(
            briefing: "Escort bombers to target.",
            telemetrySummary: "Preset Yakodin | Speed: 260 km/h EAS | Flaps: closed",
            pilotTranscript: "Check left high.",
            screenshot: screenshot,
            mapReferences: new[]
            {
                new AiPartnerReferenceImage("Stalingrad", new byte[] { 1, 2, 3 }, "image/jpeg"),
                new AiPartnerReferenceImage("Moscow", new byte[] { 4, 5, 6 }, "image/jpeg"),
            });

        Assert.Contains("Escort bombers to target.", prompt, StringComparison.Ordinal);
        Assert.Contains("Preset Yakodin", prompt, StringComparison.Ordinal);
        Assert.Contains("Check left high.", prompt, StringComparison.Ordinal);
        Assert.Contains("Screenshot attached", prompt, StringComparison.Ordinal);
        Assert.Contains("1920x1080", prompt, StringComparison.Ordinal);
        Assert.Contains("in Russian", prompt, StringComparison.Ordinal);
        Assert.Contains("Stalingrad, Moscow", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserPrompt_FallsBackWhenOptionalFieldsAreMissing()
    {
        var prompt = AiPartnerPromptBuilder.BuildUserPrompt(
            briefing: null,
            telemetrySummary: null,
            pilotTranscript: "  ",
            screenshot: null,
            mapReferences: null);

        Assert.Contains("No mission briefing provided.", prompt, StringComparison.Ordinal);
        Assert.Contains("Telemetry unavailable.", prompt, StringComparison.Ordinal);
        Assert.Contains("[no transcription]", prompt, StringComparison.Ordinal);
        Assert.Contains("No screenshot attached.", prompt, StringComparison.Ordinal);
        Assert.Contains("No reference theater maps attached.", prompt, StringComparison.Ordinal);
    }
}