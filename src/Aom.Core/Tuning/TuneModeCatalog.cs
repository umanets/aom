using System.Collections.ObjectModel;

namespace Aom.Core.Tuning;

public static class TuneModeCatalog
{
    public static IReadOnlyList<TuneModeDefinition> All { get; } = new ReadOnlyCollection<TuneModeDefinition>(
        new[]
        {
            new TuneModeDefinition("isAuto", "Auto", "yaw -> manual_yaw, x -> deltaX1, y -> deltaY1, z -> deltaZ1"),
            new TuneModeDefinition("isZoomIn", "Zoom In", "z -> deltaZ2_1"),
            new TuneModeDefinition("isZoomOut", "Zoom Out", "z -> deltaZ2_2"),
            new TuneModeDefinition("isCenter", "Center", "x -> deltaX0, y -> deltaY0"),
            new TuneModeDefinition("isManualShiftX", "Manual Shift X", "x -> deltaX2_1"),
            new TuneModeDefinition("isManualShiftY1", "Manual Shift Y 1", "y -> deltaY2_1"),
            new TuneModeDefinition("isManualShiftY2", "Manual Shift Y 2", "y -> deltaY2_2"),
            new TuneModeDefinition("isDynamicYLow", "Dynamic Y Low", "y -> deltaYLow"),
            new TuneModeDefinition("isDynamicYHigh", "Dynamic Y High", "y -> deltaYHigh"),
            new TuneModeDefinition("isCustomView", "Custom View", "yaw -> syaw, pitch -> spitch, x -> deltaX2_4, y -> deltaY2_4, z -> deltaZ2_4"),
        });

    public static TuneModeDefinition? FindByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return All.FirstOrDefault(mode => string.Equals(mode.Name, name, StringComparison.Ordinal));
    }

    public static string? GetDisplayName(string? name)
    {
        return FindByName(name)?.DisplayName;
    }

    public static string? GetNextName(string? currentName)
    {
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return All.Count == 0 ? null : All[0].Name;
        }

        for (var index = 0; index < All.Count; index++)
        {
            if (!string.Equals(All[index].Name, currentName, StringComparison.Ordinal))
            {
                continue;
            }

            return All[(index + 1) % All.Count].Name;
        }

        return All.Count == 0 ? null : All[0].Name;
    }
}