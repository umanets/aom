namespace Aom.App.Services.Input;

public sealed record JoystickDeviceInfo(int DeviceId, string Name, int ButtonCount, int AxisCount, int PovCount, int PeriodMin, int PeriodMax);