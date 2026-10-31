#include "pch.h"
#include "MainWindow.h"
#include "ThemeHelper.h"
#include "NightLightRegistryObserver.h"
#include "ConfigManager.h"
#include "resource.h"
#include <memory>

std::unique_ptr<MainWindow> g_mainWindow;
std::unique_ptr<NightLightRegistryObserver> g_nightLightObserver;
HANDLE g_hConfigWatchThread = nullptr;
std::atomic<bool> g_configWatchStop{ false };

static void PostWindowMessage(UINT msg, WPARAM wParam = 0, LPARAM lParam = 0)
{
    if (g_mainWindow && g_mainWindow->GetHwnd())
        PostMessageW(g_mainWindow->GetHwnd(), msg, wParam, lParam);
}

static void OnNightLightChanged()
{
    PostWindowMessage(WM_NIGHTLIGHT_CHANGED);
}

static DWORD WINAPI ConfigWatchThreadProc(LPVOID)
{
    std::wstring configPath = ConfigManager::GetConfigPath();
    size_t lastSlash = configPath.find_last_of(L"\\/");
    if (lastSlash == std::wstring::npos) return 0;
    std::wstring dir = configPath.substr(0, lastSlash + 1);
    HANDLE hChange = FindFirstChangeNotificationW(dir.c_str(), FALSE, FILE_NOTIFY_CHANGE_LAST_WRITE);
    if (hChange == INVALID_HANDLE_VALUE) return 0;
    while (!g_configWatchStop)
    {
        DWORD wait = WaitForSingleObject(hChange, 500);
        if (wait == WAIT_OBJECT_0)
        {
            Sleep(500);
            PostWindowMessage(WM_FILE_CHANGED);
            FindNextChangeNotification(hChange);
        }
    }
    FindCloseChangeNotification(hChange);
    return 0;
}

static bool ParseCommandLineArgs(int argc, wchar_t* argv[])
{
    for (int i = 1; i < argc; ++i)
    {
        if (_wcsicmp(argv[i], L"/toggle") == 0 || _wcsicmp(argv[i], L"--toggle") == 0)
        {
            bool current = GetCurrentSystemTheme();
            SetSystemTheme(!current);
            SetAppsTheme(!current);
            return false;
        }
    }
    return true;
}

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE, LPWSTR lpCmdLine, int nCmdShow)
{
    HANDLE hMutex = CreateMutexW(nullptr, TRUE, L"LightSwitchStandalone_Mutex");
    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        HWND hWnd = FindWindowW(L"LightSwitchMainWindow", nullptr);
        if (hWnd) { ShowWindow(hWnd, SW_SHOW); SetForegroundWindow(hWnd); }
        return 0;
    }
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    // 自绘界面使用 ComCtl32 v6 控件与 DPI 感知 API
    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_STANDARD_CLASSES | ICC_WIN95_CLASSES };
    InitCommonControlsEx(&icc);
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    bool shouldRunMain = true;
    if (argc > 1) shouldRunMain = ParseCommandLineArgs(argc, argv);
    LocalFree(argv);
    if (!shouldRunMain) { CoUninitialize(); if (hMutex) CloseHandle(hMutex); return 0; }
    ConfigManager::Instance().Load();
    g_mainWindow = std::make_unique<MainWindow>();
    if (!g_mainWindow->Initialize(hInstance, nCmdShow))
    {
        MessageBoxW(nullptr, L"初始化主窗口失败", L"错误", MB_OK | MB_ICONERROR);
        CoUninitialize(); if (hMutex) CloseHandle(hMutex); return 1;
    }
    RegisterHotKey(g_mainWindow->GetHwnd(), IDH_TOGGLE_THEME, MOD_CONTROL | MOD_ALT | MOD_SHIFT, 'D');
    SetTimer(g_mainWindow->GetHwnd(), 1, 60000, nullptr);
    g_nightLightObserver = std::make_unique<NightLightRegistryObserver>(
        HKEY_CURRENT_USER, NIGHT_LIGHT_REGISTRY_PATH, OnNightLightChanged);
    g_hConfigWatchThread = CreateThread(nullptr, 0, ConfigWatchThreadProc, nullptr, 0, nullptr);
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    KillTimer(g_mainWindow->GetHwnd(), 1);
    UnregisterHotKey(g_mainWindow->GetHwnd(), IDH_TOGGLE_THEME);
    g_configWatchStop = true;
    if (g_hConfigWatchThread) { WaitForSingleObject(g_hConfigWatchThread, 1000); CloseHandle(g_hConfigWatchThread); }
    g_nightLightObserver.reset();
    g_mainWindow->Shutdown(); g_mainWindow.reset();
    CoUninitialize(); if (hMutex) CloseHandle(hMutex);
    return static_cast<int>(msg.wParam);
}
