@echo off
chcp 65001 >nul
title LightSwitch Build

echo ============================================
echo  LightSwitch Standalone — Build Script
echo ============================================
echo.

REM Try to find Visual Studio via vswhere
set "VSINSTALLDIR="
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath 2^>nul`) do set "VSINSTALLDIR=%%i\"

if not defined VSINSTALLDIR (
    echo [ERROR] Visual Studio not found.
    echo Please install Visual Studio 2022+ with "Desktop development with C++" workload.
    echo.
    pause
    exit /b 1
)

echo Found Visual Studio: %VSINSTALLDIR%
echo.

REM Initialize build environment
call "%VSINSTALLDIR%VC\Auxiliary\Build\vcvarsall.bat" x64 >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to initialize MSVC environment.
    pause
    exit /b 1
)

REM Project paths
set "PROJDIR=%~dp0"
REM Remove trailing backslash
set "PROJDIR=%PROJDIR:~0,-1%"
set "SRCDIR=%PROJDIR%\src"
set "OUTDIR=%PROJDIR%\x64\Release"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

REM Compiler flags: C++17, Unicode, static CRT, optimize for size, UTF-8 source
set "CFLAGS=/nologo /std:c++17 /EHsc /W3 /O1 /MT /utf-8 /DUNICODE /D_UNICODE /DWIN32 /D_WINDOWS /DNDEBUG /I%SRCDIR%"

REM Linker flags: Windows subsystem, x64, static CRT, strip unused
set "LFLAGS=/SUBSYSTEM:WINDOWS /MACHINE:X64 /OPT:REF /OPT:ICF kernel32.lib user32.lib comctl32.lib advapi32.lib shell32.lib shlwapi.lib gdi32.lib ole32.lib"

echo [1/7] Compiling pch.cpp...
cl %CFLAGS% /c "%SRCDIR%\pch.cpp" /Fo"%OUTDIR%\pch.obj"
if %ERRORLEVEL% NEQ 0 goto :error

echo [2/7] Compiling ThemeHelper.cpp...
cl %CFLAGS% /c "%SRCDIR%\ThemeHelper.cpp" /Fo"%OUTDIR%\ThemeHelper.obj"
if %ERRORLEVEL% NEQ 0 goto :error

echo [3/7] Compiling ThemeScheduler.cpp...
cl %CFLAGS% /c "%SRCDIR%\ThemeScheduler.cpp" /Fo"%OUTDIR%\ThemeScheduler.obj"
if %ERRORLEVEL% NEQ 0 goto :error

echo [4/7] Compiling ConfigManager.cpp...
cl %CFLAGS% /c "%SRCDIR%\ConfigManager.cpp" /Fo"%OUTDIR%\ConfigManager.obj"
if %ERRORLEVEL% NEQ 0 goto :error

echo [5/7] Compiling LightSwitchStateManager.cpp...
cl %CFLAGS% /c "%SRCDIR%\LightSwitchStateManager.cpp" /Fo"%OUTDIR%\LightSwitchStateManager.obj"
if %ERRORLEVEL% NEQ 0 goto :error

echo [6/7] Compiling GUI modules...
cl %CFLAGS% /c "%SRCDIR%\MainWindow.cpp" /Fo"%OUTDIR%\MainWindow.obj"
if %ERRORLEVEL% NEQ 0 goto :error
cl %CFLAGS% /c "%SRCDIR%\TrayIcon.cpp" /Fo"%OUTDIR%\TrayIcon.obj"
if %ERRORLEVEL% NEQ 0 goto :error
cl %CFLAGS% /c "%SRCDIR%\main.cpp" /Fo"%OUTDIR%\main.obj"
if %ERRORLEVEL% NEQ 0 goto :error

echo [7/7] Linking...
rc /fo"%OUTDIR%\LightSwitch.res" "%PROJDIR%\LightSwitch.rc"
link %LFLAGS% ^
    /MANIFEST:EMBED /MANIFESTINPUT:"%PROJDIR%\resources\app.manifest" ^
    "%OUTDIR%\pch.obj" ^
    "%OUTDIR%\ThemeHelper.obj" ^
    "%OUTDIR%\ThemeScheduler.obj" ^
    "%OUTDIR%\ConfigManager.obj" ^
    "%OUTDIR%\LightSwitchStateManager.obj" ^
    "%OUTDIR%\MainWindow.obj" ^
    "%OUTDIR%\TrayIcon.obj" ^
    "%OUTDIR%\main.obj" ^
    "%OUTDIR%\LightSwitch.res" ^
    /OUT:"%OUTDIR%\LightSwitch.exe"
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo ============================================
echo  Build SUCCESS!
echo ============================================
echo.
echo Output: %OUTDIR%\LightSwitch.exe
dir "%OUTDIR%\LightSwitch.exe"
echo.
pause
exit /b 0

:error
echo.
echo ============================================
echo  Build FAILED!
echo ============================================
pause
exit /b 1
