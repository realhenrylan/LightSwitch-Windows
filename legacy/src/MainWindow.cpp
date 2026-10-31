#include "pch.h"
#include "MainWindow.h"
#include "ConfigManager.h"
#include "ThemeHelper.h"
#include "LightSwitchUtils.h"
#include "resource.h"

#include <cstdio>
#include <cstdlib>
#include <cwchar>
#include <cwctype>

MainWindow* g_pMainWindow = nullptr;

// ---------------------------------------------------------------------------
// 文件内工具
// ---------------------------------------------------------------------------
namespace {

constexpr int kLogicalWidth = 420;
constexpr int kLogicalHeight = 540;

const wchar_t* const kSegmentLabels[4] = {
    L"关闭", L"固定时间段", L"日出日落", L"跟随夜间模式"
};

const ScheduleMode kSegmentModes[4] = {
    ScheduleMode::Off,
    ScheduleMode::FixedHours,
    ScheduleMode::SunsetToSunrise,
    ScheduleMode::FollowNightLight
};

std::wstring TrimText(const std::wstring& text) {
    size_t begin = 0;
    size_t end = text.size();
    while (begin < end && iswspace(text[begin]) != 0) ++begin;
    while (end > begin && iswspace(text[end - 1]) != 0) --end;
    return text.substr(begin, end - begin);
}

std::wstring GetEditText(HWND edit) {
    if (!edit) return std::wstring();
    wchar_t buffer[256];
    const int length = GetWindowTextW(edit, buffer, (int)_countof(buffer));
    if (length <= 0) return std::wstring();
    return std::wstring(buffer, (size_t)length);
}

std::wstring FormatClock(int minutes) {
    int value = minutes % 1440;
    if (value < 0) value += 1440;
    wchar_t buffer[16];
    swprintf_s(buffer, L"%02d:%02d", value / 60, value % 60);
    return std::wstring(buffer);
}

// 解析时间文本：支持 "8:30"、"08:30"、"830"、"8"，非法时返回 fallback
int ParseClock(const std::wstring& text, int fallback) {
    std::wstring normalized = TrimText(text);
    if (normalized.empty()) return fallback;
    for (wchar_t& ch : normalized) {
        if (ch == L'：') ch = L':';
    }
    int hour = 0;
    int minute = 0;
    const int fields = swscanf_s(normalized.c_str(), L"%d:%d", &hour, &minute);
    if (fields == 2) {
        // 完整的时:分
    } else if (fields == 1) {
        if (normalized.find(L':') == std::wstring::npos && normalized.size() >= 3) {
            minute = hour % 100;
            hour /= 100;
        } else {
            minute = 0;
        }
    } else {
        return fallback;
    }
    if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return fallback;
    return hour * 60 + minute;
}

// 解析坐标并归一化（去掉多余的 0），非法时返回 false
bool ParseCoordinate(const std::wstring& text, double minValue, double maxValue, std::wstring& normalized) {
    const std::wstring trimmed = TrimText(text);
    if (trimmed.empty()) return false;
    wchar_t* end = nullptr;
    const double value = wcstod(trimmed.c_str(), &end);
    if (end == trimmed.c_str()) return false;
    while (end != nullptr && *end == L' ') ++end;
    if (end != nullptr && *end != L'\0') return false;
    if (!(value >= minValue && value <= maxValue)) return false;
    wchar_t buffer[48];
    swprintf_s(buffer, L"%.4f", value);
    std::wstring result(buffer);
    const size_t dot = result.find(L'.');
    if (dot != std::wstring::npos) {
        const size_t last = result.find_last_not_of(L'0');
        if (last != std::wstring::npos && last >= dot) result.erase(last + 1);
        if (!result.empty() && result.back() == L'.') result.pop_back();
    }
    if (result.empty() || result == L"-") return false;
    normalized = result;
    return true;
}

void PaintSunIcon(HDC dc, int cx, int cy, COLORREF color, int unit) {
    ui::FillCircle(dc, cx, cy, unit, color);
    const double kPi = 3.14159265358979323846;
    const int penWidth = unit > 4 ? 2 : 1;
    for (int i = 0; i < 8; ++i) {
        const double angle = i * kPi / 4.0;
        const int x1 = cx + (int)(cos(angle) * unit * 1.45);
        const int y1 = cy + (int)(sin(angle) * unit * 1.45);
        const int x2 = cx + (int)(cos(angle) * unit * 2.20);
        const int y2 = cy + (int)(sin(angle) * unit * 2.20);
        ui::DrawLine(dc, x1, y1, x2, y2, color, penWidth);
    }
}

void PaintMoonIcon(HDC dc, int cx, int cy, COLORREF color, COLORREF background, int unit) {
    ui::FillCircle(dc, cx, cy, unit, color);
    const int offsetX = (int)(unit * 0.80);
    const int offsetY = -(int)(unit * 0.45);
    ui::FillCircle(dc, cx + offsetX, cy + offsetY, unit, background);
}

} // namespace

// ---------------------------------------------------------------------------
// 构造 / 生命周期
// ---------------------------------------------------------------------------
MainWindow::MainWindow() { g_pMainWindow = this; }

MainWindow::~MainWindow() {
    if (m_fontTitle) DeleteObject(m_fontTitle);
    if (m_fontSubtitle) DeleteObject(m_fontSubtitle);
    if (m_fontLabel) DeleteObject(m_fontLabel);
    if (m_fontValue) DeleteObject(m_fontValue);
    if (m_fontButton) DeleteObject(m_fontButton);
    if (m_brField) DeleteObject(m_brField);
    if (m_brFieldDisabled) DeleteObject(m_brFieldDisabled);
    g_pMainWindow = nullptr;
}

