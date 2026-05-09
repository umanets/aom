using Aom.App.Services.TrackIr;
using Xunit;

namespace Aom.App.Tests;

public sealed class TrackIrInputProfileCatalogTests
{
    [Fact]
    public void Resolve_DefaultsToDefaultTrackIrProfile()
    {
        var profile = TrackIrInputProfileCatalog.Resolve(null);

        Assert.Equal(TrackIrInputProfileCatalog.DefaultTrackIrId, profile.Id);
        Assert.Equal("Default TrackIR", profile.DisplayName);
    }

    [Fact]
    public void Resolve_ReturnsLegacyProfileWhenRequested()
    {
        var profile = TrackIrInputProfileCatalog.Resolve(TrackIrInputProfileCatalog.LegacyAomId);

        Assert.Equal("Legacy AOM TrackIR", profile.DisplayName);
        Assert.Equal(45, profile.YEngageYawThreshold);
    }
}