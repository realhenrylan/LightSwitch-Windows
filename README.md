<div align="center">
  <img src="src/LightSwitch.App/Assets/app.png" alt="LightSwitch 图标" width="128">
  <h1>LightSwitch for Windows</h1>
  <p>让 Windows 的主题与壁纸，跟随一天的节奏。</p>
  <p>
    <a href="https://github.com/realhenrylan/LightSwitch-Windows/releases/latest"><strong>下载 LightSwitch</strong></a>
    &nbsp;·&nbsp;
    <a href="README.en.md">English</a>
    &nbsp;·&nbsp;
    <a href="CHANGELOG.md">更新日志</a>
  </p>
  <p>Windows 10 1809+ / Windows 11 · x64 · MIT License</p>
</div>

LightSwitch 是一款 Windows 桌面外观切换工具。它可以按固定时间、日出日落或 Windows 夜间模式，在浅色与深色外观之间自动切换，并为每种外观搭配壁纸。

<div align="center">
  <img src="docs/screenshots/main-light.png" alt="LightSwitch 浅色主界面" width="900">
</div>

## 功能

- **自动切换**：按固定时间、当地日出日落或 Windows 夜间模式切换浅色与深色外观。
- **主题控制**：分别选择是否调整 Windows 系统主题和应用主题。
- **壁纸联动**：为浅色和深色各设置一张壁纸，切换外观时应用到所有显示器，支持六种填充方式。
- **手动切换**：通过主界面、系统托盘或 `Ctrl+Alt+Shift+D` 立即切换；固定时间和日出日落模式会在下一个切换时间恢复自动调度。
- **桌面集成**：支持开机启动、关闭或最小化到托盘，以及命令行控制。
- **应用内更新**：启动时后台检查 GitHub Releases，也可在「关于与更新」中手动检查；安装版与便携版分别提供对应下载，SHA256 校验通过后由用户确认安装或替换。
- **原生界面**：使用 WinUI 3 和 Fluent Design，适配系统深浅色；Windows 11 使用 Mica 背景。

## 安装

系统要求为 **Windows 10 1809 或更新版本，或 Windows 11，x64**。发布包已包含 .NET 和 Windows App SDK 运行时，按当前用户安装，无需管理员权限。

