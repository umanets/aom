using System.Text.Json;
using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class PersistedIl2RawVideoCatalogTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsExistingLibraryEntry()
    {
        using var sandbox = new VideoLibrarySandbox();
        var paths = new Il2RawVideoLibraryPaths(sandbox.RootPath);
        paths.EnsureDirectories();

        var videoPath = paths.GetVideoPath("FKwNC1OXjXM");
        File.WriteAllText(videoPath, "video");
        var thumbnailPath = paths.GetThumbnailPath("FKwNC1OXjXM", ".jpg");
        File.WriteAllText(thumbnailPath, "thumb");

        var record = new Il2RawVideoRecord(
            "FKwNC1OXjXM",
            "https://www.youtube.com/watch?v=FKwNC1OXjXM",
            "Test Video",
            "Test Channel",
            TimeSpan.FromMinutes(3),
            new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero),
            videoPath,
            thumbnailPath,
            1234);

        var catalog = new PersistedIl2RawVideoCatalog(paths);
        catalog.Save(new[] { record });

        var loaded = catalog.Load();

        var actual = Assert.Single(loaded);
        Assert.Equal(record.VideoId, actual.VideoId);
        Assert.Equal(record.Title, actual.Title);
        Assert.Equal(record.LocalVideoPath, actual.LocalVideoPath);
        Assert.Equal(record.ThumbnailPath, actual.ThumbnailPath);
    }

    [Fact]
    public void Load_RemovesEntryWhenVideoFileIsMissing()
    {
        using var sandbox = new VideoLibrarySandbox();
        var paths = new Il2RawVideoLibraryPaths(sandbox.RootPath);
        paths.EnsureDirectories();

        var videoPath = paths.GetVideoPath("FKwNC1OXjXM");
        File.WriteAllText(videoPath, "video");

        var record = new Il2RawVideoRecord(
            "FKwNC1OXjXM",
            "https://www.youtube.com/watch?v=FKwNC1OXjXM",
            "Test Video",
            "Test Channel",
            TimeSpan.FromMinutes(3),
            new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero),
            videoPath,
            null,
            1234);

        var catalog = new PersistedIl2RawVideoCatalog(paths);
        catalog.Save(new[] { record });
        File.Delete(videoPath);

        var loaded = catalog.Load();
        var persistedDocuments = JsonSerializer.Deserialize<List<SavedIl2RawVideoDocument>>(File.ReadAllText(paths.CatalogPath));

        Assert.Empty(loaded);
        Assert.NotNull(persistedDocuments);
        Assert.Empty(persistedDocuments!);
    }

    [Fact]
    public void Load_ClearsMissingThumbnailPathButKeepsVideoEntry()
    {
        using var sandbox = new VideoLibrarySandbox();
        var paths = new Il2RawVideoLibraryPaths(sandbox.RootPath);
        paths.EnsureDirectories();

        var videoPath = paths.GetVideoPath("FKwNC1OXjXM");
        File.WriteAllText(videoPath, "video");
        var thumbnailPath = paths.GetThumbnailPath("FKwNC1OXjXM", ".jpg");
        File.WriteAllText(thumbnailPath, "thumb");

        var record = new Il2RawVideoRecord(
            "FKwNC1OXjXM",
            "https://www.youtube.com/watch?v=FKwNC1OXjXM",
            "Test Video",
            "Test Channel",
            TimeSpan.FromMinutes(3),
            new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero),
            videoPath,
            thumbnailPath,
            1234);

        var catalog = new PersistedIl2RawVideoCatalog(paths);
        catalog.Save(new[] { record });
        File.Delete(thumbnailPath);

        var loaded = catalog.Load();

        var actual = Assert.Single(loaded);
        Assert.Null(actual.ThumbnailPath);
        Assert.Equal(videoPath, actual.LocalVideoPath);
    }

    private sealed class VideoLibrarySandbox : IDisposable
    {
        public VideoLibrarySandbox()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "aom-tests", Guid.NewGuid().ToString("N"));
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}