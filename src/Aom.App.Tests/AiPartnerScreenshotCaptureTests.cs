using Aom.App.Services.Ai;
using Xunit;

namespace Aom.App.Tests;

public sealed class AiPartnerScreenshotCaptureTests
{
    [Fact]
    public void FitInside1080p_Downscales4kFrame()
    {
        var size = AiPartnerScreenshotResizePolicy.FitInside1080p(3840, 2160);

        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
    }

    [Fact]
    public void FitInside1080p_PreservesSmallerFrame()
    {
        var size = AiPartnerScreenshotResizePolicy.FitInside1080p(1280, 720);

        Assert.Equal(1280, size.Width);
        Assert.Equal(720, size.Height);
    }

    [Fact]
    public void FitInside1080p_PreservesAspectRatioForUltrawideFrame()
    {
        var size = AiPartnerScreenshotResizePolicy.FitInside1080p(3440, 1440);

        Assert.Equal(1920, size.Width);
        Assert.Equal(803, size.Height);
    }

    [Fact]
    public void ShouldStartCapture_ReturnsTrueWhenEnabledAndCadenceElapsed()
    {
        var now = new DateTimeOffset(2026, 4, 30, 10, 0, 3, TimeSpan.Zero);
        var lastAttempt = new DateTimeOffset(2026, 4, 30, 10, 0, 0, TimeSpan.Zero);

        var shouldCapture = AiPartnerScreenshotCaptureGate.ShouldStartCapture(
            isEnabled: true,
            isCaptureInProgress: false,
            isAiTurnInFlight: false,
            cadenceSeconds: 3,
            nowUtc: now,
            lastAttemptedAtUtc: lastAttempt);

        Assert.True(shouldCapture);
    }

    [Fact]
    public void ShouldStartCapture_ReturnsFalseWhenCaptureAlreadyInProgress()
    {
        var now = new DateTimeOffset(2026, 4, 30, 10, 0, 5, TimeSpan.Zero);

        var shouldCapture = AiPartnerScreenshotCaptureGate.ShouldStartCapture(
            isEnabled: true,
            isCaptureInProgress: true,
            isAiTurnInFlight: false,
            cadenceSeconds: 3,
            nowUtc: now,
            lastAttemptedAtUtc: DateTimeOffset.MinValue);

        Assert.False(shouldCapture);
    }

    [Fact]
    public void ShouldStartCapture_ReturnsFalseBeforeCadenceElapses()
    {
        var now = new DateTimeOffset(2026, 4, 30, 10, 0, 2, TimeSpan.Zero);
        var lastAttempt = new DateTimeOffset(2026, 4, 30, 10, 0, 0, TimeSpan.Zero);

        var shouldCapture = AiPartnerScreenshotCaptureGate.ShouldStartCapture(
            isEnabled: true,
            isCaptureInProgress: false,
            isAiTurnInFlight: false,
            cadenceSeconds: 3,
            nowUtc: now,
            lastAttemptedAtUtc: lastAttempt);

        Assert.False(shouldCapture);
    }
}