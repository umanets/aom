using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2VideoEditRenderPlanBuilderTests
{
    private readonly Il2VideoEditRenderPlanBuilder builder = new();

    [Fact]
    public void Build_InterleavesFreezeAndSlowSegmentsBySourceTimeline()
    {
        var project = CreateProject(
            freezeAnnotations:
            [
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), null, Array.Empty<string>(), Array.Empty<string>()),
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(3), null, Array.Empty<string>(), Array.Empty<string>()),
            ],
            slowRanges:
            [
                new SlowRangeProjectItem(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), 0.5, "PitchCorrected"),
            ]);

        var plan = builder.Build(project, TimeSpan.FromSeconds(30));

        Assert.Collection(
            plan.Segments,
            first => AssertSource(first, 0, 5, 1.0, "Passthrough"),
            second => AssertFreeze(second, 5, 2),
            third => AssertSource(third, 5, 10, 1.0, "Passthrough"),
            fourth => AssertSource(fourth, 10, 15, 0.5, "PitchCorrected"),
            fifth => AssertFreeze(fifth, 15, 3),
            sixth => AssertSource(sixth, 15, 20, 0.5, "PitchCorrected"),
            seventh => AssertSource(seventh, 20, 30, 1.0, "Passthrough"));

        Assert.Equal(TimeSpan.FromSeconds(45), plan.OutputDuration);
    }

    [Fact]
    public void Build_DoesNotEmitZeroLengthSourceSegmentWhenFreezeStartsAtRangeBoundary()
    {
        var project = CreateProject(
            freezeAnnotations:
            [
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), null, Array.Empty<string>(), Array.Empty<string>()),
            ],
            slowRanges:
            [
                new SlowRangeProjectItem(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(14), 0.5, "PitchCorrected"),
            ]);

        var plan = builder.Build(project, TimeSpan.FromSeconds(20));

        Assert.Collection(
            plan.Segments,
            first => AssertSource(first, 0, 10, 1.0, "Passthrough"),
            second => AssertFreeze(second, 10, 2),
            third => AssertSource(third, 10, 14, 0.5, "PitchCorrected"),
            fourth => AssertSource(fourth, 14, 20, 1.0, "Passthrough"));
    }

    [Fact]
    public void Build_RejectsFreezeOutsideSourceDuration()
    {
        var project = CreateProject(
            freezeAnnotations:
            [
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(2), null, Array.Empty<string>(), Array.Empty<string>()),
            ]);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build(project, TimeSpan.FromSeconds(30)));

        Assert.Contains("source duration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSource(Il2VideoEditRenderSegment segment, double startSeconds, double endSeconds, double speedFactor, string audioPolicy)
    {
        var sourceSegment = Assert.IsType<Il2VideoSourceRenderSegment>(segment);
        Assert.Equal(TimeSpan.FromSeconds(startSeconds), sourceSegment.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(endSeconds), sourceSegment.SourceEnd);
        Assert.Equal(speedFactor, sourceSegment.SpeedFactor);
        Assert.Equal(audioPolicy, sourceSegment.AudioPolicy);
    }

    private static void AssertFreeze(Il2VideoEditRenderSegment segment, double timestampSeconds, double holdSeconds)
    {
        var freezeSegment = Assert.IsType<Il2VideoFreezeRenderSegment>(segment);
        Assert.Equal(TimeSpan.FromSeconds(timestampSeconds), freezeSegment.SourceTimestamp);
        Assert.Equal(TimeSpan.FromSeconds(holdSeconds), freezeSegment.HoldDuration);
    }

    private static Il2VideoEditProject CreateProject(
        FreezeFrameAnnotationProjectItem[]? freezeAnnotations = null,
        SlowRangeProjectItem[]? slowRanges = null)
    {
        var createdAtUtc = new DateTimeOffset(2026, 5, 5, 11, 0, 0, TimeSpan.Zero);
        return new Il2VideoEditProject(
            "FFFFFFFFFFF",
            @"D:\work\aom\video.mp4",
            "Project Test Video",
            createdAtUtc,
            createdAtUtc,
            freezeAnnotations ?? Array.Empty<FreezeFrameAnnotationProjectItem>(),
            slowRanges ?? Array.Empty<SlowRangeProjectItem>());
    }
}