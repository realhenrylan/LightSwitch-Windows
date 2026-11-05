using System.Diagnostics;
using System.IO;
using LightSwitch.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace LightSwitch.App;

/// <summary>
/// Owns the long-lived services and the tray icon. The main window is created
/// lazily and can be closed back to the tray without tearing down the process.
/// </summary>
public sealed class AppHost
{
    private readonly DispatcherQueue _dispatcher;
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly WallpaperService _wallpaperService;
    private readonly NightLightService _nightLightService;
    private readonly StartupService _startupService;
    private readonly NightLightWatcher _nightLightWatcher;
    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueueTimer _systemEventDebounce;
    private readonly bool _startHidden;

    private TrayHost? _tray;
    private MainWindow? _window;
    private bool _exiting;

    public AppHost(string[] args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("AppHost requires the UI thread.");

        Log.Initialize(AppPaths.LogPath);
        Log.Info($"LightSwitch {typeof(AppHost).Assembly.GetName().Version} starting; args: {string.Join(' ', args)}");

        _settingsService = new SettingsService();
        Settings = _settingsService.Load();

        _themeService = new ThemeService();
        _wallpaperService = new WallpaperService();
        _nightLightService = new NightLightService();
        _startupService = new StartupService();
        Engine = new AppearanceEngine(Settings, _themeService, _nightLightService, _wallpaperService);

        _nightLightWatcher = new NightLightWatcher();
        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);

        // Coalesce system notifications: a single theme change makes Explorer,
        // DWM and our own broadcast send several WM_SETTINGCHANGE messages.
        _systemEventDebounce = _dispatcher.CreateTimer();
        _systemEventDebounce.Interval = TimeSpan.FromMilliseconds(500);
        _systemEventDebounce.IsRepeating = false;
        _systemEventDebounce.Tick += OnSystemEventDebounceTick;

        _startHidden = args.Any(a =>
            a.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--hidden", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
    }

    public AppSettings Settings { get; private set; }

    public AppearanceEngine Engine { get; }

    public string DataDirectory => AppPaths.DataDirectory;

    public event Action<EngineState>? StateUpdated;

    public void Start()
    {
        Engine.StateChanged += OnStateChanged;

        _nightLightWatcher.Changed += OnNightLightChanged;
        _nightLightWatcher.Start();

        _tray = new TrayHost();
        _tray.ShowRequested += ShowMainWindow;
        _tray.ToggleThemeRequested += () => Engine.ManualToggle();
        _tray.LightRequested += () => Engine.ManualApply(true);
        _tray.DarkRequested += () => Engine.ManualApply(false);
        _tray.AutoToggleRequested += ToggleAutoSchedule;
        _tray.ExitRequested += Exit;
        _tray.SystemChanged += OnSystemChanged;
        _tray.Create("LightSwitch");
        _tray.SetHotkey(Settings.HotkeyEnabled);

        if (Settings.RunAtStartup && !_startupService.IsEnabled)
        {
            if (!_startupService.TrySetEnabled(true, out var startupError))
            {
                Log.Warn($"Could not refresh the startup entry: {startupError}");
            }
        }

        _timer.Tick += OnTimerTick;
        _timer.Start();

        Engine.Evaluate(EvaluationTrigger.Startup);
        UpdateTray();

        if (!_startHidden)
        {
            ShowMainWindow();
        }
    }

