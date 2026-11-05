namespace LightSwitch.Core;

/// <summary>
/// Decides which appearance should be active and applies it. All state lives
/// here; the UI and tray are pure views. Evaluation is cheap and can be called
/// from any thread (the services are thread-safe enough for this use).
/// </summary>
public sealed class AppearanceEngine
{
    private readonly object _gate = new();
    private readonly IThemeService _theme;
    private readonly INightLightService _nightLight;
    private readonly IWallpaperService _wallpaper;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _timeZone;

    private AppSettings _settings;
    private EngineState _state;
    private DateTime? _manualUntil;
    private bool? _manualLight;
    private SunTimes? _sunTimes;
    private DateTime _sunDate;
    private bool _targetLight;
    private string? _lastError;
    private string? _lastAction;

    // WM_SETTINGCHANGE can be delivered synchronously (other processes broadcast
    // it, and the one-shot CLI path sends it inline), so our own tray window can
    // call Evaluate while _gate is still held by this thread. Those requests are
    // deferred and replayed once the outer operation has finished.
    private const int MaxReentrantPasses = 3;
    private int _reentrantRequest;

    public AppearanceEngine(
        AppSettings settings,
        IThemeService theme,
        INightLightService nightLight,
        IWallpaperService wallpaper,
        IClock? clock = null,
        TimeZoneInfo? timeZone = null)
    {
        _settings = settings.Clone();
        _settings.Normalize();
        _theme = theme;
        _nightLight = nightLight;
        _wallpaper = wallpaper;
        _clock = clock ?? new SystemClock();
        _timeZone = timeZone ?? TimeZoneInfo.Local;

        _targetLight = _theme.GetSystemLight() ?? _theme.GetAppsLight() ?? false;
        _state = BuildState(_clock.Now);
    }

    public event EventHandler<EngineState>? StateChanged;

    public EngineState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public void ApplySettings(AppSettings settings, bool evaluate = true)
    {
        EngineState state;
        lock (_gate)
        {
            var next = settings.Clone();
            next.Normalize();
            if (ScheduleChanged(_settings, next))
            {
                // Only a change that moves the schedule invalidates a manual
                // choice; toggling the hotkey or the tray options must not undo it.
                _manualUntil = null;
                _manualLight = null;
                _sunDate = default;
            }

            _settings = next;
            state = evaluate ? EvaluateLocked(EvaluationTrigger.SettingsChanged) : BuildState(_clock.Now);
            _state = state;
        }

        PublishIfChanged(state);
        FlushReentrant();
    }

    private static bool ScheduleChanged(AppSettings a, AppSettings b) =>
        a.ScheduleMode != b.ScheduleMode ||
        a.LightTime != b.LightTime ||
        a.DarkTime != b.DarkTime ||
        a.SunriseOffset != b.SunriseOffset ||
        a.SunsetOffset != b.SunsetOffset ||
        !string.Equals(a.Latitude, b.Latitude, StringComparison.Ordinal) ||
        !string.Equals(a.Longitude, b.Longitude, StringComparison.Ordinal);

    public void Evaluate(EvaluationTrigger trigger)
    {
        if (TryDeferReentrant(trigger))
        {
            return;
        }

        EvaluateAndPublish(trigger);
        FlushReentrant();
    }

    private void EvaluateAndPublish(EvaluationTrigger trigger)
    {
        EngineState state;
        lock (_gate)
        {
            state = EvaluateLocked(trigger);
        }

        PublishIfChanged(state);
    }

    /// <summary>
    /// Detects a callback that arrives while this thread still holds the gate -
    /// our own <see cref="ApplyTheme"/> broadcast re-entering the engine - and
    /// defers it instead of evaluating in the middle of the current operation.
    /// </summary>
    private bool TryDeferReentrant(EvaluationTrigger trigger)
    {
        if (!Monitor.IsEntered(_gate))
        {
            return false;
        }

        Interlocked.Exchange(ref _reentrantRequest, 1);
        Log.Info($"Deferred re-entrant evaluation ({trigger})");
        return true;
    }

    /// <summary>Replays a deferred request now that the outer operation is complete.</summary>
    private void FlushReentrant()
    {
        for (var pass = 0; pass < MaxReentrantPasses; pass++)
        {
            if (Interlocked.Exchange(ref _reentrantRequest, 0) == 0)
            {
                return;
            }

            EvaluateAndPublish(EvaluationTrigger.SystemEvent);
        }

        Interlocked.Exchange(ref _reentrantRequest, 0);
    }

