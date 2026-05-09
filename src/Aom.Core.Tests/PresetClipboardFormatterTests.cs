using Aom.Core.Presets;
using Xunit;

namespace Aom.Core.Tests;

public sealed class PresetClipboardFormatterTests
{
    [Fact]
    public void Format_SortsParametersAndUsesFourDecimals()
    {
        var preset = new CameraPreset(
            "demo",
            "Demo",
            new[]
            {
                new PresetParameter("deltaZ1", 2),
                new PresetParameter("deltaX0", 0.25),
                new PresetParameter("deltaY0", -1.5),
            });

        var formatted = PresetClipboardFormatter.Format(preset);

        Assert.Equal(
            "{\n    \"deltaX0\": 0.2500,\n    \"deltaY0\": -1.5000,\n    \"deltaZ1\": 2.0000\n}",
            formatted);
    }
}