bool MainWindow::Initialize(HINSTANCE hInstance, int nCmdShow) {
    (void)nCmdShow;
    m_hInst = hInstance;

    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hIcon = LoadIconW(hInstance, MAKEINTRESOURCEW(IDI_LIGHTSWITCH));
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = nullptr; // 全部自绘，避免擦除闪烁
    wc.lpszClassName = L"LightSwitchMainWindow";
    wc.hIconSm = LoadIconW(hInstance, MAKEINTRESOURCEW(IDI_LIGHTSWITCH));
    if (!RegisterClassExW(&wc) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS) {
        return false;
    }

    const DWORD style = (WS_OVERLAPPEDWINDOW & ~WS_THICKFRAME & ~WS_MAXIMIZEBOX) | WS_CLIPCHILDREN;
    const DWORD exStyle = WS_EX_APPWINDOW;
    const UINT systemDpi = GetDpiForSystem();
    const int initialWidth = ui::Scale(kLogicalWidth, systemDpi);
    const int initialHeight = ui::Scale(kLogicalHeight, systemDpi);
    m_hwnd = CreateWindowExW(exStyle, wc.lpszClassName, L"LightSwitch", style,
                             CW_USEDEFAULT, CW_USEDEFAULT, initialWidth, initialHeight,
                             nullptr, nullptr, hInstance, this);
    if (!m_hwnd) return false;

    ApplyClientSize();

    m_trayIcon.Create(m_hwnd, hInstance, 1, WM_TRAYICON, L"LightSwitch", true);
    m_stateManager = std::make_unique<LightSwitchStateManager>();
    ConfigManager::Instance().Load();
    m_stateManager->SyncInitialThemeState();
    RefreshUI();

    // 与旧版行为保持一致：程序启动后驻留托盘，不自动弹出主窗口
    ShowWindow(m_hwnd, SW_HIDE);
    return true;
}

void MainWindow::Shutdown() {
    m_shuttingDown = true;
    m_trayIcon.Destroy();
    if (m_hwnd) {
        HWND hwnd = m_hwnd;
        m_hwnd = nullptr;
        DestroyWindow(hwnd);
    }
}

void MainWindow::Show() {
    if (!m_hwnd) return;
    ui::SetDarkTitleBar(m_hwnd, m_dark);
    ShowWindow(m_hwnd, SW_SHOW);
    SetForegroundWindow(m_hwnd);
}

void MainWindow::Hide() {
    if (m_hwnd) ShowWindow(m_hwnd, SW_HIDE);
}

bool MainWindow::IsVisible() const {
    return m_hwnd && IsWindowVisible(m_hwnd);
}

// ---------------------------------------------------------------------------
// 创建期
// ---------------------------------------------------------------------------
void MainWindow::OnCreate() {
    m_dpi = GetDpiForWindow(m_hwnd);
    if (m_dpi == 0) m_dpi = 96;
    m_dark = !GetCurrentAppsTheme();
    m_colors = m_dark ? ui::DarkColors() : ui::LightColors();
    RecreateFonts();
    RecreateBrushes();
    BuildLayout();
    CreateEditControls();
    ApplyLayout();
    ui::SetDarkTitleBar(m_hwnd, m_dark);
}

void MainWindow::CreateEditControls() {
    auto createEdit = [this](int id) -> HWND {
        HWND edit = CreateWindowExW(0, L"EDIT", L"",
                                    WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL | ES_RIGHT,
                                    0, 0, 10, 10, m_hwnd,
                                    (HMENU)(INT_PTR)id, m_hInst, nullptr);
        if (!edit) return nullptr;
        SendMessageW(edit, WM_SETFONT, (WPARAM)m_fontValue, FALSE);
        SendMessageW(edit, EM_SETLIMITTEXT, 16, 0);
        SendMessageW(edit, EM_SETMARGINS, EC_LEFTMARGIN | EC_RIGHTMARGIN, MAKELPARAM(0, 0));
        SetWindowSubclass(edit, EditSubclassProc, 1, (DWORD_PTR)this);
        return edit;
    };
    m_hLightTime = createEdit(IDC_LIGHT_TIME);
    m_hDarkTime = createEdit(IDC_DARK_TIME);
    m_hLatitude = createEdit(IDC_LATITUDE);
    m_hLongitude = createEdit(IDC_LONGITUDE);
}

void MainWindow::RecreateFonts() {
    if (m_fontTitle) { DeleteObject(m_fontTitle); m_fontTitle = nullptr; }
    if (m_fontSubtitle) { DeleteObject(m_fontSubtitle); m_fontSubtitle = nullptr; }
    if (m_fontLabel) { DeleteObject(m_fontLabel); m_fontLabel = nullptr; }
    if (m_fontValue) { DeleteObject(m_fontValue); m_fontValue = nullptr; }
    if (m_fontButton) { DeleteObject(m_fontButton); m_fontButton = nullptr; }

    m_fontTitle = ui::CreateFontPx(21, FW_SEMIBOLD, m_dpi);
    m_fontSubtitle = ui::CreateFontPx(12, FW_NORMAL, m_dpi);
    m_fontLabel = ui::CreateFontPx(13, FW_NORMAL, m_dpi);
    m_fontValue = ui::CreateFontPx(13, FW_NORMAL, m_dpi);
    m_fontButton = ui::CreateFontPx(13, FW_SEMIBOLD, m_dpi);

    HWND edits[4] = { m_hLightTime, m_hDarkTime, m_hLatitude, m_hLongitude };
    for (HWND edit : edits) {
        if (edit) SendMessageW(edit, WM_SETFONT, (WPARAM)m_fontValue, TRUE);
    }
}

