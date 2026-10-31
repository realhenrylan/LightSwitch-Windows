using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LightSwitch.Core;

/// <summary>
/// Writes the same HKCU values the Windows Settings app uses and broadcasts the
/// messages Explorer / DWM listen to. Uses only documented registry values.
/// </summary>
public sealed class ThemeService : IThemeService
{
    public const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string SystemValue = "SystemUsesLightTheme";
    private const string AppsValue = "AppsUseLightTheme";
    private const string ColorPrevalence = "ColorPrevalence";

    private const uint WmSettingChange = 0x001A;
    private const uint WmThemeChanged = 0x031A;
    private const uint WmDwmColorizationColorChanged = 0x0320;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint SendTimeoutMs = 5000;
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);

    public bool? GetSystemLight() => ReadDword(SystemValue) is int value ? value != 0 : null;

    public bool? GetAppsLight() => ReadDword(AppsValue) is int value ? value != 0 : null;

    public bool SetSystemLight(bool light)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizePath, writable: true);
            if (key is null)
            {
                return false;
            }

            key.SetValue(SystemValue, light ? 1 : 0, RegistryValueKind.DWord);
            if (light)
            {
                // The accent colour on the taskbar only exists in dark mode.
                key.SetValue(ColorPrevalence, 0, RegistryValueKind.DWord);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to set the system theme", ex);
            return false;
        }
    }

    public bool SetAppsLight(bool light)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizePath, writable: true);
            if (key is null)
            {
                return false;
            }

            key.SetValue(AppsValue, light ? 1 : 0, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to set the app theme", ex);
            return false;
        }
    }

    public void BroadcastThemeChanged()
    {
        // Explorer, DWM and open apps all key off different messages; send the same trio as the C++ build.
        SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveColorSet",
            SmtoAbortIfHung, SendTimeoutMs, out _);
        SendMessageTimeout(HwndBroadcast, WmThemeChanged, IntPtr.Zero, IntPtr.Zero,
            SmtoAbortIfHung, SendTimeoutMs, out _);
        SendMessageTimeout(HwndBroadcast, WmDwmColorizationColorChanged, IntPtr.Zero, IntPtr.Zero,
            SmtoAbortIfHung, SendTimeoutMs, out _);
    }

    private static int? ReadDword(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath);
            return key?.GetValue(name) is int value ? value : null;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