    /// <summary>Applies one appearance immediately and pauses automatic changes until the next boundary.</summary>
    public void ManualApply(bool light)
    {
        EngineState state;
        lock (_gate)
        {
            var now = _clock.Now;
            _lastError = null;
            _lastAction = null;
            EnsureSunTimes(now);
            var (lightTime, darkTime) = EffectiveTimes();

            // Establish the override before applying anything: the theme
            // broadcast inside ApplyTheme is delivered synchronously and can
            // call straight back into Evaluate.
            _targetLight = light;
            _manualLight = light;
            _manualUntil = _settings.ScheduleMode is ScheduleMode.FixedHours or ScheduleMode.SunsetToSunrise
                ? ScheduleMath.NextBoundary(now, lightTime, darkTime)
                : null;

            ApplyTheme(light, respectTargetFlags: true);
            ApplyWallpaper(light);

            _lastAction = light ? "已手动切换到浅色外观" : "已手动切换到深色外观";
            Log.Info($"Manual appearance applied: {(light ? "light" : "dark")}; override until {_manualUntil?.ToString("yyyy-MM-dd HH:mm") ?? "n/a"}");
            state = BuildState(now);
            _state = state;
        }

        PublishIfChanged(state);
        FlushReentrant();
    }

    public void ManualToggle()
    {
        var current = _theme.GetSystemLight() ?? _theme.GetAppsLight() ?? false;
        ManualApply(!current);
    }

    /// <summary>Applies just the light/dark wallpaper, e.g. from the "preview" buttons in settings.</summary>
    public void ManualApplyWallpaper(bool light)
    {
        EngineState state;
        lock (_gate)
        {
            var now = _clock.Now;
            _lastError = null;
            _lastAction = null;
            ApplyWallpaper(light, force: true);
            state = BuildState(now);
            _state = state;
        }

        PublishIfChanged(state);
        FlushReentrant();
    }

    public void ClearManualOverride()
    {
        EngineState state;
        lock (_gate)
        {
            _manualUntil = null;
            _manualLight = null;
            _lastAction = "已恢复自动调度";
            state = EvaluateLocked(EvaluationTrigger.SettingsChanged);
        }

        PublishIfChanged(state);
        FlushReentrant();
    }

    /// <summary>Recomputes the snapshot without changing anything.</summary>
    public void Refresh()
    {
        EngineState state;
        lock (_gate)
        {
            state = BuildState(_clock.Now);
            _state = state;
        }

        PublishIfChanged(state);
    }

    private EngineState EvaluateLocked(EvaluationTrigger trigger)
    {
        var now = _clock.Now;
        var settings = _settings;
        _lastError = null;
        _lastAction = null;

        EnsureSunTimes(now);
        var (lightTime, darkTime) = EffectiveTimes();

        if (_manualUntil is { } until)
        {
            if (now < until)
            {
                _targetLight = _theme.GetSystemLight() ?? _targetLight;
                if (_manualLight is { } manual)
                {
                    // A wallpaper path/position edit during the override must still
                    // reach the manually chosen side; Matches dedupes when nothing changed.
                    ApplyWallpaper(manual, force: false);
                }

                _state = BuildState(now);
                return _state;
            }

            _manualUntil = null;
            _manualLight = null;
            Log.Info("Manual override expired; resuming the schedule");
        }

        if (settings.ScheduleMode == ScheduleMode.Off)
        {
            _targetLight = _theme.GetSystemLight() ?? _theme.GetAppsLight() ?? _targetLight;
            // Off still keeps the wallpaper in sync with the appearance Windows is in.
            ApplyWallpaper(_targetLight, force: false);
            _state = BuildState(now);
            return _state;
        }

        bool shouldBeLight;
        switch (settings.ScheduleMode)
        {
            case ScheduleMode.FollowNightLight:
                shouldBeLight = !_nightLight.IsActive;
                break;
            case ScheduleMode.SunsetToSunrise when _sunTimes is not null:
                shouldBeLight = ScheduleMath.ShouldBeLight(now.Hour * 60 + now.Minute, lightTime, darkTime);
                break;
            default:
                shouldBeLight = ScheduleMath.ShouldBeLight(now.Hour * 60 + now.Minute, lightTime, darkTime);
                break;
        }

        _targetLight = shouldBeLight;
        var themeChanged = ApplyTheme(shouldBeLight, respectTargetFlags: true);
        var wallpaperChanged = ApplyWallpaper(shouldBeLight, force: false);

        _lastAction = themeChanged || wallpaperChanged
            ? $"已切换到{(shouldBeLight ? "浅色" : "深色")}外观"
            : "外观已是最新";

        _state = BuildState(now);

        // The 30 s timer evaluates even when nothing changed; logging every one of
        // those lines rotated the 512 KB log in under a day. Keep the ones that
        // carry information: a changed result or a non-timer trigger.
        if (trigger != EvaluationTrigger.Timer || themeChanged || wallpaperChanged)
        {
            Log.Info($"Evaluated {trigger}: {settings.ScheduleMode}, target={(shouldBeLight ? "light" : "dark")}, " +
                     $"themeChanged={themeChanged}, wallpaperChanged={wallpaperChanged}");
        }

        return _state;
    }

