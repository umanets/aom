namespace Aom.App.Services.Videos;

public sealed record Il2VideoEditProject(
    string VideoId,
    string SourceVideoPath,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    FreezeFrameAnnotationProjectItem[] FreezeAnnotations,
    SlowRangeProjectItem[] SlowRanges)
{
    public static Il2VideoEditProject Create(Il2RawVideoRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var createdAtUtc = DateTimeOffset.UtcNow;
        return new Il2VideoEditProject(
            record.VideoId,
            record.LocalVideoPath,
            record.Title,
            createdAtUtc,
            createdAtUtc,
            Array.Empty<FreezeFrameAnnotationProjectItem>(),
            Array.Empty<SlowRangeProjectItem>());
    }

    public Il2VideoEditProject AddFreezeAnnotation(TimeSpan sourceTimestamp, TimeSpan holdDuration)
    {
        if (sourceTimestamp < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceTimestamp));
        }

        if (holdDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(holdDuration));
        }

        var entry = new FreezeFrameAnnotationProjectItem(
            sourceTimestamp,
            holdDuration,
            null,
            Array.Empty<string>(),
            Array.Empty<string>());

        return this with
        {
            FreezeAnnotations = FreezeAnnotations
                .Append(entry)
                .OrderBy(item => item.SourceTimestamp)
                .ThenBy(item => item.HoldDuration)
                .ToArray(),
        };
    }

    public Il2VideoEditProject AddSlowRange(TimeSpan start, TimeSpan end, double speedFactor, string audioPolicy)
    {
        if (start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        if (speedFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speedFactor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(audioPolicy);

        if (SlowRanges.Any(existing => start < existing.End && end > existing.Start))
        {
            throw new InvalidOperationException("Slow ranges cannot overlap existing ranges.");
        }

        var entry = new SlowRangeProjectItem(start, end, speedFactor, audioPolicy);
        return this with
        {
            SlowRanges = SlowRanges
                .Append(entry)
                .OrderBy(item => item.Start)
                .ThenBy(item => item.End)
                .ToArray(),
        };
    }

    public Il2VideoEditProject AddFreezeShapeDescriptor(int freezeIndex, string descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor);
        var existing = GetFreezeAnnotation(freezeIndex);
        var updated = existing with
        {
            Shapes = existing.Shapes.Append(descriptor).ToArray(),
        };

        return ReplaceFreezeAnnotation(freezeIndex, updated);
    }

    public Il2VideoEditProject AddFreezeTextDescriptor(int freezeIndex, string descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor);
        var existing = GetFreezeAnnotation(freezeIndex);
        var updated = existing with
        {
            TextAnnotations = existing.TextAnnotations.Append(descriptor).ToArray(),
        };

        return ReplaceFreezeAnnotation(freezeIndex, updated);
    }

    public Il2VideoEditProject ReplaceFreezeShapeDescriptor(int freezeIndex, int shapeIndex, string descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor);
        var existing = GetFreezeAnnotation(freezeIndex);
        var updated = existing with
        {
            Shapes = ReplaceDescriptor(existing.Shapes, shapeIndex, descriptor),
        };

        return ReplaceFreezeAnnotation(freezeIndex, updated);
    }

    public Il2VideoEditProject ReplaceFreezeTextDescriptor(int freezeIndex, int textIndex, string descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor);
        var existing = GetFreezeAnnotation(freezeIndex);
        var updated = existing with
        {
            TextAnnotations = ReplaceDescriptor(existing.TextAnnotations, textIndex, descriptor),
        };

        return ReplaceFreezeAnnotation(freezeIndex, updated);
    }

    public Il2VideoEditProject RemoveFreezeShapeDescriptor(int freezeIndex, int shapeIndex)
    {
        var existing = GetFreezeAnnotation(freezeIndex);
        var updated = existing with
        {
            Shapes = RemoveDescriptor(existing.Shapes, shapeIndex),
        };

        return ReplaceFreezeAnnotation(freezeIndex, updated);
    }

    public Il2VideoEditProject RemoveFreezeTextDescriptor(int freezeIndex, int textIndex)
    {
        var existing = GetFreezeAnnotation(freezeIndex);
        var updated = existing with
        {
            TextAnnotations = RemoveDescriptor(existing.TextAnnotations, textIndex),
        };

        return ReplaceFreezeAnnotation(freezeIndex, updated);
    }

    public Il2VideoEditProject RemoveFreezeAnnotation(int freezeIndex)
    {
        GetFreezeAnnotation(freezeIndex);
        return this with
        {
            FreezeAnnotations = FreezeAnnotations
                .Where((_, index) => index != freezeIndex)
                .ToArray(),
        };
    }

    private FreezeFrameAnnotationProjectItem GetFreezeAnnotation(int freezeIndex)
    {
        if (freezeIndex < 0 || freezeIndex >= FreezeAnnotations.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeIndex));
        }

        return FreezeAnnotations[freezeIndex];
    }

    private Il2VideoEditProject ReplaceFreezeAnnotation(int freezeIndex, FreezeFrameAnnotationProjectItem updated)
    {
        var items = FreezeAnnotations.ToArray();
        items[freezeIndex] = updated;
        return this with
        {
            FreezeAnnotations = items,
        };
    }

    private static string[] ReplaceDescriptor(string[] items, int index, string descriptor)
    {
        if (index < 0 || index >= items.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var updated = items.ToArray();
        updated[index] = descriptor;
        return updated;
    }

    private static string[] RemoveDescriptor(string[] items, int index)
    {
        if (index < 0 || index >= items.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return items
            .Where((_, currentIndex) => currentIndex != index)
            .ToArray();
    }
}