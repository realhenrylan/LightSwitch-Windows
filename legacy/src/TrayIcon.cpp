#include "pch.h"
#include "TrayIcon.h"

// 用 32bpp DIBSection 绘制带 Alpha 通道的图标，兼容 Windows 10/11
static HICON CreateColorBlockIcon(bool light) {
    constexpr int S = 32;
    HDC sdc = GetDC(nullptr);
    if (!sdc) return nullptr;

    // 创建 32bpp 位图（含 Alpha 通道）
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = S;
    bmi.bmiHeader.biHeight = -S;  // 负值 = 从上到下（非翻转）
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP hBitmap = CreateDIBSection(sdc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!hBitmap) { ReleaseDC(nullptr, sdc); return nullptr; }

    // 直接操作像素：BGRA 格式
    auto* p = static_cast<unsigned char*>(bits);
    for (int y = 0; y < S; ++y) {
        for (int x = 0; x < S; ++x) {
            int idx = (y * S + x) * 4;
            int dx = x - S/2, dy = y - S/2;
            int dist2 = dx*dx + dy*dy;
            int r2 = (S/2 - 1) * (S/2 - 1);

            if (dist2 >= r2) {
                // 圆形外部：全透明
                p[idx+0] = 0;   // B
                p[idx+1] = 0;   // G
                p[idx+2] = 0;   // R
                p[idx+3] = 0;   // A
            } else {
                uint8_t a = 255;
                // 抗锯齿边缘（羽化 1 像素）
                if (dist2 >= (S/2 - 3) * (S/2 - 3)) {
                    a = (uint8_t)(255.0f * ((S/2 - 1) - std::sqrt((float)dist2)));
                }
                if (light) {
                    // 亮色：太阳暖黄 + 内圈高光
                    int inner = (S/2 - 6) * (S/2 - 6);
                    if (dist2 < inner) {
                        p[idx+0] = 30;  p[idx+1] = 170; p[idx+2] = 255;  // 亮黄色
                    } else {
                        p[idx+0] = 50;  p[idx+1] = 200; p[idx+2] = 255;
                    }
                } else {
                    // 暗色：深蓝 + 内圈亮蓝
                    int inner = (S/2 - 6) * (S/2 - 6);
                    if (dist2 < inner) {
                        p[idx+0] = 150; p[idx+1] = 100; p[idx+2] = 70;   // 亮蓝
                    } else {
                        p[idx+0] = 120; p[idx+1] = 50;  p[idx+2] = 50;   // 深蓝
                    }
                }
                p[idx+3] = a;
            }
        }
    }

    // 从位图创建图标
    HDC mdc = CreateCompatibleDC(sdc);
    if (!mdc) { DeleteObject(hBitmap); ReleaseDC(nullptr, sdc); return nullptr; }

    // 遮罩：全白（透明度由 DIBSection 的 Alpha 通道控制）
    HBITMAP hMsk = CreateBitmap(S, S, 1, 1, nullptr);
    if (!hMsk) { DeleteDC(mdc); DeleteObject(hBitmap); ReleaseDC(nullptr, sdc); return nullptr; }
    HGDIOBJ o = SelectObject(mdc, hMsk);
    RECT rc = {0, 0, S, S};
    FillRect(mdc, &rc, (HBRUSH)GetStockObject(WHITE_BRUSH));
    SelectObject(mdc, o); DeleteDC(mdc);

    ICONINFO ii = {};
    ii.fIcon = TRUE;
    ii.hbmMask = hMsk;
    ii.hbmColor = hBitmap;
    HICON hi = CreateIconIndirect(&ii);

    DeleteObject(hBitmap);
    DeleteObject(hMsk);
    ReleaseDC(nullptr, sdc);
    return hi;
}
TrayIcon::TrayIcon() {}
TrayIcon::~TrayIcon() { Destroy(); }
bool TrayIcon::Create(HWND h,HINSTANCE hi,UINT id,UINT cb,LPCWSTR tip,bool light) {
    if(m_created) return true;
    m_hwnd=h; m_hInst=hi; m_uID=id; m_uCallbackMsg=cb;
    m_hIconLight=CreateColorBlockIcon(true); m_hIconDark=CreateColorBlockIcon(false);
    m_iconNeedsDestroy=true;
    if(!m_hIconLight||!m_hIconDark) return false;
    ZeroMemory(&m_nid,sizeof(m_nid)); m_nid.cbSize=sizeof(m_nid);
    m_nid.hWnd=h; m_nid.uID=id; m_nid.uFlags=NIF_ICON|NIF_MESSAGE|NIF_TIP;
    m_nid.uCallbackMessage=cb; m_nid.hIcon=light?m_hIconLight:m_hIconDark;
    wcsncpy_s(m_nid.szTip,tip,_TRUNCATE);
    if(!Shell_NotifyIconW(NIM_ADD,&m_nid)) return false;
    // 尝试设置 V4 版本，失败不影响基本功能
    m_nid.uVersion=NOTIFYICON_VERSION_4;
    Shell_NotifyIconW(NIM_SETVERSION,&m_nid);
    m_created=true; return true;
}
void TrayIcon::Destroy() {
    if(m_created) { Shell_NotifyIconW(NIM_DELETE,&m_nid); m_created=false; }
    if(m_hIconLight&&m_iconNeedsDestroy){DestroyIcon(m_hIconLight);m_hIconLight=nullptr;}
    if(m_hIconDark&&m_iconNeedsDestroy){DestroyIcon(m_hIconDark);m_hIconDark=nullptr;}
}
void TrayIcon::SetIcon(bool light) { if(!m_created) return; m_nid.hIcon=light?m_hIconLight:m_hIconDark; Shell_NotifyIconW(NIM_MODIFY,&m_nid); }
void TrayIcon::SetTooltip(LPCWSTR t) { if(!m_created) return; wcsncpy_s(m_nid.szTip,t,_TRUNCATE); Shell_NotifyIconW(NIM_MODIFY,&m_nid); }
void TrayIcon::ShowContextMenu(HWND h) {
    POINT pt; GetCursorPos(&pt);
    HMENU hm=CreatePopupMenu(); if(!hm) return;
    AppendMenuW(hm,MF_STRING,IDM_TRAY_TOGGLE,L"切换主题");
    AppendMenuW(hm,MF_STRING,IDM_TRAY_TOGGLE_SCHEDULE,L"启用/禁用自动调度");
    AppendMenuW(hm,MF_SEPARATOR,0,0);
    AppendMenuW(hm,MF_STRING,IDM_TRAY_OPEN_SETTINGS,L"打开设置");
    AppendMenuW(hm,MF_STRING,IDM_TRAY_EXIT,L"退出");
    SetForegroundWindow(h); TrackPopupMenu(hm,TPM_RIGHTALIGN|TPM_BOTTOMALIGN,pt.x,pt.y,0,h,0); DestroyMenu(hm);
}
