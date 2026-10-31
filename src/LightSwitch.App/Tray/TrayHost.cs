using System.Runtime.InteropServices;
using LightSwitch.Core;

namespace LightSwitch.App;

/// <summary>
/// A hidden top-level window that owns the notification-area icon and the
/// global hotkey. It also receives the single-instance activation messages.
/// </summary>
internal sealed class TrayHost : IDisposable
{
    private const string WindowClassName = "LightSwitch.TrayHost";
    private const int TrayIconId = 1;
    private const int HotkeyId = 0x4C53;

    private static TrayHost? s_instance;
    private static NativeMethods.WndProcDelegate? s_wndProc;
    private static uint s_taskbarCreatedMessage;

    private IntPtr _hwnd;
    private IntPtr _icon;
    private NativeMethods.NOTIFYICONDATAW _data;
    private string _tooltip = "LightSwitch";
    private bool _disposed;

    public bool AutoEnabled { get; set; }

    public bool HotkeyRegistered { get; private set; }

    public event Action? ShowRequested;

    public event Action? ToggleThemeRequested;

    public event Action? LightRequested;

    public event Action? DarkRequested;

    public event Action? AutoToggleRequested;

    public event Action? ExitRequested;

    public event Action? SystemChanged;

    public void Create(string tooltip)
    {
        if (_hwnd != IntPtr.Zero)
        {
            UpdateTooltip(tooltip);
            return;
        }

        _tooltip = tooltip;
        s_instance = this;
        s_wndProc = StaticWndProc;

        var hInstance = NativeMethods.GetModuleHandleW(null);
        var windowClass = new NativeMethods.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = hInstance,
            lpszClassName = WindowClassName,
        };

        var atom = NativeMethods.RegisterClassExW(ref windowClass);
        if (atom == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            Log.Warn($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
        }

        _hwnd = NativeMethods.CreateWindowExW(
            0x00000080, // WS_EX_TOOLWINDOW
            WindowClassName,
            "LightSwitch",
            0x80000000, // WS_POPUP, intentionally not visible
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            Log.Error($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
            s_instance = null;
            return;
        }

        s_taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
        _icon = IconFactory.LoadAppIcon();
        AddIcon();
    }

    public void UpdateTooltip(string tooltip)
    {
        _tooltip = Truncate(tooltip, 127);
        if (_hwnd == IntPtr.Zero || _data.hWnd == IntPtr.Zero)
        {
            return;
        }

        _data.szTip = _tooltip;
        _data.uFlags = NativeMethods.NIF_TIP;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref _data);
    }

    public bool SetHotkey(bool enabled)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (HotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            HotkeyRegistered = false;
        }

        if (!enabled)
        {
            return true;
        }

        HotkeyRegistered = NativeMethods.RegisterHotKey(
            _hwnd,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_D);

        if (!HotkeyRegistered)
        {
            Log.Warn($"RegisterHotKey failed: {Marshal.GetLastWin32Error()}");
        }

        return HotkeyRegistered;
    }

    private void AddIcon()
    {
        _data = new NativeMethods.NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = TrayIconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.MsgTrayCallback,
            hIcon = _icon,
            szTip = Truncate(_tooltip, 127),
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };

        if (!NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref _data))
        {
            Log.Warn($"Shell_NotifyIcon(NIM_ADD) failed: {Marshal.GetLastWin32Error()}");
            return;
        }

        _data.uTimeoutOrVersion = NativeMethods.NOTIFYICON_VERSION_4;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_SETVERSION, ref _data);
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            var instance = s_instance;
            if (instance is not null)
            {
                return instance.HandleMessage(hWnd, msg, wParam, lParam);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Tray window procedure failed", ex);
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (s_taskbarCreatedMessage != 0 && msg == s_taskbarCreatedMessage)
        {
            AddIcon();
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.MsgTrayCallback)
        {
            var notification = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (notification)
            {
                case NativeMethods.WM_LBUTTONUP:
                case NativeMethods.WM_LBUTTONDBLCLK:
                    ShowRequested?.Invoke();
                    return IntPtr.Zero;
                case NativeMethods.WM_RBUTTONUP:
                case NativeMethods.WM_CONTEXTMENU:
                    ShowMenu();
                    return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        switch (msg)
        {
            case NativeMethods.MsgShow:
                ShowRequested?.Invoke();
                return IntPtr.Zero;
            case NativeMethods.MsgToggleTheme:
                ToggleThemeRequested?.Invoke();
                return IntPtr.Zero;
            case NativeMethods.MsgApplyLight:
                LightRequested?.Invoke();
                return IntPtr.Zero;
            case NativeMethods.MsgApplyDark:
                DarkRequested?.Invoke();
                return IntPtr.Zero;
            case NativeMethods.WM_HOTKEY:
                ToggleThemeRequested?.Invoke();
                return IntPtr.Zero;
            case NativeMethods.WM_SETTINGCHANGE:
            {
                var area = lParam == IntPtr.Zero ? null : Marshal.PtrToStringUni(lParam);
                if (string.IsNullOrEmpty(area) || area == "ImmersiveColorSet")
                {
                    SystemChanged?.Invoke();
                }

                return IntPtr.Zero;
            }
            case NativeMethods.WM_TIMECHANGE:
            case NativeMethods.WM_DISPLAYCHANGE:
                SystemChanged?.Invoke();
                return IntPtr.Zero;
            case NativeMethods.WM_POWERBROADCAST:
                if ((uint)wParam == NativeMethods.PBT_APMRESUMEAUTOMATIC)
                {
                    SystemChanged?.Invoke();
                }

                return IntPtr.Zero;
            case NativeMethods.WM_CLOSE:
                // The tray window is the app's anchor; never let a stray close kill it.
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var point);
        NativeMethods.SetForegroundWindow(_hwnd);

        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)1, "显示主窗口");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)2, "切换到浅色");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)3, "切换到深色");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
            var autoFlags = NativeMethods.MF_STRING | (AutoEnabled ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenuW(menu, autoFlags, (UIntPtr)4, "启用自动调度");
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, (UIntPtr)5, "退出 LightSwitch");

            var command = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_LEFTALIGN,
                point.X,
                point.Y,
                _hwnd,
                IntPtr.Zero);

            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case 1:
                    ShowRequested?.Invoke();
                    break;
                case 2:
                    LightRequested?.Invoke();
                    break;
                case 3:
                    DarkRequested?.Invoke();
                    break;
                case 4:
                    AutoToggleRequested?.Invoke();
                    break;
                case 5:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            if (_icon != IntPtr.Zero)
            {
                NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref _data);
            }

            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        if (_icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }

        s_instance = null;
        ShowRequested = null;
        ToggleThemeRequested = null;
        LightRequested = null;
        DarkRequested = null;
        AutoToggleRequested = null;
        ExitRequested = null;
        SystemChanged = null;
    }
}
