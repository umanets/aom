namespace Aom.App.Services.Videos;

public sealed record Il2VideoEditRenderPlan(
    TimeSpan SourceDuration,
    Il2VideoEditRenderSegment[] Segments)
{
    public TimeSpan OutputDuration => TimeSpan.FromTicks(Segments.Sum(segment => segment.OutputDuration.Ticks));
}

public abstract record Il2VideoEditRenderSegment
{
    public abstract TimeSpan OutputDuration { get; }
}

public sealed record Il2VideoSourceRenderSegment(
    TimeSpan SourceStart,
    TimeSpan SourceEnd,
    double SpeedFactor,
    string AudioPolicy) : Il2VideoEditRenderSegment
{
    public TimeSpan SourceDuration => SourceEnd - SourceStart;

    public override TimeSpan OutputDuration => TimeSpan.FromTicks((long)Math.Round(SourceDuration.Ticks / SpeedFactor));
}

public sealed record Il2VideoFreezeRenderSegment(
    TimeSpan SourceTimestamp,
    TimeSpan HoldDuration,
    FreezeFrameAnnotationProjectItem FreezeAnnotation) : Il2VideoEditRenderSegment
{
    public override TimeSpan OutputDuration => HoldDuration;
}

public sealed class Il2VideoEditRenderPlanBuilder
{
    private const string DefaultAudioPolicy = "Passthrough";

    public Il2VideoEditRenderPlan Build(Il2VideoEditProject project, TimeSpan sourceDuration)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (sourceDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceDuration));
        }

        ValidateProject(project, sourceDuration);

        var orderedSlowRanges = project.SlowRanges
            .OrderBy(item => item.Start)
            .ThenBy(item => item.End)
            .ToArray();
        var orderedFreezeAnnotations = project.FreezeAnnotations
            .OrderBy(item => item.SourceTimestamp)
            .ThenBy(item => item.HoldDuration)
            .ToArray();

        var segments = new List<Il2VideoEditRenderSegment>();
        var cursor = TimeSpan.Zero;
        var nextFreezeIndex = 0;

        foreach (var slowRange in orderedSlowRanges)
        {
            EmitSourceTimelineSegments(
                segments,
                orderedFreezeAnnotations,
                ref nextFreezeIndex,
                ref cursor,
                slowRange.Start,
                1.0,
                DefaultAudioPolicy);

            EmitSourceTimelineSegments(
                segments,
                orderedFreezeAnnotations,
                ref nextFreezeIndex,
                ref cursor,
                slowRange.End,
                slowRange.SpeedFactor,
                slowRange.AudioPolicy);
        }

        EmitSourceTimelineSegments(
            segments,
            orderedFreezeAnnotations,
            ref nextFreezeIndex,
            ref cursor,
            sourceDuration,
            1.0,
            DefaultAudioPolicy);

        return new Il2VideoEditRenderPlan(sourceDuration, segments.ToArray());
    }

    private static void EmitSourceTimelineSegments(
        List<Il2VideoEditRenderSegment> segments,
        FreezeFrameAnnotationProjectItem[] freezeAnnotations,
        ref int nextFreezeIndex,
        ref TimeSpan cursor,
        TimeSpan segmentEnd,
        double speedFactor,
        string audioPolicy)
    {
        while (nextFreezeIndex < freezeAnnotations.Length && freezeAnnotations[nextFreezeIndex].SourceTimestamp <= segmentEnd)
        {
            var freezeAnnotation = freezeAnnotations[nextFreezeIndex];
            if (freezeAnnotation.SourceTimestamp < cursor)
            {
                nextFreezeIndex++;
                continue;
            }

            if (cursor < freezeAnnotation.SourceTimestamp)
            {
                segments.Add(new Il2VideoSourceRenderSegment(cursor, freezeAnnotation.SourceTimestamp, speedFactor, audioPolicy));
            }

            segments.Add(new Il2VideoFreezeRenderSegment(freezeAnnotation.SourceTimestamp, freezeAnnotation.HoldDuration, freezeAnnotation));
            cursor = freezeAnnotation.SourceTimestamp;
            nextFreezeIndex++;
        }

        if (cursor < segmentEnd)
        {
            segments.Add(new Il2VideoSourceRenderSegment(cursor, segmentEnd, speedFactor, audioPolicy));
        }

        cursor = segmentEnd;
    }

    private static void ValidateProject(Il2VideoEditProject project, TimeSpan sourceDuration)
    {
        foreach (var freeze in project.FreezeAnnotations)
        {
            if (freeze.SourceTimestamp < TimeSpan.Zero || freeze.SourceTimestamp > sourceDuration)
            {
                throw new InvalidOperationException("Freeze annotations must stay within the source duration.");
            }
        }

        var orderedSlowRanges = project.SlowRanges
            .OrderBy(item => item.Start)
            .ThenBy(item => item.End)
            .ToArray();

        TimeSpan? previousEnd = null;
        foreach (var slowRange in orderedSlowRanges)
        {
            if (slowRange.Start < TimeSpan.Zero || slowRange.End > sourceDuration)
            {
                throw new InvalidOperationException("Slow ranges must stay within the source duration.");
            }

            if (previousEnd is not null && slowRange.Start < previousEnd.Value)
            {
                throw new InvalidOperationException("Slow ranges cannot overlap in the export render plan.");
            }

            previousEnd = slowRange.End;
        }
    }
}