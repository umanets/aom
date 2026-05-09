namespace Aom.Core.Runtime;

public sealed record SpeechCalloutState
{
    public SpeechCallout LastIssuedCallout { get; init; }
}