    private bool ApplyTheme(bool light, bool respectTargetFlags)
    {
        var changed = false;
        if (respectTargetFlags && _settings.ChangeSystem)
        {
            if (_theme.GetSystemLight() != light)
            {
                changed |= _theme.SetSystemLight(light);
            }
        }

        if (respectTargetFlags && _settings.ChangeApps)
        {
            if (_theme.GetAppsLight() != light)
            {
                changed |= _theme.SetAppsLight(light);
            }
        }

        if (changed)
        {
            _theme.BroadcastThemeChanged();
        }

        return changed;
    }

    private bool ApplyWallpaper(bool light, bool force = false)
    {
        if (!_settings.ChangeWallpaper && !force)
        {
            return false;
        }

        var path = light ? _settings.LightWallpaper : _settings.DarkWallpaper;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!File.Exists(path))
        {
            _lastError = $"壁纸文件不存在：{path}";
            Log.Warn(_lastError);
            return false;
        }

        if (!force)
        {
            var snapshot = _wallpaper.GetSnapshot();
            if (_wallpaper.Matches(snapshot, path, _settings.WallpaperPosition))
            {
                return false;
            }
        }

        var result = _wallpaper.Apply(path, _settings.WallpaperPosition);
        if (!result.Success)
        {
            _lastError = result.Error ?? "壁纸设置失败。";
            Log.Warn($"Wallpaper apply failed: {result.Error}");
            return false;
        }

        Log.Info($"Wallpaper applied ({(light ? "light" : "dark")}): {path} " +
                 $"{_settings.WallpaperPosition}" + (result.UsedFallback ? " [fallback]" : string.Empty));
        return true;
    }

    private void EnsureSunTimes(DateTime now)
    {
        if (_settings.ScheduleMode != ScheduleMode.SunsetToSunrise)
        {
            return;
        }

        if (_sunTimes is not null && _sunDate == now.Date)
        {
            return;
        }

        if (!_settings.TryGetCoordinates(out var latitude, out var longitude))
        {
            _lastError = "经纬度无效，已改用自定义的亮/暗时间。";
            _sunTimes = null;
            _sunDate = now.Date;
            Log.Warn(_lastError);
            return;
        }

        var calculated = SunCalculator.Calculate(now.Date, latitude, longitude, _timeZone);
        if (calculated is null)
        {
            _lastError = "当前日期该纬度没有日出或日落（极昼/极夜），已临时使用 06:00 / 18:00。";
            _sunTimes = new SunTimes(360, 1080);
            _sunDate = now.Date;
            Log.Warn(_lastError);
            return;
        }

        _sunTimes = calculated;
        _sunDate = now.Date;
        Log.Info($"Sun times for {now:yyyy-MM-dd}: sunrise {ScheduleMath.FormatMinutes(calculated.Value.SunriseMinutes)}, " +
                 $"sunset {ScheduleMath.FormatMinutes(calculated.Value.SunsetMinutes)}");
    }

    private (int Light, int Dark) EffectiveTimes()
    {
        if (_settings.ScheduleMode == ScheduleMode.SunsetToSunrise && _sunTimes is { } times)
        {
            return (ScheduleMath.NormalizeMinutes(times.SunriseMinutes + _settings.SunriseOffset),
                ScheduleMath.NormalizeMinutes(times.SunsetMinutes + _settings.SunsetOffset));
        }

        return (_settings.LightTime, _settings.DarkTime);
    }

    private EngineState BuildState(DateTime now)
    {
        var (lightTime, darkTime) = EffectiveTimes();
        var sunTimes = _settings.ScheduleMode == ScheduleMode.SunsetToSunrise && _sunTimes is { } times
            ? new SunTimes(lightTime, darkTime)
            : (SunTimes?)null;

        DateTime? nextSwitch = _settings.ScheduleMode switch
        {
            ScheduleMode.FixedHours or ScheduleMode.SunsetToSunrise
                when _manualUntil is null => ScheduleMath.NextBoundary(now, lightTime, darkTime),
            _ => null,
        };

        return new EngineState
        {
            Mode = _settings.ScheduleMode,
            IsSystemLight = _theme.GetSystemLight() ?? false,
            IsAppsLight = _theme.GetAppsLight() ?? false,
            NightLightActive = _settings.ScheduleMode == ScheduleMode.FollowNightLight && _nightLight.IsActive,
            IsManualOverride = _manualUntil is { } until && now < until,
            ManualUntil = _manualUntil,
            ManualLight = _manualUntil is { } manualUntil && now < manualUntil ? _manualLight : null,
            NextSwitchAt = nextSwitch,
            IsLightTarget = _targetLight,
            SunTimes = sunTimes,
            LastEvaluation = now,
            LastError = _lastError,
            LastAction = _lastAction,
            ChangeWallpaper = _settings.ChangeWallpaper,
            ActiveWallpaper = _settings.ChangeWallpaper
                ? (_targetLight ? _settings.LightWallpaper : _settings.DarkWallpaper)
                : null,
        };
    }

    private void PublishIfChanged(EngineState state)
    {
        var handler = StateChanged;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(this, state);
        }
        catch (Exception ex)
        {
            Log.Error("StateChanged handler failed", ex);
        }
    }
}
