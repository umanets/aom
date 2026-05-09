namespace Aom.App.Services.Videos;

public sealed record SlowRangeProjectItem(
    TimeSpan Start,
    TimeSpan End,
    double SpeedFactor,
    string AudioPolicy);