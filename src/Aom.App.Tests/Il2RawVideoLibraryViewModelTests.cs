using Aom.App.Services.Videos;
using Aom.App.ViewModels;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2RawVideoLibraryViewModelTests
{
    [Fact]
    public async Task ChangeLibraryPath_SwitchesToNewLibraryWithoutMovingExistingFiles()
    {
        using var sandbox = new VideoLibrarySandbox();
        var firstRoot = Path.Combine(sandbox.RootPath, "first");
        var secondRoot = Path.Combine(sandbox.RootPath, "second");

        var firstPaths = new Il2RawVideoLibraryPaths(firstRoot);
        firstPaths.EnsureDirectories();
        var firstVideoPath = firstPaths.GetVideoPath("AAAAAAAAAAA");
        File.WriteAllText(firstVideoPath, "first-video");
        new PersistedIl2RawVideoCatalog(firstPaths).Save(new[]
        {
            new Il2RawVideoRecord(
                "AAAAAAAAAAA",
                "https://www.youtube.com/watch?v=AAAAAAAAAAA",
                "First Video",
                "Channel One",
                TimeSpan.FromMinutes(1),
                new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                firstVideoPath,
                null,
                12),
        });

        var secondPaths = new Il2RawVideoLibraryPaths(secondRoot);
        secondPaths.EnsureDirectories();
        var secondVideoPath = secondPaths.GetVideoPath("BBBBBBBBBBB");
        File.WriteAllText(secondVideoPath, "second-video");
        new PersistedIl2RawVideoCatalog(secondPaths).Save(new[]
        {
            new Il2RawVideoRecord(
                "BBBBBBBBBBB",
                "https://www.youtube.com/watch?v=BBBBBBBBBBB",
                "Second Video",
                "Channel Two",
                TimeSpan.FromMinutes(2),
                new DateTimeOffset(2026, 5, 4, 11, 0, 0, TimeSpan.Zero),
                secondVideoPath,
                null,
                24),
        });

        await using var viewModel = new Il2RawVideoLibraryViewModel(firstRoot, chooseLibraryFolder: _ => null);

        var changed = viewModel.ChangeLibraryPath(secondRoot);

        Assert.True(changed);
        Assert.Equal(Path.GetFullPath(secondRoot), viewModel.LibraryPath);
        var video = Assert.Single(viewModel.Videos);
        Assert.Equal("Second Video", video.Title);
        Assert.True(File.Exists(firstVideoPath));
        Assert.Contains("Existing downloads stay in the previous folder", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditCommand_RaisesEditRequestedForExistingVideo()
    {
        using var sandbox = new VideoLibrarySandbox();
        var libraryRoot = Path.Combine(sandbox.RootPath, "library");
        var paths = new Il2RawVideoLibraryPaths(libraryRoot);
        paths.EnsureDirectories();
        var videoPath = paths.GetVideoPath("CCCCCCCCCCC");
        File.WriteAllText(videoPath, "video");

        var record = new Il2RawVideoRecord(
            "CCCCCCCCCCC",
            "https://www.youtube.com/watch?v=CCCCCCCCCCC",
            "Editor Test Video",
            "Channel Three",
            TimeSpan.FromMinutes(3),
            new DateTimeOffset(2026, 5, 5, 8, 0, 0, TimeSpan.Zero),
            videoPath,
            null,
            512);

        new PersistedIl2RawVideoCatalog(paths).Save(new[] { record });

        await using var viewModel = new Il2RawVideoLibraryViewModel(libraryRoot, chooseLibraryFolder: _ => null);
        Il2RawVideoRecord? requestedRecord = null;
        viewModel.EditRequested += entry => requestedRecord = entry;

        var item = Assert.Single(viewModel.Videos);

        item.EditCommand.Execute(null);

        Assert.NotNull(requestedRecord);
        Assert.Equal(record.VideoId, requestedRecord!.VideoId);
        Assert.Contains("Opening editor", viewModel.Status, StringComparison.Ordinal);
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