void MainWindow::RecreateBrushes() {
    if (m_brField) { DeleteObject(m_brField); m_brField = nullptr; }
    if (m_brFieldDisabled) { DeleteObject(m_brFieldDisabled); m_brFieldDisabled = nullptr; }
    m_brField = CreateSolidBrush(m_colors.field);
    m_brFieldDisabled = CreateSolidBrush(m_colors.fieldDisabled);
}

void MainWindow::BuildLayout() {
    auto rc = [this](int left, int top, int right, int bottom) {
        RECT r;
        r.left = S(left);
        r.top = S(top);
        r.right = S(right);
        r.bottom = S(bottom);
        return r;
    };

    m_layout.headerTitle = rc(20, 12, 310, 38);
    m_layout.headerSubtitle = rc(20, 40, 310, 56);
    m_layout.pill = rc(312, 16, 400, 42);

    m_layout.card = rc(20, 70, 400, 418);

    m_layout.captionMode = rc(36, 84, 384, 100);
    m_layout.segTrack = rc(36, 102, 384, 134);
    const int segLeft = m_layout.segTrack.left;
    const int segRight = m_layout.segTrack.right;
    const int segTop = m_layout.segTrack.top;
    const int segBottom = m_layout.segTrack.bottom;
    const int segWidth = segRight - segLeft;
    for (int i = 0; i < 4; ++i) {
        m_layout.seg[i].left = segLeft + segWidth * i / 4;
        m_layout.seg[i].right = segLeft + segWidth * (i + 1) / 4;
        m_layout.seg[i].top = segTop;
        m_layout.seg[i].bottom = segBottom;
    }

    m_layout.rowSystem = rc(20, 146, 400, 190);
    m_layout.rowApps = rc(20, 190, 400, 234);
    m_layout.rowLight = rc(20, 234, 400, 278);
    m_layout.rowDark = rc(20, 278, 400, 322);
    m_layout.rowLat = rc(20, 322, 400, 366);
    m_layout.rowLon = rc(20, 366, 400, 410);

    m_layout.fieldLight = rc(280, 241, 384, 271);
    m_layout.fieldDark = rc(280, 285, 384, 315);
    m_layout.fieldLat = rc(264, 329, 384, 359);
    m_layout.fieldLon = rc(264, 373, 384, 403);

    m_layout.btnLight = rc(20, 430, 203, 468);
    m_layout.btnDark = rc(217, 430, 400, 468);

    m_layout.statusTitle = rc(20, 474, 400, 492);
    m_layout.statusSub = rc(20, 492, 400, 508);
    m_layout.hint = rc(20, 512, 400, 528);

    m_layout.divider[0] = rc(36, 146, 384, 147);
    m_layout.divider[1] = rc(36, 234, 384, 235);
    m_layout.divider[2] = rc(36, 322, 384, 323);
}

