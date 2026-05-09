using SharpDX;
using SharpDX.DirectInput;

namespace Aom.App.Services.Input;

public sealed class DirectInputJoystickDiscoveryService : IDisposable
{
    private static readonly InputRange StandardAxisRange = new(0, 65535);
    private readonly DirectInput directInput = new();
    private readonly Dictionary<int, Joystick> controllers = new();

    public IReadOnlyList<JoystickDeviceInfo> Discover(nint windowHandle)
    {
        ReleaseControllers();

        var discovered = new List<JoystickDeviceInfo>();
        var nextFallbackDeviceId = 0;
        var devices = directInput
            .GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
            .OrderBy(device => device.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var device in devices)
        {
            Joystick? joystick = null;

            try
            {
                joystick = new Joystick(directInput, device.InstanceGuid);
                joystick.SetCooperativeLevel(windowHandle, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                joystick.Properties.Range = StandardAxisRange;
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();

                var deviceId = ResolveDeviceId(joystick, nextFallbackDeviceId);
                while (controllers.ContainsKey(deviceId))
                {
                    deviceId++;
                }

                controllers[deviceId] = joystick;
                joystick = null;

                var capabilities = controllers[deviceId].Capabilities;
                discovered.Add(new JoystickDeviceInfo(
                    deviceId,
                    string.IsNullOrWhiteSpace(device.InstanceName) ? $"Joystick {deviceId}" : device.InstanceName,
                    capabilities.ButtonCount,
                    capabilities.AxeCount,
                    capabilities.PovCount,
                    0,
                    0));

                nextFallbackDeviceId = Math.Max(nextFallbackDeviceId, deviceId + 1);
            }
            catch (SharpDXException)
            {
                joystick?.Dispose();
            }
        }

        return discovered
            .OrderBy(device => device.DeviceId)
            .ToArray();
    }

    public bool TryReadState(JoystickDeviceInfo device, out JoystickLiveState? state)
    {
        state = null;

        if (!controllers.TryGetValue(device.DeviceId, out var joystick))
        {
            return false;
        }

        if (!TryGetCurrentState(joystick, out var currentState))
        {
            return false;
        }

        var pressedButtons = currentState.Buttons
            .Select((pressed, index) => (pressed, index))
            .Where(entry => entry.pressed)
            .Select(entry => entry.index + 1)
            .ToArray();

        var buttonMask = pressedButtons
            .Where(button => button <= 32)
            .Aggregate(0u, (mask, button) => mask | (1u << (button - 1)));

        var pov = currentState.PointOfViewControllers.FirstOrDefault(-1);
        var summary = pressedButtons.Length == 0
            ? $"X {currentState.X} | Y {currentState.Y} | Z {currentState.Z} | POV {FormatPov(pov)} | no buttons"
            : $"X {currentState.X} | Y {currentState.Y} | Z {currentState.Z} | POV {FormatPov(pov)} | buttons {string.Join(", ", pressedButtons)}";

        state = new JoystickLiveState(
            device.DeviceId,
            device.Name,
            ToUnsignedAxis(currentState.X),
            ToUnsignedAxis(currentState.Y),
            ToUnsignedAxis(currentState.Z),
            ToUnsignedAxis(currentState.RotationX),
            ToUnsignedAxis(currentState.RotationY),
            ToUnsignedAxis(currentState.RotationZ),
            pov < 0 ? 0xFFFFu : (uint)pov,
            buttonMask,
            pressedButtons,
            summary);
        return true;
    }

    public void Dispose()
    {
        ReleaseControllers();
        directInput.Dispose();
    }

    private static int ResolveDeviceId(Joystick joystick, int fallbackDeviceId)
    {
        try
        {
            var joystickId = joystick.Properties.JoystickId;
            return joystickId >= 0 ? joystickId : fallbackDeviceId;
        }
        catch (SharpDXException)
        {
            return fallbackDeviceId;
        }
    }

    private static bool TryGetCurrentState(Joystick joystick, out JoystickState state)
    {
        try
        {
            joystick.Poll();
            state = joystick.GetCurrentState();
            return true;
        }
        catch (SharpDXException)
        {
            try
            {
                joystick.Acquire();
                joystick.Poll();
                state = joystick.GetCurrentState();
                return true;
            }
            catch (SharpDXException)
            {
                state = new JoystickState();
                return false;
            }
        }
    }

    private static uint ToUnsignedAxis(int value) => (uint)Math.Clamp(value, 0, 65535);

    private static string FormatPov(int pov) => pov < 0 ? "centered" : (pov / 100.0).ToString("0.##");

    private void ReleaseControllers()
    {
        foreach (var controller in controllers.Values)
        {
            try
            {
                controller.Unacquire();
            }
            catch (SharpDXException)
            {
            }

            controller.Dispose();
        }

        controllers.Clear();
    }
}