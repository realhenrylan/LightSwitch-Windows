@echo off
chcp 65001 >nul
title LightSwitch Install

echo ============================================
echo  LightSwitch Standalone — 安装/卸载
echo ============================================
echo.
echo  1. 安装 — 添加到开机启动
echo  2. 卸载 — 移除开机启动
echo  3. 退出
echo.
set /p choice="请选择 (1/2/3): "

if "%choice%"=="1" goto install
if "%choice%"=="2" goto uninstall
if "%choice%"=="3" exit /b 0

echo 无效选择
pause
exit /b 1

:install
echo.
set "EXE_PATH=%~dp0x64\Release\LightSwitch.exe"
if not exist "%EXE_PATH%" (
    echo [错误] 未找到编译后的程序。
    echo 请先运行 build.bat 编译。
    pause
    exit /b 1
)
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" ^
    /v "LightSwitch" /t REG_SZ /d "%EXE_PATH%" /f >nul
if %ERRORLEVEL% EQU 0 (
    echo [成功] LightSwitch 已添加到开机启动！
) else (
    echo [错误] 注册失败
)
pause
exit /b 0

:uninstall
echo.
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" ^
    /v "LightSwitch" /f >nul 2>&1
echo [完成] 已移除开机启动项。
pause
exit /b 0