void MainWindow::ApplyLayout() {
    auto place = [this](HWND edit, const RECT& field) {
        if (!edit) return;
        const int left = field.left + S(8);
        const int top = field.top + S(4);
        const int width = (field.right - field.left) - S(16);
        const int height = (field.bottom - field.top) - S(8);
        SetWindowPos(edit, nullptr, left, top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    };
    place(m_hLightTime, m_layout.fieldLight);
    place(m_hDarkTime, m_layout.fieldDark);
    place(m_hLatitude, m_layout.fieldLat);
    place(m_hLongitude, m_layout.fieldLon);
}

void MainWindow::ApplyClientSize() {
    if (!m_hwnd) return;
    RECT desired = {};
    desired.right = S(kLogicalWidth);
    desired.bottom = S(kLogicalHeight);
    const DWORD style = (DWORD)GetWindowLongPtrW(m_hwnd, GWL_STYLE);
    const DWORD exStyle = (DWORD)GetWindowLongPtrW(m_hwnd, GWL_EXSTYLE);
    if (!AdjustWindowRectExForDpi(&desired, style, FALSE, exStyle, m_dpi)) {
        AdjustWindowRectEx(&desired, style, FALSE, exStyle);
    }
    SetWindowPos(m_hwnd, nullptr, 0, 0,
                 desired.right - desired.left, desired.bottom - desired.top,
                 SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
}

// ---------------------------------------------------------------------------
// 主题 / 状态
// ---------------------------------------------------------------------------
void MainWindow::UpdateTheme(bool force) {
    if (!m_hwnd) return;
    const bool dark = !GetCurrentAppsTheme();
    if (!force && dark == m_dark && m_brField != nullptr) return;
    m_dark = dark;
    m_colors = dark ? ui::DarkColors() : ui::LightColors();
    RecreateBrushes();
    ui::SetDarkTitleBar(m_hwnd, dark);
    RedrawWindow(m_hwnd, nullptr, nullptr, RDW_INVALIDATE | RDW_ALLCHILDREN);
}

void MainWindow::UpdateStatusText() {
    const auto& cfg = ConfigManager::Instance().Get();
    LightSwitchState state;
    if (m_stateManager) state = m_stateManager->GetState();

    const bool systemLight = GetCurrentSystemTheme();
    const bool appsLight = GetCurrentAppsTheme();

    m_statusTitle = systemLight ? L"亮色模式" : L"暗色模式";
    if (state.isManualOverride) m_statusTitle += L" · 手动覆盖";

    std::wstring sub = L"系统主题 ";
    sub += systemLight ? L"亮" : L"暗";
    sub += L" · 应用主题 ";
    sub += appsLight ? L"亮" : L"暗";
    sub += L" · ";
    switch (cfg.scheduleMode) {
    case ScheduleMode::Off:
        sub += L"自动切换已关闭";
        break;
    case ScheduleMode::FixedHours:
        sub += L"固定时间段 ";
        sub += FormatClock(cfg.lightTime);
        sub += L" → ";
        sub += FormatClock(cfg.darkTime);
        break;
    case ScheduleMode::SunsetToSunrise:
        sub += L"日出 ";
        sub += FormatClock(cfg.lightTime);
        sub += L" / 日落 ";
        sub += FormatClock(cfg.darkTime);
        break;
    case ScheduleMode::FollowNightLight:
        sub += L"跟随 Windows 夜间模式";
        break;
    }
    m_statusSub = sub;

    if (cfg.scheduleMode == ScheduleMode::Off) {
        m_pillText = L"已关闭";
        m_pillActive = false;
    } else if (state.isManualOverride) {
        m_pillText = L"手动";
        m_pillActive = true;
    } else {
        m_pillText = L"自动";
        m_pillActive = true;
    }
}

void MainWindow::RefreshUI() {
    if (!m_hwnd || m_shuttingDown) return;
    UpdateTheme();

    const auto& cfg = ConfigManager::Instance().Get();
    m_modeIndex = (int)cfg.scheduleMode;
    m_systemOn = cfg.changeSystem;
    m_appsOn = cfg.changeApps;

    auto setText = [](HWND edit, const std::wstring& text) {
        if (!edit || GetFocus() == edit) return;
        if (GetEditText(edit) != text) SetWindowTextW(edit, text.c_str());
    };
    setText(m_hLightTime, FormatClock(cfg.lightTime));
    setText(m_hDarkTime, FormatClock(cfg.darkTime));
    setText(m_hLatitude, cfg.latitude);
    setText(m_hLongitude, cfg.longitude);

    const BOOL useCoordinates = (cfg.scheduleMode == ScheduleMode::SunsetToSunrise) ? TRUE : FALSE;
    if (m_hLatitude) EnableWindow(m_hLatitude, useCoordinates);
    if (m_hLongitude) EnableWindow(m_hLongitude, useCoordinates);

    UpdateStatusText();
    m_trayIcon.SetIcon(GetCurrentSystemTheme());

    std::wstring tooltip = L"LightSwitch - ";
    tooltip += m_statusTitle;
    m_trayIcon.SetTooltip(tooltip.c_str());

    InvalidateRect(m_hwnd, nullptr, FALSE);
    if (m_hLatitude) InvalidateRect(m_hLatitude, nullptr, FALSE);
    if (m_hLongitude) InvalidateRect(m_hLongitude, nullptr, FALSE);
}

// ---------------------------------------------------------------------------
// 绘制
// ---------------------------------------------------------------------------
void MainWindow::Paint(HDC dc, const RECT& client) {
    ui::FillSolid(dc, client, m_colors.window);
    PaintHeader(dc);

    ui::FillRound(dc, m_layout.card, S(12), m_colors.card, m_colors.cardBorder);

    ui::DrawTextLine(dc, L"调度模式", m_layout.captionMode, m_fontSubtitle,
                     m_colors.textSecondary, DT_SINGLELINE | DT_VCENTER | DT_LEFT);
    PaintSegmented(dc);

    PaintSwitchRow(dc, m_layout.rowSystem, L"修改系统主题", m_systemOn, HIT_SYSTEM);
    PaintSwitchRow(dc, m_layout.rowApps, L"修改应用主题", m_appsOn, HIT_APPS);
    PaintFieldRow(dc, m_layout.rowLight, m_layout.fieldLight, m_hLightTime, L"亮色时间");
    PaintFieldRow(dc, m_layout.rowDark, m_layout.fieldDark, m_hDarkTime, L"暗色时间");
    PaintFieldRow(dc, m_layout.rowLat, m_layout.fieldLat, m_hLatitude, L"纬度");
    PaintFieldRow(dc, m_layout.rowLon, m_layout.fieldLon, m_hLongitude, L"经度");

    for (int i = 0; i < 3; ++i) {
        ui::FillSolid(dc, m_layout.divider[i], m_colors.divider);
    }

    PaintActionButton(dc, m_layout.btnLight, true, HIT_LIGHT);
    PaintActionButton(dc, m_layout.btnDark, false, HIT_DARK);
    PaintStatus(dc);
}

void MainWindow::PaintHeader(HDC dc) {
    ui::DrawTextLine(dc, L"LightSwitch", m_layout.headerTitle, m_fontTitle,
                     m_colors.text, DT_SINGLELINE | DT_VCENTER | DT_LEFT);
    ui::DrawTextLine(dc, L"自动切换 Windows 深浅色主题", m_layout.headerSubtitle, m_fontSubtitle,
                     m_colors.textSecondary, DT_SINGLELINE | DT_VCENTER | DT_LEFT);

    const RECT& pill = m_layout.pill;
    const COLORREF background = m_pillActive
        ? ui::Blend(m_colors.window, m_colors.accent, 16)
        : m_colors.field;
    const COLORREF foreground = m_pillActive ? m_colors.accent : m_colors.textSecondary;
    ui::FillRound(dc, pill, (pill.bottom - pill.top) / 2, background, background);
    ui::DrawTextLine(dc, m_pillText, pill, m_fontSubtitle, foreground,
                     DT_CENTER | DT_SINGLELINE | DT_VCENTER);
}

void MainWindow::PaintSegmented(HDC dc) {
    ui::FillRound(dc, m_layout.segTrack, S(8), m_colors.field, m_colors.fieldBorder);

    for (int i = 0; i < 4; ++i) {
        const RECT& seg = m_layout.seg[i];
        RECT inner = seg;
        inner.left += S(3);
        inner.right -= S(3);
        inner.top += S(3);
        inner.bottom -= S(3);

        const bool selected = (i == m_modeIndex);
        const bool hovered = (m_hot == HIT_MODE_OFF + i);
        if (selected) {
            const COLORREF fill = hovered ? m_colors.accentHover : m_colors.accent;
            ui::FillRound(dc, inner, S(6), fill, fill);
        } else if (hovered) {
            const COLORREF fill = ui::Blend(m_colors.field, m_colors.accent, 10);
            ui::FillRound(dc, inner, S(6), fill, fill);
        }

        ui::DrawTextLine(dc, kSegmentLabels[i], seg, m_fontSubtitle,
                         selected ? m_colors.onAccent : m_colors.text,
                         DT_CENTER | DT_SINGLELINE | DT_VCENTER);
    }
}

void MainWindow::PaintSwitchRow(HDC dc, const RECT& row, const wchar_t* label, bool on, int hitId) {
    const bool hovered = (m_hot == hitId);
    if (hovered) {
        RECT hover = row;
        hover.left += S(8);
        hover.right -= S(8);
        hover.top += S(3);
        hover.bottom -= S(3);
        const COLORREF fill = ui::Blend(m_colors.card, m_colors.text, 5);
        ui::FillRound(dc, hover, S(6), fill, fill);
    }

    RECT labelRect = row;
    labelRect.left += S(16);
    labelRect.right -= S(64);
    ui::DrawTextLine(dc, label, labelRect, m_fontLabel, m_colors.text,
                     DT_SINGLELINE | DT_VCENTER | DT_LEFT | DT_END_ELLIPSIS);

    const int trackWidth = S(44);
    const int trackHeight = S(24);
    const int centerY = (row.top + row.bottom) / 2;
    RECT track;
    track.left = row.right - S(16) - trackWidth;
    track.right = row.right - S(16);
    track.top = centerY - trackHeight / 2;
    track.bottom = centerY + trackHeight / 2;

    COLORREF trackColor = on ? m_colors.accent : m_colors.trackOff;
    if (hovered) {
        trackColor = on ? m_colors.accentHover : ui::Blend(m_colors.trackOff, m_colors.text, 8);
    }
    ui::FillRound(dc, track, trackHeight / 2, trackColor, trackColor);

    const int knobRadius = trackHeight / 2 - S(3);
    const int knobCenterX = on ? (track.right - trackHeight / 2) : (track.left + trackHeight / 2);
    ui::FillCircle(dc, knobCenterX, centerY, knobRadius, m_colors.knob);
}

void MainWindow::PaintFieldRow(HDC dc, const RECT& row, const RECT& field, HWND edit, const wchar_t* label) {
    RECT labelRect = row;
    labelRect.left += S(16);
    labelRect.right = field.left - S(12);
    ui::DrawTextLine(dc, label, labelRect, m_fontLabel, m_colors.text,
                     DT_SINGLELINE | DT_VCENTER | DT_LEFT | DT_END_ELLIPSIS);

    const bool enabled = edit && IsWindowEnabled(edit);
    const COLORREF fill = enabled ? m_colors.field : m_colors.fieldDisabled;
    COLORREF border = m_colors.fieldBorder;
    if (m_invalidEdit == edit && edit != nullptr) {
        border = m_colors.danger;
    } else if (edit && GetFocus() == edit) {
        border = m_colors.accent;
    }
    ui::FillRound(dc, field, S(6), fill, border);
}

void MainWindow::PaintActionButton(HDC dc, const RECT& rect, bool light, int hitId) {
    const bool hovered = (m_hot == hitId);
    const bool pressed = (m_pressed == hitId && hovered);

    COLORREF fill = m_colors.card;
    if (pressed) fill = ui::Blend(m_colors.card, m_colors.accent, 14);
    else if (hovered) fill = ui::Blend(m_colors.card, m_colors.accent, 8);
    const COLORREF border = hovered ? ui::Blend(m_colors.cardBorder, m_colors.accent, 35) : m_colors.cardBorder;
    ui::FillRound(dc, rect, S(8), fill, border);

    const int centerY = (rect.top + rect.bottom) / 2;
    const int iconCenterX = rect.left + S(22);
    if (light) {
        PaintSunIcon(dc, iconCenterX, centerY, m_colors.sun, S(4));
    } else {
        PaintMoonIcon(dc, iconCenterX, centerY, m_colors.moon, fill, S(4));
    }

    RECT textRect = rect;
    textRect.left = iconCenterX + S(15);
    textRect.right -= S(10);
    ui::DrawTextLine(dc, light ? L"切换为亮色" : L"切换为暗色", textRect, m_fontButton,
                     m_colors.text, DT_SINGLELINE | DT_VCENTER | DT_LEFT);
}

void MainWindow::PaintStatus(HDC dc) {
    const bool systemLight = GetCurrentSystemTheme();
    const int centerY = (m_layout.statusTitle.top + m_layout.statusTitle.bottom) / 2;
    ui::FillCircle(dc, S(26), centerY, S(4), systemLight ? m_colors.sun : m_colors.moon);

    RECT titleRect = m_layout.statusTitle;
    titleRect.left = S(36);
    ui::DrawTextLine(dc, m_statusTitle, titleRect, m_fontLabel, m_colors.text,
                     DT_SINGLELINE | DT_VCENTER | DT_LEFT | DT_END_ELLIPSIS);
    ui::DrawTextLine(dc, m_statusSub, m_layout.statusSub, m_fontSubtitle, m_colors.textSecondary,
                     DT_SINGLELINE | DT_VCENTER | DT_LEFT | DT_END_ELLIPSIS);
    ui::DrawTextLine(dc, L"Ctrl + Alt + Shift + D 快速切换主题", m_layout.hint, m_fontSubtitle,
                     m_colors.textDisabled, DT_SINGLELINE | DT_VCENTER | DT_LEFT);
}

// ---------------------------------------------------------------------------
// 鼠标交互
// ---------------------------------------------------------------------------
int MainWindow::HitTest(POINT pt) const {
    for (int i = 0; i < 4; ++i) {
        if (PtInRect(&m_layout.seg[i], pt)) return HIT_MODE_OFF + i;
    }
    if (PtInRect(&m_layout.rowSystem, pt)) return HIT_SYSTEM;
    if (PtInRect(&m_layout.rowApps, pt)) return HIT_APPS;
    if (PtInRect(&m_layout.btnLight, pt)) return HIT_LIGHT;
    if (PtInRect(&m_layout.btnDark, pt)) return HIT_DARK;
    return HIT_NONE;
}

void MainWindow::Activate(int hitId) {
    switch (hitId) {
    case HIT_MODE_OFF: SetMode(0); break;
    case HIT_MODE_FIXED: SetMode(1); break;
    case HIT_MODE_SUN: SetMode(2); break;
    case HIT_MODE_NIGHT: SetMode(3); break;
    case HIT_SYSTEM: ToggleFlag(true); break;
    case HIT_APPS: ToggleFlag(false); break;
    case HIT_LIGHT: ToggleTheme(true); break;
    case HIT_DARK: ToggleTheme(false); break;
    default: break;
    }
}

void MainWindow::SetMode(int index) {
    if (index < 0 || index > 3) return;
    auto& cfg = ConfigManager::Instance().Get();
    if (cfg.scheduleMode == kSegmentModes[index]) return;
    cfg.scheduleMode = kSegmentModes[index];
    PersistSettings();
}

void MainWindow::ToggleFlag(bool systemFlag) {
    auto& cfg = ConfigManager::Instance().Get();
    if (systemFlag) cfg.changeSystem = !cfg.changeSystem;
    else cfg.changeApps = !cfg.changeApps;
    PersistSettings();
}

void MainWindow::ToggleTheme(bool light) {
    SetSystemTheme(light);
    SetAppsTheme(light);
    if (m_stateManager) m_stateManager->OnManualOverride();
    RefreshUI();
}

void MainWindow::PersistSettings() {
    if (m_shuttingDown) return;
    ConfigManager::Instance().Save();
    if (m_stateManager) m_stateManager->OnSettingsChanged();
    RefreshUI();
}

// ---------------------------------------------------------------------------
// 输入提交
// ---------------------------------------------------------------------------
void MainWindow::CommitTimes() {
    if (m_shuttingDown || !m_hLightTime || !m_hDarkTime) return;
    auto& cfg = ConfigManager::Instance().Get();
    const std::wstring lightText = GetEditText(m_hLightTime);
    const std::wstring darkText = GetEditText(m_hDarkTime);
    const int light = ParseClock(lightText, cfg.lightTime);
    const int dark = ParseClock(darkText, cfg.darkTime);
    const std::wstring lightValue = FormatClock(light);
    const std::wstring darkValue = FormatClock(dark);

    bool changed = false;
    if (light != cfg.lightTime) { cfg.lightTime = light; changed = true; }
    if (dark != cfg.darkTime) { cfg.darkTime = dark; changed = true; }
    if (lightText != lightValue) SetWindowTextW(m_hLightTime, lightValue.c_str());
    if (darkText != darkValue) SetWindowTextW(m_hDarkTime, darkValue.c_str());
    if (!changed) return;
    PersistSettings();
}

void MainWindow::CommitCoordinates() {
    if (m_shuttingDown || !m_hLatitude || !m_hLongitude) return;
    auto& cfg = ConfigManager::Instance().Get();
    const std::wstring latText = GetEditText(m_hLatitude);
    const std::wstring lonText = GetEditText(m_hLongitude);
    std::wstring latValue;
    std::wstring lonValue;
    const bool latOk = ParseCoordinate(latText, -90.0, 90.0, latValue);
    const bool lonOk = ParseCoordinate(lonText, -180.0, 180.0, lonValue);

    bool changed = false;
    if (latOk) {
        if (latValue != cfg.latitude) { cfg.latitude = latValue; changed = true; }
        if (latText != latValue) SetWindowTextW(m_hLatitude, latValue.c_str());
    } else {
        FlashInvalid(m_hLatitude);
        if (latText != cfg.latitude) SetWindowTextW(m_hLatitude, cfg.latitude.c_str());
    }
    if (lonOk) {
        if (lonValue != cfg.longitude) { cfg.longitude = lonValue; changed = true; }
        if (lonText != lonValue) SetWindowTextW(m_hLongitude, lonValue.c_str());
    } else {
        FlashInvalid(m_hLongitude);
        if (lonText != cfg.longitude) SetWindowTextW(m_hLongitude, cfg.longitude.c_str());
    }
    if (!latOk || !lonOk) return;
    if (!changed) return;
    PersistSettings();
}

void MainWindow::CommitFocusedEdit() {
    HWND focus = GetFocus();
    if (focus == m_hLightTime || focus == m_hDarkTime) {
        CommitTimes();
    } else if (focus == m_hLatitude || focus == m_hLongitude) {
        CommitCoordinates();
    }
}

void MainWindow::RevertFocusedEdit() {
    const auto& cfg = ConfigManager::Instance().Get();
    HWND focus = GetFocus();
    if (focus == m_hLightTime) {
        SetWindowTextW(focus, FormatClock(cfg.lightTime).c_str());
    } else if (focus == m_hDarkTime) {
        SetWindowTextW(focus, FormatClock(cfg.darkTime).c_str());
    } else if (focus == m_hLatitude) {
        SetWindowTextW(focus, cfg.latitude.c_str());
    } else if (focus == m_hLongitude) {
        SetWindowTextW(focus, cfg.longitude.c_str());
    }
    InvalidateRect(m_hwnd, nullptr, FALSE);
}

void MainWindow::FocusNextEdit(HWND current, bool backward) {
    HWND edits[4] = { m_hLightTime, m_hDarkTime, m_hLatitude, m_hLongitude };
    int index = -1;
    for (int i = 0; i < 4; ++i) {
        if (edits[i] == current) { index = i; break; }
    }
    if (index < 0) index = backward ? 0 : 3;
    for (int step = 1; step <= 4; ++step) {
        const int next = backward ? ((index - step + 8) % 4) : ((index + step) % 4);
        HWND target = edits[next];
        if (target && IsWindowEnabled(target)) {
            SetFocus(target);
            SendMessageW(target, EM_SETSEL, 0, -1);
            return;
        }
    }
}

void MainWindow::FlashInvalid(HWND edit) {
    m_invalidEdit = edit;
    SetTimer(m_hwnd, 2, 900, nullptr);
    InvalidateRect(m_hwnd, nullptr, FALSE);
}

// ---------------------------------------------------------------------------
// 消息处理
// ---------------------------------------------------------------------------
LRESULT CALLBACK MainWindow::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    MainWindow* self = nullptr;
    if (msg == WM_NCCREATE) {
        CREATESTRUCTW* cs = (CREATESTRUCTW*)lParam;
        self = (MainWindow*)cs->lpCreateParams;
        if (self) {
            self->m_hwnd = hwnd;
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, (LONG_PTR)self);
        }
    } else {
        self = (MainWindow*)GetWindowLongPtrW(hwnd, GWLP_USERDATA);
    }
    if (!self) return DefWindowProcW(hwnd, msg, wParam, lParam);
    return self->HandleMessage(hwnd, msg, wParam, lParam);
}

