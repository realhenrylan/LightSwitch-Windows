using System.Globalization;
using System.IO;
using LightSwitch.Core;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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

    public MainWindow(AppHost host)
    {
        _host = host;
        _edit = host.Settings.Clone();
        _subclassProc = SubclassWndProc;

        InitializeComponent();
        ConfigureWindow();
        LoadSettingsIntoUi();
        UpdateState(_host.Engine.State);

        _host.StateUpdated += OnHostStateUpdated;
        Closed += OnClosed;
        Activated += OnActivated;
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        Title = "LightSwitch";
        AppWindow.Title = "LightSwitch";

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

            LightWallpaperPath.Text = string.IsNullOrWhiteSpace(_edit.LightWallpaper) ? "未选择" : _edit.LightWallpaper;
            DarkWallpaperPath.Text = string.IsNullOrWhiteSpace(_edit.DarkWallpaper) ? "未选择" : _edit.DarkWallpaper;
            LightPreview.Source = LoadPreview(_edit.LightWallpaper);
            DarkPreview.Source = LoadPreview(_edit.DarkWallpaper);

            DataDirectoryText.Text = _host.DataDirectory;
            VersionText.Text = $"LightSwitch {typeof(AppHost).Assembly.GetName().Version?.ToString(3)} · WinUI 3 · Windows App SDK";

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

    private static BitmapImage? LoadPreview(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return new BitmapImage
            {
                DecodePixelWidth = 160,
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
            : $"{mode} · 目标{(state.IsLightTarget ? "浅色" : "深色")}";

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
        _host.StateUpdated -= OnHostStateUpdated;
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

        Commit();
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
        Commit();
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

    private void OnLightClick(object sender, RoutedEventArgs e) => _host.ManualApply(true);

    private void OnDarkClick(object sender, RoutedEventArgs e) => _host.ManualApply(false);

    private void OnRestoreAutoClick(object sender, RoutedEventArgs e) => _host.RestoreAutomatic();

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
