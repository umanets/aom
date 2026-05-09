using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2RawVideoDownloadServiceTests
{
    [Fact]
    public void NormalizeVideoId_ExtractsValueFromWatchUrl()
    {
        var normalized = Il2RawVideoDownloadService.NormalizeVideoId("https://www.youtube.com/watch?v=FKwNC1OXjXM");

        Assert.Equal("FKwNC1OXjXM", normalized);
    }

    [Fact]
    public void NormalizeVideoId_RejectsInvalidUrl()
    {
        Assert.Throws<ArgumentException>(() => Il2RawVideoDownloadService.NormalizeVideoId("https://example.com/not-youtube"));
    }
}