namespace Aom.App.Services.Videos;

public sealed record Il2VideoEditExportProgress(
    double Completion,
    string Stage,
    string Detail)
{
    public double ClampedCompletion => Math.Clamp(Completion, 0.0, 1.0);

    public int Percent => (int)Math.Round(ClampedCompletion * 100, MidpointRounding.AwayFromZero);
}