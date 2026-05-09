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

$projectPath = Join-Path $PSScriptRoot 'src\Aom.Overlay.D3D11\Aom.Overlay.D3D11.vcxproj'
& $msbuild $projectPath /p:Configuration=Debug /p:Platform=x64
exit $LASTEXITCODE