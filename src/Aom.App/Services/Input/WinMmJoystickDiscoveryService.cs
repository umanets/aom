using System.Runtime.InteropServices;

namespace Aom.App.Services.Input;

public sealed class WinMmJoystickDiscoveryService
{
    private const int JoystickErrorNone = 0;
    private const uint JoyReturnAll = 0x000000FF;

    public IReadOnlyList<JoystickDeviceInfo> Discover()
    {
        var discovered = new List<JoystickDeviceInfo>();
        var deviceCount = joyGetNumDevs();

        for (var deviceId = 0; deviceId < deviceCount; deviceId++)
        {
            var caps = new JoyCaps();
            var result = joyGetDevCaps((uint)deviceId, ref caps, (uint)Marshal.SizeOf<JoyCaps>());
            if (result != JoystickErrorNone)
            {
                continue;
            }

            discovered.Add(new JoystickDeviceInfo(
                deviceId,
                caps.Name,
                (int)caps.MaxButtons,
                (int)caps.MaxAxes,
                (int)caps.MaxPovs,
                (int)caps.PeriodMin,
                (int)caps.PeriodMax));
        }

        return discovered;
    }

    public bool TryReadState(JoystickDeviceInfo device, out JoystickLiveState? state)
    {
        var info = new JoyInfoEx
        {
            Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
            Flags = JoyReturnAll,
        };

        var result = joyGetPosEx((uint)device.DeviceId, ref info);
        if (result != JoystickErrorNone)
        {
            state = null;
            return false;
        }

        var pressedButtons = Enumerable.Range(0, 32)
            .Where(index => (info.Buttons & (1u << index)) != 0)
            .Select(index => index + 1)
            .ToArray();

        var summary = pressedButtons.Length == 0
            ? $"X {info.X} | Y {info.Y} | Z {info.Z} | POV {FormatPov(info.Pov)} | no buttons"
            : $"X {info.X} | Y {info.Y} | Z {info.Z} | POV {FormatPov(info.Pov)} | buttons {string.Join(", ", pressedButtons)}";

        state = new JoystickLiveState(
            device.DeviceId,
            device.Name,
            info.X,
            info.Y,
            info.Z,
            info.R,
            info.U,
            info.V,
            info.Pov,
            info.Buttons,
            pressedButtons,
            summary);
        return true;
    }

    private static string FormatPov(uint pov) => pov == 0xFFFF ? "centered" : (pov / 100.0).ToString("0.##");

    [DllImport("winmm.dll")]
    private static extern uint joyGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int joyGetDevCaps(uint uJoyID, ref JoyCaps pjc, uint cbjc);

    [DllImport("winmm.dll")]
    private static extern int joyGetPosEx(uint uJoyID, ref JoyInfoEx pji);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort ManufacturerId;
        public ushort ProductId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;

        public uint XMin;
        public uint XMax;
        public uint YMin;
        public uint YMax;
        public uint ZMin;
        public uint ZMax;
        public uint NumberOfButtons;
        public uint PeriodMin;
        public uint PeriodMax;
        public uint Reserved1;
        public uint Reserved2;
        public uint RMin;
        public uint RMax;
        public uint UMin;
        public uint UMax;
        public uint VMin;
        public uint VMax;
        public uint Caps;
        public uint MaxAxes;
        public uint NumberOfAxes;
        public uint MaxButtons;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string RegistryKey;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string OemVxd;

        public uint MaxPovs;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint Size;
        public uint Flags;
        public uint X;
        public uint Y;
        public uint Z;
        public uint R;
        public uint U;
        public uint V;
        public uint Buttons;
        public uint ButtonNumber;
        public uint Pov;
        public uint Reserved1;
        public uint Reserved2;
    }
}