@echo off
echo Building Pixel Motion Photo Player...

REM Clean previous builds
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

REM Restore packages
dotnet restore

REM Build release version
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

echo.
echo Build completed successfully!
echo Executable location: bin\Release\net8.0-windows\win-x64\publish\PixelMpPlayer.exe
echo.

REM Create installer if Inno Setup is available
where iscc >nul 2>nul
if %ERRORLEVEL% == 0 (
    echo Creating installer...
    cd installer
    iscc setup.iss
    echo Installer created: installer\output\PixelMpPlayer-Setup-v1.0.0.exe
    cd ..
) else (
    echo Inno Setup not found. To create installer:
    echo 1. Install Inno Setup from https://jrsoftware.org/isinfo.php
    echo 2. Run: iscc installer\setup.iss
)

pause