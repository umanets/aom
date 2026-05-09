namespace Aom.Core.Runtime;

public sealed record CheckSixNotifierResult(CheckSixNotifierState NextState, bool ShouldNotify, bool Toggled, CheckSixSpeedCue Cue);