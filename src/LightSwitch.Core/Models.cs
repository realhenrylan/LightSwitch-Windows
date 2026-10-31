namespace LightSwitch.Core;

/// <summary>How LightSwitch decides between the light and the dark appearance.</summary>
public enum ScheduleMode
{
    Off = 0,
    FixedHours = 1,
    SunsetToSunrise = 2,
    FollowNightLight = 3,
}

/// <summary>Mirrors the Win32 WALLPAPER_POSITION values.</summary>
public enum WallpaperPosition
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

/// <summary>Why the engine ran an evaluation. Only used for logging and the state snapshot.</summary>
public enum EvaluationTrigger
{
    Startup = 0,
    Timer = 1,
    SettingsChanged = 2,
    ManualTheme = 3,
    ManualWallpaper = 4,
    NightLightChanged = 5,
    SystemEvent = 6,
    Resume = 7,
}

/// <summary>A local-time sunrise/sunset pair expressed as minutes after midnight.</summary>
public readonly record struct SunTimes(int SunriseMinutes, int SunsetMinutes);

/// <summary>What the desktop wallpaper currently looks like.</summary>
public sealed record WallpaperSnapshot(
    IReadOnlyList<string> MonitorIds,
    IReadOnlyList<string> MonitorWallpapers,
    WallpaperPosition? Position,
    string? GlobalWallpaper)
{
    public bool HasPerMonitorApi => MonitorIds.Count > 0;
}

public sealed record WallpaperApplyResult(
    bool Success,
    int UpdatedMonitors,
    bool UsedFallback,
    string? Error);
