using Aom.App.Services.TrackIr;
using Xunit;

namespace Aom.App.Tests;

public sealed class TrackIrScalingTests
{
    [Theory]
    [InlineData(-1.0, 0.25)]
    [InlineData(0.10, 0.25)]
    [InlineData(1.00, 1.00)]
    [InlineData(2.50, 2.50)]
    [InlineData(5.00, 4.00)]
    public void ClampYawMultiplier_UsesExpectedBounds(double input, double expected)
    {
        var result = TrackIrScaling.ClampYawMultiplier(input);

        Assert.Equal(expected, result, 6);
    }

    [Fact]
    public void ApplyYawMultiplier_ScalesProfileYaw()
    {
        var result = TrackIrScaling.ApplyYawMultiplier(0.12, 2.0);

        Assert.Equal(0.24, result, 6);
    }
}