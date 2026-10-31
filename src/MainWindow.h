#pragma once
#include "pch.h"
#include "TrayIcon.h"
#include "LightSwitchStateManager.h"
#include "UiTheme.h"
#include <memory>

// ---------------------------------------------------------------------------
// MainWindow - 主窗口（纯 Win32 + GDI 自绘）
//
// 功能与旧版完全一致：调度模式、系统/应用主题开关、时间与坐标设置、
// 手动切换按钮、状态显示、托盘交互、热键、配置热重载。
// 界面改为自绘的设置页风格：DPI 自适应、跟随应用主题、深色标题栏。
// ---------------------------------------------------------------------------
class MainWindow {
public:
    MainWindow();
    ~MainWindow();

    bool Initialize(HINSTANCE hInstance, int nCmdShow);
    void Shutdown();
    void Show();
    void Hide();
    bool IsVisible() const;
    void RefreshUI();

    HWND GetHwnd() const { return m_hwnd; }
    TrayIcon& GetTrayIcon() { return m_trayIcon; }
    LightSwitchStateManager& GetStateManager() { return *m_stateManager; }

    static LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);
    LRESULT HandleMessage(HWND, UINT, WPARAM, LPARAM);

private:
    enum HitId {
        HIT_NONE = 0,
        HIT_MODE_OFF = 1,
        HIT_MODE_FIXED,
        HIT_MODE_SUN,
        HIT_MODE_NIGHT,
        HIT_SYSTEM = 16,
        HIT_APPS,
        HIT_LIGHT = 24,
        HIT_DARK
    };

    struct Layout {
        RECT headerTitle;
        RECT headerSubtitle;
        RECT pill;
        RECT card;
        RECT captionMode;
        RECT segTrack;
        RECT seg[4];
        RECT rowSystem;
        RECT rowApps;
        RECT rowLight;
        RECT rowDark;
        RECT rowLat;
        RECT rowLon;
        RECT fieldLight;
        RECT fieldDark;
        RECT fieldLat;
        RECT fieldLon;
        RECT btnLight;
        RECT btnDark;
        RECT statusTitle;
        RECT statusSub;
        RECT hint;
        RECT divider[3];
    };

    // 生命周期 / 资源
    void OnCreate();
    void CreateEditControls();
    void RecreateFonts();
    void RecreateBrushes();
    void BuildLayout();
    void ApplyLayout();
    void ApplyClientSize();
    void UpdateTheme(bool force = false);
    void UpdateStatusText();

    // 绘制
    void Paint(HDC dc, const RECT& client);
    void PaintHeader(HDC dc);
    void PaintSegmented(HDC dc);
    void PaintSwitchRow(HDC dc, const RECT& row, const wchar_t* label, bool on, int hitId);
    void PaintFieldRow(HDC dc, const RECT& row, const RECT& field, HWND edit, const wchar_t* label);
    void PaintActionButton(HDC dc, const RECT& rc, bool light, int hitId);
    void PaintStatus(HDC dc);

    // 交互
    int HitTest(POINT pt) const;
    void Activate(int hitId);
    void SetMode(int index);
    void ToggleFlag(bool systemFlag);
    void ToggleTheme(bool light);

    // 输入
    void CommitTimes();
    void CommitCoordinates();
    void CommitFocusedEdit();
    void RevertFocusedEdit();
    void FocusNextEdit(HWND current, bool backward);
    void PersistSettings();
    void FlashInvalid(HWND edit);

    static LRESULT CALLBACK EditSubclassProc(HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR);

    int S(int value) const { return ui::Scale(value, m_dpi); }

    HINSTANCE m_hInst = nullptr;
    HWND m_hwnd = nullptr;
    TrayIcon m_trayIcon;
    std::unique_ptr<LightSwitchStateManager> m_stateManager;

    HWND m_hLightTime = nullptr;
    HWND m_hDarkTime = nullptr;
    HWND m_hLatitude = nullptr;
    HWND m_hLongitude = nullptr;

    Layout m_layout;
    ui::Colors m_colors;
    UINT m_dpi = 96;
    bool m_dark = false;

    HFONT m_fontTitle = nullptr;
    HFONT m_fontSubtitle = nullptr;
    HFONT m_fontLabel = nullptr;
    HFONT m_fontValue = nullptr;
    HFONT m_fontButton = nullptr;

    HBRUSH m_brField = nullptr;
    HBRUSH m_brFieldDisabled = nullptr;

    HWND m_invalidEdit = nullptr;
    int m_hot = HIT_NONE;
    int m_pressed = HIT_NONE;
    bool m_trackingMouse = false;
    bool m_shuttingDown = false;

    int m_modeIndex = 1;
    bool m_systemOn = true;
    bool m_appsOn = true;
    bool m_pillActive = true;
    std::wstring m_pillText;
    std::wstring m_statusTitle;
    std::wstring m_statusSub;
};

extern MainWindow* g_pMainWindow;
