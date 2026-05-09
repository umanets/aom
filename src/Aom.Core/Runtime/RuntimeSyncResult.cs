namespace Aom.Core.Runtime;

public readonly record struct RuntimeSyncResult(bool IsSyncFrame, bool SendGlobalCenterPulse, RuntimeViewState NextState, HeadPose Pose);