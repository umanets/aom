$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    Write-Error 'vswhere.exe not found.'
    exit 1
}

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
$hostProjectPath = Join-Path $PSScriptRoot 'src\Aom.Overlay.D3D11.Host\Aom.Overlay.D3D11.Host.vcxproj'

& $msbuild $dllProjectPath /p:Configuration=Debug /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $msbuild $hostProjectPath /p:Configuration=Debug /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$hostExe = Join-Path $PSScriptRoot 'src\Aom.Overlay.D3D11.Host\bin\x64\Debug\AomOverlayPocHost.exe'
$overlayDll = Join-Path $PSScriptRoot 'src\Aom.Overlay.D3D11\bin\x64\Debug\AomOverlayD3D11.dll'

if (-not (Test-Path $hostExe)) {
    Write-Error "POC host executable was not found at $hostExe."
    exit 1
}

if (-not (Test-Path $overlayDll)) {
    Write-Error "Overlay DLL was not found at $overlayDll."
    exit 1
}

Get-Process AomOverlayPocHost -ErrorAction SilentlyContinue | Stop-Process -Force

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

$previousEnvValue = $env:AOM_OVERLAY_AUTOHOOK
$env:AOM_OVERLAY_AUTOHOOK = '1'
try {
    $process = Start-Process -FilePath $hostExe -ArgumentList '--no-direct-overlay-call', '--wait-for-attach' -PassThru
}
finally {
    if ($null -eq $previousEnvValue) {
        Remove-Item Env:\AOM_OVERLAY_AUTOHOOK -ErrorAction SilentlyContinue
    }
    else {
        $env:AOM_OVERLAY_AUTOHOOK = $previousEnvValue
    }
}

if ($null -eq $process) {
    Write-Error 'Failed to start the POC host process.'
    exit 1
}

$continueEvent = $null
$continueDeadline = [DateTime]::UtcNow.AddSeconds(10)
while ([DateTime]::UtcNow -lt $continueDeadline -and $null -eq $continueEvent) {
    try {
        $continueEvent = [System.Threading.EventWaitHandle]::OpenExisting('Local\AomOverlayPocContinue')
    }
    catch {
        Start-Sleep -Milliseconds 100
    }
}

if ($null -eq $continueEvent) {
    Write-Error 'The POC host did not expose the attach synchronization event.'
    try { Stop-Process -Id $process.Id -Force } catch {}
    exit 1
}

$processHandle = [AomInjectorNativeMethods]::OpenProcess(
    [AomInjectorNativeMethods+ProcessAccessFlags]::CreateThread -bor
    [AomInjectorNativeMethods+ProcessAccessFlags]::QueryInformation -bor
    [AomInjectorNativeMethods+ProcessAccessFlags]::VirtualMemoryOperation -bor
    [AomInjectorNativeMethods+ProcessAccessFlags]::VirtualMemoryWrite -bor
    [AomInjectorNativeMethods+ProcessAccessFlags]::VirtualMemoryRead,
    $false,
    $process.Id)

if ($processHandle -eq [IntPtr]::Zero) {
    Write-Error 'OpenProcess failed for the POC host.'
    try { Stop-Process -Id $process.Id -Force } catch {}
    exit 1
}

try {
    $dllBytes = [System.Text.Encoding]::Unicode.GetBytes($overlayDll + [char]0)
    $remoteMemory = [AomInjectorNativeMethods]::VirtualAllocEx(
        $processHandle,
        [IntPtr]::Zero,
        [UIntPtr]::op_Explicit($dllBytes.Length),
        [AomInjectorNativeMethods+AllocationType]::Commit -bor [AomInjectorNativeMethods+AllocationType]::Reserve,
        [AomInjectorNativeMethods+MemoryProtection]::ReadWrite)

    if ($remoteMemory -eq [IntPtr]::Zero) {
        Write-Error 'VirtualAllocEx failed for the remote DLL path.'
        try { Stop-Process -Id $process.Id -Force } catch {}
        exit 1
    }

    try {
        $bytesWritten = [IntPtr]::Zero
        if (-not [AomInjectorNativeMethods]::WriteProcessMemory($processHandle, $remoteMemory, $dllBytes, $dllBytes.Length, [ref]$bytesWritten)) {
            Write-Error 'WriteProcessMemory failed for the remote DLL path.'
            try { Stop-Process -Id $process.Id -Force } catch {}
            exit 1
        }

        $kernel32 = [AomInjectorNativeMethods]::GetModuleHandle('kernel32.dll')
        $loadLibraryW = [AomInjectorNativeMethods]::GetProcAddress($kernel32, 'LoadLibraryW')
        if ($kernel32 -eq [IntPtr]::Zero -or $loadLibraryW -eq [IntPtr]::Zero) {
            Write-Error 'Could not resolve LoadLibraryW for remote injection.'
            try { Stop-Process -Id $process.Id -Force } catch {}
            exit 1
        }

        $remoteThreadId = 0
        $remoteThread = [AomInjectorNativeMethods]::CreateRemoteThread($processHandle, [IntPtr]::Zero, [UIntPtr]::Zero, $loadLibraryW, $remoteMemory, 0, [ref]$remoteThreadId)
        if ($remoteThread -eq [IntPtr]::Zero) {
            Write-Error 'CreateRemoteThread failed for LoadLibraryW injection.'
            try { Stop-Process -Id $process.Id -Force } catch {}
            exit 1
        }

        try {
            [void][AomInjectorNativeMethods]::WaitForSingleObject($remoteThread, 10000)
        }
        finally {
            [void][AomInjectorNativeMethods]::CloseHandle($remoteThread)
        }
    }
    finally {
        [void][AomInjectorNativeMethods]::VirtualFreeEx($processHandle, $remoteMemory, [UIntPtr]::Zero, [AomInjectorNativeMethods+FreeType]::Release)
    }
}
finally {
    [void][AomInjectorNativeMethods]::CloseHandle($processHandle)
}

[void]$continueEvent.Set()

$hookHitEvent = $null
$hookDeadline = [DateTime]::UtcNow.AddSeconds(10)
while ([DateTime]::UtcNow -lt $hookDeadline -and $null -eq $hookHitEvent) {
    try {
        $hookHitEvent = [System.Threading.EventWaitHandle]::OpenExisting('Local\AomOverlayPresentHookHit')
    }
    catch {
        Start-Sleep -Milliseconds 100
    }
}

if ($null -eq $hookHitEvent) {
    Write-Error 'The injected overlay did not expose the Present-hook hit event.'
    exit 1
}

if (-not $hookHitEvent.WaitOne(10000)) {
    Write-Error 'The Present hook did not fire within 10 seconds.'
    exit 1
}

$process.Refresh()
[pscustomobject]@{
    ProcessId = $process.Id
    MainWindowTitle = $process.MainWindowTitle
    Injected = $true
    PresentHookObserved = $true
} | Format-List