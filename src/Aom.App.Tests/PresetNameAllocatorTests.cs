using Aom.App.Services.Settings;
using Xunit;

namespace Aom.App.Tests;

public sealed class PresetNameAllocatorTests
{
    [Fact]
    public void AllocateUnknownDisplayName_UsesUnknownWhenItIsAvailable()
    {
        var name = PresetNameAllocator.AllocateUnknownDisplayName(new[] { "Lagg", "Yakodin" });

        Assert.Equal("Unknown", name);
    }

    [Fact]
    public void AllocateUnknownDisplayName_AppendsNextAvailableIndex()
    {
        var name = PresetNameAllocator.AllocateUnknownDisplayName(new[] { "Unknown", "Unknown (2)", "Unknown (3)" });

        Assert.Equal("Unknown (4)", name);
    }
}