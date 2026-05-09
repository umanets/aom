using Aom.App.Services.Input;
using Xunit;

namespace Aom.App.Tests;

public sealed class KeyboardVirtualKeyNormalizerTests
{
    [Fact]
    public void Normalize_MapsNonExtendedInsertScanCodeToNumPad0()
    {
        var normalized = KeyboardVirtualKeyNormalizer.Normalize(0x2D, 0x52, 0);

        Assert.Equal(0x60, normalized);
    }

    [Fact]
    public void Normalize_KeepsExtendedInsertAsInsert()
    {
        var normalized = KeyboardVirtualKeyNormalizer.Normalize(0x2D, 0x52, 0x01);

        Assert.Equal(0x2D, normalized);
    }

    [Fact]
    public void Normalize_MapsNonExtendedUpArrowScanCodeToNumPad8()
    {
        var normalized = KeyboardVirtualKeyNormalizer.Normalize(0x26, 0x48, 0);

        Assert.Equal(0x68, normalized);
    }

    [Fact]
    public void Normalize_KeepsExtendedUpArrowAsArrow()
    {
        var normalized = KeyboardVirtualKeyNormalizer.Normalize(0x26, 0x48, 0x01);

        Assert.Equal(0x26, normalized);
    }
}