    public void ShowMainWindow()
    {
        if (_exiting)
        {
            return;
        }

        if (_window is null)
        {
            var window = new MainWindow(this);
            window.AppWindow.Closing += OnWindowClosing;
            window.Closed += OnWindowClosed;
            _window = window;
        }

        if (_window.AppWindow.Presenter is OverlappedPresenter presenter &&
            presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        _window.AppWindow.Show();
        _window.Activate();
        NativeMethods.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window));
    }

    public void ManualApply(bool light) => Engine.ManualApply(light);

    public void RestoreAutomatic() => Engine.ClearManualOverride();

    public void ApplyWallpaperPreview(bool light) => Engine.ManualApplyWallpaper(light);

    public string? SaveSettings(AppSettings settings, bool evaluate = true)
    {
        try
        {
            settings.Normalize();
            var previousStartup = Settings.RunAtStartup;

            if (_settingsService.Save(settings) == SaveResult.Failed)
            {
                return _settingsService.LastError ?? "无法保存设置。";
            }

            Settings = settings.Clone();
            Engine.ApplySettings(Settings, evaluate);
            _tray?.SetHotkey(Settings.HotkeyEnabled);
            UpdateTray();

            if (Settings.RunAtStartup != previousStartup)
            {
                if (!_startupService.TrySetEnabled(Settings.RunAtStartup, out var startupError))
                {
                    return startupError ?? "无法更新开机启动项。";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Saving settings failed", ex);
            return ex.Message;
        }
    }

    public void ToggleAutoSchedule()
    {
        var settings = Settings.Clone();
        settings.ScheduleMode = settings.ScheduleMode == ScheduleMode.Off
            ? ScheduleMode.FixedHours
            : ScheduleMode.Off;
        var error = SaveSettings(settings);
        if (error is not null)
        {
            Log.Warn($"Toggle auto schedule failed: {error}");
        }
    }

    public void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open the data folder: {ex.Message}");
        }
    }

    public void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Log.Info("LightSwitch exiting.");

        try
        {
            _timer.Stop();
            _systemEventDebounce.Stop();
            Engine.StateChanged -= OnStateChanged;
            _nightLightWatcher.Changed -= OnNightLightChanged;
            _nightLightWatcher.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"Cleanup failed: {ex.Message}");
        }

        try
        {
            _tray?.Dispose();
            _tray = null;
        }
        catch
        {
            // Ignore.
        }

        try
        {
            _window?.Close();
        }
        catch
        {
            // Ignore.
        }

        Application.Current.Exit();
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!_exiting)
        {
            Engine.Evaluate(EvaluationTrigger.Timer);
        }
    }

    /// <summary>
    /// WM_SETTINGCHANGE is delivered synchronously by SendMessageTimeout, so the
    /// tray window procedure is re-entered in the middle of our own ApplyTheme.
    /// Evaluate later instead: the manual override is in place by then.
    /// </summary>
    private void OnSystemChanged()
    {
        _systemEventDebounce.Stop();   // Restart: several messages coalesce into one evaluation.
        _systemEventDebounce.Start();
    }

    private void OnSystemEventDebounceTick(DispatcherQueueTimer sender, object args)
    {
        if (!_exiting)
        {
            Engine.Evaluate(EvaluationTrigger.SystemEvent);
        }
    }

    private void OnNightLightChanged(object? sender, EventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (!_exiting)
            {
                Engine.Evaluate(EvaluationTrigger.NightLightChanged);
            }
        });
    }

    private void OnStateChanged(object? sender, EngineState state)
    {
        void Update()
        {
            if (_exiting)
            {
                return;
            }

            try
            {
                StateUpdated?.Invoke(state);
            }
            catch (Exception ex)
            {
                Log.Error("A UI state handler failed", ex);
            }

            UpdateTray();
        }

        if (_dispatcher.HasThreadAccess)
        {
            Update();
        }
        else
        {
            _dispatcher.TryEnqueue(Update);
        }
    }

    private void UpdateTray()
    {
        if (_tray is null)
        {
            return;
        }

        var state = Engine.State;
        var appearance = state.IsSystemLight ? "浅色" : "深色";
        var mode = state.Mode switch
        {
            ScheduleMode.FixedHours => "固定时间",
            ScheduleMode.SunsetToSunrise => "日出日落",
            ScheduleMode.FollowNightLight => "跟随夜灯",
            _ => "自动调度关闭",
        };

        var next = state.NextSwitchAt is { } time ? $"，下次 {time:HH:mm}" : string.Empty;
        var manual = state.IsManualOverride ? "，手动覆盖中" : string.Empty;
        _tray.AutoEnabled = Settings.ScheduleMode != ScheduleMode.Off;
        _tray.UpdateTooltip($"LightSwitch · {appearance} · {mode}{next}{manual}");
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting)
        {
            return;
        }

        if (Settings.CloseToTray)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
        if (!_exiting && !Settings.CloseToTray)
        {
            Exit();
        }
    }
}
