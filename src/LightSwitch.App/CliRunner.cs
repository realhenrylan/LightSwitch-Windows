using System.IO;
using LightSwitch.Core;

namespace LightSwitch.App;

/// <summary>Executes the one-shot commands without starting WinUI at all.</summary>
internal static class CliRunner
{
    private const string TrayWindowClass = "LightSwitch.TrayHost";

    public static bool TryForward(CommandLine command)
    {
        // Startup changes belong to the on-disk configuration, not to the
        // running tray instance, so they are always executed here.
        if (command.StartupPreference is not null)
        {
            return false;
        }

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
            var settingsService = new SettingsService();
            var settings = settingsService.Load();

            if (command.StartupPreference is bool enabled)
            {
                settings.RunAtStartup = enabled;
                settingsService.Save(settings);
                if (settingsService.LastError is not null)
                {
                    Log.Error($"Could not save the start-at-sign-in setting: {settingsService.LastError}");
                    Environment.ExitCode = 1;
                    return;
                }

                if (!new StartupService().TrySetEnabled(enabled, out var startupError))
                {
                    Log.Error($"Could not update the start-at-sign-in registry entry: {startupError}");
                    Environment.ExitCode = 1;
                    return;
                }

                Log.Info($"Start at sign-in {(enabled ? "enabled" : "disabled")}.");
                return;
            }

            var theme = new ThemeService();
            var current = theme.GetSystemLight() ?? theme.GetAppsLight() ?? false;
            var light = command.Light || (!command.Dark && !current);

            theme.SetSystemLight(light);
            theme.SetAppsLight(light);
            theme.BroadcastThemeChangedSync();

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
