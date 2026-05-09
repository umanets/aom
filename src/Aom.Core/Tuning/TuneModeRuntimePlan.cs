using Aom.Core.Runtime;

namespace Aom.Core.Tuning;

public sealed record TuneModeRuntimePlan(RuntimeViewState ViewState, HeadPose TrackIrPose, double StickY);