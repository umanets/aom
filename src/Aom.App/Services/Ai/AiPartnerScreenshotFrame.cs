namespace Aom.App.Services.Ai;

public sealed record AiPartnerScreenshotFrame(
    byte[] ImageBytes,
    string MediaType,
    int Width,
    int Height,
    DateTimeOffset CapturedAtUtc,
    string WindowTitle);