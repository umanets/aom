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
if (-not (Test-Path $hostExe)) {
    Write-Error "POC host executable was not found at $hostExe."
    exit 1
}

Start-Process -FilePath $hostExe