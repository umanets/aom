param(
    [string]$GameExePath = 'E:\Programs\IL-2 Sturmovik Great Battles\bin\game\Il-2.exe',
    [string]$LauncherExePath = 'E:\Programs\IL-2 Sturmovik Great Battles\bin\game\launcher.exe',
    [switch]$UseLauncher,
    [switch]$DryRun,
    [int]$TimeoutSeconds = 120
)

function Write-Step {
    param([string]$Message)

    Write-Host "[AOM IL2] $Message"
}

function Wait-EventInterruptible {
    param(
        [Parameter(Mandatory = $true)]
        [System.Threading.EventWaitHandle]$EventHandle,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds,
        [Parameter(Mandatory = $true)]
        [string]$WaitingMessage,
        [string]$TimeoutMessage,
        [int]$PollMilliseconds = 500
    )

    Write-Step $WaitingMessage
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastHeartbeatAt = [DateTime]::MinValue

    while ([DateTime]::UtcNow -lt $deadline) {
        if ($EventHandle.WaitOne($PollMilliseconds)) {
            return $true
        }

        if ([DateTime]::UtcNow - $lastHeartbeatAt -ge [TimeSpan]::FromSeconds(5)) {
            $remainingSeconds = [Math]::Max(0, [int]($deadline - [DateTime]::UtcNow).TotalSeconds)
            Write-Step "$WaitingMessage Remaining: ${remainingSeconds}s"
            $lastHeartbeatAt = [DateTime]::UtcNow
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($TimeoutMessage)) {
        Write-Error $TimeoutMessage
    }

    return $false
}

function Start-ProcessSuspended {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    if (-not ('AomProcessStarterNativeMethods' -as [type])) {
        $processStarterSource = @"
using System;
using System.Runtime.InteropServices;

public static class AomProcessStarterNativeMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public uint cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessW(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
}
"@

        Add-Type -TypeDefinition $processStarterSource | Out-Null
    }

    $startupInfo = New-Object AomProcessStarterNativeMethods+STARTUPINFO
    $startupInfo.cb = [System.Runtime.InteropServices.Marshal]::SizeOf($startupInfo)
    $processInformation = New-Object AomProcessStarterNativeMethods+PROCESS_INFORMATION
    $workingDirectory = Split-Path -Path $ExecutablePath -Parent
    $createSuspended = 0x00000004

    $created = [AomProcessStarterNativeMethods]::CreateProcessW(
        $ExecutablePath,
        $null,
        [IntPtr]::Zero,
        [IntPtr]::Zero,
        $false,
        $createSuspended,
        [IntPtr]::Zero,
        $workingDirectory,
        [ref]$startupInfo,
        [ref]$processInformation)

    if (-not $created) {
        $lastError = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "CreateProcessW failed with Win32 error $lastError for $ExecutablePath"
    }

    return [pscustomobject]@{
        ProcessId = $processInformation.dwProcessId
        ProcessHandle = $processInformation.hProcess
        ThreadHandle = $processInformation.hThread
    }
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    Write-Error 'vswhere.exe not found.'
    exit 1
}

Get-Process AomOverlayPocHost -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Step 'Resolving Visual Studio Build Tools and preparing overlay DLL build.'

$installationPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ([string]::IsNullOrWhiteSpace($installationPath)) {
    Write-Error 'Visual Studio Build Tools with C++ workload were not found.'
    exit 1
}

$msbuild = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild.exe not found under $installationPath."
    exit 1
}

$dllProjectPath = Join-Path $PSScriptRoot 'src\Aom.Overlay.D3D11\Aom.Overlay.D3D11.vcxproj'
& $msbuild $dllProjectPath /p:Configuration=Debug /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$overlayDll = Join-Path $PSScriptRoot 'src\Aom.Overlay.D3D11\bin\x64\Debug\AomOverlayD3D11.dll'
if (-not (Test-Path $overlayDll)) {
    Write-Error "Overlay DLL was not found at $overlayDll."
    exit 1
}

$launchPath = if ($UseLauncher) { $LauncherExePath } else { $GameExePath }
if (-not (Test-Path $launchPath)) {
    Write-Error "Launch target was not found at $launchPath."
    exit 1
}

$hookEventName = 'Local\AomOverlayPresentHookHit.IL2.' + [Guid]::NewGuid().ToString('N')
Write-Step "Using hook event name: $hookEventName"

if ($DryRun) {
    [pscustomobject]@{
        LaunchPath = $launchPath
        GameExePath = $GameExePath
        UseLauncher = [bool]$UseLauncher
        OverlayDll = $overlayDll
        HookEventName = $hookEventName
    } | Format-List
    exit 0
}

