using System.Collections.ObjectModel;

namespace Aom.Core.Presets;

public static class PresetCatalog
{
    public static IReadOnlyList<CameraPreset> All { get; } = new ReadOnlyCollection<CameraPreset>(
        new[]
        {
            Create(
                "reset",
                "Reset",
                ("deltaX0", 0),
                ("deltaX1", 0),
                ("deltaX2_1", 0),
                ("deltaX2_4", 0),
                ("deltaY0", 0),
                ("deltaY1", 0),
                ("deltaY2_1", 0),
                ("deltaY2_2", 0),
                ("deltaY2_4", 0),
                ("deltaYHigh", 0),
                ("deltaYLow", 0),
                ("deltaZ1", 0),
                ("deltaZ2_1", 0),
                ("deltaZ2_2", 0),
                ("deltaZ2_4", 0),
                ("spitch", 0),
                ("syaw", 0)),
            Create(
                "lagg",
                "Lagg",
                ("deltaX0", 0.2500),
                ("deltaX1", 1.2804),
                ("deltaX2_1", 1.6000),
                ("deltaX2_4", 0.0000),
                ("deltaY0", 0.1000),
                ("deltaY1", -0.1304),
                ("deltaY2_1", 1.1000),
                ("deltaY2_2", 1.6000),
                ("deltaY2_4", -4.4500),
                ("deltaYHigh", 1.4000),
                ("deltaYLow", -0.6500),
                ("deltaZ1", 6.5200),
                ("deltaZ2_1", -4.0000),
                ("deltaZ2_2", 7.4900),
                ("deltaZ2_4", 8.4500),
                ("manual_yaw", -140.0000),
                ("spitch", -1.0000),
                ("syaw", -2.5000)),
            Create(
                "yakodin",
                "Yakodin",
                ("deltaX0", 0.0400),
                ("deltaX1", 1.4504),
                ("deltaX2_1", 2.5500),
                ("deltaX2_4", -0.4500),
                ("deltaY0", -0.0100),
                ("deltaY1", 2.8100),
                ("deltaY2_1", 1.1000),
                ("deltaY2_2", 3.4500),
                ("deltaY2_4", -5.0400),
                ("deltaYHigh", 3.7000),
                ("deltaYLow", -1.0500),
                ("deltaZ1", 6.6000),
                ("deltaZ2_1", -4.2000),
                ("deltaZ2_2", 8.6900),
                ("deltaZ2_4", 6.4400),
                ("manual_yaw", -140.0000),
                ("spitch", -1.5000),
                ("syaw", -1.0000)),
            Create(
                "f4",
                "F4",
                ("deltaX0", 0.0000),
                ("deltaX1", 1.5800),
                ("deltaX2_1", 3.5000),
                ("deltaX2_4", 0.0000),
                ("deltaY0", 0.2200),
                ("deltaY1", 0.9400),
                ("deltaY2_1", 0.7500),
                ("deltaY2_2", 3.0500),
                ("deltaY2_4", -5.3500),
                ("deltaYHigh", 2.5500),
                ("deltaYLow", -0.5500),
                ("deltaZ1", 5.0200),
                ("deltaZ2_1", -5.8500),
                ("deltaZ2_2", 8.7500),
                ("deltaZ2_4", 3.9900),
                ("spitch", -1.0000),
                ("syaw", -1.5000)),
            Create(
                "yakodin-and-b",
                "Yakodin and B",
                ("deltaX0", 0.0000),
                ("deltaX1", 1.1800),
                ("deltaX2_1", 3.5500),
                ("deltaX2_4", -0.3000),
                ("deltaY0", 0.1000),
                ("deltaY1", 2.1900),
                ("deltaY2_1", 0.5500),
                ("deltaY2_2", 3.6000),
                ("deltaY2_4", -5.9500),
                ("deltaYHigh", 2.3000),
                ("deltaYLow", -0.9000),
                ("deltaZ1", 1.0400),
                ("deltaZ2_1", -4.9900),
                ("deltaZ2_2", 5.7400),
                ("deltaZ2_4", 5.2900),
                ("spitch", -2.0000),
                ("syaw", -2.5000)),
            Create(
                "la-five",
                "LA Five",
                ("deltaX0", 0.0000),
                ("deltaX1", 1.1904),
                ("deltaX2_1", 3.2500),
                ("deltaX2_4", -0.2500),
                ("deltaY0", 0.0000),
                ("deltaY1", -2.7200),
                ("deltaY2_1", 1.2500),
                ("deltaY2_2", 1.4500),
                ("deltaY2_4", -3.9500),
                ("deltaYHigh", 1.2500),
                ("deltaYLow", -0.7500),
                ("deltaZ1", 6.7804),
                ("deltaZ2_1", -4.3000),
                ("deltaZ2_2", 6.3000),
                ("deltaZ2_4", 0.3500),
                ("manual_yaw", -134.5000),
                ("spitch", -1.0000),
                ("syaw", -2.0000)),
        });

    public static CameraPreset Default => All[0];

    public static CameraPreset? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return All.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static CameraPreset Create(string id, string displayName, params (string Name, double Value)[] parameters)
    {
        var orderedParameters = parameters
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(parameter => new PresetParameter(parameter.Name, parameter.Value))
            .ToArray();

        return new CameraPreset(id, displayName, orderedParameters);
    }
}