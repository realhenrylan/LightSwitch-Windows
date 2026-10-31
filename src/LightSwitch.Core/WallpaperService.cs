using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace LightSwitch.Core;

/// <summary>
/// Applies one global wallpaper to every monitor through the documented
/// IDesktopWallpaper shell COM API, with a SystemParametersInfo fallback.
/// </summary>
public sealed class WallpaperService : IWallpaperService
{
    private static readonly Guid DesktopWallpaperClsid = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");

    private const uint SpiSetDesktopWallpaper = 0x0014;
    private const uint SpiGetDesktopWallpaper = 0x0073;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendWinIniChange = 0x02;
    private const string DesktopKeyPath = @"Control Panel\Desktop";

    public WallpaperSnapshot GetSnapshot()
    {
        try
        {
            using var session = DesktopWallpaperSession.Create();
            var api = session.Api;
            var count = api.GetMonitorDevicePathCount();
            var ids = new List<string>((int)Math.Min(count, 16));
            var wallpapers = new List<string>((int)Math.Min(count, 16));

            for (uint i = 0; i < count; i++)
            {
                var id = api.GetMonitorDevicePathAt(i);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                ids.Add(id);
                wallpapers.Add(api.GetWallpaper(id) ?? string.Empty);
            }

            WallpaperPosition? position = null;
            try
            {
                position = api.GetPosition();
            }
            catch (Exception ex)
            {
                Log.Warn($"Unable to read wallpaper position: {ex.Message}");
            }

            return new WallpaperSnapshot(ids, wallpapers, position, GetGlobalWallpaper());
        }
        catch (Exception ex)
        {
            Log.Warn($"IDesktopWallpaper unavailable, falling back to SPI: {ex.Message}");
            return new WallpaperSnapshot(Array.Empty<string>(), Array.Empty<string>(), null, GetGlobalWallpaper());
        }
    }

    public WallpaperApplyResult Apply(string path, WallpaperPosition position)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new WallpaperApplyResult(false, 0, false, "壁纸文件不存在。");
        }

        try
        {
            using var session = DesktopWallpaperSession.Create();
            var api = session.Api;
            var count = api.GetMonitorDevicePathCount();
            if (count == 0)
            {
                throw new InvalidOperationException("系统未报告任何显示器。");
            }

            api.SetPosition(position);

            var updated = 0;
            for (uint i = 0; i < count; i++)
            {
                var id = api.GetMonitorDevicePathAt(i);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                api.SetWallpaper(id, path);
                updated++;
            }

            if (updated == 0)
            {
                throw new InvalidOperationException("没有可设置壁纸的显示器。");
            }

            return new WallpaperApplyResult(true, updated, false, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"IDesktopWallpaper failed ({ex.Message}); trying SystemParametersInfo");
            var fallback = ApplyViaSystemParameters(path, position);
            return fallback with { Error = fallback.Success ? null : $"{ex.Message} {fallback.Error}".Trim() };
        }
    }

    public bool Matches(WallpaperSnapshot snapshot, string path, WallpaperPosition position)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var target = NormalizePath(path);
        if (snapshot.HasPerMonitorApi)
        {
            if (snapshot.Position is { } currentPosition && currentPosition != position)
            {
                return false;
            }

            if (snapshot.MonitorWallpapers.Count == 0)
            {
                return false;
            }

            foreach (var wallpaper in snapshot.MonitorWallpapers)
            {
                if (!PathEquals(wallpaper, target))
                {
                    return false;
                }
            }

            return true;
        }

        // SPI_GETDESKWALLPAPER usually reports ...\TranscodedWallpaper, which does not
        // identify the source image. Treat it as unknown and re-apply; this is cheap.
        return false;
    }

    private static WallpaperApplyResult ApplyViaSystemParameters(string path, WallpaperPosition position)
    {
        try
        {
            var (style, tile) = StyleFor(position);
            using (var key = Registry.CurrentUser.CreateSubKey(DesktopKeyPath, writable: true))
            {
                key?.SetValue("WallpaperStyle", style, RegistryValueKind.String);
                key?.SetValue("TileWallpaper", tile, RegistryValueKind.String);
            }

            if (!SystemParametersInfo(SpiSetDesktopWallpaper, 0, path,
                    SpifUpdateIniFile | SpifSendWinIniChange))
            {
                var error = Marshal.GetLastWin32Error();
                return new WallpaperApplyResult(false, 0, true, $"SystemParametersInfo 失败（错误 {error}）。");
            }

            return new WallpaperApplyResult(true, 1, true, null);
        }
        catch (Exception ex)
        {
            Log.Error("Wallpaper fallback failed", ex);
            return new WallpaperApplyResult(false, 0, true, ex.Message);
        }
    }

    private static (string Style, string Tile) StyleFor(WallpaperPosition position) => position switch
    {
        WallpaperPosition.Center => ("0", "0"),
        WallpaperPosition.Tile => ("0", "1"),
        WallpaperPosition.Stretch => ("2", "0"),
        WallpaperPosition.Fit => ("6", "0"),
        WallpaperPosition.Span => ("22", "0"),
        _ => ("10", "0"),
    };

    private static string? GetGlobalWallpaper()
    {
        try
        {
            var buffer = new StringBuilder(32768);
            if (SystemParametersInfo(SpiGetDesktopWallpaper, (uint)buffer.Capacity, buffer, 0) && buffer.Length > 0)
            {
                return buffer.ToString();
            }
        }
        catch
        {
            // Ignore.
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim().Trim('"');
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    private static bool PathEquals(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        return string.Equals(NormalizePath(left), right, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
            [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        NativeRect GetMonitorRect([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

        void SetBackgroundColor(uint color);

        uint GetBackgroundColor();

        void SetPosition(WallpaperPosition position);

        WallpaperPosition GetPosition();

        void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed class DesktopWallpaperSession : IDisposable
    {
        private object? _com;

        private DesktopWallpaperSession(object com, IDesktopWallpaper api)
        {
            _com = com;
            Api = api;
        }

        public IDesktopWallpaper Api { get; }

        public static DesktopWallpaperSession Create()
        {
            var type = Type.GetTypeFromCLSID(DesktopWallpaperClsid, throwOnError: true);
            var com = Activator.CreateInstance(type!);
            if (com is not IDesktopWallpaper api)
            {
                if (com is not null && Marshal.IsComObject(com))
                {
                    Marshal.FinalReleaseComObject(com);
                }

                throw new InvalidCastException("无法创建 IDesktopWallpaper COM 对象。");
            }

            return new DesktopWallpaperSession(com, api);
        }

        public void Dispose()
        {
            var com = _com;
            _com = null;
            if (com is not null && Marshal.IsComObject(com))
            {
                try
                {
                    Marshal.FinalReleaseComObject(com);
                }
                catch
                {
                    // Nothing useful to do.
                }
            }
        }
    }
}
