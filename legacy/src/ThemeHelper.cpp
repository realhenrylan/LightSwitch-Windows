#include "pch.h"
#include "ThemeHelper.h"
static void SendThemeChangeBroadcast()
{
    SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);
    SendMessageTimeoutW(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    SendMessageTimeoutW(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
}
static void ResetColorPrevalence()
{
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
    {
        DWORD value = 0;
        RegSetValueExW(hKey, L"ColorPrevalence", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);
        SendThemeChangeBroadcast();
    }
}
void SetAppsTheme(bool mode)
{
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
    {
        DWORD value = mode ? 1 : 0;
        RegSetValueExW(hKey, L"AppsUseLightTheme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);
        SendThemeChangeBroadcast();
        OutputDebugStringW(mode ? L"[LightSwitch] Apps theme set to LIGHT\n" : L"[LightSwitch] Apps theme set to DARK\n");
    }
}
void SetSystemTheme(bool mode)
{
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
    {
        DWORD value = mode ? 1 : 0;
        RegSetValueExW(hKey, L"SystemUsesLightTheme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);
        if (mode) { ResetColorPrevalence(); OutputDebugStringW(L"[LightSwitch] Reset ColorPrevalence when switching to light mode.\n"); }
        else { SendThemeChangeBroadcast(); }
        OutputDebugStringW(mode ? L"[LightSwitch] System theme set to LIGHT\n" : L"[LightSwitch] System theme set to DARK\n");
    }
}
bool GetCurrentSystemTheme()
{
    HKEY hKey; DWORD value = 1, size = sizeof(value);
    if (RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    { RegQueryValueExW(hKey, L"SystemUsesLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size); RegCloseKey(hKey); }
    return value == 1;
}
bool GetCurrentAppsTheme()
{
    HKEY hKey; DWORD value = 1, size = sizeof(value);
    if (RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    { RegQueryValueExW(hKey, L"AppsUseLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size); RegCloseKey(hKey); }
    return value == 1;
}
bool IsNightLightEnabled()
{
    HKEY hKey; const wchar_t* path = NIGHT_LIGHT_REGISTRY_PATH;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, path, 0, KEY_READ, &hKey) != ERROR_SUCCESS) return false;
    DWORD size = 0;
    if (RegGetValueW(hKey, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, nullptr, &size) != ERROR_SUCCESS || size < 25)
    { RegCloseKey(hKey); return false; }
    std::vector<BYTE> data(size);
    if (RegGetValueW(hKey, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, data.data(), &size) != ERROR_SUCCESS)
    { RegCloseKey(hKey); return false; }
    RegCloseKey(hKey);
    return data[23] == 0x10 && data[24] == 0x00;
}
