#include "pch.h"
#include "MainWindow.h"
#include "ConfigManager.h"
#include "ThemeHelper.h"
#include "LightSwitchUtils.h"
#include "resource.h"
MainWindow* g_pMainWindow = nullptr;
int MainWindow::MinutesToHM(int m, wchar_t* buf, size_t sz) { return swprintf_s(buf, sz, L"%02d:%02d", (m/60)%24, m%60); }
int MainWindow::HMToMinutes(const wchar_t* t) { int h=0,m=0; return swscanf_s(t, L"%d:%d",&h,&m)==2 ? (h*60+m)%1440 : 0; }
MainWindow::MainWindow() { g_pMainWindow = this; }
MainWindow::~MainWindow() { g_pMainWindow = nullptr; }
bool MainWindow::Initialize(HINSTANCE h, int nCmd) {
    m_hInst = h;
    WNDCLASSEXW wc={sizeof(wc)}; wc.lpfnWndProc=WndProc; wc.hInstance=h;
    wc.hIcon = LoadIconW(h, MAKEINTRESOURCEW(IDI_LIGHTSWITCH));
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)(COLOR_BTNFACE+1);
    wc.lpszClassName = L"LightSwitchMainWindow";
    wc.hIconSm = LoadIconW(h, MAKEINTRESOURCEW(IDI_LIGHTSWITCH));
    if (!RegisterClassExW(&wc)) return false;
    m_hwnd = CreateWindowExW(0, wc.lpszClassName, L"LightSwitch - 自动深浅色切换",
        WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_MINIMIZEBOX, CW_USEDEFAULT, CW_USEDEFAULT, 360, 400, nullptr, nullptr, h, this);
    if (!m_hwnd) return false;
    m_trayIcon.Create(m_hwnd, h, 1, WM_TRAYICON, L"LightSwitch", true);
    m_stateManager = std::make_unique<LightSwitchStateManager>();
    ConfigManager::Instance().Load();
    m_stateManager->SyncInitialThemeState();
    RefreshUI();
    if (nCmd != SW_HIDE) ShowWindow(m_hwnd, SW_HIDE);
    return true;
}
void MainWindow::Shutdown() { m_trayIcon.Destroy(); if (m_hwnd) { DestroyWindow(m_hwnd); m_hwnd=nullptr; } }
void MainWindow::Show() { if (m_hwnd) { ShowWindow(m_hwnd, SW_SHOW); SetForegroundWindow(m_hwnd); } }
void MainWindow::Hide() { if (m_hwnd) ShowWindow(m_hwnd, SW_HIDE); }
bool MainWindow::IsVisible() const { return m_hwnd && IsWindowVisible(m_hwnd); }
void MainWindow::CreateControls() {
    HFONT f = (HFONT)GetStockObject(DEFAULT_GUI_FONT);
    CreateWindowW(L"STATIC",L"调度模式:",WS_VISIBLE|WS_CHILD|SS_LEFT,20,20,80,20,m_hwnd,(HMENU)IDS_LABEL_SCHEDULE,m_hInst,0);
    m_hScheduleMode = CreateWindowW(L"COMBOBOX",L"",WS_VISIBLE|WS_CHILD|CBS_DROPDOWNLIST|WS_TABSTOP,110,18,200,120,m_hwnd,(HMENU)IDC_SCHEDULE_MODE,m_hInst,0);
    SendMessageW(m_hScheduleMode,WM_SETFONT,(WPARAM)f,0);
    SendMessageW(m_hScheduleMode,CB_ADDSTRING,0,(LPARAM)L"关闭");
    SendMessageW(m_hScheduleMode,CB_ADDSTRING,0,(LPARAM)L"固定时间段");
    SendMessageW(m_hScheduleMode,CB_ADDSTRING,0,(LPARAM)L"日出日落");
    SendMessageW(m_hScheduleMode,CB_ADDSTRING,0,(LPARAM)L"跟随Night Light");
    m_hChangeSystem = CreateWindowW(L"BUTTON",L"修改系统主题",WS_VISIBLE|WS_CHILD|BS_AUTOCHECKBOX|WS_TABSTOP,20,55,300,20,m_hwnd,(HMENU)IDC_CHANGE_SYSTEM,m_hInst,0);
    SendMessageW(m_hChangeSystem,WM_SETFONT,(WPARAM)f,0);
    m_hChangeApps = CreateWindowW(L"BUTTON",L"修改应用主题",WS_VISIBLE|WS_CHILD|BS_AUTOCHECKBOX|WS_TABSTOP,20,80,300,20,m_hwnd,(HMENU)IDC_CHANGE_APPS,m_hInst,0);
    SendMessageW(m_hChangeApps,WM_SETFONT,(WPARAM)f,0);
    CreateWindowW(L"STATIC",L"亮色时间:",WS_VISIBLE|WS_CHILD|SS_LEFT,20,115,70,20,m_hwnd,(HMENU)IDS_LABEL_LIGHT_TIME,m_hInst,0);
    m_hLightTime = CreateWindowW(L"EDIT",L"",WS_VISIBLE|WS_CHILD|WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,90,112,80,22,m_hwnd,(HMENU)IDC_LIGHT_TIME,m_hInst,0);
    SendMessageW(m_hLightTime,WM_SETFONT,(WPARAM)f,0);
    CreateWindowW(L"STATIC",L"暗色时间:",WS_VISIBLE|WS_CHILD|SS_LEFT,185,115,70,20,m_hwnd,(HMENU)IDS_LABEL_DARK_TIME,m_hInst,0);
    m_hDarkTime = CreateWindowW(L"EDIT",L"",WS_VISIBLE|WS_CHILD|WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,255,112,80,22,m_hwnd,(HMENU)IDC_DARK_TIME,m_hInst,0);
    SendMessageW(m_hDarkTime,WM_SETFONT,(WPARAM)f,0);
    CreateWindowW(L"STATIC",L"纬度:",WS_VISIBLE|WS_CHILD|SS_LEFT,20,148,50,20,m_hwnd,(HMENU)IDS_LABEL_LAT,m_hInst,0);
    m_hLatitude = CreateWindowW(L"EDIT",L"",WS_VISIBLE|WS_CHILD|WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,70,145,100,22,m_hwnd,(HMENU)IDC_LATITUDE,m_hInst,0);
    SendMessageW(m_hLatitude,WM_SETFONT,(WPARAM)f,0);
    CreateWindowW(L"STATIC",L"经度:",WS_VISIBLE|WS_CHILD|SS_LEFT,185,148,50,20,m_hwnd,(HMENU)IDS_LABEL_LON,m_hInst,0);
    m_hLongitude = CreateWindowW(L"EDIT",L"",WS_VISIBLE|WS_CHILD|WS_BORDER|ES_AUTOHSCROLL|WS_TABSTOP,230,145,100,22,m_hwnd,(HMENU)IDC_LONGITUDE,m_hInst,0);
    SendMessageW(m_hLongitude,WM_SETFONT,(WPARAM)f,0);
    // 子类化经纬度编辑框：拦截回车键，按回车立即触发坐标变更
    SetWindowSubclass(m_hLatitude, CoordEditProc, 0, (DWORD_PTR)this);
    SetWindowSubclass(m_hLongitude, CoordEditProc, 0, (DWORD_PTR)this);
    CreateWindowW(L"BUTTON",L"☀ 手动切换为亮色",WS_VISIBLE|WS_CHILD|BS_PUSHBUTTON|WS_TABSTOP,20,185,150,28,m_hwnd,(HMENU)IDC_BTN_TOGGLE_LIGHT,m_hInst,0);
    SendMessageW(m_hLightTime,WM_SETFONT,(WPARAM)f,0);
    CreateWindowW(L"BUTTON",L"☾ 手动切换为暗色",WS_VISIBLE|WS_CHILD|BS_PUSHBUTTON|WS_TABSTOP,185,185,150,28,m_hwnd,(HMENU)IDC_BTN_TOGGLE_DARK,m_hInst,0);
    SendMessageW(m_hDarkTime,WM_SETFONT,(WPARAM)f,0);
    m_hStatus = CreateWindowW(L"STATIC",L"当前状态: 初始化中...",WS_VISIBLE|WS_CHILD|SS_LEFT,20,230,300,40,m_hwnd,(HMENU)IDC_STATUS_TEXT,m_hInst,0);
    SendMessageW(m_hStatus,WM_SETFONT,(WPARAM)f,0);
}
void MainWindow::RefreshUI() {
    const auto& c = ConfigManager::Instance().Get(); wchar_t buf[32];
    int idx=0; switch(c.scheduleMode) { case ScheduleMode::FixedHours: idx=1; break; case ScheduleMode::SunsetToSunrise: idx=2; break; case ScheduleMode::FollowNightLight: idx=3; break; default: idx=0; }
    SendMessageW(m_hScheduleMode,CB_SETCURSEL,idx,0);
    SendMessageW(m_hChangeSystem,BM_SETCHECK,c.changeSystem?BST_CHECKED:BST_UNCHECKED,0);
    SendMessageW(m_hChangeApps,BM_SETCHECK,c.changeApps?BST_CHECKED:BST_UNCHECKED,0);
    // 仅在编辑框无焦点时更新文本，避免覆盖用户正在输入的内容
    if (GetFocus() != m_hLightTime) { MinutesToHM(c.lightTime,buf,_countof(buf)); SetWindowTextW(m_hLightTime,buf); }
    if (GetFocus() != m_hDarkTime) { MinutesToHM(c.darkTime,buf,_countof(buf)); SetWindowTextW(m_hDarkTime,buf); }
    if (GetFocus() != m_hLatitude) SetWindowTextW(m_hLatitude,c.latitude.c_str());
    if (GetFocus() != m_hLongitude) SetWindowTextW(m_hLongitude,c.longitude.c_str());
    bool sl = GetCurrentSystemTheme(), al = GetCurrentAppsTheme();
    std::wstring s = L"当前: "; s += sl ? L"☀亮" : L"☾暗"; s += L" 系统="; s += sl ? L"亮":L"暗"; s += L" 应用="; s += al ? L"亮":L"暗";
    s += L" 模式="; s += ToString(c.scheduleMode);
    SetWindowTextW(m_hStatus,s.c_str());
    m_trayIcon.SetIcon(sl); m_trayIcon.SetTooltip(s.c_str());
    bool ce = (c.scheduleMode == ScheduleMode::SunsetToSunrise);
    EnableWindow(m_hLatitude,ce); EnableWindow(m_hLongitude,ce);
}
void MainWindow::UpdateStatusBar(const std::wstring& t) { SetWindowTextW(m_hStatus,t.c_str()); }
void MainWindow::OnScheduleModeChanged() {
    ScheduleMode modes[] = { ScheduleMode::Off, ScheduleMode::FixedHours, ScheduleMode::SunsetToSunrise, ScheduleMode::FollowNightLight };
    int i = (int)SendMessageW(m_hScheduleMode,CB_GETCURSEL,0,0);
    if (i>=0&&i<4) { auto& c = ConfigManager::Instance().Get(); c.scheduleMode = modes[i]; ConfigManager::Instance().Save(); m_stateManager->OnSettingsChanged(); RefreshUI(); }
}
void MainWindow::OnCheckboxChanged() {
    auto& c = ConfigManager::Instance().Get();
    c.changeSystem = SendMessageW(m_hChangeSystem,BM_GETCHECK,0,0) == BST_CHECKED;
    c.changeApps = SendMessageW(m_hChangeApps,BM_GETCHECK,0,0) == BST_CHECKED;
    ConfigManager::Instance().Save(); m_stateManager->OnSettingsChanged(); RefreshUI();
}
void MainWindow::OnTimeChanged() {
    wchar_t b[16]; auto& c=ConfigManager::Instance().Get();
    GetWindowTextW(m_hLightTime,b,_countof(b)); c.lightTime = HMToMinutes(b);
    GetWindowTextW(m_hDarkTime,b,_countof(b)); c.darkTime = HMToMinutes(b);
    ConfigManager::Instance().Save(); m_stateManager->OnSettingsChanged(); RefreshUI();
}
void MainWindow::OnCoordinatesChanged() {
    wchar_t b[32]; auto& c=ConfigManager::Instance().Get();
    GetWindowTextW(m_hLatitude,b,_countof(b)); c.latitude=b;
    GetWindowTextW(m_hLongitude,b,_countof(b)); c.longitude=b;
    ConfigManager::Instance().Save(); m_stateManager->OnSettingsChanged(); RefreshUI();
}
void MainWindow::OnToggleLight() { SetSystemTheme(true); SetAppsTheme(true); m_stateManager->OnManualOverride(); RefreshUI(); }
void MainWindow::OnToggleDark() { SetSystemTheme(false); SetAppsTheme(false); m_stateManager->OnManualOverride(); RefreshUI(); }
LRESULT CALLBACK MainWindow::WndProc(HWND h,UINT m,WPARAM w,LPARAM l) {
    MainWindow* p=nullptr;
    if (m==WM_CREATE) { p=(MainWindow*)((CREATESTRUCTW*)l)->lpCreateParams; SetWindowLongPtrW(h,GWLP_USERDATA,(LONG_PTR)p); p->m_hwnd=h; p->CreateControls(); return 0; }
    else p=(MainWindow*)GetWindowLongPtrW(h,GWLP_USERDATA);
    return p ? p->HandleMessage(h,m,w,l) : DefWindowProcW(h,m,w,l);
}
LRESULT MainWindow::HandleMessage(HWND h,UINT msg,WPARAM w,LPARAM l) {
    switch(msg) {
        case WM_TRAYICON: {
            // V4: LOWORD(lParam)=鼠标消息, HIWORD(lParam)=图标ID
            // 旧版: lParam=鼠标消息, wParam=图标ID
            // LOWORD 在两种版本下均正确提取鼠标消息，无需为 NIM_SETVERSION 失败额外分支
            UINT trayMsg = LOWORD(l);
            if (trayMsg == WM_LBUTTONUP || trayMsg == WM_LBUTTONDBLCLK)
                IsVisible() ? Hide() : Show();
            else if (trayMsg == WM_RBUTTONUP || trayMsg == WM_CONTEXTMENU)
                m_trayIcon.ShowContextMenu(h);
            return 0;
        }
        case WM_COMMAND: {
            WORD id=LOWORD(w), code=HIWORD(w);
            switch(id) {
                case IDC_SCHEDULE_MODE: if(code==CBN_SELCHANGE) OnScheduleModeChanged(); break;
                case IDC_CHANGE_SYSTEM: case IDC_CHANGE_APPS: if(code==BN_CLICKED) OnCheckboxChanged(); break;
                case IDC_LIGHT_TIME: case IDC_DARK_TIME: if(code==EN_KILLFOCUS) OnTimeChanged(); break;
                case IDC_LATITUDE: case IDC_LONGITUDE: if(code==EN_KILLFOCUS) OnCoordinatesChanged(); break;
                case IDC_BTN_TOGGLE_LIGHT: if(code==BN_CLICKED) OnToggleLight(); break;
                case IDC_BTN_TOGGLE_DARK: if(code==BN_CLICKED) OnToggleDark(); break;
                case IDM_TRAY_TOGGLE: { bool cur=GetCurrentSystemTheme(); SetSystemTheme(!cur); SetAppsTheme(!cur); m_stateManager->OnManualOverride(); RefreshUI(); break; }
                case IDM_TRAY_TOGGLE_SCHEDULE: { auto& c=ConfigManager::Instance().Get(); c.scheduleMode=(c.scheduleMode==ScheduleMode::Off)?ScheduleMode::FixedHours:ScheduleMode::Off; ConfigManager::Instance().Save(); m_stateManager->OnSettingsChanged(); RefreshUI(); break; }
                case IDM_TRAY_OPEN_SETTINGS: Show(); break;
                case IDM_TRAY_EXIT: PostQuitMessage(0); break;
            } return 0;
        }
        case WM_TIMER: m_stateManager->OnTick(); RefreshUI(); return 0;
        case WM_FILE_CHANGED: ConfigManager::Instance().Load(); m_stateManager->OnSettingsChanged(); RefreshUI(); return 0;
        case WM_NIGHTLIGHT_CHANGED: m_stateManager->OnNightLightChange(); RefreshUI(); return 0;
        case WM_HOTKEY: if(w==IDH_TOGGLE_THEME) { bool cur=GetCurrentSystemTheme(); SetSystemTheme(!cur); SetAppsTheme(!cur); m_stateManager->OnManualOverride(); RefreshUI(); } return 0;
        case WM_DESTROY: PostQuitMessage(0); return 0;
        case WM_CLOSE: Hide(); return 0;
    }
    return DefWindowProcW(h,msg,w,l);
}
// 经纬度编辑框子类化回调：按回车立即触发坐标变更
LRESULT CALLBACK MainWindow::CoordEditProc(HWND h, UINT msg, WPARAM w, LPARAM l, UINT_PTR uid, DWORD_PTR refData) {
    if (msg == WM_KEYDOWN && w == VK_RETURN) {
        MainWindow* p = (MainWindow*)refData;
        if (p) p->OnCoordinatesChanged();
        return 0; // 已处理，不传递给原编辑框
    }
    return DefSubclassProc(h, msg, w, l);
}
