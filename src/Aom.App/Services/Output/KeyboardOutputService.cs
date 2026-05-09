using System.Runtime.InteropServices;
using Aom.Core.Runtime;

namespace Aom.App.Services.Output;

public sealed class KeyboardOutputService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const int GlobalCenterKeyHoldMilliseconds = 30;
    private const ushort VirtualKeyF = 0x46;
    private const ushort VirtualKeyF7 = 0x76;
    private const ushort VirtualKeyLeftShift = 0xA0;

    public void TapGlobalCenterKey()
    {
        SendInputs(Input.ForKey(VirtualKeyF7, 0));
        Thread.Sleep(GlobalCenterKeyHoldMilliseconds);
        SendInputs(Input.ForKey(VirtualKeyF7, KeyEventFKeyUp));
    }

    public void ApplyFlapOutput(FlapAutomationOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.ReleaseOpenKeys)
        {
            SendInputs(Input.ForKey(VirtualKeyF, KeyEventFKeyUp));
        }

        if (output.ReleaseCloseKeys)
        {
            SendInputs(
                Input.ForKey(VirtualKeyF, KeyEventFKeyUp),
                Input.ForKey(VirtualKeyLeftShift, KeyEventFKeyUp));
        }

        if (output.PressOpenKeys)
        {
            SendInputs(Input.ForKey(VirtualKeyF, 0));
        }

        if (output.PressCloseKeys)
        {
            SendInputs(
                Input.ForKey(VirtualKeyF, 0),
                Input.ForKey(VirtualKeyLeftShift, 0));
        }
    }

    public void ReleaseAutomationKeys()
    {
        SendInputs(
            Input.ForKey(VirtualKeyF, KeyEventFKeyUp),
            Input.ForKey(VirtualKeyLeftShift, KeyEventFKeyUp));
    }

    private static void SendInputs(params Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Failed to send keyboard automation input.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, ReadOnlySpan<Input> pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;

        public static Input ForKey(ushort virtualKey, uint flags) =>
            new()
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        ScanCode = 0,
                        Flags = flags,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero,
                    },
                },
            };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}