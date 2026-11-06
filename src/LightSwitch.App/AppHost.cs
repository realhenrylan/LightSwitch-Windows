using System.Diagnostics;
using System.IO;
using LightSwitch.Core;
using LightSwitch.Core.Updates;
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
    private readonly UpdateService _updateService;
    private readonly CancellationTokenSource _updateCancellation = new();
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private readonly bool _startHidden;

    private TrayHost? _tray;
    private MainWindow? _window;
    private bool _exiting;

    public const string ReleasesPage = "https://github.com/realhenrylan/LightSwitch-Windows/releases";

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

        var assemblyVersion = typeof(AppHost).Assembly.GetName().Version ?? new Version(0, 0, 0);
        CurrentVersion = SemanticVersion.Parse(
            $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}");
        InstallationMode = InstallationModeDetector.Detect(AppPaths.ExecutablePath);
        _updateService = new UpdateService(
            CurrentVersion,
            InstallationMode,
            new UpdateStateStore(),
            new GitHubReleaseClient(),
            new UpdateDownloader(),
            AppPaths.UpdatesDirectory);

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

    public SemanticVersion CurrentVersion { get; }

    public InstallationMode InstallationMode { get; }

    public UpdateCheckResult? LastUpdateCheck { get; private set; }

    public event Action<EngineState>? StateUpdated;

    /// <summary>Raised after the automatic startup check finishes, marshalled to the UI thread.</summary>
    public event Action<UpdateCheckResult>? BackgroundUpdateChecked;

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

        if (Settings.CheckForUpdates)
        {
            _ = RunBackgroundUpdateCheckAsync();
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

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool manual, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            _updateCancellation.Token);
        await _updateGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var result = await _updateService.CheckAsync(manual, linked.Token).ConfigureAwait(false);
            LastUpdateCheck = result;

            if (!manual)
            {
                NotifyBackgroundUpdate(result);
            }

            return result;
        }
        finally
        {
            _updateGate.Release();
        }
    }

    public bool IsUpdatePostponed(ReleaseInfo release) => _updateService.IsPostponed(release.Version);

    public void PostponeUpdate(ReleaseInfo release) => _updateService.Postpone(release.Version);

    public Task<UpdateDownloadResult> DownloadUpdateAsync(ReleaseInfo release, IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        _updateService.DownloadAsync(release, progress, cancellationToken);

    public void OpenDownloadedFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true,
                });
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
                {
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open the downloaded update: {ex.Message}");
        }
    }

    /// <summary>Starts the verified installer only after the user explicitly confirmed it.</summary>
    public string? RunDownloadedInstaller(string path)
    {
        if (!File.Exists(path))
        {
            return "安装程序不存在，请重新下载更新。";
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Could not start the downloaded installer", ex);
            return $"无法启动安装程序：{ex.Message}";
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
            _updateCancellation.Cancel();
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not cancel the update task: {ex.Message}");
        }

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

    private async Task RunBackgroundUpdateCheckAsync()
    {
        try
        {
            await CheckForUpdatesAsync(manual: false, _updateCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The app is exiting.
        }
        catch (Exception ex)
        {
            Log.Error("Background update check failed", ex);
        }
    }

    private void NotifyBackgroundUpdate(UpdateCheckResult result)
    {
        void Notify()
        {
            if (_exiting)
            {
                return;
            }

            try
            {
                BackgroundUpdateChecked?.Invoke(result);
            }
            catch (Exception ex)
            {
                Log.Error("An update UI handler failed", ex);
            }
        }

        if (_dispatcher.HasThreadAccess)
        {
            Notify();
        }
        else
        {
            _dispatcher.TryEnqueue(Notify);
        }
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!_exiting)
        {
            Engine.Evaluate(EvaluationTrigger.Timer);
        }
    }

    /// <summary>
    /// WM_SETTINGCHANGE arrives from the background broadcast pump and from
    /// other processes. Coalesce a burst of messages through a short timer so a
    /// single theme change does not trigger several evaluations, and so the
    /// tray window procedure never runs business logic inline.
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
