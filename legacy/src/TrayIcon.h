#pragma once
#include "pch.h"
#include "resource.h"
class TrayIcon {
public:
    TrayIcon(); ~TrayIcon();
    bool Create(HWND h, HINSTANCE hi, UINT id, UINT cb, LPCWSTR tip, bool light);
    void Destroy();
    void SetIcon(bool light); void SetTooltip(LPCWSTR tip);
    void ShowContextMenu(HWND h);
    bool IsCreated() const { return m_created; }
private:
    bool m_created = false, m_iconNeedsDestroy = false;
    HWND m_hwnd = nullptr; HINSTANCE m_hInst = nullptr;
    UINT m_uID = 0, m_uCallbackMsg = 0;
    HICON m_hIconLight = nullptr, m_hIconDark = nullptr;
    NOTIFYICONDATAW m_nid{};
};
