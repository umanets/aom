using System.Runtime.InteropServices;
using System.Text;
using Aom.Core.Runtime;

namespace Aom.App.Services.TrackIr;

public sealed class TrackIrNativeClient : IDisposable
{
    private const short DataId = 119;
    private const short ProgramProfileId = 13302;

    private readonly nint libraryHandle;
    private readonly GetSignatureDelegate getSignature;
    private readonly QueryVersionDelegate queryVersion;
    private readonly RegisterWindowHandleDelegate registerWindowHandle;
    private readonly UnregisterWindowHandleDelegate unregisterWindowHandle;
    private readonly RegisterProgramProfileIdDelegate registerProgramProfileId;
    private readonly RequestDataDelegate requestData;
    private readonly StartDataTransmissionDelegate startDataTransmission;
    private readonly StopDataTransmissionDelegate stopDataTransmission;
    private readonly StartCursorDelegate startCursor;
    private readonly StopCursorDelegate stopCursor;
    private readonly GetDataDelegate getData;
    private bool initialized;
    private ushort lastFrame;

    public TrackIrNativeClient(string dllPath)
    {
        libraryHandle = NativeLibrary.Load(dllPath);
        getSignature = GetExport<GetSignatureDelegate>("NP_GetSignature");
        queryVersion = GetExport<QueryVersionDelegate>("NP_QueryVersion");
        registerWindowHandle = GetExport<RegisterWindowHandleDelegate>("NP_RegisterWindowHandle");
        unregisterWindowHandle = GetExport<UnregisterWindowHandleDelegate>("NP_UnregisterWindowHandle");
        registerProgramProfileId = GetExport<RegisterProgramProfileIdDelegate>("NP_RegisterProgramProfileID");
        requestData = GetExport<RequestDataDelegate>("NP_RequestData");
        startDataTransmission = GetExport<StartDataTransmissionDelegate>("NP_StartDataTransmission");
        stopDataTransmission = GetExport<StopDataTransmissionDelegate>("NP_StopDataTransmission");
        startCursor = GetExport<StartCursorDelegate>("NP_StartCursor");
        stopCursor = GetExport<StopCursorDelegate>("NP_StopCursor");
        getData = GetExport<GetDataDelegate>("NP_GetData");
    }

    public string GetSignature()
    {
        var buffer = Marshal.AllocHGlobal(400);
        try
        {
            Span<byte> clear = stackalloc byte[400];
            Marshal.Copy(clear.ToArray(), 0, buffer, clear.Length);
            Execute(getSignature(buffer), "NP_GetSignature");

            var bytes = new byte[400];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            var firstNull = Array.IndexOf(bytes, (byte)0);
            if (firstNull < 0)
            {
                firstNull = bytes.Length;
            }

            return Encoding.ASCII.GetString(bytes, 0, firstNull);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public short QueryVersion()
    {
        var memory = Marshal.AllocHGlobal(sizeof(short));
        try
        {
            Execute(queryVersion(memory), "NP_QueryVersion");
            return Marshal.ReadInt16(memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    public void Initialize(nint windowHandle)
    {
        Execute(registerWindowHandle(windowHandle), "NP_RegisterWindowHandle");
        Execute(requestData(DataId), "NP_RequestData");
        Execute(registerProgramProfileId(ProgramProfileId), "NP_RegisterProgramProfileID");
        Execute(stopCursor(), "NP_StopCursor");
        Execute(startDataTransmission(), "NP_StartDataTransmission");
        initialized = true;
    }

    public bool TryReadPose(out HeadPose pose)
    {
        pose = default;
        var raw = new TrackIrRawData();
        Execute(getData(ref raw), "NP_GetData");

        if (raw.Frame == 0 || raw.Frame == lastFrame)
        {
            return false;
        }

        lastFrame = raw.Frame;
        pose = new HeadPose(
            Yaw: -(raw.Yaw * 180.0f) / 16384.0f,
            Pitch: -(raw.Pitch * 180.0f) / 16384.0f,
            Roll: -(raw.Roll * 180.0f) / 16384.0f,
            X: -raw.Tx / 64.0f,
            Y: raw.Ty / 64.0f,
            Z: raw.Tz / 64.0f);
        return true;
    }

    public void Dispose()
    {
        if (libraryHandle == nint.Zero)
        {
            return;
        }

        try
        {
            if (initialized)
            {
                stopDataTransmission();
                startCursor();
                unregisterWindowHandle();
            }
        }
        finally
        {
            NativeLibrary.Free(libraryHandle);
        }
    }

    private T GetExport<T>(string name) where T : Delegate
    {
        var export = NativeLibrary.GetExport(libraryHandle, name);
        return Marshal.GetDelegateForFunctionPointer<T>(export);
    }

    private static void Execute(int result, string apiName)
    {
        if (result != 0)
        {
            throw new InvalidOperationException($"{apiName} failed with error code {result}.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackIrRawData
    {
        public short Status;
        public ushort Frame;
        public uint Checksum;
        public float Roll;
        public float Pitch;
        public float Yaw;
        public float Tx;
        public float Ty;
        public float Tz;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public float[] Padding;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetSignatureDelegate(nint buffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryVersionDelegate(nint version);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int RegisterWindowHandleDelegate(nint windowHandle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int UnregisterWindowHandleDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int RegisterProgramProfileIdDelegate(short profileId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int RequestDataDelegate(short dataId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StartDataTransmissionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StopDataTransmissionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StartCursorDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StopCursorDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDataDelegate(ref TrackIrRawData data);
}