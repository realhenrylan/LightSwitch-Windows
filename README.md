# LightSwitch for Windows

> 让 Windows 的**系统主题、应用主题和桌面壁纸**跟随时间、日出日落或夜间模式自动切换。
>
> v2.0 使用 **C# + WinUI 3 + Windows App SDK + Fluent Design** 重写；保留系统托盘常驻、全局热键、单实例、开机自启和命令行控制。

**新功能：浅色 / 深色壁纸分别设置。** 切换主题时，LightSwitch 会把对应壁纸应用到所有显示器，并支持填充、适应、拉伸、居中、平铺、跨屏六种模式。

![LightSwitch 主界面](docs/screenshots/main-light.png)

*浅色主题下的主界面（Windows 11，Mica 背景）*

---

## 目录

- [功能特性](#功能特性)
- [系统要求](#系统要求)
- [快速开始](#快速开始)
- [使用指南](#使用指南)
- [命令行](#命令行)
- [配置文件](#配置文件)
- [性能](#性能)
- [架构设计](#架构设计)
- [壁纸实现](#壁纸实现)
- [已知限制](#已知限制)
- [故障排查](#故障排查)
- [开发](#开发)
- [许可](#许可)

---

## 功能特性

### 自动切换

- **固定时间段**：设置浅色 / 深色时间，例如 08:00 浅色、20:00 深色。
- **日出日落**：输入经纬度，按当地天文时间切换，可为日出、日落分别设置偏移分钟数。
- **跟随 Night Light**：Windows 夜间模式开启时使用深色，关闭时使用浅色。
- **跨午夜**：支持浅色时段跨过午夜（如 22:00–06:00）。
- **手动覆盖自动恢复**：手动切换后暂停自动调度，跨越下一个时间边界后自动恢复；也可以点「恢复自动」立即恢复。
- **系统主题与应用主题独立控制**：可只控制任务栏 / 开始菜单，或只控制应用窗口。

### 壁纸联动（v2.0 新增）

- **浅色 / 深色壁纸分别设置**：各选一张图片，跟随主题自动应用。
- **应用到所有显示器**：通过 `IDesktopWallpaper` 逐台设置；显示器热插拔后下次切换会重新枚举。
- **六种填充方式**：填充 `Fill`、适应 `Fit`、拉伸 `Stretch`、居中 `Center`、平铺 `Tile`、跨屏 `Span`。
- **缩略图预览**：界面只按 160px 解码预览图，避免把 4K 壁纸整张读进内存。
- **手动应用**：每张壁纸旁的「应用」按钮可以立即预览，即使壁纸开关暂时关闭。
- **路径校验**：文件不存在时不会写注册表 / 调 COM，界面会给出错误提示。
- **与调度管线集成**：自动切换、手动切换、托盘菜单、全局热键、`--apply light|dark` 都会同时切换主题和壁纸。

![壁纸设置卡片](docs/screenshots/wallpaper-dark.png)

*壁纸设置：浅色 / 深色各一张，缩略图与立即应用*

### 桌面体验

- **托盘常驻**：关闭或最小化窗口时隐藏到托盘，主窗口按需创建。
- **单实例**：重复启动会把已有窗口唤到前台；开机自启的第二次启动不会弹出窗口。
- **全局热键**：`Ctrl+Alt+Shift+D` 一键切换浅色 / 深色。
- **开机自启**：应用内开关，写入当前用户的 `Run` 键，以 `--startup` 静默启动。
- **命令行**：`--toggle`、`--apply light|dark`、`--startup` 等。
- **Fluent 界面**：WinUI 3 卡片布局、Mica 背景（Windows 11）、自定义标题栏、深浅色自适应。
- **零管理员权限**：只写当前用户注册表，不安装服务、不需要提权。

---

## 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 1809+ / Windows 11，x64 |
| 运行时（默认构建） | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| 图形 | 支持 WinUI 3 的 Windows 桌面环境 |
| 权限 | 普通用户即可（HKCU + 当前用户桌面壁纸） |

说明：

- Windows App SDK 已随应用一起分发（`WindowsAppSDKSelfContained=true`），不需要单独安装 Windows App Runtime。
- Mica 是 Windows 11 特性；在 Windows 10 上会自动回退为纯色背景，其他功能不受影响。
- 如果想生成**不依赖 .NET 运行时**的版本，见[快速开始](#快速开始)中的 self-contained 发布命令。

---

## 快速开始

### 从源码构建

前置条件：

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（10.0.4xx 或更高）
- Windows 10/11 x64

```powershell
git clone https://github.com/realhenrylan/LightSwitch-Windows.git
cd LightSwitch-Windows

# 构建 Release
.\build.ps1

# 构建并运行 30 个单元测试
.\build.ps1 -Test

# 发布到 artifacts\LightSwitch-win-x64（需要目标机器安装 .NET 10 Desktop Runtime）
.\build.ps1 -Publish

# 发布为自包含版本（体积更大，但不需要安装 .NET 运行时）
# 首次执行需要联网还原 .NET runtime packs
.\build.ps1 -Publish -SelfContained
```

也可以直接双击 `build.bat`，或使用：

```powershell
dotnet build LightSwitch.slnx -c Release
```

默认输出目录：

```text
src\LightSwitch.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\LightSwitch.exe
```

> 首次构建需要能访问 NuGet（`nuget.org`）以下载 Windows App SDK 与 SDK BuildTools；之后会走本机缓存。
>
> `-Publish -SelfContained` 首次执行还需要下载 .NET 10 的 runtime packs（约数百 MB）。离线环境会报 `NU1301`，此时可先在有网络的环境还原一次，或继续使用默认的框架依赖发布。

### 从 v1.x 升级

1. 退出旧的 C++ 版 LightSwitch。
2. 运行 v2 的 `LightSwitch.exe`。
3. 如果 `%LOCALAPPDATA%\LightSwitch\config.json` 不存在，v2 会尝试读取 exe 同目录的旧 `config.json` 并迁移字段。
4. 打开设置，检查壁纸路径、开机自启和热键开关。

旧版源码与构建脚本已归档到 [`legacy/`](legacy/README.md)，仅用于查阅，不再维护。

---

## 使用指南

### 主窗口

| 区域 | 说明 |
|------|------|
| 当前外观 | 显示当前系统主题、调度模式和下次切换时间；提供「浅色」「深色」「恢复自动」按钮 |
| 调度 | 关闭 / 固定时间段 / 日出日落 / 跟随夜间模式；时间使用 24 小时制 |
| 日出日落 | 纬度、经度、日出偏移、日落偏移；仅该模式可编辑 |
| 应用外观的对象 | 分别控制 Windows 系统主题（任务栏、开始菜单）和应用主题（窗口、控件） |
| 壁纸 | 启用开关、填充方式、浅色壁纸、深色壁纸；支持选择 / 应用 / 清除 |
| 通用 | 开机自启、关闭到托盘、最小化到托盘、全局热键、数据目录 |
| 关于 | 版本号与所使用技术栈 |

所有修改在控件失焦或选择后立即保存到 `config.json`，无需手动点「保存」。

### 壁纸设置

1. 打开「壁纸」卡片，启用右上角开关。
2. 点浅色壁纸一行的「选择」，选一张浅色主题用的图片。
3. 点深色壁纸一行的「选择」，选一张深色主题用的图片。
4. 选择「填充方式」（默认「填充 Fill」）。
5. 点击某一行右侧的「应用」可以立即预览该壁纸，不等待下一次调度。

推荐使用与显示器分辨率比例接近的图片；`Span`（跨屏）适合多显示器拼接壁纸。

### 系统托盘

右键托盘图标：

| 菜单项 | 功能 |
|--------|------|
| 显示主窗口 | 打开设置窗口 |
| 切换为浅色 / 深色 | 立即手动切换主题与壁纸 |
| 启用 / 关闭自动调度 | 在「关闭」与「固定时间段」之间切换 |
| 退出 | 完全退出进程 |

双击或左键单击托盘图标也会显示主窗口。Windows 11 默认把新图标放进溢出区，可在「任务栏设置 → 其他系统托盘图标」中固定。

### 全局热键

| 热键 | 功能 |
|------|------|
| `Ctrl+Alt+Shift+D` | 切换浅色 / 深色（同时切换壁纸） |

热键在设置中可关闭。若被其他程序占用，日志会记录 `RegisterHotKey failed`，此时热键失效但托盘和界面仍可用。

### 关闭与最小化

- 「关闭窗口时最小化到托盘」开启（默认）：点关闭按钮只隐藏窗口，进程继续运行。
- 「最小化时隐藏到托盘」开启（默认）：最小化后从任务栏移除。
- 两项都关闭时，关闭窗口会退出应用。
- 退出请使用托盘菜单「退出」，或在设置中关闭上述选项后关窗。

---

## 命令行

```powershell
LightSwitch.exe                    # 显示主窗口（带托盘）
LightSwitch.exe --startup          # 静默启动，只驻留托盘（开机自启用）
LightSwitch.exe --hidden           # 同上
LightSwitch.exe --tray             # 同上
LightSwitch.exe --toggle           # 切换一次主题+壁纸后退出
LightSwitch.exe --apply light      # 应用浅色主题+浅色壁纸后退出
LightSwitch.exe --apply dark       # 应用深色主题+深色壁纸后退出
```

- `/toggle`、`/light`、`/dark` 也受支持。
- 一次性命令不会初始化 WinUI；如果已有实例在运行，命令会通过隐藏窗口转发给主实例。
- `--startup` / `--hidden` / `--tray` 在已有实例运行时**不会**把窗口弹出来。

---

## 配置文件

路径：

```text
%LOCALAPPDATA%\LightSwitch\config.json
```

修改设置后自动生成；也可以先复制 [`config.example.json`](config.example.json) 再改。首次运行且新路径不存在时，会尝试从 exe 同目录的旧 `config.json` 迁移。

```json
{
  "scheduleMode": "FixedHours",
  "changeSystem": true,
  "changeApps": true,
  "lightTime": 480,
  "darkTime": 1200,
  "latitude": "39.9",
  "longitude": "116.4",
  "sunrise_offset": 0,
  "sunset_offset": 0,
  "changeWallpaper": true,
  "lightWallpaper": "C:\\Wallpapers\\light.jpg",
  "darkWallpaper": "C:\\Wallpapers\\dark.jpg",
  "wallpaperPosition": "Fill",
  "runAtStartup": false,
  "minimizeToTray": true,
  "closeToTray": true,
  "hotkeyEnabled": true
}
```

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `scheduleMode` | string | `"Off"` | `Off` / `FixedHours` / `SunsetToSunrise` / `FollowNightLight` |
| `changeSystem` | bool | `true` | 是否控制系统主题 |
| `changeApps` | bool | `true` | 是否控制应用主题 |
| `lightTime` | int | `480` | 浅色开始时间，距午夜分钟数（480 = 08:00） |
| `darkTime` | int | `1200` | 深色开始时间，距午夜分钟数（1200 = 20:00） |
| `latitude` | string | `"39.9"` | 纬度，十进制度数 |
| `longitude` | string | `"116.4"` | 经度，十进制度数 |
| `sunrise_offset` | int | `0` | 日出偏移分钟数，-720～720 |
| `sunset_offset` | int | `0` | 日落偏移分钟数，-720～720 |
| `changeWallpaper` | bool | `false` | 是否随主题切换壁纸 |
| `lightWallpaper` | string | `""` | 浅色壁纸绝对路径 |
| `darkWallpaper` | string | `""` | 深色壁纸绝对路径 |
| `wallpaperPosition` | string | `"Fill"` | `Fill` / `Fit` / `Stretch` / `Center` / `Tile` / `Span` |
| `runAtStartup` | bool | `false` | 是否写入 HKCU 开机启动 |
| `minimizeToTray` | bool | `true` | 最小化时隐藏到托盘 |
| `closeToTray` | bool | `true` | 关闭窗口时隐藏到托盘 |
| `hotkeyEnabled` | bool | `true` | 是否注册 `Ctrl+Alt+Shift+D` |

说明：

- `sunrise_offset` / `sunset_offset` 使用旧版字段名，方便从 v1 配置迁移；其他字段为 camelCase。
- `lightTime` / `darkTime` 在界面中以 `HH:mm` 显示，文件中存分钟数。
- 外部直接编辑 `config.json` 后，当前版本需要**重启应用**才能读到新值；v1 的目录热重载未在 v2 中保留。
- 修改壁纸路径时建议使用绝对路径；路径不存在会在日志和界面中报错，不会清除原壁纸。

---

## 性能

在一台 Windows 11（i5-14490F，x64）上的实测数据：

| 场景 | 工作集（WS） | 私有内存 | 线程 | 说明 |
|------|--------------|----------|------|------|
| 托盘静默启动（`--startup`） | **91.1 MB** | 64.1 MB | 33 | 不创建主窗口，10 秒后保持稳定 |
| 主窗口打开 | **147–162 MB** | ~101 MB | 67 | 卡片、Mica、壁纸缩略图全部加载后 |
| 空闲 CPU | **≈ 0%** | — | — | 30 秒定时器 + 事件驱动，无 sleep 轮询 |
| 构建输出 | 155.3 MB / 336 文件 | — | — | 含 Windows App SDK 运行时文件 |

**这是 WinUI 3 应用，内存占用无法回到 v1 的亚 MB 级。** 为了保持“轻量”的使用体验，v2 做了这些取舍：

- **不创建后台服务**，单用户进程，无管理员权限。
- **托盘启动不建窗口**：只有用户主动打开设置时才创建 `MainWindow`，关闭后窗口对象保留但可隐藏（WinUI 限制，销毁最后一个窗口会退出进程）。
- **不 sleep 轮询**：`DispatcherQueueTimer` 每 30 秒评估一次；Night Light 使用注册表变更通知，托盘/系统事件直接触发评估。
- **壁纸预览限制解码宽度**：`BitmapImage.DecodePixelWidth = 160`，不把原图完整解码进 UI 内存。
- **无第三方 UI 框架**：只依赖 Windows App SDK / WinUI 3。
- **日志轮转**：512 KB 自动滚动，避免长期运行写入无限增长。

如果内存是首要指标，v1 的 C++ 实现仍保留在 [`legacy/`](legacy/README.md)，但不再获得新功能。

---

## 架构设计

### 项目结构

```text
LightSwitch-Windows/
├── LightSwitch.slnx              # 解决方案（.NET 10 的 slnx 格式）
├── Directory.Build.props         # 版本、语言版本等公共属性
├── NuGet.config                  # nuget.org 源
├── build.ps1 / build.bat         # 构建、测试、发布
├── config.example.json           # 配置示例
├── README.md / CHANGELOG.md / PLAN.md
├── LICENSE
├── src/
│   ├── LightSwitch.Core/         # 纯逻辑，不依赖 WinUI
│   │   ├── AppSettings.cs        # 配置模型、Clone、Normalize、坐标校验
│   │   ├── SettingsService.cs    # 加载/保存、旧配置迁移、原子写、损坏备份
│   │   ├── AppearanceEngine.cs   # 调度状态机、手动覆盖、状态快照
│   │   ├── ThemeService.cs       # HKCU Personalize 注册表 + 系统广播
│   │   ├── WallpaperService.cs   # IDesktopWallpaper COM + SPI 回退
│   │   ├── NightLightService.cs  # 读取 CloudStore Night Light 状态
│   │   ├── NightLightWatcher.cs  # RegNotifyChangeKeyValue 后台监听
│   │   ├── ScheduleMath.cs       # 跨午夜判断、下一个边界
│   │   ├── SunCalculator.cs      # 日出日落天文公式
│   │   ├── StartupService.cs     # HKCU Run 开机自启
│   │   ├── AppPaths.cs / Log.cs / Models.cs / EngineState.cs / Interfaces.cs
│   └── LightSwitch.App/          # WinUI 3 应用
│       ├── Program.cs            # 自定义 Main、单实例、CLI 分流
│       ├── App.xaml(.cs)         # XAML 资源与启动入口
│       ├── AppHost.cs            # 生命周期、托盘、定时器、保存设置
│       ├── MainWindow.xaml(.cs)  # Fluent 设置界面
│       ├── CommandLine.cs / CliRunner.cs
│       ├── Tray/TrayHost.cs      # 隐藏 Win32 窗口 + Shell_NotifyIcon
│       ├── Tray/IconFactory.cs
│       ├── Interop/NativeMethods.cs
│       └── Assets/app.ico
├── tests/
│   └── LightSwitch.Tests/        # 零依赖控制台测试 harness（30 个用例）
└── legacy/                       # v1.1.0 C++ / Win32 + GDI 归档
```

### 运行流程

```text
时间触发（30s）/ 系统事件 / Night Light 变更 / 手动点击 / 托盘 / 热键 / CLI
                              │
                              ▼
                    AppearanceEngine.Evaluate
                              │
              ┌───────────────┼────────────────┐
              ▼               ▼                ▼
        ThemeService     WallpaperService  状态快照 StateChanged
        （注册表+广播）   （COM 逐显示器）        │
                                              ▼
                                      MainWindow / TrayHost
```

- **主题**：写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` 的 `SystemUsesLightTheme`、`AppsUseLightTheme`，然后广播 `WM_SETTINGCHANGE("ImmersiveColorSet")`、`WM_THEMECHANGED`、`WM_DWMCOLORIZATIONCOLORCHANGED`。
- **壁纸**：`CoCreateInstance(CLSID_DesktopWallpaper)` → `IDesktopWallpaper.SetPosition` + 逐显示器 `SetWallpaper`；COM 不可用时回退 `SystemParametersInfo(SPI_SETDESKWALLPAPER)` 并写 `WallpaperStyle`。
- **Night Light**：读取 `HKCU\...\CloudStore\...\bluelightreductionstate` 的二进制 `Data`，长度 ≥ 25 且 `data[23] == 0x10 && data[24] == 0x00` 视为开启；后台线程用 `RegNotifyChangeKeyValue` 监听。
- **手动覆盖**：在固定时间 / 日出日落模式下，手动切换后记录 `manualUntil = 下一个亮暗边界`；到期后自动恢复调度。`Off` 和 `FollowNightLight` 不记录覆盖。
- **单实例**：`Local\LightSwitch.SingleInstance` 互斥体；已存在时通过 `FindWindowW("LightSwitch.TrayHost")` + `PostMessage` 发 `WM_APP` 消息。
- **托盘**：隐藏的顶层窗口接收 `Shell_NotifyIcon` 回调，右键用原生 `TrackPopupMenuEx`，热键用 `RegisterHotKey`（`MOD_NOREPEAT`）。

### 调度模式语义

| 模式 | 目标 | 说明 |
|------|------|------|
| `Off` | 不写主题 | 仍会把壁纸同步到当前系统主题（若壁纸启用） |
| `FixedHours` | 按 `lightTime` / `darkTime` | 支持跨午夜；手动覆盖到下一个边界 |
| `SunsetToSunrise` | 按经纬度计算的日出/日落 | 每天重新计算；极昼/极夜回退 06:00 / 18:00 |
| `FollowNightLight` | Night Light 开 = 深色，关 = 浅色 | 监听注册表变化，不记录手动覆盖 |

---

## 壁纸实现

v2 的壁纸行为参考了 [AutoDarkMode / Windows-Auto-Night-Mode](https://github.com/AutoDarkMode/Windows-Auto-Night-Mode) 的公开设计，但只实现了一个**简化子集**：

| 能力 | LightSwitch v2 | AutoDarkMode |
|------|----------------|--------------|
| 浅色 / 深色各一张 | ✅ | ✅ |
| 应用到所有显示器 | ✅ 循环 `SetWallpaper` | ✅ 支持全局与逐显示器 |
| 按显示器单独设置 | ❌ | ✅ |
| 填充方式 | ✅ 六种 | ✅ |
| 纯色 / Spotlight | ❌ | ✅ |
| 缩略图预览 | ✅ 160px 解码 | ✅ |
| 与调度 / 手动切换集成 | ✅ | ✅ |

实现要点：

- 使用 `IDesktopWallpaper`（IID `{B92B56A9-8B55-4E14-9A89-0199BBB6F93B}`，CLSID `{C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD}`）。
- 每个显示器调用一次 `SetWallpaper(monitorId, path)`；`SetPosition` 是全局设置。
- 不能通过 `HKCU\Control Panel\Desktop\WallPaper` 判断当前壁纸（Windows 会把它改成 `TranscodedWallpaper`）；状态比较使用 `GetWallpaper(monitorId)`。
- 壁纸是原路径引用，不复制文件；移动/删除图片后下次切换会失败并写日志。

---

## 已知限制

- **壁纸是全局一对**：不能给不同显示器配不同图片（AutoDarkMode 支持，本版未实现）。
- **不支持纯色 / Windows Spotlight / 壁纸轮播历史**。
- **外部修改 `config.json` 不会热重载**；请重启应用，或在设置窗口中重新打开/修改一次设置。
- **Mica 仅 Windows 11**；Windows 10 使用纯色背景。
- **内存基线**：WinUI 3 托盘静默约 91 MB WS，主窗口打开约 147–162 MB；无法达到 v1 的亚 MB 级。
- **热键固定为 `Ctrl+Alt+Shift+D`**，暂不支持自定义组合键。
- **界面目前为简体中文**，尚未做多语言资源。
- **单用户会话**：互斥体在 `Local\`，不支持多用户同时运行同一实例（不同登录会话可以各自运行）。

---

## 故障排查

### 日志

```text
%LOCALAPPDATA%\LightSwitch\LightSwitch.log
```

超过 512 KB 会滚动为 `LightSwitch.log.old`。日志记录启动参数、每次评估、主题/壁纸应用结果、托盘/热键注册失败、配置保存错误等。

### 托盘图标找不到

Windows 11 会把新图标收进溢出区。打开「设置 → 个性化 → 任务栏 → 其他系统托盘图标」，把 LightSwitch 设为显示。若仍找不到，检查日志中是否有 `Shell_NotifyIcon(NIM_ADD) failed`。

### 壁纸不切换

1. 确认「壁纸」开关已启用。
2. 确认浅色 / 深色壁纸都已选择，且文件仍然存在。
3. 检查当前调度模式：`Off` 不会主动改主题，但会按当前主题同步壁纸；`FollowNightLight` 需要 Night Light 状态变化。
4. 点对应壁纸行的「应用」强制应用一次，观察日志中的 `Wallpaper applied ...` 或错误信息。
5. 如果日志提示 `Changed desktop wallpaper` 但桌面没变，可能是组策略/第三方壁纸软件接管了桌面。

### 开机自启没生效

- 设置中打开「开机时自动启动」会写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值为 `"<当前 exe 路径>" --startup`。
- 移动过 exe 后，关闭再打开一次该开关，刷新路径。
- 某些安全软件会拦截 Run 键写入；查看日志中的 `startup` 错误。

### 热键无效

日志出现 `RegisterHotKey failed` 说明组合键被占用；先在设置中关闭热键，退出占用该组合键的程序后再打开。

### 重置设置

删除 `%LOCALAPPDATA%\LightSwitch\config.json`，重启应用即回到默认值（默认调度关闭、壁纸关闭）。也可以在设置中「打开数据目录」后手动删除。

---

## 开发

### 构建与测试

```powershell
dotnet build LightSwitch.slnx -c Release
.\build.ps1 -Test
```

测试使用零依赖的控制台 harness（`tests/LightSwitch.Tests`），不依赖 xUnit 包，覆盖：

- `ScheduleMath`：同日/跨午夜边界、下一个边界、精确边界顺延；
- `SunCalculator`：北京、赤道、极昼、非法坐标；
- `SettingsService`：旧 snake_case 配置、往返序列化、Normalize；
- `AppearanceEngine`：固定时间、手动覆盖到下一个边界、FollowNightLight、亮/暗壁纸、Off 模式、日落偏移。

当前结果：**30/30 通过**。

### 发布

```powershell
# 依赖 .NET 10 Desktop Runtime
.\build.ps1 -Publish

# 自包含（不需要 .NET 运行时）
.\build.ps1 -Publish -SelfContained
```

输出在 `artifacts\LightSwitch-win-x64\`（可用 `-OutputDirectory` 覆盖）。自包含版本会把 .NET 运行时一起打包，体积明显更大。

### 代码约定

- `LightSwitch.Core` 不引用 WinUI；所有平台相关调用（注册表、COM、P/Invoke）集中在 Core 的 Service 中，便于测试。
- `AppSettings` 交给引擎前视为不可变；UI 编辑 `Clone()` 后整体回传。
- 新增调度逻辑优先放进 `ScheduleMath` / `AppearanceEngine` 并补测试。
- P/Invoke 常量集中在 `Interop/NativeMethods.cs`，不要在业务代码里散落魔法数字。

---

## 许可

本项目使用 [MIT License](LICENSE)。

- v1 基于 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 的 LightSwitch 模块（MIT）构建；v2 保留了其主题注册表、Night Light 检测和日出日落算法的思路。
- 壁纸功能的设计参考了 [AutoDarkMode / Windows-Auto-Night-Mode](https://github.com/AutoDarkMode/Windows-Auto-Night-Mode)（MIT）。
- Windows、WinUI、Windows App SDK 是 Microsoft 的商标/产品；本项目与 Microsoft 无隶属关系。
