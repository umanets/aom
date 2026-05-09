namespace Aom.App.Services.Videos;

public sealed record FreezeFrameAnnotationProjectItem(
    TimeSpan SourceTimestamp,
    TimeSpan HoldDuration,
    string? RenderedStillPath,
    string[] Shapes,
    string[] TextAnnotations);