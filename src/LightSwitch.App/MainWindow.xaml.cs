using System.Diagnostics;
using System.Globalization;
using System.IO;
using LightSwitch.Core;
using LightSwitch.Core.Updates;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;

namespace LightSwitch.App;

public sealed partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly NativeMethods.SubclassProc _subclassProc;
    private AppSettings _edit;
    private bool _initializing;
    private string? _lastError;
    private DispatcherQueueTimer? _commitTimer;
    private ReleaseInfo? _pendingUpdate;
    private string? _downloadedFilePath;
    private CancellationTokenSource? _downloadCancellation;
    private bool _checkingUpdate;
    private bool _downloadingUpdate;

    public MainWindow(AppHost host)
    {
        _host = host;
        _edit = host.Settings.Clone();
        _subclassProc = SubclassWndProc;

        InitializeComponent();
        ConfigureWindow();
        LoadSettingsIntoUi();

        _host.StateUpdated += OnHostStateUpdated;
        _host.BackgroundUpdateChecked += OnBackgroundUpdateChecked;
        RestoreUpdateState();
        UpdateState(_host.Engine.State);

        Closed += OnClosed;
        Activated += OnActivated;
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        Title = "LightSwitch";
        AppWindow.Title = "LightSwitch";
        TitleBarIcon.Source = LoadPreview(Path.Combine(AppContext.BaseDirectory, "Assets", "app.png"), 48);

        try
        {
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(icon))
            {
                AppWindow.SetIcon(icon);
            }
        }
        catch
        {
            // The icon is cosmetic.
        }

        try
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1040, 820));
            CenterWindow();
        }
        catch
        {
            // Keep the default size.
        }

        try
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            }
            else
            {
                // Mica is a Windows 11 feature. Keep the window readable on Windows 10.
                RootGrid.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
            }
        }
        catch
        {
            RootGrid.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
        }

        try
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
        catch
        {
            // Cosmetic only.
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.SetWindowSubclass(hwnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);
    }

    private void CenterWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var work = area.WorkArea;
        var size = AppWindow.Size;
        var x = work.X + Math.Max(0, (work.Width - size.Width) / 2);
        var y = work.Y + Math.Max(0, (work.Height - size.Height) / 2);
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private void LoadSettingsIntoUi()
    {
        _initializing = true;
        try
        {
            SelectComboByTag(ModeCombo, _edit.ScheduleMode.ToString());
            SelectComboByTag(PositionCombo, _edit.WallpaperPosition.ToString());

            LightTimePicker.SelectedTime = TimeSpan.FromMinutes(_edit.LightTime);
            DarkTimePicker.SelectedTime = TimeSpan.FromMinutes(_edit.DarkTime);
            LatitudeBox.Value = ParseDouble(_edit.Latitude);
            LongitudeBox.Value = ParseDouble(_edit.Longitude);
            SunriseOffsetBox.Value = _edit.SunriseOffset;
            SunsetOffsetBox.Value = _edit.SunsetOffset;

            SystemToggle.IsOn = _edit.ChangeSystem;
            AppsToggle.IsOn = _edit.ChangeApps;
            WallpaperToggle.IsOn = _edit.ChangeWallpaper;
            StartupToggle.IsOn = _edit.RunAtStartup;
            CloseToTrayToggle.IsOn = _edit.CloseToTray;
            MinimizeToTrayToggle.IsOn = _edit.MinimizeToTray;
            HotkeyToggle.IsOn = _edit.HotkeyEnabled;
            CheckForUpdatesToggle.IsOn = _edit.CheckForUpdates;

            LightWallpaperPath.Text = string.IsNullOrWhiteSpace(_edit.LightWallpaper) ? "未选择" : _edit.LightWallpaper;
            DarkWallpaperPath.Text = string.IsNullOrWhiteSpace(_edit.DarkWallpaper) ? "未选择" : _edit.DarkWallpaper;
            LightPreview.Source = LoadPreview(_edit.LightWallpaper);
            DarkPreview.Source = LoadPreview(_edit.DarkWallpaper);

            DataDirectoryText.Text = _host.DataDirectory;
            AboutVersionText.Text = $"LightSwitch {_host.CurrentVersion}";
            AboutModeText.Text = _host.InstallationMode == InstallationMode.Installed
                ? "安装版 · 使用安装程序升级并保留配置"
                : "便携版 · 使用便携 ZIP 手动替换升级";
            VersionText.Text = $"LightSwitch {_host.CurrentVersion} · WinUI 3 · Windows App SDK";

            UpdateEnabledStates();
        }
        finally
        {
            _initializing = false;
        }
    }

    private void UpdateEnabledStates()
    {
        var fixedHours = _edit.ScheduleMode == ScheduleMode.FixedHours;
        LightTimePicker.IsEnabled = fixedHours;
        DarkTimePicker.IsEnabled = fixedHours;
        LocationPanel.Visibility = _edit.ScheduleMode == ScheduleMode.SunsetToSunrise
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem comboItem &&
                string.Equals(comboItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = comboItem;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : double.NaN;

    private static string FormatCoordinate(double value) =>
        double.IsNaN(value) ? string.Empty : value.ToString("0.####", CultureInfo.InvariantCulture);

    private static BitmapImage? LoadPreview(string path, int decodePixelWidth = 160)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return new BitmapImage
            {
                DecodePixelWidth = decodePixelWidth,
                UriSource = new Uri(path),
            };
        }
        catch
        {
            return null;
        }
    }

    private void Commit()
    {
        var error = _host.SaveSettings(_edit.Clone());
        if (error is not null)
        {
            ShowMessage("设置未保存", error, InfoBarSeverity.Error);
            return;
        }

        _edit = _host.Settings.Clone();
        _lastError = null;
        UpdateState(_host.Engine.State);
    }

    /// <summary>
    /// Time pickers and number boxes fire once per interaction, and each commit
    /// saves the file and runs a full evaluation. Coalesce them into one commit
    /// so dragging a number box does not produce a burst of evaluations.
    /// </summary>
    private void ScheduleCommit()
    {
        _commitTimer ??= CreateCommitTimer();
        _commitTimer.Stop();   // Restart: only the last change in the window lands.
        _commitTimer.Start();
    }

    private DispatcherQueueTimer CreateCommitTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(400);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => Commit();
        return timer;
    }

    /// <summary>Applies a still-pending debounced edit before an action that needs it.</summary>
    private void FlushPendingCommit()
    {
        if (_commitTimer?.IsRunning == true)
        {
            _commitTimer.Stop();
            Commit();
        }
    }

    private void OnHostStateUpdated(EngineState state)
    {
        void Apply()
        {
            var modeChanged = _host.Settings.ScheduleMode != _edit.ScheduleMode;
            var wallpaperChanged = _host.Settings.ChangeWallpaper != _edit.ChangeWallpaper;
            if (modeChanged || wallpaperChanged)
            {
                _edit = _host.Settings.Clone();
                LoadSettingsIntoUi();
            }

            UpdateState(state);
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Apply();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Apply);
        }
    }

    private void UpdateState(EngineState state)
    {
        var isLight = state.IsSystemLight;
        HeroIcon.Glyph = isLight ? "\uE706" : "\uE708";
        HeroTitle.Text = isLight ? "浅色外观" : "深色外观";

        var mode = state.Mode switch
        {
            ScheduleMode.FixedHours => "固定时间",
            ScheduleMode.SunsetToSunrise => "日出日落",
            ScheduleMode.FollowNightLight => "跟随夜间模式",
            _ => "自动调度已关闭",
        };

        HeroSubtitle.Text = state.Mode == ScheduleMode.Off
            ? "自动调度已关闭"
            : $"{mode} · 目标{((state.ManualLight ?? state.IsLightTarget) ? "浅色" : "深色")}";

        UpdateAppearanceButtons(state);

        var details = new List<string>();
        if (state.IsManualOverride && state.ManualUntil is { } until)
        {
            details.Add($"手动覆盖至 {until:MM-dd HH:mm}");
        }

        if (state.NextSwitchAt is { } next)
        {
            details.Add($"下次切换 {next:MM-dd HH:mm}");
        }

        if (state.SunTimes is { } sun)
        {
            details.Add($"日出 {ScheduleMath.FormatMinutes(sun.SunriseMinutes)} · 日落 {ScheduleMath.FormatMinutes(sun.SunsetMinutes)}");
        }

        HeroDetail.Text = string.Join(" · ", details);

        StatusPillText.Text = state.IsManualOverride
            ? "手动覆盖"
            : state.Mode == ScheduleMode.Off
                ? "调度已关闭"
                : "自动调度";
        StatusDot.Fill = new SolidColorBrush(state.IsManualOverride ? Colors.Orange : Colors.SeaGreen);

        NextSwitchText.Text = state.NextSwitchAt is { } nextSwitch
            ? $"下一次自动切换：{nextSwitch:yyyy-MM-dd HH:mm}"
            : state.Mode == ScheduleMode.Off
                ? "自动调度已关闭；壁纸仍会跟随当前系统外观同步。"
                : "正在计算下一次切换时间。";

        SunInfoText.Text = state.SunTimes is { } times
            ? $"今天这里：日出 {ScheduleMath.FormatMinutes(times.SunriseMinutes)}，日落 {ScheduleMath.FormatMinutes(times.SunsetMinutes)}。"
            : "填写经纬度后自动计算日出日落，也可用偏移量微调。";

        if (state.LastError is { } error)
        {
            if (error != _lastError)
            {
                _lastError = error;
                ShowMessage("提示", error, InfoBarSeverity.Warning);
            }
        }
        else
        {
            _lastError = null;
        }
    }

    private void UpdateAppearanceButtons(EngineState state)
    {
        // Exactly one of the three buttons shows the active choice:
        // 浅色/深色 while a manual choice is in effect (or while automatic
        // control is off), 恢复自动 while the schedule is driving the look.
        // Prefer ManualLight because IsLightTarget re-syncs to the system theme.
        bool? selectedLight = state.IsManualOverride
            ? state.ManualLight ?? state.IsLightTarget
            : state.Mode == ScheduleMode.Off
                ? state.IsSystemLight
                : null;

        var selectedStyle = (Style)Application.Current.Resources["AppearanceSelectedButtonStyle"];
        ApplyChoice(LightButton, selectedLight == true, selectedStyle);
        ApplyChoice(DarkButton, selectedLight == false, selectedStyle);
        ApplyChoice(AutoButton, selectedLight is null, selectedStyle);

        AutoButton.IsEnabled = state.Mode != ScheduleMode.Off;
        ToolTipService.SetToolTip(AutoButton, AutoButton.IsEnabled
            ? "清除手动覆盖，跟随调度"
            : "自动调度已关闭，请先在「调度」中选择切换方式");
    }

    private static void ApplyChoice(Button button, bool selected, Style selectedStyle)
    {
        if (selected)
        {
            button.Style = selectedStyle;
        }
        else if (button.Style is not null)
        {
            button.ClearValue(FrameworkElement.StyleProperty);
        }

        var label = button.Content?.ToString() ?? string.Empty;
        AutomationProperties.SetName(button, selected ? $"{label}，当前已选择" : label);
    }

    private void ShowMessage(string title, string message, InfoBarSeverity severity)
    {
        MessageBar.Title = title;
        MessageBar.Message = message;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = true;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        // The tray can change the mode without the window being visible.
        if (_host.Settings.ScheduleMode != _edit.ScheduleMode ||
            _host.Settings.ChangeWallpaper != _edit.ChangeWallpaper)
        {
            _edit = _host.Settings.Clone();
            LoadSettingsIntoUi();
            UpdateState(_host.Engine.State);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Persist any debounced edit before the window goes away.
        FlushPendingCommit();
        _host.StateUpdated -= OnHostStateUpdated;
        _host.BackgroundUpdateChecked -= OnBackgroundUpdateChecked;

        // Cancel an in-flight download instead of disposing its source: the download
        // may still be registering the token, and a canceled token is harmless here.
        try
        {
            _downloadCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing left to cancel.
        }

        _downloadCancellation = null;
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ModeCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (Enum.TryParse<ScheduleMode>(item.Tag?.ToString(), out var mode))
        {
            _edit.ScheduleMode = mode;
            UpdateEnabledStates();
            Commit();
        }
    }

    private void OnTimesChanged(TimePicker sender, TimePickerSelectedValueChangedEventArgs args)
    {
        if (_initializing || args.NewTime is not { } time)
        {
            return;
        }

        var minutes = Math.Clamp((int)Math.Round(time.TotalMinutes), 0, 1439);
        if (ReferenceEquals(sender, LightTimePicker))
        {
            _edit.LightTime = minutes;
        }
        else
        {
            _edit.DarkTime = minutes;
        }

        ScheduleCommit();
    }

    private void OnLocationChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_initializing)
        {
            return;
        }

        _edit.Latitude = FormatCoordinate(LatitudeBox.Value);
        _edit.Longitude = FormatCoordinate(LongitudeBox.Value);
        _edit.SunriseOffset = ToInt(SunriseOffsetBox.Value);
        _edit.SunsetOffset = ToInt(SunsetOffsetBox.Value);
        ScheduleCommit();
    }

    private static int ToInt(double value) =>
        double.IsNaN(value) ? 0 : Math.Clamp((int)Math.Round(value), -720, 720);

    private void OnTargetToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _edit.ChangeSystem = SystemToggle.IsOn;
        _edit.ChangeApps = AppsToggle.IsOn;
        Commit();
    }

    private void OnWallpaperToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _edit.ChangeWallpaper = WallpaperToggle.IsOn;
        Commit();
    }

    private void OnPositionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || PositionCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (Enum.TryParse<WallpaperPosition>(item.Tag?.ToString(), out var position))
        {
            _edit.WallpaperPosition = position;
            Commit();
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _edit.RunAtStartup = StartupToggle.IsOn;
        Commit();
    }

    private void OnGeneralToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _edit.CloseToTray = CloseToTrayToggle.IsOn;
        _edit.MinimizeToTray = MinimizeToTrayToggle.IsOn;
        Commit();
    }

    private void OnHotkeyToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _edit.HotkeyEnabled = HotkeyToggle.IsOn;
        Commit();
    }

    private async void OnPickLightWallpaper(object sender, RoutedEventArgs e)
    {
        var path = await PickImageAsync();
        if (path is null)
        {
            return;
        }

        _edit.LightWallpaper = path;
        LightWallpaperPath.Text = path;
        LightPreview.Source = LoadPreview(path);
        Commit();
    }

    private async void OnPickDarkWallpaper(object sender, RoutedEventArgs e)
    {
        var path = await PickImageAsync();
        if (path is null)
        {
            return;
        }

        _edit.DarkWallpaper = path;
        DarkWallpaperPath.Text = path;
        DarkPreview.Source = LoadPreview(path);
        Commit();
    }

    private async Task<string?> PickImageAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };

            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff" })
            {
                picker.FileTypeFilter.Add(extension);
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            ShowMessage("无法打开文件选择器", ex.Message, InfoBarSeverity.Error);
            return null;
        }
    }

    private void OnClearLightWallpaper(object sender, RoutedEventArgs e)
    {
        _edit.LightWallpaper = string.Empty;
        LightWallpaperPath.Text = "未选择";
        LightPreview.Source = null;
        Commit();
    }

    private void OnClearDarkWallpaper(object sender, RoutedEventArgs e)
    {
        _edit.DarkWallpaper = string.Empty;
        DarkWallpaperPath.Text = "未选择";
        DarkPreview.Source = null;
        Commit();
    }

    private void OnApplyLightWallpaper(object sender, RoutedEventArgs e) => ApplyWallpaperPreview(true, _edit.LightWallpaper);

    private void OnApplyDarkWallpaper(object sender, RoutedEventArgs e) => ApplyWallpaperPreview(false, _edit.DarkWallpaper);

    private void ApplyWallpaperPreview(bool light, string path)
    {
        FlushPendingCommit();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ShowMessage("没有可用的壁纸", "请先选择一张图片。", InfoBarSeverity.Warning);
            return;
        }

        var error = _host.SaveSettings(_edit.Clone());
        if (error is not null)
        {
            ShowMessage("设置未保存", error, InfoBarSeverity.Error);
            return;
        }

        _edit = _host.Settings.Clone();
        _host.ApplyWallpaperPreview(light);
    }

    private void OnLightClick(object sender, RoutedEventArgs e)
    {
        // The engine reads the saved settings, so a pending edit must land first.
        FlushPendingCommit();
        _host.ManualApply(true);
    }

    private void OnDarkClick(object sender, RoutedEventArgs e)
    {
        FlushPendingCommit();
        _host.ManualApply(false);
    }

    private void OnRestoreAutoClick(object sender, RoutedEventArgs e)
    {
        FlushPendingCommit();
        _host.RestoreAutomatic();
    }

    private void RestoreUpdateState()
    {
        if (_host.LastUpdateCheck is { } result)
        {
            RenderUpdateCheckResult(result, manual: false);
        }
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_checkingUpdate)
        {
            return;
        }

        _checkingUpdate = true;
        CheckUpdateButton.IsEnabled = false;
        SetUpdateStatus("正在检查更新…");
        try
        {
            var result = await _host.CheckForUpdatesAsync(manual: true, CancellationToken.None);
            RenderUpdateCheckResult(result, manual: true);
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            SetUpdateStatus($"检查更新失败：{ex.Message}");
            ShowMessage("检查更新失败", ex.Message, InfoBarSeverity.Warning);
        }
        finally
        {
            _checkingUpdate = false;
            CheckUpdateButton.IsEnabled = !_downloadingUpdate;
        }
    }

    private void OnBackgroundUpdateChecked(UpdateCheckResult result)
    {
        if (result.Status == UpdateCheckStatus.UpdateAvailable &&
            result.Release is { } release &&
            !_host.IsUpdatePostponed(release))
        {
            ShowUpdateAvailable(release);
        }
        else if (result.Status is UpdateCheckStatus.NetworkError or UpdateCheckStatus.Timeout
                 or UpdateCheckStatus.RateLimited or UpdateCheckStatus.HttpError
                 or UpdateCheckStatus.InvalidResponse)
        {
            SetUpdateStatus($"自动检查更新失败：{result.Message}");
        }
    }

    private void RenderUpdateCheckResult(UpdateCheckResult result, bool manual)
    {
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable when result.Release is { } release:
                if (_host.IsUpdatePostponed(release))
                {
                    HideUpdatePanel();
                    if (manual)
                    {
                        SetUpdateStatus($"已推迟版本 {release.Version} 的提醒，可稍后再次检查。");
                    }
                    return;
                }

                ShowUpdateAvailable(release);
                return;

            case UpdateCheckStatus.UpToDate:
                HideUpdatePanel();
                if (manual)
                {
                    SetUpdateStatus($"已是最新版本（v{_host.CurrentVersion}）。");
                }
                return;

            case UpdateCheckStatus.NoStableRelease:
                HideUpdatePanel();
                if (manual)
                {
                    SetUpdateStatus("GitHub 上暂无稳定版本。");
                }
                return;

            case UpdateCheckStatus.Throttled:
                if (manual)
                {
                    SetUpdateStatus("刚刚已经检查过更新，请稍后再试。");
                }
                return;

            case UpdateCheckStatus.Canceled:
                if (manual)
                {
                    SetUpdateStatus("已取消检查更新。");
                }
                return;

            default:
                HideUpdatePanel();
                if (manual)
                {
                    SetUpdateStatus($"检查更新失败：{result.Message}");
                    ShowMessage("检查更新失败", result.Message, InfoBarSeverity.Warning);
                }
                else
                {
                    SetUpdateStatus($"自动检查更新失败：{result.Message}");
                }
                return;
        }
    }

    private void ShowUpdateAvailable(ReleaseInfo release)
    {
        _pendingUpdate = release;
        UpdateTitleText.Text = $"发现新版本 {release.Version}";
        UpdateNotesText.Text = string.IsNullOrWhiteSpace(release.Notes)
            ? "该版本未提供更新说明。"
            : release.Notes.Trim();

        UpdateProgressBar.Value = 0;
        UpdateProgressBar.Visibility = Visibility.Collapsed;
        UpdateProgressText.Visibility = Visibility.Collapsed;
        UpdateGuidanceText.Visibility = Visibility.Collapsed;
        OpenDownloadButton.Visibility = Visibility.Collapsed;
        InstallUpdateButton.Visibility = Visibility.Collapsed;
        DownloadUpdateButton.Visibility = Visibility.Visible;
        DownloadUpdateButton.IsEnabled = true;
        PostponeUpdateButton.IsEnabled = true;
        UpdatePanel.Visibility = Visibility.Visible;
        SetUpdateStatus($"当前版本 v{_host.CurrentVersion}，新版本 v{release.Version} 可用。");
    }

    private void HideUpdatePanel()
    {
        UpdatePanel.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
    }

    private void SetUpdateStatus(string text)
    {
        UpdateStatusText.Text = text;
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private void OnPostponeUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is not { } release)
        {
            return;
        }

        _host.PostponeUpdate(release);
        HideUpdatePanel();
        SetUpdateStatus($"已推迟版本 {release.Version} 的提醒，可稍后再次检查。");
    }

    private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is not { } release || _downloadingUpdate)
        {
            return;
        }

        _downloadingUpdate = true;
        _downloadCancellation ??= new CancellationTokenSource();
        DownloadUpdateButton.IsEnabled = false;
        PostponeUpdateButton.IsEnabled = false;
        CheckUpdateButton.IsEnabled = false;
        UpdateProgressBar.Value = 0;
        UpdateProgressBar.Visibility = Visibility.Visible;
        UpdateProgressText.Text = "正在下载更新…";
        UpdateProgressText.Visibility = Visibility.Visible;
        UpdateGuidanceText.Visibility = Visibility.Collapsed;
        InstallUpdateButton.Visibility = Visibility.Collapsed;
        OpenDownloadButton.Visibility = Visibility.Collapsed;

        try
        {
            var progress = new Progress<double>(value =>
            {
                var fraction = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
                UpdateProgressBar.Value = fraction;
                UpdateProgressText.Text = $"正在下载更新… {fraction:P0}";
            });

            var result = await _host.DownloadUpdateAsync(release, progress, _downloadCancellation.Token);
            if (result.Success && result.FilePath is { } path)
            {
                _downloadedFilePath = path;
                UpdateProgressBar.Value = 1;
                UpdateProgressText.Text = "下载完成，SHA256 校验通过。";
                DownloadUpdateButton.Visibility = Visibility.Collapsed;
                PostponeUpdateButton.IsEnabled = true;
                OpenDownloadButton.Visibility = Visibility.Visible;
                ShowDownloadGuidance(release);
                SetUpdateStatus($"版本 {release.Version} 已下载并通过校验。");
            }
            else
            {
                ShowDownloadFailure(result);
            }
        }
        catch (OperationCanceledException)
        {
            UpdateProgressText.Text = "下载已取消。";
        }
        catch (Exception ex)
        {
            Log.Error("Update download failed", ex);
            ShowMessage("下载更新失败", ex.Message, InfoBarSeverity.Error);
            UpdateProgressText.Text = "下载失败。";
        }
        finally
        {
            _downloadingUpdate = false;
            CheckUpdateButton.IsEnabled = true;
            DownloadUpdateButton.IsEnabled = _pendingUpdate is not null;
            PostponeUpdateButton.IsEnabled = _pendingUpdate is not null;
        }
    }

    private void ShowDownloadGuidance(ReleaseInfo release)
    {
        if (_host.InstallationMode == InstallationMode.Installed)
        {
            InstallUpdateButton.Visibility = Visibility.Visible;
            UpdateGuidanceText.Text =
                "点击「运行安装程序」后将启动安装向导，LightSwitch 会先退出；升级会保留当前配置。";
        }
        else
        {
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            UpdateGuidanceText.Text =
                $"便携版更新：请先退出 LightSwitch，再将版本 {release.Version} 的 ZIP 解压并手动替换当前程序文件。不要直接覆盖正在运行的程序。";
        }

        UpdateGuidanceText.Visibility = Visibility.Visible;
    }

    private void ShowDownloadFailure(UpdateDownloadResult result)
    {
        var message = result.Status switch
        {
            UpdateDownloadStatus.MissingAsset or UpdateDownloadStatus.MissingChecksum =>
                "该版本缺少对应的安装包或 SHA256 文件，请前往 GitHub 发布页面手动下载。",
            UpdateDownloadStatus.ChecksumMismatch =>
                "SHA256 校验失败，已删除下载文件。请重试，或前往 GitHub 发布页面手动下载。",
            UpdateDownloadStatus.TooLarge => "安装包超出允许的大小，已停止下载。",
            UpdateDownloadStatus.RateLimited => "GitHub 暂时限制了下载请求，请稍后再试。",
            _ => result.Message,
        };

        UpdateProgressText.Text = message;
        UpdateProgressText.Visibility = Visibility.Visible;
        SetUpdateStatus(message);
        ShowMessage("下载更新失败", message, InfoBarSeverity.Warning);
    }

    private void OnOpenDownloadFolderClick(object sender, RoutedEventArgs e)
    {
        if (_downloadedFilePath is not null)
        {
            _host.OpenDownloadedFile(_downloadedFilePath);
        }
    }

    private async void OnInstallUpdateClick(object sender, RoutedEventArgs e)
    {
        var path = _downloadedFilePath;
        if (path is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "运行安装程序",
            Content = "安装向导启动后 LightSwitch 会退出。是否继续？",
            PrimaryButtonText = "运行安装程序",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var error = _host.RunDownloadedInstaller(path);
        if (error is not null)
        {
            ShowMessage("无法启动安装程序", error, InfoBarSeverity.Error);
            return;
        }

        SetUpdateStatus("安装程序已启动。");
    }

    private void OnCheckForUpdatesToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _edit.CheckForUpdates = CheckForUpdatesToggle.IsOn;
        Commit();
    }

    private void OnOpenReleasesPageClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppHost.ReleasesPage) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowMessage("无法打开浏览器", ex.Message, InfoBarSeverity.Warning);
        }
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e) => _host.OpenDataFolder();

    private IntPtr SubclassWndProc(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        IntPtr dwRefData)
    {
        if (msg == NativeMethods.WM_SIZE &&
            wParam.ToInt32() == NativeMethods.SIZE_MINIMIZED &&
            _host.Settings.MinimizeToTray)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }

                AppWindow.Hide();
            });
        }

        return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
    }
}
