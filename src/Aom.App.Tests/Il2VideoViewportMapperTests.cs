using System.Windows;
using Aom.App.Services.Videos;
using Xunit;

namespace Aom.App.Tests;

public sealed class Il2VideoViewportMapperTests
{
    [Fact]
    public void GetViewportRect_CentersVideoInsideTallerContainer()
    {
        var viewport = Il2VideoViewportMapper.GetViewportRect(1000, 800, 1920, 1080);

        Assert.Equal(0, viewport.Left, 3);
        Assert.Equal(118.75, viewport.Top, 3);
        Assert.Equal(1000, viewport.Width, 3);
        Assert.Equal(562.5, viewport.Height, 3);
    }

    [Fact]
    public void NormalizePoint_ClampsLetterboxPaddingToVideoEdge()
    {
        var viewport = Il2VideoViewportMapper.GetViewportRect(1000, 800, 1920, 1080);

        var normalized = Il2VideoViewportMapper.NormalizePoint(new Point(250, 40), viewport);

        Assert.Equal(0.25, normalized.X, 3);
        Assert.Equal(0, normalized.Y, 3);
    }

    [Fact]
    public void ToCanvasPoint_PreservesVideoRelativePositionAcrossResize()
    {
        var authoringViewport = Il2VideoViewportMapper.GetViewportRect(1000, 800, 1920, 1080);
        var resizedViewport = Il2VideoViewportMapper.GetViewportRect(1400, 800, 1920, 1080);
        var authoredPoint = Il2VideoViewportMapper.ToCanvasPoint(new Point(0.25, 0.75), authoringViewport);

        var stored = Il2VideoViewportMapper.NormalizePoint(authoredPoint, authoringViewport);
        var resizedPoint = Il2VideoViewportMapper.ToCanvasPoint(stored, resizedViewport);
        var expectedPoint = Il2VideoViewportMapper.ToCanvasPoint(new Point(0.25, 0.75), resizedViewport);

        Assert.Equal(expectedPoint.X, resizedPoint.X, 3);
        Assert.Equal(expectedPoint.Y, resizedPoint.Y, 3);
    }
}