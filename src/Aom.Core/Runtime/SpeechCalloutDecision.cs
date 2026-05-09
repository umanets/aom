namespace Aom.Core.Runtime;

public sealed record SpeechCalloutDecision(SpeechCalloutState NextState, SpeechCallout Callout);