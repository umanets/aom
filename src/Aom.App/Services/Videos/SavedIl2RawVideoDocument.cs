namespace Aom.App.Services.Videos;

public sealed class SavedIl2RawVideoDocument
{
    public string? VideoId { get; set; }

    public string? SourceUrl { get; set; }

    public string? Title { get; set; }

    public string? ChannelTitle { get; set; }

    public double? DurationSeconds { get; set; }

    public DateTimeOffset DownloadedAtUtc { get; set; }

    public string? LocalVideoPath { get; set; }

    public string? ThumbnailPath { get; set; }

    public long FileSizeBytes { get; set; }

    public Il2RawVideoRecord? ToRecord()
    {
        if (string.IsNullOrWhiteSpace(VideoId)
            || string.IsNullOrWhiteSpace(SourceUrl)
            || string.IsNullOrWhiteSpace(Title)
            || string.IsNullOrWhiteSpace(ChannelTitle)
            || string.IsNullOrWhiteSpace(LocalVideoPath))
        {
            return null;
        }

        TimeSpan? duration = DurationSeconds.HasValue
            ? TimeSpan.FromSeconds(DurationSeconds.Value)
            : null;

        return new Il2RawVideoRecord(
            VideoId.Trim(),
            SourceUrl.Trim(),
            Title.Trim(),
            ChannelTitle.Trim(),
            duration,
            DownloadedAtUtc,
            LocalVideoPath.Trim(),
            string.IsNullOrWhiteSpace(ThumbnailPath) ? null : ThumbnailPath.Trim(),
            FileSizeBytes);
    }

    public static SavedIl2RawVideoDocument FromRecord(Il2RawVideoRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SavedIl2RawVideoDocument
        {
            VideoId = record.VideoId,
            SourceUrl = record.SourceUrl,
            Title = record.Title,
            ChannelTitle = record.ChannelTitle,
            DurationSeconds = record.Duration?.TotalSeconds,
            DownloadedAtUtc = record.DownloadedAtUtc,
            LocalVideoPath = record.LocalVideoPath,
            ThumbnailPath = record.ThumbnailPath,
            FileSizeBytes = record.FileSizeBytes,
        };
    }
}