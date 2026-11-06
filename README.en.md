<div align="center">
  <img src="src/LightSwitch.App/Assets/app.png" alt="LightSwitch icon" width="128">
  <h1>LightSwitch for Windows</h1>
  <p>Let your Windows theme and wallpaper follow the rhythm of your day.</p>
  <p>
    <a href="https://github.com/realhenrylan/LightSwitch-Windows/releases/latest"><strong>Download LightSwitch</strong></a>
    &nbsp;·&nbsp;
    <a href="README.md">简体中文</a>
    &nbsp;·&nbsp;
    <a href="CHANGELOG.md">Changelog</a>
  </p>
  <p>Windows 10 1809+ / Windows 11 · x64 · MIT License</p>
</div>

LightSwitch is a Windows desktop appearance switcher. It changes between light and dark themes on a schedule, at sunrise and sunset, or with Windows Night Light, and pairs each appearance with a wallpaper.

<div align="center">
  <img src="docs/screenshots/main-light.png" alt="LightSwitch main window in light mode" width="900">
</div>

## Features

- **Automatic switching** based on fixed hours, local sunrise and sunset, or Windows Night Light.
- **Theme controls** for the Windows system theme and app theme, independently.
- **Matching wallpapers** for light and dark appearances, applied to every display with a choice of six fit modes.
- **Manual switching** from the main window, system tray, or `Ctrl+Alt+Shift+D`. Fixed-hours and sunrise/sunset schedules resume at the next switching time.
- **Desktop integration** with launch at sign-in, close or minimize to tray, and command-line controls.
- **In-app updates** that check GitHub Releases in the background at startup or on demand from About & updates. Installed and portable builds get the matching download, verified by SHA256 before you confirm the install or replacement.
- **Native interface** built with WinUI 3 and Fluent Design, adapting to light and dark themes and using Mica on Windows 11.

## Installation

LightSwitch requires **Windows 10 1809 or later, or Windows 11, x64**. Releases include the .NET and Windows App SDK runtimes. The installer installs for the current user and does not require administrator privileges.

