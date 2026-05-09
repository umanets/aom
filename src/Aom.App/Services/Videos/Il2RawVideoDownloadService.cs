using System.IO;
using System.Net.Http;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos;

namespace Aom.App.Services.Videos;

public sealed class Il2RawVideoDownloadService : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly YoutubeClient youtubeClient;
    private readonly FfmpegExecutableResolver ffmpegExecutableResolver;
    private readonly bool disposeHttpClient;
    private readonly bool disposeYoutubeClient;

    public Il2RawVideoDownloadService(
        Il2RawVideoLibraryPaths? paths = null,
        HttpClient? httpClient = null,
        YoutubeClient? youtubeClient = null,
        FfmpegExecutableResolver? ffmpegExecutableResolver = null)
    {
        Paths = paths ?? new Il2RawVideoLibraryPaths();
        this.httpClient = httpClient ?? new HttpClient();
        this.youtubeClient = youtubeClient ?? new YoutubeClient();
        this.ffmpegExecutableResolver = ffmpegExecutableResolver ?? new FfmpegExecutableResolver();
        disposeHttpClient = httpClient is null;
        disposeYoutubeClient = youtubeClient is null;
    }

    public Il2RawVideoLibraryPaths Paths { get; }

    public static string NormalizeVideoId(string videoIdOrUrl) => VideoId.Parse(videoIdOrUrl).Value;

    public async Task<Il2RawVideoRecord> DownloadAsync(
        string videoIdOrUrl,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoIdOrUrl);

        var normalizedVideoId = NormalizeVideoId(videoIdOrUrl.Trim());
        var ffmpegPath = ffmpegExecutableResolver.Resolve();
        Paths.EnsureDirectories();

        progress?.Report(0.02);
        var video = await youtubeClient.Videos.GetAsync(normalizedVideoId, cancellationToken);
        progress?.Report(0.08);

        var thumbnailPath = await TryDownloadThumbnailAsync(video, cancellationToken);
        var finalVideoPath = Paths.GetVideoPath(normalizedVideoId);
        var temporaryVideoPath = Paths.GetTemporaryVideoPath(normalizedVideoId);

        if (File.Exists(temporaryVideoPath))
        {
            File.Delete(temporaryVideoPath);
        }

        try
        {
            var conversionProgress = progress is null
                ? null
                : new Progress<double>(value => progress.Report(0.1 + (Math.Clamp(value, 0.0, 1.0) * 0.9)));

            await youtubeClient.Videos.DownloadAsync(
                video.Id,
                temporaryVideoPath,
                builder => builder.SetFFmpegPath(ffmpegPath),
                conversionProgress,
                cancellationToken);

            if (File.Exists(finalVideoPath))
            {
                File.Delete(finalVideoPath);
            }

            File.Move(temporaryVideoPath, finalVideoPath);
            progress?.Report(1.0);

            return new Il2RawVideoRecord(
                normalizedVideoId,
                video.Url,
                video.Title,
                video.Author.ChannelTitle,
                video.Duration,
                DateTimeOffset.UtcNow,
                finalVideoPath,
                thumbnailPath,
                new FileInfo(finalVideoPath).Length);
        }
        finally
        {
            if (File.Exists(temporaryVideoPath))
            {
                File.Delete(temporaryVideoPath);
            }
        }
    }

    public void Dispose()
    {
        if (disposeHttpClient)
        {
            httpClient.Dispose();
        }

        if (disposeYoutubeClient)
        {
            youtubeClient.Dispose();
        }
    }

    private async Task<string?> TryDownloadThumbnailAsync(Video video, CancellationToken cancellationToken)
    {
        var thumbnailUrl = video.Thumbnails
            .OrderByDescending(thumbnail => thumbnail.Resolution.Area)
            .Select(thumbnail => thumbnail.Url)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return null;
        }

        try
        {
            using var response = await httpClient.GetAsync(thumbnailUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var extension = GetThumbnailExtension(
                response.Content.Headers.ContentType?.MediaType,
                thumbnailUrl);

            if (extension is null)
            {
                return null;
            }

            var thumbnailPath = Paths.GetThumbnailPath(video.Id.Value, extension);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(thumbnailPath, bytes, cancellationToken);
            return thumbnailPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetThumbnailExtension(string? mediaType, string thumbnailUrl)
    {
        if (string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ".jpg";
        }

        if (string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return ".png";
        }

        if (string.Equals(mediaType, "image/bmp", StringComparison.OrdinalIgnoreCase))
        {
            return ".bmp";
        }

        if (string.Equals(mediaType, "image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return ".gif";
        }

        var extension = Path.GetExtension(thumbnailUrl);
        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
        {
            return extension;
        }

        return null;
    }
}