1. 前往 [Releases](https://github.com/realhenrylan/LightSwitch-Windows/releases/latest)，下载 `LightSwitch-<版本>-setup.exe`。
2. 运行安装程序，按照向导完成安装。
3. 从开始菜单启动 LightSwitch，在「调度」中选择切换方式。

安装向导在首次安装时默认勾选开机启动，升级时保留现有选择。安装包尚未签名，Windows SmartScreen 可能显示提示；Release 同时提供 SHA256 校验文件。

也可以下载 `LightSwitch-<版本>-portable.zip`，解压后运行 `LightSwitch.exe`。静默安装、卸载及安装器构建说明见 [installer/README.md](installer/README.md)。

安装版和便携版都可以在应用内检查更新。更新下载完成后会先校验 Release 提供的 SHA256，再由你确认启动安装向导或手动替换文件，不会静默安装或重启应用。

## 使用

### 选择调度方式

| 模式 | 切换方式 |
| --- | --- |
| 固定时间段 | 分别设置浅色和深色开始时间，支持跨午夜时段。 |
| 日出日落 | 输入经纬度，按当地日出和日落时间切换，可分别设置时间偏移。 |
| 跟随夜间模式 | Windows 夜间模式开启时使用深色，关闭时使用浅色。 |
| 关闭自动调度 | 保持主题手动控制；启用壁纸联动时，壁纸仍会跟随当前系统外观。 |

首次运行时，自动调度和壁纸联动默认关闭。在固定时间段或日出日落模式下，手动选择外观会持续到下一个切换时间；也可以点击「恢复自动」立即恢复调度。

### 设置壁纸

1. 在「壁纸」区域启用壁纸联动。
2. 分别选择浅色壁纸和深色壁纸。
3. 选择填充方式：`Fill`、`Fit`、`Stretch`、`Center`、`Tile` 或 `Span`。
4. 点击对应行的「应用」，可以立即预览壁纸。

同一组浅色和深色壁纸会应用到所有显示器。LightSwitch 使用图片原路径，移动或删除图片后需要重新选择。

### 托盘与快捷键

关闭或最小化窗口时，LightSwitch 默认隐藏到系统托盘。点击托盘图标可以重新打开设置，右键菜单提供外观切换、自动调度和退出选项。

全局快捷键为 **`Ctrl+Alt+Shift+D`**，可在「通用」中关闭。开机启动、关闭到托盘和最小化到托盘也可在此调整。

### 检查更新

「通用」下方的「关于与更新」显示当前版本和安装方式，并提供「检查更新」按钮。启动时默认在后台检查一次，可通过「启动时自动检查更新」关闭；自动检查至少间隔 24 小时，手动检查不受此限制。

发现新版本后，面板会显示版本号、Release Notes 以及「下载更新」「稍后提醒」。更新只使用 GitHub Releases 中最新的稳定版本，草稿和预发布版本会被忽略。

- **安装版**：下载 `LightSwitch-<版本>-setup.exe`，校验 Release 提供的 SHA256 后，由你点击「运行安装程序」启动安装向导；升级沿用现有安装流程，并保留 `%LOCALAPPDATA%\LightSwitch` 中的配置。
- **便携版**：下载 `LightSwitch-<版本>-portable.zip`，校验后打开下载目录。请先退出 LightSwitch，再解压并手动替换程序文件；应用不会覆盖正在运行的可执行文件。

断网、超时、GitHub API 限流或校验失败时，界面会显示提示并继续正常调度，不会自动安装或重启。检查记录和推迟的版本保存在 `%LOCALAPPDATA%\LightSwitch\update-state.json`。

### 命令行

在 `LightSwitch.exe` 所在目录运行：

```powershell
.\LightSwitch.exe                    # 打开主窗口
.\LightSwitch.exe --startup          # 静默启动到托盘
.\LightSwitch.exe --toggle           # 切换浅色 / 深色外观
.\LightSwitch.exe --apply light      # 应用浅色外观
.\LightSwitch.exe --apply dark       # 应用深色外观
.\LightSwitch.exe --enable-startup    # 启用开机启动
.\LightSwitch.exe --disable-startup   # 关闭开机启动
```

启用壁纸联动后，外观切换命令也会应用对应壁纸。`--hidden` 和 `--tray` 可用于静默启动；更多安装器参数见 [安装器文档](installer/README.md)。

## 配置与日志

设置在界面中修改后自动保存，数据位于：

```text
%LOCALAPPDATA%\LightSwitch\config.json
%LOCALAPPDATA%\LightSwitch\LightSwitch.log
%LOCALAPPDATA%\LightSwitch\update-state.json
%LOCALAPPDATA%\LightSwitch\updates\
```

可参考 [config.example.json](config.example.json) 编辑配置；手动修改文件后需要重启应用。日志达到 512 KB 时会轮转。`update-state.json` 保存最近检查时间和推迟提醒的版本，`updates` 目录存放下载并通过校验的更新文件。

| 配置项 | 说明 |
| --- | --- |
| `scheduleMode` | `Off`、`FixedHours`、`SunsetToSunrise` 或 `FollowNightLight`。 |
| `changeSystem` / `changeApps` | 分别控制 Windows 系统主题和应用主题。 |
| `lightTime` / `darkTime` | 浅色和深色开始时间，以距午夜的分钟数表示；例如 `480` 为 08:00。 |
| `latitude` / `longitude` | 日出日落模式使用的十进制经纬度。 |
| `sunrise_offset` / `sunset_offset` | 日出和日落时间偏移，单位为分钟，范围为 -720 至 720。 |
| `changeWallpaper` | 启用壁纸联动。 |
| `lightWallpaper` / `darkWallpaper` | 浅色和深色壁纸的绝对路径。 |
| `wallpaperPosition` | 壁纸填充方式，默认为 `Fill`。 |
| `runAtStartup` | 开机启动。 |
| `minimizeToTray` / `closeToTray` | 最小化或关闭窗口时隐藏到托盘。 |
| `hotkeyEnabled` | 启用全局快捷键。 |
| `checkForUpdates` | 启动时在后台检查更新，默认为 `true`；手动检查不受此开关限制。 |

从 v1 升级时，如果新位置没有配置文件，应用会尝试导入程序目录中的旧 `config.json`。卸载默认保留用户配置和日志，可在卸载向导中选择清除。

### 常用排查

- **托盘图标**：在 Windows 任务栏的托盘溢出区域中查找，并可在任务栏设置中将其设为可见。
- **壁纸**：确认壁纸联动已启用、图片路径仍然有效；应用结果和错误会写入日志。
- **开机启动**：移动程序后，在「通用」中关闭再启用开机启动，以更新程序路径。
- **全局快捷键**：日志中的 `RegisterHotKey failed` 表示注册失败，可检查其他程序是否占用该组合键。
- **检查更新**：自动检查至少间隔 24 小时，手动检查可立即发起。GitHub 不可达或 API 限流时会在「关于与更新」中提示，详细原因写入日志；SHA256 校验失败时下载文件会被删除，可稍后重试。

## 运行参考

以下为项目 Release 构建在 Windows 11、Intel i5-14490F 上的参考测量，实际占用随设备和使用情况变化。

| 场景 | 工作集 | 私有内存 |
| --- | ---: | ---: |
| 静默启动到托盘，未打开主窗口 | 91.9 MB | 65.0 MB |
| 主窗口打开 | 148–149 MB | 约 100 MB |
| 打开主窗口后隐藏到托盘 | 约 146–149 MB | — |

## 从源码构建

构建需要 Windows x64、[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 和 NuGet 包访问；生成安装包还需要 [Inno Setup 6.5 或更新版本](https://jrsoftware.org/isdl.php)。

```powershell
git clone https://github.com/realhenrylan/LightSwitch-Windows.git
cd LightSwitch-Windows

.\build.ps1                         # 构建 Release
.\build.ps1 -Test                   # 构建并运行测试
.\build.ps1 -Publish                # 发布自包含版本
.\build.ps1 -Publish -Installer     # 发布并生成安装包
.\build.ps1 -Publish -FrameworkDependent  # 发布框架依赖版本
```

自包含版本默认输出到 `artifacts\LightSwitch-win-x64\`。框架依赖版本需要目标电脑安装 .NET 10 Desktop Runtime。安装包构建选项见 [installer/README.md](installer/README.md)。

## 项目结构

```text
src/LightSwitch.App/       WinUI 3 界面、托盘与命令行
src/LightSwitch.Core/      调度、主题、壁纸、配置与更新检查逻辑
tests/LightSwitch.Tests/   核心逻辑测试
installer/                 Windows 安装包
legacy/                    v1 C++ 版本归档
```

## 许可与致谢

本项目采用 [MIT License](LICENSE)。v1 基于 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 的 LightSwitch 模块；壁纸功能的设计参考了 [AutoDarkMode](https://github.com/AutoDarkMode/Windows-Auto-Night-Mode)。
