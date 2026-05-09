using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class PersistedIl2VideoEditProjectStoreTests
{
    [Fact]
    public void LoadOrCreate_CreatesProjectFileForVideo()
    {
        using var sandbox = new VideoLibrarySandbox();
        var paths = new Il2RawVideoLibraryPaths(sandbox.RootPath);
        var store = new PersistedIl2VideoEditProjectStore(paths);
        var videoPath = paths.GetVideoPath("DDDDDDDDDDD");
        paths.EnsureDirectories();
        File.WriteAllText(videoPath, "video");

        var record = new Il2RawVideoRecord(
            "DDDDDDDDDDD",
            "https://www.youtube.com/watch?v=DDDDDDDDDDD",
            "Persistent Project Video",
            "Channel Four",
            TimeSpan.FromMinutes(4),
            new DateTimeOffset(2026, 5, 5, 9, 0, 0, TimeSpan.Zero),
            videoPath,
            null,
            1024);

        var project = store.LoadOrCreate(record);

        Assert.Equal(record.VideoId, project.VideoId);
        Assert.Equal(record.LocalVideoPath, project.SourceVideoPath);
        Assert.Empty(project.FreezeAnnotations);
        Assert.Empty(project.SlowRanges);
        Assert.True(File.Exists(paths.GetProjectPath(record.VideoId)));
    }

    [Fact]
    public void Save_PersistsFreezeAndSlowRanges()
    {
        using var sandbox = new VideoLibrarySandbox();
        var paths = new Il2RawVideoLibraryPaths(sandbox.RootPath);
        var store = new PersistedIl2VideoEditProjectStore(paths);
        var videoPath = paths.GetVideoPath("EEEEEEEEEEE");
        paths.EnsureDirectories();
        File.WriteAllText(videoPath, "video");

        var record = new Il2RawVideoRecord(
            "EEEEEEEEEEE",
            "https://www.youtube.com/watch?v=EEEEEEEEEEE",
            "Roundtrip Video",
            "Channel Five",
            TimeSpan.FromMinutes(5),
            new DateTimeOffset(2026, 5, 5, 10, 0, 0, TimeSpan.Zero),
            videoPath,
            null,
            2048);

        var project = store.LoadOrCreate(record)
            .AddFreezeAnnotation(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2))
            .AddSlowRange(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30), 0.5, "PitchCorrected");

        store.Save(project);

        var reloaded = store.LoadOrCreate(record);

        var freeze = Assert.Single(reloaded.FreezeAnnotations);
        Assert.Equal(TimeSpan.FromSeconds(10), freeze.SourceTimestamp);
        Assert.Equal(TimeSpan.FromSeconds(2), freeze.HoldDuration);

        var slow = Assert.Single(reloaded.SlowRanges);
        Assert.Equal(TimeSpan.FromSeconds(20), slow.Start);
        Assert.Equal(TimeSpan.FromSeconds(30), slow.End);
        Assert.Equal(0.5, slow.SpeedFactor);
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