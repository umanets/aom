namespace Aom.App.Services.Ai;

public sealed record AiPartnerScreenshotCaptureResult(AiPartnerScreenshotFrame? Frame, string StatusMessage)
{
    public bool Succeeded => Frame is not null;
}