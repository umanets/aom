namespace Aom.App.Services.Videos;

public sealed record Il2RawVideoRecord(
    string VideoId,
    string SourceUrl,
    string Title,
    string ChannelTitle,
    TimeSpan? Duration,
    DateTimeOffset DownloadedAtUtc,
    string LocalVideoPath,
    string? ThumbnailPath,
    long FileSizeBytes);