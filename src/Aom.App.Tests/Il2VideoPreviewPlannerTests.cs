using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2VideoPreviewPlannerTests
{
    private readonly Il2VideoPreviewPlanner planner = new();

    [Fact]
    public void GetNextFreezeIndex_ReturnsFirstMarkerAtOrAfterPosition()
    {
        var project = CreateProject(
            freezeAnnotations:
            [
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), null, Array.Empty<string>(), Array.Empty<string>()),
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(3), null, Array.Empty<string>(), Array.Empty<string>()),
            ]);

        var nextIndex = planner.GetNextFreezeIndex(project, TimeSpan.FromSeconds(6));

        Assert.Equal(1, nextIndex);
    }

    [Fact]
    public void GetFreezeToTrigger_RequiresPositionWithinTolerance()
    {
        var project = CreateProject(
            freezeAnnotations:
            [
                new FreezeFrameAnnotationProjectItem(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(2), null, Array.Empty<string>(), Array.Empty<string>()),
            ]);

        var beforeTolerance = planner.GetFreezeToTrigger(project, 0, TimeSpan.FromSeconds(7.6), TimeSpan.FromMilliseconds(200));
        var insideTolerance = planner.GetFreezeToTrigger(project, 0, TimeSpan.FromSeconds(7.85), TimeSpan.FromMilliseconds(200));

        Assert.Null(beforeTolerance);
        Assert.NotNull(insideTolerance);
    }

    [Fact]
    public void GetEffectiveSpeedRatio_UsesSlowRangeWhenPositionIsInsideRange()
    {
        var project = CreateProject(
            slowRanges:
            [
                new SlowRangeProjectItem(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), 0.5, "PitchCorrected"),
            ]);

        var insideRatio = planner.GetEffectiveSpeedRatio(project, TimeSpan.FromSeconds(12), 1.0);
        var outsideRatio = planner.GetEffectiveSpeedRatio(project, TimeSpan.FromSeconds(22), 1.0);

        Assert.Equal(0.5, insideRatio);
        Assert.Equal(1.0, outsideRatio);
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