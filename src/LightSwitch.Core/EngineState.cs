namespace LightSwitch.Core;

/// <summary>
/// Immutable snapshot of the appearance engine. The UI renders this and never
/// touches the services directly, which keeps all state changes in one place.
/// </summary>
public sealed record EngineState
{
    public ScheduleMode Mode { get; init; } = ScheduleMode.Off;
    public bool IsSystemLight { get; init; }
    public bool IsAppsLight { get; init; }
    public bool NightLightActive { get; init; }
    public bool IsManualOverride { get; init; }
    public DateTime? ManualUntil { get; init; }
    public DateTime? NextSwitchAt { get; init; }
    public bool IsLightTarget { get; init; }
    public SunTimes? SunTimes { get; init; }
    public DateTime LastEvaluation { get; init; }
    public string? LastError { get; init; }
    public string? LastAction { get; init; }
    public bool ChangeWallpaper { get; init; }
    public string? ActiveWallpaper { get; init; }
}
