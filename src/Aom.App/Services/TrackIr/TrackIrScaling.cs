namespace Aom.App.Services.TrackIr;

public static class TrackIrScaling
{
    public const double DefaultYawMultiplier = 1.0;
    public const double MinimumYawMultiplier = 0.25;
    public const double MaximumYawMultiplier = 4.0;

    public static double ClampYawMultiplier(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultYawMultiplier;
        }

        return Math.Clamp(value, MinimumYawMultiplier, MaximumYawMultiplier);
    }

    public static double ApplyYawMultiplier(double baseYawScale, double yawMultiplier)
    {
        return baseYawScale * ClampYawMultiplier(yawMultiplier);
    }
}