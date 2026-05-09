namespace Aom.Core.Runtime;

public sealed class RuntimeActionSnapshot
{
    private readonly HashSet<string> activeActionIds;
    private readonly HashSet<string> pressedActionIds;
    private readonly Dictionary<string, double> axisValues;

    public RuntimeActionSnapshot(IEnumerable<string>? activeActionIds = null, IEnumerable<string>? pressedActionIds = null, IDictionary<string, double>? axisValues = null)
    {
        this.activeActionIds = new HashSet<string>(activeActionIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        this.pressedActionIds = new HashSet<string>(pressedActionIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        this.axisValues = new Dictionary<string, double>(axisValues ?? new Dictionary<string, double>(), StringComparer.Ordinal);
    }

    public bool IsActive(string actionId) => activeActionIds.Contains(actionId);

    public bool WasPressed(string actionId) => pressedActionIds.Contains(actionId);

    public double GetAxisValueOrDefault(string actionId, double defaultValue) =>
        axisValues.TryGetValue(actionId, out var value) ? value : defaultValue;
}