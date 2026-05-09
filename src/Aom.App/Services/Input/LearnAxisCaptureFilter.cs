namespace Aom.App.Services.Input;

public sealed class LearnAxisCaptureFilter
{
    private const double RangeComparisonEpsilon = 0.0001;
    private readonly double minimumRangeNormalized;
    private readonly Dictionary<LearnAxisKey, LearnAxisWindow> windows = new();

    public LearnAxisCaptureFilter(double minimumRangeNormalized = 0.18)
    {
        if (minimumRangeNormalized <= 0 || minimumRangeNormalized > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRangeNormalized));
        }

        this.minimumRangeNormalized = minimumRangeNormalized;
    }

    public void Reset(IReadOnlyList<JoystickLiveState> joystickStates)
    {
        windows.Clear();
        Seed(joystickStates);
    }

    public void Clear() => windows.Clear();

    public string? TryCapture(IReadOnlyList<JoystickLiveState> joystickStates)
    {
        Seed(joystickStates);

        LearnAxisCandidate? bestCandidate = null;

        foreach (var state in joystickStates)
        {
            EvaluateCandidate(state.DeviceId, "Y", state.Y, 0, ref bestCandidate);
            EvaluateCandidate(state.DeviceId, "X", state.X, 1, ref bestCandidate);
            EvaluateCandidate(state.DeviceId, "Z", state.Z, 2, ref bestCandidate);
        }

        return bestCandidate is null
            ? null
            : $"Joystick {bestCandidate.Value.DeviceId} Axis {bestCandidate.Value.AxisName}";
    }

    private void Seed(IReadOnlyList<JoystickLiveState> joystickStates)
    {
        foreach (var state in joystickStates)
        {
            SeedAxis(state.DeviceId, "Y", state.Y);
            SeedAxis(state.DeviceId, "X", state.X);
            SeedAxis(state.DeviceId, "Z", state.Z);
        }
    }

    private void SeedAxis(int deviceId, string axisName, uint rawValue)
    {
        var key = new LearnAxisKey(deviceId, axisName);
        windows.TryAdd(key, LearnAxisWindow.Start(NormalizeAxis(rawValue)));
    }

    private void EvaluateCandidate(int deviceId, string axisName, uint rawValue, int priority, ref LearnAxisCandidate? bestCandidate)
    {
        var key = new LearnAxisKey(deviceId, axisName);
        var currentValue = NormalizeAxis(rawValue);
        var nextWindow = windows.TryGetValue(key, out var existingWindow)
            ? existingWindow.Include(currentValue)
            : LearnAxisWindow.Start(currentValue);

        windows[key] = nextWindow;

        if (nextWindow.Range < minimumRangeNormalized)
        {
            return;
        }

        var candidate = new LearnAxisCandidate(deviceId, axisName, nextWindow.Range, priority);
        if (bestCandidate is null || IsBetterCandidate(candidate, bestCandidate.Value))
        {
            bestCandidate = candidate;
        }
    }

    private static bool IsBetterCandidate(LearnAxisCandidate candidate, LearnAxisCandidate currentBest)
    {
        if (candidate.Range > currentBest.Range + RangeComparisonEpsilon)
        {
            return true;
        }

        return Math.Abs(candidate.Range - currentBest.Range) <= RangeComparisonEpsilon && candidate.Priority < currentBest.Priority;
    }

    private static double NormalizeAxis(uint value) => Math.Clamp(value / 65535.0, 0, 1);

    private readonly record struct LearnAxisKey(int DeviceId, string AxisName);

    private readonly record struct LearnAxisCandidate(int DeviceId, string AxisName, double Range, int Priority);

    private readonly record struct LearnAxisWindow(double Minimum, double Maximum)
    {
        public double Range => Maximum - Minimum;

        public static LearnAxisWindow Start(double value) => new(value, value);

        public LearnAxisWindow Include(double value) => new(Math.Min(Minimum, value), Math.Max(Maximum, value));
    }
}