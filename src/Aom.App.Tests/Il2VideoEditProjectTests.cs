using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2VideoEditProjectTests
{
    [Fact]
    public void AddFreezeAnnotation_KeepsMarkersOrdered()
    {
        var project = CreateProject()
            .AddFreezeAnnotation(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2))
            .AddFreezeAnnotation(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3));

        Assert.Collection(
            project.FreezeAnnotations,
            first => Assert.Equal(TimeSpan.FromSeconds(10), first.SourceTimestamp),
            second => Assert.Equal(TimeSpan.FromSeconds(30), second.SourceTimestamp));
    }

    [Fact]
    public void AddSlowRange_RejectsOverlappingRanges()
    {
        var project = CreateProject()
            .AddSlowRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), 0.5, "PitchCorrected");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            project.AddSlowRange(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(25), 0.75, "PitchCorrected"));

        Assert.Contains("cannot overlap", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddFreezeShapeDescriptor_AppendsToSelectedFreeze()
    {
        var project = CreateProject()
            .AddFreezeAnnotation(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(2))
            .AddFreezeShapeDescriptor(0, "shape-json")
            .AddFreezeTextDescriptor(0, "text-json");

        var freeze = Assert.Single(project.FreezeAnnotations);
        Assert.Equal(new[] { "shape-json" }, freeze.Shapes);
        Assert.Equal(new[] { "text-json" }, freeze.TextAnnotations);
    }

    [Fact]
    public void RemoveFreezeAnnotation_RemovesSelectedMarker()
    {
        var project = CreateProject()
            .AddFreezeAnnotation(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(2))
            .AddFreezeAnnotation(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(3));

        var updated = project.RemoveFreezeAnnotation(0);

        var remaining = Assert.Single(updated.FreezeAnnotations);
        Assert.Equal(TimeSpan.FromSeconds(20), remaining.SourceTimestamp);
    }

    [Fact]
    public void ReplaceFreezeShapeDescriptor_ReplacesExistingShape()
    {
        var project = CreateProject()
            .AddFreezeAnnotation(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(2))
            .AddFreezeShapeDescriptor(0, "shape-json");

        var updated = project.ReplaceFreezeShapeDescriptor(0, 0, "shape-json-updated");

        var freeze = Assert.Single(updated.FreezeAnnotations);
        Assert.Equal(new[] { "shape-json-updated" }, freeze.Shapes);
    }

    [Fact]
    public void RemoveFreezeTextDescriptor_RemovesSelectedTextPrimitive()
    {
        var project = CreateProject()
            .AddFreezeAnnotation(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(2))
            .AddFreezeTextDescriptor(0, "text-json")
            .AddFreezeTextDescriptor(0, "text-json-2");

        var updated = project.RemoveFreezeTextDescriptor(0, 0);

        var freeze = Assert.Single(updated.FreezeAnnotations);
        Assert.Equal(new[] { "text-json-2" }, freeze.TextAnnotations);
    }

    private static Il2VideoEditProject CreateProject()
    {
        var createdAtUtc = new DateTimeOffset(2026, 5, 5, 11, 0, 0, TimeSpan.Zero);
        return new Il2VideoEditProject(
            "FFFFFFFFFFF",
            @"D:\work\aom\video.mp4",
            "Project Test Video",
            createdAtUtc,
            createdAtUtc,
            Array.Empty<FreezeFrameAnnotationProjectItem>(),
            Array.Empty<SlowRangeProjectItem>());
    }
}