1. Open [Releases](https://github.com/realhenrylan/LightSwitch-Windows/releases/latest) and download `LightSwitch-<version>-setup.exe`.
2. Run the installer and follow the setup wizard.
3. Launch LightSwitch from the Start menu and select a switching mode under Schedule (调度).

The installer selects launch at sign-in by default on a fresh install and preserves the existing setting during upgrades. The installer is unsigned and may trigger a Windows SmartScreen prompt; SHA256 files are available with the release.

For a portable installation, download `LightSwitch-<version>-portable.zip`, extract it, and run `LightSwitch.exe`. See [installer/README.md](installer/README.md) for silent installation, uninstallation, and installer builds.

Both installed and portable builds can check for updates in the app. The downloaded file is verified against the SHA256 published with the release, and you then confirm whether to launch the installer or replace the files. LightSwitch never installs silently or restarts on its own.

The application interface is currently in Simplified Chinese.

## Usage

### Choose a schedule

| Mode | Behavior |
| --- | --- |
| Fixed hours | Set the start times for light and dark appearances, including periods that cross midnight. |
| Sunrise and sunset | Enter coordinates to switch at local sunrise and sunset, with optional offsets for each. |
| Follow Night Light | Use the dark appearance when Windows Night Light is on and the light appearance when it is off. |
| Automatic switching off | Control the theme manually. When wallpaper switching is enabled, the wallpaper still follows the current system appearance. |

Automatic switching and wallpaper pairing are off by default. In fixed-hours and sunrise/sunset modes, a manual choice lasts until the next switching time; select Restore automatic (恢复自动) to resume the schedule immediately.

### Set wallpapers

1. Enable wallpaper switching in the Wallpaper (壁纸) section.
2. Select a light wallpaper and a dark wallpaper.
3. Choose a fit mode: `Fill`, `Fit`, `Stretch`, `Center`, `Tile`, or `Span`.
4. Use the apply button on either row to preview that wallpaper immediately.

The same light and dark wallpaper pair is applied to all displays. LightSwitch uses the original image paths, so select the images again if they are moved or deleted.

### Tray and shortcut

By default, closing or minimizing the window hides LightSwitch in the system tray. Select the tray icon to reopen settings, or use its context menu to switch appearance, control automatic scheduling, or exit.

The global shortcut is **`Ctrl+Alt+Shift+D`** and can be disabled under General (通用). Launch at sign-in, close to tray, and minimize to tray can be adjusted there as well.

### Check for updates

About & updates (关于与更新), below General, shows the current version and installation mode and provides a Check for updates (检查更新) button. The app checks once in the background at startup; turn off the 启动时自动检查更新 toggle to disable it. Automatic checks run at most once every 24 hours, while a manual check always starts immediately.

When a new version is available, the panel shows its version, release notes, and Download update (下载更新) / Remind me later (稍后提醒). Only the latest stable GitHub release is considered; drafts and pre-releases are ignored.

- **Installed build**: downloads `LightSwitch-<version>-setup.exe` and verifies the SHA256 published with the release. Run installer (运行安装程序) then starts the setup wizard; upgrades keep the existing installer flow and preserve the configuration under `%LOCALAPPDATA%\LightSwitch`.
- **Portable build**: downloads `LightSwitch-<version>-portable.zip` and, after verification, opens the download folder. Exit LightSwitch first, then extract the ZIP and replace the program files manually. The app never overwrites a running executable.

Network failures, timeouts, GitHub API rate limits, and checksum failures are shown as non-blocking messages; scheduling continues normally. The last check time and any postponed version are stored in `%LOCALAPPDATA%\LightSwitch\update-state.json`.

### Command line

Run these commands from the directory containing `LightSwitch.exe`:

```powershell
.\LightSwitch.exe                    # Open the main window
.\LightSwitch.exe --startup          # Start silently in the tray
.\LightSwitch.exe --toggle           # Toggle light / dark appearance
.\LightSwitch.exe --apply light      # Apply the light appearance
.\LightSwitch.exe --apply dark       # Apply the dark appearance
.\LightSwitch.exe --enable-startup    # Enable launch at sign-in
.\LightSwitch.exe --disable-startup   # Disable launch at sign-in
```

Appearance commands also apply the corresponding wallpaper when wallpaper switching is enabled. `--hidden` and `--tray` can also be used for silent startup. See the [installer documentation](installer/README.md) for installer options.

## Configuration and logs

Settings are saved automatically when changed in the interface. The data files are located at:

```text
%LOCALAPPDATA%\LightSwitch\config.json
%LOCALAPPDATA%\LightSwitch\LightSwitch.log
%LOCALAPPDATA%\LightSwitch\update-state.json
%LOCALAPPDATA%\LightSwitch\updates\
```

Use [config.example.json](config.example.json) as a reference. Restart the app after editing the configuration file directly. The log rotates at 512 KB. `update-state.json` stores the last check time and any postponed version, and the `updates` folder holds downloaded update files that passed verification.

| Setting | Description |
| --- | --- |
| `scheduleMode` | `Off`, `FixedHours`, `SunsetToSunrise`, or `FollowNightLight`. |
| `changeSystem` / `changeApps` | Control the Windows system theme and app theme independently. |
| `lightTime` / `darkTime` | Start times in minutes after midnight; for example, `480` is 08:00. |
| `latitude` / `longitude` | Decimal coordinates for the sunrise/sunset schedule. |
| `sunrise_offset` / `sunset_offset` | Sunrise and sunset offsets in minutes, from -720 to 720. |
| `changeWallpaper` | Enable wallpaper switching. |
| `lightWallpaper` / `darkWallpaper` | Absolute paths to the light and dark wallpapers. |
| `wallpaperPosition` | Wallpaper fit mode; defaults to `Fill`. |
| `runAtStartup` | Launch at sign-in. |
| `minimizeToTray` / `closeToTray` | Hide to the tray when minimizing or closing the window. |
| `hotkeyEnabled` | Enable the global shortcut. |
| `checkForUpdates` | Check for updates in the background at startup; defaults to `true`. Manual checks are always available. |

When upgrading from v1, the app attempts to import an old `config.json` from the executable directory if no configuration exists at the new location. Uninstallation keeps user settings and logs by default; the uninstall wizard offers an option to remove them.

### Troubleshooting

- **Tray icon**: Look in the taskbar overflow area and set the icon to visible in Windows taskbar settings.
- **Wallpaper**: Check that wallpaper switching is enabled and the image paths still exist. Application results and errors are recorded in the log.
- **Launch at sign-in**: After moving the executable, turn the option off and on under General (通用) to update its path.
- **Global shortcut**: `RegisterHotKey failed` in the log indicates registration failed; check whether another app is using the combination.
- **Update checks**: automatic checks run at most once every 24 hours; a manual check runs immediately. Unreachable GitHub endpoints or API rate limits are reported under About & updates and logged. A failed SHA256 check deletes the downloaded file, and you can retry later.

## Resource usage

The following reference measurements were taken from a Release build on Windows 11 with an Intel i5-14490F. Actual usage varies by device and activity.

| Scenario | Working set | Private memory |
| --- | ---: | ---: |
| Silent tray startup, main window not yet opened | 91.9 MB | 65.0 MB |
| Main window open | 148–149 MB | About 100 MB |
| Hidden to tray after opening the main window | About 146–149 MB | — |

## Build from source

Building requires Windows x64, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and access to NuGet packages. Building the installer also requires [Inno Setup 6.5 or later](https://jrsoftware.org/isdl.php).

```powershell
git clone https://github.com/realhenrylan/LightSwitch-Windows.git
cd LightSwitch-Windows

.\build.ps1                         # Build Release
.\build.ps1 -Test                   # Build and run tests
.\build.ps1 -Publish                # Publish a self-contained build
.\build.ps1 -Publish -Installer     # Publish and build the installer
.\build.ps1 -Publish -FrameworkDependent  # Publish a framework-dependent build
```

The self-contained build is written to `artifacts\LightSwitch-win-x64\` by default. The framework-dependent build requires the .NET 10 Desktop Runtime on the target computer. See [installer/README.md](installer/README.md) for installer build options.

## Project structure

```text
src/LightSwitch.App/       WinUI 3 interface, tray, and command line
src/LightSwitch.Core/      Scheduling, theme, wallpaper, settings, and update-check logic
tests/LightSwitch.Tests/   Core logic tests
installer/                 Windows installer
legacy/                    Archived v1 C++ version
```

## License and credits

This project is licensed under the [MIT License](LICENSE). The v1 implementation was based on the LightSwitch module in [Microsoft PowerToys](https://github.com/microsoft/PowerToys); the wallpaper design references [AutoDarkMode](https://github.com/AutoDarkMode/Windows-Auto-Night-Mode).
