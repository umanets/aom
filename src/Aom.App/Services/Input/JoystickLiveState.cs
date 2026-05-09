namespace Aom.App.Services.Input;

public sealed record JoystickLiveState(
    int DeviceId,
    string Name,
    uint X,
    uint Y,
    uint Z,
    uint R,
    uint U,
    uint V,
    uint Pov,
    uint Buttons,
    IReadOnlyList<int> PressedButtons,
    string Summary);