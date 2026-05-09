namespace Aom.App.Services.Input;

public sealed class KeyboardInputSnapshot
{
    public static KeyboardInputSnapshot Empty { get; } = new(Array.Empty<int>());

    private readonly HashSet<int> pressedVirtualKeys;

    public KeyboardInputSnapshot(IEnumerable<int> pressedVirtualKeys)
    {
        this.pressedVirtualKeys = new HashSet<int>(pressedVirtualKeys);
    }

    public IReadOnlyCollection<int> PressedVirtualKeys => pressedVirtualKeys;

    public bool Contains(int virtualKey) => pressedVirtualKeys.Contains(virtualKey);
}