LRESULT MainWindow::HandleMessage(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_CREATE:
        OnCreate();
        return 0;

    case WM_ERASEBKGND:
        return 1;

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(hwnd, &ps);
        if (dc) {
            RECT client;
            GetClientRect(hwnd, &client);
            const int width = client.right - client.left;
            const int height = client.bottom - client.top;
            HDC memoryDc = CreateCompatibleDC(dc);
            HBITMAP bitmap = nullptr;
            HGDIOBJ oldBitmap = nullptr;
            if (memoryDc && width > 0 && height > 0) {
                bitmap = CreateCompatibleBitmap(dc, width, height);
                if (bitmap) oldBitmap = SelectObject(memoryDc, bitmap);
            }
            if (memoryDc && bitmap && oldBitmap) {
                Paint(memoryDc, client);
                BitBlt(dc, 0, 0, width, height, memoryDc, 0, 0, SRCCOPY);
            } else {
                Paint(dc, client);
            }
            if (memoryDc) {
                if (oldBitmap) SelectObject(memoryDc, oldBitmap);
                if (bitmap) DeleteObject(bitmap);
                DeleteDC(memoryDc);
            }
        }
        EndPaint(hwnd, &ps);
        return 0;
    }

    case WM_CTLCOLOREDIT: {
        HDC dc = (HDC)wParam;
        SetBkColor(dc, m_colors.field);
        SetTextColor(dc, m_colors.text);
        return (LRESULT)(m_brField ? m_brField : GetStockObject(WHITE_BRUSH));
    }

    case WM_CTLCOLORSTATIC: {
        HDC dc = (HDC)wParam;
        SetBkColor(dc, m_colors.fieldDisabled);
        SetTextColor(dc, m_colors.textDisabled);
        return (LRESULT)(m_brFieldDisabled ? m_brFieldDisabled : GetStockObject(WHITE_BRUSH));
    }

    case WM_MOUSEMOVE: {
        if (!m_trackingMouse) {
            TRACKMOUSEEVENT tme = {};
            tme.cbSize = sizeof(tme);
            tme.dwFlags = TME_LEAVE;
            tme.hwndTrack = hwnd;
            TrackMouseEvent(&tme);
            m_trackingMouse = true;
        }
        POINT pt;
        pt.x = (int)(short)LOWORD(lParam);
        pt.y = (int)(short)HIWORD(lParam);
        const int hit = HitTest(pt);
        if (hit != m_hot) {
            m_hot = hit;
            InvalidateRect(hwnd, nullptr, FALSE);
        }
        return 0;
    }

    case WM_MOUSELEAVE:
        m_trackingMouse = false;
        if (m_hot != HIT_NONE) {
            m_hot = HIT_NONE;
            InvalidateRect(hwnd, nullptr, FALSE);
        }
        return 0;

    case WM_SETCURSOR:
        if (LOWORD(lParam) == HTCLIENT) {
            POINT cursor = {};
            if (GetCursorPos(&cursor) && ScreenToClient(hwnd, &cursor) &&
                HitTest(cursor) != HIT_NONE) {
                SetCursor(LoadCursorW(nullptr, IDC_HAND));
                return TRUE;
            }
        }
        break;

    case WM_LBUTTONDOWN: {
        POINT pt;
        pt.x = (int)(short)LOWORD(lParam);
        pt.y = (int)(short)HIWORD(lParam);
        const int hit = HitTest(pt);
        if (hit != HIT_NONE) {
            SetFocus(hwnd); // 让正在编辑的输入框先提交
            m_pressed = hit;
            SetCapture(hwnd);
            InvalidateRect(hwnd, nullptr, FALSE);
        }
        return 0;
    }

    case WM_LBUTTONUP: {
        POINT pt;
        pt.x = (int)(short)LOWORD(lParam);
        pt.y = (int)(short)HIWORD(lParam);
        const int hit = HitTest(pt);
        const int pressed = m_pressed;
        m_pressed = HIT_NONE;
        if (GetCapture() == hwnd) ReleaseCapture();
        if (pressed != HIT_NONE && pressed == hit) Activate(pressed);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }

    case WM_CAPTURECHANGED:
        if (m_pressed != HIT_NONE) {
            m_pressed = HIT_NONE;
            InvalidateRect(hwnd, nullptr, FALSE);
        }
        return 0;

    case WM_KEYDOWN:
        if (wParam == VK_TAB && GetFocus() == hwnd) {
            FocusNextEdit(nullptr, (GetKeyState(VK_SHIFT) & 0x8000) != 0);
            return 0;
        }
        break;

    case WM_TRAYICON: {
        // V4: LOWORD(lParam)=鼠标消息；旧版: lParam=鼠标消息
        const UINT trayMsg = LOWORD(lParam);
        if (trayMsg == WM_LBUTTONUP || trayMsg == WM_LBUTTONDBLCLK) {
            IsVisible() ? Hide() : Show();
        } else if (trayMsg == WM_RBUTTONUP || trayMsg == WM_CONTEXTMENU) {
            m_trayIcon.ShowContextMenu(hwnd);
        }
        return 0;
    }

    case WM_COMMAND: {
        HWND source = (HWND)lParam;
        const WORD id = LOWORD(wParam);
        const WORD code = HIWORD(wParam);

        if (source == m_hLightTime || source == m_hDarkTime) {
            if (code == EN_KILLFOCUS) CommitTimes();
            else if (code == EN_SETFOCUS) InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }
        if (source == m_hLatitude || source == m_hLongitude) {
            if (code == EN_KILLFOCUS) CommitCoordinates();
            else if (code == EN_SETFOCUS) InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }

        switch (id) {
        case IDM_TRAY_TOGGLE: {
            const bool current = GetCurrentSystemTheme();
            SetSystemTheme(!current);
            SetAppsTheme(!current);
            if (m_stateManager) m_stateManager->OnManualOverride();
            RefreshUI();
            break;
        }
        case IDM_TRAY_TOGGLE_SCHEDULE: {
            auto& cfg = ConfigManager::Instance().Get();
            cfg.scheduleMode = (cfg.scheduleMode == ScheduleMode::Off)
                ? ScheduleMode::FixedHours : ScheduleMode::Off;
            ConfigManager::Instance().Save();
            if (m_stateManager) m_stateManager->OnSettingsChanged();
            RefreshUI();
            break;
        }
        case IDM_TRAY_OPEN_SETTINGS:
            Show();
            break;
        case IDM_TRAY_EXIT:
            PostQuitMessage(0);
            break;
        default:
            break;
        }
        return 0;
    }

    case WM_TIMER:
        if (wParam == 2) {
            KillTimer(hwnd, 2);
            m_invalidEdit = nullptr;
            InvalidateRect(hwnd, nullptr, FALSE);
            return 0;
        }
        if (m_stateManager) m_stateManager->OnTick();
        RefreshUI();
        return 0;

    case WM_FILE_CHANGED:
        ConfigManager::Instance().Load();
        if (m_stateManager) m_stateManager->OnSettingsChanged();
        RefreshUI();
        return 0;

    case WM_NIGHTLIGHT_CHANGED:
        if (m_stateManager) m_stateManager->OnNightLightChange();
        RefreshUI();
        return 0;

    case WM_HOTKEY:
        if (wParam == IDH_TOGGLE_THEME) {
            const bool current = GetCurrentSystemTheme();
            SetSystemTheme(!current);
            SetAppsTheme(!current);
            if (m_stateManager) m_stateManager->OnManualOverride();
            RefreshUI();
        }
        return 0;

    case WM_SETTINGCHANGE:
        if (wParam == 0 && lParam != 0) {
            const wchar_t* area = (const wchar_t*)lParam;
            if (lstrcmpW(area, L"ImmersiveColorSet") == 0) {
                UpdateTheme();
                RefreshUI();
            }
        }
        return 0;

    case WM_THEMECHANGED:
        UpdateTheme();
        return 0;

    case WM_DPICHANGED: {
        const UINT newDpi = HIWORD(wParam);
        if (newDpi != 0) m_dpi = newDpi;
        RecreateFonts();
        BuildLayout();
        ApplyLayout();
        InvalidateRect(hwnd, nullptr, FALSE);
        RECT* suggested = (RECT*)lParam;
        RECT desired = {};
        desired.right = S(kLogicalWidth);
        desired.bottom = S(kLogicalHeight);
        const DWORD style = (DWORD)GetWindowLongPtrW(hwnd, GWL_STYLE);
        const DWORD exStyle = (DWORD)GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        if (suggested && AdjustWindowRectExForDpi(&desired, style, FALSE, exStyle, m_dpi)) {
            SetWindowPos(hwnd, nullptr, suggested->left, suggested->top,
                         desired.right - desired.left, desired.bottom - desired.top,
                         SWP_NOZORDER | SWP_NOACTIVATE);
        }
        return 0;
    }

    case WM_CLOSE:
        Hide();
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;

    default:
        break;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

// ---------------------------------------------------------------------------
// 编辑框子类化：回车提交 / ESC 还原 / Tab 切换焦点
// ---------------------------------------------------------------------------
LRESULT CALLBACK MainWindow::EditSubclassProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam,
                                              UINT_PTR, DWORD_PTR refData) {
    MainWindow* self = (MainWindow*)refData;
    if (self) {
        if (msg == WM_KEYDOWN) {
            if (wParam == VK_RETURN) {
                self->CommitFocusedEdit();
                return 0;
            }
            if (wParam == VK_ESCAPE) {
                self->RevertFocusedEdit();
                return 0;
            }
            if (wParam == VK_TAB) {
                self->FocusNextEdit(hwnd, (GetKeyState(VK_SHIFT) & 0x8000) != 0);
                return 0;
            }
        } else if (msg == WM_CHAR) {
            // 吞掉 Tab/回车/ESC 产生的字符，避免系统提示音
            if (wParam == L'\t' || wParam == L'\r' || wParam == L'\n' || wParam == 27) return 0;
        }
    }
    return DefSubclassProc(hwnd, msg, wParam, lParam);
}
