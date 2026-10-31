using System.Text.Json.Serialization;

namespace LightSwitch.Core;

/// <summary>
/// Persisted user configuration. Property names are camelCase by convention;
/// the two snake_case offsets are kept for backward compatibility with the
/// original C++ build. Treat instances as immutable once handed to the engine:
/// the UI edits a <see cref="Clone"/> and passes the finished copy back.
/// </summary>
public sealed class AppSettings
{
    [JsonPropertyName("scheduleMode")]
    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Off;

    [JsonPropertyName("changeSystem")]
    public bool ChangeSystem { get; set; } = true;

    [JsonPropertyName("changeApps")]
    public bool ChangeApps { get; set; } = true;

    /// <summary>Light-theme start in minutes after midnight (0-1439).</summary>
    [JsonPropertyName("lightTime")]
    public int LightTime { get; set; } = 480;

    /// <summary>Dark-theme start in minutes after midnight (0-1439).</summary>
    [JsonPropertyName("darkTime")]
    public int DarkTime { get; set; } = 1200;

    [JsonPropertyName("latitude")]
    public string Latitude { get; set; } = "39.9";

    [JsonPropertyName("longitude")]
    public string Longitude { get; set; } = "116.4";

    [JsonPropertyName("sunrise_offset")]
    public int SunriseOffset { get; set; }

    [JsonPropertyName("sunset_offset")]
    public int SunsetOffset { get; set; }

    // ---- Wallpaper (new in the WinUI 3 rewrite) ----

    [JsonPropertyName("changeWallpaper")]
    public bool ChangeWallpaper { get; set; }

    [JsonPropertyName("lightWallpaper")]
    public string LightWallpaper { get; set; } = string.Empty;

    [JsonPropertyName("darkWallpaper")]
    public string DarkWallpaper { get; set; } = string.Empty;

    [JsonPropertyName("wallpaperPosition")]
    public WallpaperPosition WallpaperPosition { get; set; } = WallpaperPosition.Fill;

    // ---- General ----

    [JsonPropertyName("runAtStartup")]
    public bool RunAtStartup { get; set; }

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = true;

    [JsonPropertyName("closeToTray")]
    public bool CloseToTray { get; set; } = true;

    [JsonPropertyName("hotkeyEnabled")]
    public bool HotkeyEnabled { get; set; } = true;

    [JsonIgnore]
    public bool HasLightWallpaper => !string.IsNullOrWhiteSpace(LightWallpaper);

    [JsonIgnore]
    public bool HasDarkWallpaper => !string.IsNullOrWhiteSpace(DarkWallpaper);

    public AppSettings Clone() => new()
    {
        ScheduleMode = ScheduleMode,
        ChangeSystem = ChangeSystem,
        ChangeApps = ChangeApps,
        LightTime = LightTime,
        DarkTime = DarkTime,
        Latitude = Latitude,
        Longitude = Longitude,
        SunriseOffset = SunriseOffset,
        SunsetOffset = SunsetOffset,
        ChangeWallpaper = ChangeWallpaper,
        LightWallpaper = LightWallpaper,
        DarkWallpaper = DarkWallpaper,
        WallpaperPosition = WallpaperPosition,
        RunAtStartup = RunAtStartup,
        MinimizeToTray = MinimizeToTray,
        CloseToTray = CloseToTray,
        HotkeyEnabled = HotkeyEnabled,
    };

    /// <summary>Clamps out-of-range values that may come from a hand-edited file.</summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(ScheduleMode))
        {
            ScheduleMode = ScheduleMode.Off;
        }

        if (!Enum.IsDefined(WallpaperPosition))
        {
            WallpaperPosition = WallpaperPosition.Fill;
        }

        LightTime = Math.Clamp(LightTime, 0, 1439);
        DarkTime = Math.Clamp(DarkTime, 0, 1439);
        SunriseOffset = Math.Clamp(SunriseOffset, -720, 720);
        SunsetOffset = Math.Clamp(SunsetOffset, -720, 720);
        Latitude = (Latitude ?? string.Empty).Trim();
        Longitude = (Longitude ?? string.Empty).Trim();
        LightWallpaper = (LightWallpaper ?? string.Empty).Trim();
        DarkWallpaper = (DarkWallpaper ?? string.Empty).Trim();
    }

    public bool TryGetCoordinates(out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (!double.TryParse(Latitude, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out latitude) ||
            !double.TryParse(Longitude, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out longitude))
        {
            return false;
        }

        if (double.IsNaN(latitude) || double.IsNaN(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return false;
        }

        // The original app also rejected 0,0 as "not configured".
        return !(Math.Abs(latitude) < double.Epsilon && Math.Abs(longitude) < double.Epsilon);
    }
}
