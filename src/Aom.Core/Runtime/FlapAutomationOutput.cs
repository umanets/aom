namespace Aom.Core.Runtime;

public sealed record FlapAutomationOutput(bool PressOpenKeys, bool ReleaseOpenKeys, bool PressCloseKeys, bool ReleaseCloseKeys)
{
    public static FlapAutomationOutput None { get; } = new(false, false, false, false);
}