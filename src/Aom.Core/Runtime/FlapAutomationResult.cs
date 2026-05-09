namespace Aom.Core.Runtime;

public sealed record FlapAutomationResult(FlapAutomationState NextState, FlapAutomationOutput Output, bool ShouldSpeakReminder);