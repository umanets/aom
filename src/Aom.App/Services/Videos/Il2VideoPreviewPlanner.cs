namespace Aom.App.Services.Videos;

public sealed class Il2VideoPreviewPlanner
{
    public int GetNextFreezeIndex(Il2VideoEditProject project, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(project);

        for (var index = 0; index < project.FreezeAnnotations.Length; index++)
        {
            if (project.FreezeAnnotations[index].SourceTimestamp >= position)
            {
                return index;
            }
        }

        return -1;
    }

    public FreezeFrameAnnotationProjectItem? GetFreezeToTrigger(
        Il2VideoEditProject project,
        int nextFreezeIndex,
        TimeSpan position,
        TimeSpan tolerance)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (tolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        if (nextFreezeIndex < 0 || nextFreezeIndex >= project.FreezeAnnotations.Length)
        {
            return null;
        }

        var candidate = project.FreezeAnnotations[nextFreezeIndex];
        return position + tolerance >= candidate.SourceTimestamp
            ? candidate
            : null;
    }

    public SlowRangeProjectItem? GetActiveSlowRange(Il2VideoEditProject project, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.SlowRanges.FirstOrDefault(range => position >= range.Start && position < range.End);
    }

    public double GetEffectiveSpeedRatio(Il2VideoEditProject project, TimeSpan position, double basePreviewSpeedRatio)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (basePreviewSpeedRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePreviewSpeedRatio));
        }

        return GetActiveSlowRange(project, position)?.SpeedFactor ?? basePreviewSpeedRatio;
    }
}