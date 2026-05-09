namespace Aom.App.Services.Videos;

public sealed record Il2VideoAnnotationDescriptor(
    string Tool,
    double StartX,
    double StartY,
    double EndX,
    double EndY,
    string? Text,
    string StrokeHex,
    double StrokeThickness,
    string CoordinateSpace = "Canvas")
{
    public const string CanvasCoordinateSpace = "Canvas";
    public const string VideoViewportCoordinateSpace = "VideoViewport";

    public bool UsesVideoViewportCoordinates => string.Equals(CoordinateSpace, VideoViewportCoordinateSpace, StringComparison.Ordinal);
}