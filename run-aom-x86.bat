@echo off
setlocal

set "BUILD_ONLY="
set "SKIP_ELEVATE="

for %%I in (%*) do (
    if /I "%%~I"=="--build-only" set "BUILD_ONLY=1"
    if /I "%%~I"=="--no-elevate" set "SKIP_ELEVATE=1"
)

if not defined SKIP_ELEVATE (
    powershell -NoProfile -Command "$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent()); if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 0 } else { exit 1 }"
    if errorlevel 1 (
        echo Requesting administrator privileges...
        powershell -NoProfile -Command "try { Start-Process -FilePath '%~f0' -ArgumentList '%*' -WorkingDirectory '%~dp0' -Verb RunAs | Out-Null; exit 0 } catch { exit 1 }"
        if errorlevel 1 (
            echo.
            echo Elevation was cancelled or failed.
            exit /b 1
        )
        exit /b 0
    )
)

pushd "%~dp0"

set "PROJECT=src\Aom.App\Aom.App.csproj"
set "PUBLISH_DIR=%~dp0src\Aom.App\bin\Debug\net10.0-windows\win-x86\publish"
set "APP_EXE=%PUBLISH_DIR%\Aom.App.exe"

echo Publishing Aom.App for win-x86...
dotnet publish "%PROJECT%" -c Debug -r win-x86 --self-contained false -p:PlatformTarget=x86
if errorlevel 1 (
    echo.
    echo Publish failed.
    popd
    exit /b 1
)

if defined BUILD_ONLY (
    echo.
    echo Publish completed. Skipping launch because --build-only was specified.
    popd
    exit /b 0
)

if not exist "%APP_EXE%" (
    echo.
    echo Published executable not found:
    echo %APP_EXE%
    popd
    exit /b 1
)

echo.
echo Starting:
echo %APP_EXE%
start "" "%APP_EXE%"

popd
endlocal
exit /b 0