using System.IO;
using LightSwitch.Core;

namespace LightSwitch.App;

/// <summary>Executes the one-shot commands without starting WinUI at all.</summary>
internal static class CliRunner
{
    private const string TrayWindowClass = "LightSwitch.TrayHost";

    public static bool TryForward(CommandLine command)
    {
        var hwnd = NativeMethods.FindWindowW(TrayWindowClass, null);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var message = command.Toggle
            ? NativeMethods.MsgToggleTheme
            : command.Light
                ? NativeMethods.MsgApplyLight
                : NativeMethods.MsgApplyDark;

        NativeMethods.PostMessageW(hwnd, (uint)message, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    public static void SignalShow()
    {
        var hwnd = NativeMethods.FindWindowW(TrayWindowClass, null);
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessageW(hwnd, NativeMethods.MsgShow, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void Execute(CommandLine command)
    {
        try
        {
            Log.Initialize(AppPaths.LogPath);
            var settings = new SettingsService().Load();
            var theme = new ThemeService();
            var current = theme.GetSystemLight() ?? theme.GetAppsLight() ?? false;
            var light = command.Light || (!command.Dark && !current);

            theme.SetSystemLight(light);
            theme.SetAppsLight(light);
            theme.BroadcastThemeChanged();

            if (settings.ChangeWallpaper)
            {
                var path = light ? settings.LightWallpaper : settings.DarkWallpaper;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    var result = new WallpaperService().Apply(path, settings.WallpaperPosition);
                    if (!result.Success)
                    {
                        Log.Warn($"Wallpaper apply failed: {result.Error}");
                    }
                }
            }

            Log.Info($"One-shot CLI applied {(light ? "light" : "dark")} appearance.");
        }
        catch (Exception ex)
        {
            Log.Error("One-shot command failed", ex);
        }
    }
}