if (-not ('AomInjectorNativeMethods' -as [type])) {
$source = @"
using System;
using System.Runtime.InteropServices;

public static class AomInjectorNativeMethods
{
    [Flags]
    public enum ProcessAccessFlags : uint
    {
        CreateThread = 0x0002,
        QueryInformation = 0x0400,
        VirtualMemoryOperation = 0x0008,
        VirtualMemoryRead = 0x0010,
        VirtualMemoryWrite = 0x0020,
    }

    [Flags]
    public enum AllocationType : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000,
    }

    [Flags]
    public enum MemoryProtection : uint
    {
        ReadWrite = 0x04,
    }

    [Flags]
    public enum FreeType : uint
    {
        Release = 0x8000,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccessFlags desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(IntPtr processHandle, IntPtr address, UIntPtr size, AllocationType allocationType, MemoryProtection protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(IntPtr processHandle, IntPtr address, UIntPtr size, FreeType freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, int size, out IntPtr bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr moduleHandle, string procedureName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(IntPtr processHandle, IntPtr threadAttributes, UIntPtr stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, out int threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);
}
"@

Add-Type -TypeDefinition $source | Out-Null
}
Write-Step 'Native injector helpers loaded.'

$previousAutoHookValue = $env:AOM_OVERLAY_AUTOHOOK
$previousHookEventValue = $env:AOM_OVERLAY_HOOK_EVENT
$suspendedProcess = $null
$startedProcess = $null
$injectionProcessHandle = [IntPtr]::Zero
$resumeThreadHandle = [IntPtr]::Zero
$env:AOM_OVERLAY_AUTOHOOK = '1'
$env:AOM_OVERLAY_HOOK_EVENT = $hookEventName
try {
    if ($UseLauncher) {
        Write-Step "Starting launcher process: $launchPath"
        $startedProcess = Start-Process -FilePath $launchPath -PassThru
    }
    else {
        Write-Step "Starting target process suspended: $launchPath"
        $suspendedProcess = Start-ProcessSuspended -ExecutablePath $launchPath
        $injectionProcessHandle = $suspendedProcess.ProcessHandle
        $resumeThreadHandle = $suspendedProcess.ThreadHandle
        $startedProcess = Get-Process -Id $suspendedProcess.ProcessId -ErrorAction Stop
    }
}
finally {
    if ($null -eq $previousAutoHookValue) {
        Remove-Item Env:\AOM_OVERLAY_AUTOHOOK -ErrorAction SilentlyContinue
    }
    else {
        $env:AOM_OVERLAY_AUTOHOOK = $previousAutoHookValue
    }

    if ($null -eq $previousHookEventValue) {
        Remove-Item Env:\AOM_OVERLAY_HOOK_EVENT -ErrorAction SilentlyContinue
    }
    else {
        $env:AOM_OVERLAY_HOOK_EVENT = $previousHookEventValue
    }
}

if ($null -eq $startedProcess) {
    Write-Error 'Failed to start IL-2.'
    exit 1
}

Write-Step "Started process id $($startedProcess.Id)."

$targetProcess = $null
if (-not $UseLauncher) {
    $targetProcess = $startedProcess
}
else {
    Write-Step 'Waiting for launcher.exe to spawn Il-2.exe.'
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    while ([DateTime]::UtcNow -lt $deadline -and $null -eq $targetProcess) {
        $targetProcess = Get-CimInstance Win32_Process -Filter "Name = 'Il-2.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.ExecutablePath -eq $GameExePath } |
            Select-Object -First 1
        if ($null -eq $targetProcess) {
            Start-Sleep -Milliseconds 500
        }
    }

    if ($null -eq $targetProcess) {
        Write-Error 'IL-2.exe did not appear after starting launcher.exe.'
        exit 1
    }
}

$targetProcessId = if ($UseLauncher) { [int]$targetProcess.ProcessId } else { $targetProcess.Id }
Write-Step "Injection target process id: $targetProcessId"
if ($UseLauncher) {
    $processHandle = [AomInjectorNativeMethods]::OpenProcess(
        [AomInjectorNativeMethods+ProcessAccessFlags]::CreateThread -bor
        [AomInjectorNativeMethods+ProcessAccessFlags]::QueryInformation -bor
        [AomInjectorNativeMethods+ProcessAccessFlags]::VirtualMemoryOperation -bor
        [AomInjectorNativeMethods+ProcessAccessFlags]::VirtualMemoryWrite -bor
        [AomInjectorNativeMethods+ProcessAccessFlags]::VirtualMemoryRead,
        $false,
        $targetProcessId)

    if ($processHandle -eq [IntPtr]::Zero) {
        Write-Error 'OpenProcess failed for IL-2. Try running VS Code or PowerShell as administrator.'
        exit 1
    }

    $injectionProcessHandle = $processHandle
}

try {
    Write-Step 'Allocating memory in the IL-2 process for the overlay DLL path.'
    $dllBytes = [System.Text.Encoding]::Unicode.GetBytes($overlayDll + [char]0)
    $remoteMemory = [AomInjectorNativeMethods]::VirtualAllocEx(
        $injectionProcessHandle,
        [IntPtr]::Zero,
        [UIntPtr]::op_Explicit($dllBytes.Length),
        [AomInjectorNativeMethods+AllocationType]::Commit -bor [AomInjectorNativeMethods+AllocationType]::Reserve,
        [AomInjectorNativeMethods+MemoryProtection]::ReadWrite)

    if ($remoteMemory -eq [IntPtr]::Zero) {
        Write-Error 'VirtualAllocEx failed for the IL-2 DLL path.'
        exit 1
    }

    try {
        $bytesWritten = [IntPtr]::Zero
        if (-not [AomInjectorNativeMethods]::WriteProcessMemory($injectionProcessHandle, $remoteMemory, $dllBytes, $dllBytes.Length, [ref]$bytesWritten)) {
            Write-Error 'WriteProcessMemory failed for the IL-2 DLL path.'
            exit 1
        }

        Write-Step 'Injecting overlay DLL with LoadLibraryW remote thread.'

        $kernel32 = [AomInjectorNativeMethods]::GetModuleHandle('kernel32.dll')
        $loadLibraryW = [AomInjectorNativeMethods]::GetProcAddress($kernel32, 'LoadLibraryW')
        if ($kernel32 -eq [IntPtr]::Zero -or $loadLibraryW -eq [IntPtr]::Zero) {
            Write-Error 'Could not resolve LoadLibraryW for remote injection.'
            exit 1
        }

        $remoteThreadId = 0
        $remoteThread = [AomInjectorNativeMethods]::CreateRemoteThread($injectionProcessHandle, [IntPtr]::Zero, [UIntPtr]::Zero, $loadLibraryW, $remoteMemory, 0, [ref]$remoteThreadId)
        if ($remoteThread -eq [IntPtr]::Zero) {
            Write-Error 'CreateRemoteThread failed for IL-2 injection.'
            exit 1
        }

        try {
            [void][AomInjectorNativeMethods]::WaitForSingleObject($remoteThread, 15000)
            Write-Step 'Remote LoadLibraryW thread completed.'
        }
        finally {
            [void][AomInjectorNativeMethods]::CloseHandle($remoteThread)
        }

        if (-not $UseLauncher -and $resumeThreadHandle -ne [IntPtr]::Zero) {
            Write-Step 'Resuming the suspended IL-2 main thread after injection.'
            [void][AomProcessStarterNativeMethods]::ResumeThread($resumeThreadHandle)
            [void][AomProcessStarterNativeMethods]::CloseHandle($resumeThreadHandle)
            $resumeThreadHandle = [IntPtr]::Zero
        }
    }
    finally {
        [void][AomInjectorNativeMethods]::VirtualFreeEx($injectionProcessHandle, $remoteMemory, [UIntPtr]::Zero, [AomInjectorNativeMethods+FreeType]::Release)
    }
}
finally {
    if ($UseLauncher -and $injectionProcessHandle -ne [IntPtr]::Zero) {
        [void][AomInjectorNativeMethods]::CloseHandle($injectionProcessHandle)
    }

    if ($resumeThreadHandle -ne [IntPtr]::Zero) {
        [void][AomProcessStarterNativeMethods]::CloseHandle($resumeThreadHandle)
    }

    if (-not $UseLauncher -and $suspendedProcess -ne $null -and $suspendedProcess.ProcessHandle -ne [IntPtr]::Zero) {
        [void][AomProcessStarterNativeMethods]::CloseHandle($suspendedProcess.ProcessHandle)
    }
}

$hookHitEvent = $null
$hookDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
Write-Step 'Waiting for the overlay DLL to expose its Present-hook event.'
while ([DateTime]::UtcNow -lt $hookDeadline -and $null -eq $hookHitEvent) {
    try {
        $hookHitEvent = [System.Threading.EventWaitHandle]::OpenExisting($hookEventName)
    }
    catch {
        Start-Sleep -Milliseconds 250
    }
}

if ($null -eq $hookHitEvent) {
    Write-Error 'The injected IL-2 overlay did not expose the Present-hook event.'
    exit 1
}

if (-not (Wait-EventInterruptible -EventHandle $hookHitEvent -TimeoutSeconds $TimeoutSeconds -WaitingMessage 'Waiting for the first hooked Present call from IL-2.' -TimeoutMessage 'The IL-2 Present hook did not fire before timeout. Try entering a 3D scene or increasing -TimeoutSeconds.')) {
    exit 1
}

Write-Step 'The first hooked Present call was observed.'

[pscustomobject]@{
    TargetProcessId = $targetProcessId
    LaunchPath = $launchPath
    UseLauncher = [bool]$UseLauncher
    HookEventName = $hookEventName
    PresentHookObserved = $true
} | Format-List