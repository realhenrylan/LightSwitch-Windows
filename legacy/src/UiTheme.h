#pragma once
#include "pch.h"

// ---------------------------------------------------------------------------
// UiTheme.h - LightSwitch 轻量界面绘制层
//
// 只依赖 Windows SDK（User32 / GDI32），不引入任何第三方框架：
//   * 配色（亮色 / 暗色）
//   * DPI 缩放（96 DPI 为设计基准）
//   * 圆角矩形、圆形、直线、文字等基础绘制
//   * 字体创建（Segoe UI，ClearType）
//   * Win10/11 深色标题栏（dwmapi 动态加载，不新增链接依赖）
// ---------------------------------------------------------------------------
namespace ui {

struct Colors {
    COLORREF window;        // 窗口底色
    COLORREF card;          // 卡片底色
    COLORREF cardBorder;    // 卡片描边
    COLORREF divider;       // 行分隔线
    COLORREF text;          // 主文字
    COLORREF textSecondary; // 次要文字
    COLORREF textDisabled;  // 禁用文字
    COLORREF accent;        // 强调色
    COLORREF accentHover;   // 强调色悬停
    COLORREF onAccent;      // 强调色上的文字
    COLORREF field;         // 输入框底色
    COLORREF fieldDisabled; // 禁用输入框底色
    COLORREF fieldBorder;   // 输入框描边
    COLORREF trackOff;      // 开关关闭时的轨道
    COLORREF knob;          // 开关滑块
    COLORREF sun;           // 亮色图标
    COLORREF moon;          // 暗色图标
    COLORREF danger;        // 校验失败
};

inline Colors LightColors() {
    Colors c;
    c.window        = RGB(243, 243, 243);
    c.card          = RGB(255, 255, 255);
    c.cardBorder    = RGB(231, 231, 231);
    c.divider       = RGB(238, 238, 238);
    c.text          = RGB(26, 26, 26);
    c.textSecondary = RGB(96, 96, 96);
    c.textDisabled  = RGB(158, 158, 158);
    c.accent        = RGB(0, 95, 184);
    c.accentHover   = RGB(25, 110, 200);
    c.onAccent      = RGB(255, 255, 255);
    c.field         = RGB(253, 253, 253);
    c.fieldDisabled = RGB(246, 246, 246);
    c.fieldBorder   = RGB(224, 224, 224);
    c.trackOff      = RGB(200, 200, 200);
    c.knob          = RGB(255, 255, 255);
    c.sun           = RGB(240, 162, 58);
    c.moon          = RGB(92, 110, 200);
    c.danger        = RGB(196, 43, 28);
    return c;
}

inline Colors DarkColors() {
    Colors c;
    c.window        = RGB(32, 32, 32);
    c.card          = RGB(44, 44, 44);
    c.cardBorder    = RGB(56, 56, 56);
    c.divider       = RGB(54, 54, 54);
    c.text          = RGB(255, 255, 255);
    c.textSecondary = RGB(160, 160, 160);
    c.textDisabled  = RGB(110, 110, 110);
    c.accent        = RGB(76, 194, 255);
    c.accentHover   = RGB(100, 205, 255);
    c.onAccent      = RGB(0, 24, 40);
    c.field         = RGB(52, 52, 52);
    c.fieldDisabled = RGB(46, 46, 46);
    c.fieldBorder   = RGB(70, 70, 70);
    c.trackOff      = RGB(90, 90, 90);
    c.knob          = RGB(245, 245, 245);
    c.sun           = RGB(255, 190, 90);
    c.moon          = RGB(160, 176, 255);
    c.danger        = RGB(255, 110, 110);
    return c;
}

// 把 96 DPI 下的逻辑像素换算成当前 DPI 的物理像素
inline int Scale(int value, UINT dpi) {
    if (dpi == 0 || dpi == 96) return value;
    return MulDiv(value, (int)dpi, 96);
}

// 颜色混合：frontPercent 为 front 的百分比（0..100）
inline COLORREF Blend(COLORREF back, COLORREF front, int frontPercent) {
    if (frontPercent <= 0) return back;
    if (frontPercent >= 100) return front;
    const int b = 100 - frontPercent;
    return RGB((GetRValue(front) * frontPercent + GetRValue(back) * b) / 100,
               (GetGValue(front) * frontPercent + GetGValue(back) * b) / 100,
               (GetBValue(front) * frontPercent + GetBValue(back) * b) / 100);
}

inline void FillSolid(HDC dc, const RECT& rc, COLORREF color) {
    SetDCBrushColor(dc, color);
    FillRect(dc, &rc, (HBRUSH)GetStockObject(DC_BRUSH));
}

inline void FillRound(HDC dc, const RECT& rc, int radius, COLORREF fill, COLORREF border) {
    HGDIOBJ oldBrush = SelectObject(dc, GetStockObject(DC_BRUSH));
    HGDIOBJ oldPen = SelectObject(dc, GetStockObject(DC_PEN));
    SetDCBrushColor(dc, fill);
    SetDCPenColor(dc, border);
    SetBkMode(dc, TRANSPARENT);
    RoundRect(dc, rc.left, rc.top, rc.right, rc.bottom, radius * 2, radius * 2);
    SelectObject(dc, oldPen);
    SelectObject(dc, oldBrush);
}

inline void FillCircle(HDC dc, int cx, int cy, int radius, COLORREF color) {
    HGDIOBJ oldBrush = SelectObject(dc, GetStockObject(DC_BRUSH));
    HGDIOBJ oldPen = SelectObject(dc, GetStockObject(DC_PEN));
    SetDCBrushColor(dc, color);
    SetDCPenColor(dc, color);
    Ellipse(dc, cx - radius, cy - radius, cx + radius + 1, cy + radius + 1);
    SelectObject(dc, oldPen);
    SelectObject(dc, oldBrush);
}

inline void DrawLine(HDC dc, int x1, int y1, int x2, int y2, COLORREF color, int width) {
    HPEN pen = CreatePen(PS_SOLID, width > 0 ? width : 1, color);
    if (!pen) return;
    HGDIOBJ oldPen = SelectObject(dc, pen);
    MoveToEx(dc, x1, y1, nullptr);
    LineTo(dc, x2, y2);
    SelectObject(dc, oldPen);
    DeleteObject(pen);
}

inline void DrawTextLine(HDC dc, const wchar_t* text, int length, const RECT& rc,
                         HFONT font, COLORREF color, UINT format) {
    if (!font || !text || length <= 0) return;
    RECT target = rc;
    HGDIOBJ oldFont = SelectObject(dc, font);
    int oldMode = SetBkMode(dc, TRANSPARENT);
    COLORREF oldColor = SetTextColor(dc, color);
    DrawTextW(dc, text, length, &target, format | DT_NOPREFIX);
    SetTextColor(dc, oldColor);
    SetBkMode(dc, oldMode);
    SelectObject(dc, oldFont);
}

inline void DrawTextLine(HDC dc, const std::wstring& text, const RECT& rc,
                         HFONT font, COLORREF color, UINT format) {
    DrawTextLine(dc, text.c_str(), (int)text.size(), rc, font, color, format);
}

inline HFONT CreateFontPx(int pixelHeight, int weight, UINT dpi) {
    LOGFONTW lf = {};
    lf.lfHeight = -MulDiv(pixelHeight, (int)(dpi ? dpi : 96), 96);
    lf.lfWeight = weight;
    lf.lfCharSet = DEFAULT_CHARSET;
    lf.lfOutPrecision = OUT_DEFAULT_PRECIS;
    lf.lfClipPrecision = CLIP_DEFAULT_PRECIS;
    lf.lfQuality = CLEARTYPE_QUALITY;
    lf.lfPitchAndFamily = DEFAULT_PITCH | FF_DONTCARE;
    lstrcpynW(lf.lfFaceName, L"Segoe UI", LF_FACESIZE);
    return CreateFontIndirectW(&lf);
}

// 深色标题栏 / 圆角窗口（Win10 1809+ / Win11；不支持的版本静默忽略）
inline void SetDarkTitleBar(HWND hwnd, bool dark) {
    if (!hwnd) return;
    HMODULE dwm = LoadLibraryW(L"dwmapi.dll");
    if (!dwm) return;
    typedef HRESULT(WINAPI* SetAttrFn)(HWND, DWORD, LPCVOID, DWORD);
    SetAttrFn setAttribute = (SetAttrFn)GetProcAddress(dwm, "DwmSetWindowAttribute");
    if (setAttribute) {
        BOOL value = dark ? TRUE : FALSE;
        if (FAILED(setAttribute(hwnd, 20, &value, sizeof(value)))) {
            setAttribute(hwnd, 19, &value, sizeof(value)); // 旧版属性号
        }
        int corner = 2; // DWMWCP_ROUND
        setAttribute(hwnd, 33, &corner, sizeof(corner));
    }
    FreeLibrary(dwm);
}

} // namespace ui
