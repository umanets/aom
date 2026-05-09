namespace Aom.App.Services.Ai;

public static class AiPartnerScreenshotResizePolicy
{
    public const int MaxContextWidth = 1920;
    public const int MaxContextHeight = 1080;

    public static AiPartnerScreenshotSize FitInside1080p(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var scale = Math.Min(
            1d,
            Math.Min(
                MaxContextWidth / (double)width,
                MaxContextHeight / (double)height));

        return new AiPartnerScreenshotSize(
            Math.Max(1, (int)Math.Floor(width * scale)),
            Math.Max(1, (int)Math.Floor(height * scale)));
    }
}

public readonly record struct AiPartnerScreenshotSize(int Width, int Height);