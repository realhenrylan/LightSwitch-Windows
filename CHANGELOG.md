# 变更日志

## [v2.0.2] — 2026-11-01

### 🎨 界面修复

- **外观按钮选中反馈** — “浅色 / 深色 / 恢复自动”会用强调色标出当前生效的选项；手动切换后保持高亮，直到覆盖结束或点击“恢复自动”。引擎快照新增 `ManualLight`，系统主题未变化时选中状态也不会回跳。

### 📦 Windows 安装包

- **新增 Inno Setup 安装器** — `installer/LightSwitch.iss` 生成 `artifacts/LightSwitch-2.0.2-setup.exe`（约 43.5 MB，465 个文件）。按用户安装到 `%LOCALAPPDATA%\Programs\LightSwitch`，无需管理员权限；创建开始菜单快捷方式，可选桌面快捷方式。
- **安装选项** — 默认勾选开机自启；安装器通过 `--enable-startup` 写入 HKCU Run 项。仅全新安装时应用该选项，覆盖升级不会改动用户在设置中的选择。
- **卸载** — 注册为当前用户应用，可从“设置 → 应用”或开始菜单卸载；卸载前结束正在运行的 LightSwitch。默认保留 `%LOCALAPPDATA%\LightSwitch` 中的设置、日志和缓存，交互式卸载可选择一并删除；静默卸载不再弹窗，默认保留。
- **中英文界面** — 安装向导优先显示简体中文，可切换 English；包含 Inno Setup 官方简体中文语言文件。
- **安装包构建** — 新增 `build.ps1 -Installer` 与 `tools/build-installer.ps1`：复用 `-Publish` 输出，定位 ISCC.exe，校验 `LightSwitch.exe` 与语言文件，编译后输出 setup 和 SHA256。`-FrameworkDependent` 与 `-Installer` 互斥。
- **命令行自启开关** — 新增 `--enable-startup` / `--disable-startup`，供安装器和脚本以一次性命令写入/删除自启项；失败时返回退出码 1。
- **发布自动化** — 新增 `.github/workflows/release.yml`：推送 `v*` 标签或手动触发时，在 windows-latest 上构建、测试、生成安装包与便携版 zip，上传构建产物并创建/更新 GitHub Release。
- **文档** — README 中英文增加安装版快速开始、安装包构建、卸载与 SmartScreen 说明；PLAN 更新分发进度。安装包暂未做代码签名。

---

## [v2.0.1] — 2026-11-01

### 📦 默认自包含发布

- **默认发布不再依赖 .NET 运行时** — `build.ps1 -Publish` 生成的 `artifacts\LightSwitch-win-x64` 约 **170 MB / 461 文件**，目标机器无需安装 .NET 10 Desktop Runtime，也无需 Windows App Runtime。
- **新增 `tools/bundle-dotnet-runtime.ps1`** — 把构建机上的 .NET 10 运行时装入发布目录：复制 `Microsoft.NETCore.App` 共享运行时与 `hostfxr.dll`，将 `runtimeconfig.json` 改为 `includedFrameworks`，并把运行时资产合并进 `deps.json`。不依赖 NuGet runtime pack。
- **新增 `-FrameworkDependent` 开关** — 仍可生成约 93 MB 的框架依赖版本，适合目标机已装 .NET 10 Desktop Runtime 的场景。
- **精简 Windows App SDK 依赖** — 应用只引用 `Microsoft.WindowsAppSDK.WinUI` 与 `Microsoft.WindowsAppSDK.Runtime`，不再携带完整 metapackage；应用自身输出从 153 MB 降到约 93 MB。
- **发布版窗口崩溃修复** — `dotnet publish` 不会复制 XAML 编译产物（`*.xbf`）和资源索引（`*.pri`），导致发布版托盘可启动、创建窗口时崩溃（`Microsoft.UI.Xaml.dll`，`0xc000027b`）。`LightSwitch.App.csproj` 增加 `CopyXamlArtifactsToPublish` target 显式复制。
- **实测** — 自包含发布在剥离 `DOTNET_ROOT`、PATH 仅保留系统目录的环境下：窗口正常显示（150.8 MB WS），`--startup` 托盘模式 91.6 MB WS / 64.4 MB 私有内存 / 34 线程，关闭到托盘与单实例唤起正常。
- **文档** — README 重写为中英文双语（`README.md` / `README.en.md`），补充轻量化设计、实测数据与边界说明；系统要求、快速开始、性能、架构与发布章节已同步。v2.0.0 的“需要 .NET 10 Desktop Runtime”说明由本版本取代。

---

## [v2.0.0] — 2026-11-01

### ✨ 新增：浅色 / 深色壁纸分别设置

- **两张壁纸跟随主题切换** — 浅色主题使用 `lightWallpaper`，深色主题使用 `darkWallpaper`；启用后与固定时间、日出日落、跟随 Night Light、手动切换、托盘菜单、全局热键、`--apply light|dark` 全部联动。
- **应用到所有显示器** — 通过 `IDesktopWallpaper` 逐台显示器调用 `SetWallpaper`，支持显示器热插拔后重新枚举。
- **六种填充方式** — 填充 `Fill`、适应 `Fit`、拉伸 `Stretch`、居中 `Center`、平铺 `Tile`、跨屏 `Span`。
- **缩略图预览** — 使用 `BitmapImage.DecodePixelWidth = 160`，不会把原图完整解码进内存。
- **独立「应用」按钮** — 即使壁纸开关关闭，也可以立即预览某一张壁纸。
- **路径校验与错误提示** — 文件不存在时不调用 COM，界面 InfoBar 显示错误，日志记录原因。
- **状态去重** — 通过 `GetWallpaper` 比较每个显示器当前路径，已匹配时不重复写入。

### 🔧 重构：C# + WinUI 3 + Windows App SDK + Fluent Design

- **技术栈** — .NET 10、WinUI 3、Windows App SDK 2.6.4、C#，unpackaged（`WindowsPackageType=None`）。
- **Fluent 界面** — 卡片布局、Mica 背景（Windows 11）、自定义 48px 标题栏、系统深浅色资源、圆角/描边/状态胶囊。
- **分层架构** — `LightSwitch.Core`（配置、调度、主题、夜灯、壁纸、日出日落、自启、日志）与 `LightSwitch.App`（WinUI、托盘、CLI）分离，Core 可独立测试。
- **状态机重写** — 保留 v1 的固定时间、日出日落、跟随夜灯、跨午夜、手动覆盖到下一个边界等语义；状态改为不可变 `EngineState` 快照 + `StateChanged` 事件。
- **托盘重写** — 隐藏的 Win32 顶层窗口 + `Shell_NotifyIcon` V4 + 原生 `TrackPopupMenuEx`；关闭/最小化隐藏到托盘，主窗口懒创建。
- **单实例** — `Local\LightSwitch.SingleInstance` 互斥体 + 隐藏窗口 `WM_APP` 消息转发；`--startup` 不会弹出已有窗口。
- **命令行** — `--toggle`、`--apply light|dark`、`--startup`、`--hidden`、`--tray`；一次性命令不初始化 WinUI。
- **配置迁移** — 新路径 `%LOCALAPPDATA%\LightSwitch\config.json`；首次运行可从 exe 同目录旧 `config.json` 导入，兼容 `snake_case` 偏移字段。
- **原子保存** — 先写 `.tmp` 再替换；损坏配置备份为 `*.bad-yyyyMMddHHmmss`。
- **日志** — `%LOCALAPPDATA%\LightSwitch\LightSwitch.log`，512 KB 滚动。
- **测试** — 新增零依赖控制台测试 harness（30 个用例），覆盖调度边界、日出日落、配置兼容、手动覆盖、壁纸联动、Off 模式。

### 🚀 性能与体验

- **托盘静默启动**：不创建主窗口，工作集约 **91 MB**、私有内存 **64 MB**、33 线程，空闲 CPU ≈ 0%。
- **主窗口打开**：工作集约 **147–162 MB**、私有内存约 101 MB。
- **无 sleep 轮询**：30 秒 `DispatcherQueueTimer` + Night Light 注册表通知 + 托盘/系统事件驱动。
- **壁纸预览限制解码尺寸**，避免 4K 图片占用大量 UI 内存。
- **关闭到托盘**后进程保留，再次打开窗口比冷启动快。
- 对比 v1（C++ / Win32，< 2 MB）内存明显上升；这是 WinUI 3 运行时的固定成本，已在 README 中如实说明。

### ⚠️ 不兼容变更

- **配置目录变更**：从 exe 同目录改为 `%LOCALAPPDATA%\LightSwitch\config.json`（首次启动尝试自动迁移）。
- **不再单 EXE 零依赖**：v2.0.0 默认构建自带 Windows App SDK，但需要 **.NET 10 Desktop Runtime**；v2.0.1 起默认发布已改为自包含。
- **外部编辑配置不再热重载**：v1 的 `FindFirstChangeNotification` 目录监听未移植；修改 `config.json` 后需重启应用。
- **壁纸为全局一对**：暂不支持按显示器分别设置、纯色、Spotlight。
- **旧 C++ 实现归档**：v1.1.0 源码、工程和构建脚本移动到 `legacy/`，不再维护。
- **系统要求**：Windows 10 1809+ / Windows 11 x64；Mica 仅 Windows 11，Windows 10 回退纯色背景。

### 🐛 开发期修复

- 修复 `AppearanceEngine` 在 `ApplySettings(evaluate:false)`、`ManualApply`、`ManualApplyWallpaper`、`Refresh` 中只构造局部状态、未写回 `_state`，导致 UI 状态快照滞后于实际主题的问题。
- 修复壁纸开关关闭时「应用」按钮不生效：`ApplyWallpaper` 改为仅在 `!changeWallpaper && !force` 时跳过。
- 修复已最小化窗口无法从托盘恢复：`ShowMainWindow` 先 `OverlappedPresenter.Restore()`。
- 修复 `RootGrid` 不透明背景遮住 Mica：改为透明，并在 Mica 不可用时回退主题背景色。
- 修复 `--startup` 在已有实例运行时误弹窗口。
- 修复 `Program.cs` lambda 参数命名导致的 `CS0029`，以及 `TrayHost` 缺少 `using LightSwitch.Core` 的编译错误。

---

## [v1.1.0] — 2026-10-31

### ✨ 界面重构（微软原生 SDK，零新增依赖）

- **全新 Win32 + GDI 自绘设置界面** — 仅使用 Windows SDK 自带的 User32 / GDI32 / ComCtl32，将主窗口重写为卡片式设置页：分段模式选择、开关控件、圆角输入框、操作按钮与状态区，替代原先固定坐标的经典控件；调度、托盘、热键、配置文件等行为完全不变
- **高 DPI 自适应** — 声明 Per-Monitor V2，窗口与字体随显示器缩放调整，不再在高缩放下模糊
- **深浅色跟随应用主题** — 界面按 `AppsUseLightTheme` 自动切换亮/暗两套配色，支持时同步深色标题栏与圆角窗口
- **输入体验改进** — 时间/坐标输入框支持 `Enter` 提交、`Esc` 还原、`Tab` 切换；时间自动规范为 `HH:MM`；经纬度增加范围校验（-90~90 / -180~180），非法输入回退并短暂高亮
- **Common-Controls v6 清单** — `app.manifest` 声明 Visual Styles 依赖，`build.bat` 通过 `/MANIFEST:EMBED` 内嵌清单
- **版本号** 1.0.3 → 1.1.0

### 🔧 内部

- 新增 `src/UiTheme.h`：配色表、DPI 缩放、圆角/圆形/文字绘制、字体创建、深色标题栏（动态加载 `dwmapi.dll`，不新增链接依赖）
- `src/MainWindow.h/.cpp` 重写为自绘窗口；保留 `WM_TRAYICON`、`WM_HOTKEY`、`WM_FILE_CHANGED`、托盘菜单等全部既有消息与行为
- `src/main.cpp` 增加 `InitCommonControlsEx`，移除 `IsDialogMessageW`（Tab 由编辑框子类处理）
- `src/resource.h` 精简为 4 个输入框 ID + 托盘/热键 ID

### 🐛 修复

- **`build.bat` 找不到 Visual Studio Build Tools** — `vswhere` 默认不返回 Build Tools 实例，脚本因此报 `[ERROR] Visual Studio not found`；查询补上 `-products *`
- **清单编译警告 RC 81010002** — `resources/app.manifest` 的 `requestedExecutionLevel` 补上 `requestedPrivileges`（`urn:schemas-microsoft-com:asm.v3`）外层节点，清单编译无警告
- **单实例检测误判** — `CreateMutexW` 前重置 `LastError` 并检查句柄非空，避免残留错误码让新进程误以为已有实例而直接退出

## [v1.0.4] — 2026-08-01

### 🐛 修复

- **编辑框输入被定时器/文件变更覆盖** — `RefreshUI()` 无条件调用 `SetWindowTextW` 更新所有编辑框，当用户正在输入经纬度或时间时，`WM_TIMER`（每 60 秒）或 `WM_FILE_CHANGED`（config.json 变更）触发 `RefreshUI()` 会覆盖用户尚未提交的输入内容，导致修改失败。修复：`RefreshUI()` 中对编辑框更新前检查 `GetFocus()`，仅当编辑框无焦点时才更新文本

## [v1.0.3] — 2026-07-24

### 🐛 修复

- **修改经纬度后日出日落时间更新延迟** — `EvaluateAndApplyIfNeeded()` 使用 `lastEvaluatedDay` 做每日缓存，同一天内修改经纬度不会触发 `UpdateSunTimes()` 重新计算，导致日出日落时间仍为旧坐标的结果。修复：在 `OnSettingsChanged()` 中将 `lastEvaluatedDay` 重置为 -1，强制重新计算

- **经纬度编辑框支持回车键提交** — 通过 `SetWindowSubclass` 子类化经纬度 Edit 控件，拦截 `VK_RETURN` 按键立即触发 `OnCoordinatesChanged()`，无需等待失去焦点

## [v1.0.2] — 2026-07-24

### 📝 文档

- **移除 AI agent 一键安装方法** — 从 README 中删除「AI agent 一键安装（绕过 SmartScreen 警告）」段落，避免引导用户临时关闭 Windows Defender

## [v1.0.1] — 2026-07-23

### 🐛 修复

- **托盘图标点击无反应（根因修复）** — `NOTIFYICON_VERSION_4` 下 lParam 格式为 `MAKELPARAM(鼠标消息, 图标ID)`，鼠标消息在 `LOWORD(lParam)` 中，`HIWORD(lParam)` 是图标 ID。代码使用 `(UINT)l` 整体比较，图标 ID 为 1 时 `0x00010202 != WM_LBUTTONUP(0x0202)`，匹配失败。修复为 `LOWORD(l)`，兼容 V4 和旧版两种消息格式

- **托盘图标透明不可见** — 修复图标渲染问题，确保托盘图标正常显示

### 2026-07-21 — README 重写 & 推送

- **重写 README.md** — 基于项目实际状态，包含完整的功能说明、架构设计、使用指南、构建步骤、对比表格和技术细节
- **推送所有修改** — 提交并推送 8 个修复文件 + CHANGELOG 到远程仓库
- **创建 GitHub Release v1.0.0** — 发布初始版本，附 `LightSwitch.exe` (352 KB) 下载
- ~~**README 新增 AI agent 一键安装方法**~~ — 已在 v1.0.2 移除

### ✨ 变更

- **PLAN.md v2 重写** — 基于 [PowerToys](https://github.com/microsoft/PowerToys) 源码直接复用策略重新设计方案
  - 逐文件复用 PowerToys LightSwitch 源码（ThemeHelper、ThemeScheduler、LightSwitchUtils、NightLightRegistryObserver 等）
  - 事件驱动架构（WaitForMultipleObjects），替代 Sleep 轮询，CPU 占用 ≈ 0%
  - 新增极简 Win32 GUI 设计（< 10 KB 额外开销）
  - 单进程模型，内存目标 < 2 MB
  - 零外部依赖，仅需 VC++ 运行时
  - 剔除所有 PowerToys 内部依赖（Logger、SettingsAPI、GPO、Trace、ETW）

### 2026-07-21 — Phase 1–5 完成

- **Phase 1: 源码移植**
  - 下载 PowerToys LightSwitch 核心源文件（ThemeHelper、ThemeScheduler、LightSwitchUtils、SettingsConstants、SettingsObserver、NightLightRegistryObserver）
  - 适配 ThemeHelper.cpp：移除 Logger 依赖，替换为 OutputDebugStringW
  - 创建 pch.h 预编译头，统一所有 Windows API 和标准库头文件

- **Phase 2: 配置管理（ConfigManager）**
  - 实现手写轻量 JSON 解析器（~200 行，零外部依赖，支持 string/int/bool 提取）
  - 实现 ConfigManager 单例类，读写 `config.json`
  - 配置文件热重载：通过 FindFirstChangeNotification 监听文件变更

- **Phase 3: 状态机适配（LightSwitchStateManager）**
  - 复用 PowerToys 核心状态机逻辑（EvaluateAndApplyIfNeeded、OnTick、OnManualOverride、OnNightLightChange）
  - 替换 PowerToysSettings::PowerToyValues 为 ConfigManager
  - 移除 NotifyPowerDisplayThemeChanged（无 PowerDisplay 依赖）
  - 保留日出日落每日重新计算、跨午夜处理、手动覆盖自动恢复等核心行为

- **Phase 4: GUI 实现**
  - **MainWindow**：纯 Win32 对话框（360×400），包含下拉框、复选框、编辑框、按钮
  - **TrayIcon**：Shell_NotifyIcon 系统托盘图标，双击显示/隐藏，右键菜单（切换/启用调度/打开/退出）
  - **消息循环**：WM_TIMER（每分钟）、WM_HOTKEY（全局热键 Ctrl+Alt+Shift+D）、WM_TRAYICON、WM_FILE_CHANGED、WM_NIGHTLIGHT_CHANGED
  - 状态栏实时更新当前主题状态

- **Phase 5: 构建系统**
  - 创建 `LightSwitch.vcxproj`（Visual Studio 2022，v143 工具集，C++17，Unicode，/SUBSYSTEM:WINDOWS）
  - 零外部依赖：kernel32.lib、user32.lib、comctl32.lib、advapi32.lib、shell32.lib
  - 支持 `/toggle` 命令行参数直接切换主题
  - 单实例运行（互斥锁 + 激活已有窗口）
  - **单 EXE 发布**：Release 模式静态链接 CRT（`/MT`），零 DLL 依赖，即拷即用
  - **资源文件**：`LightSwitch.rc` 嵌入 PowerToys 官方图标 + 版本信息（LightSwitch.exe 右键属性可见）
  - **构建脚本**：`build.bat` 一键 MSBuild 编译
  - **安装脚本**：`install.bat` 管理 HKCU 开机自启注册

### 2026-07-21 — 代码审查修复

- **修复 1** — 删除 `MainWindow.h` 中无定义的 `LayoutControls()` 死声明
- **修复 2** — 托盘图标改用运行时 GDI 绘制色块图标（亮色=暖黄，暗色=深蓝），消除 `LoadIconW` 系统图标 + 误用 `DestroyIcon` 的未定义行为
- **修复 3** — `MainWindow::RefreshUI()` 联动灰化经纬度输入框（仅「日出日落」模式可用）
- **修复 4** — `pch.h` 添加 `_countof` 跨编译器 fallback 宏（GCC/MinGW 兼容）
- **修复 5** — `main.cpp` 复用 `ThemeHelper.h` 中已定义的 `NIGHT_LIGHT_REGISTRY_PATH` 常量，消除路径硬编码重复
- **修复 6** — `ConfigManager.cpp` JSON 读写改用 `WideCharToMultiByte` / `MultiByteToWideChar` 标准 UTF-8 编解码，支持非 ASCII 配置值

### 2026-07-21 — 编译修复（首次成功编译）

- **修复 7** — `ConfigManager.cpp` 修复 `std::wsearch` 拼写错误 → `std::wstring search`
- **修复 8** — `SettingsObserver.h` 修复 C++17 不兼容的 `std::unordered_set::contains()` → `find() != end()`
- **修复 9** — `pch.h` 为 `_CRT_SECURE_NO_WARNINGS` 添加 `#ifndef` 防重定义保护
- **修复 10** — `LightSwitch.rc` 版本信息字符串改用 ASCII（RC 编译器不兼容 UTF-8 中文），语言改为 `LANG_ENGLISH`
- **修复 11** — `resources/LightSwitch.ico` 从 Base64 文本解码为正确的 ICO 二进制文件（32×32, 32bpp）
- **修复 12** — 构建脚本添加缺失的链接库 `gdi32.lib`（GDI 绑图操作）和 `ole32.lib`（COM 初始化）
- **更新 1** — `LightSwitch.vcxproj` 工具集从 `v143`（VS 2022）更新为 `v180`（匹配本机 VS 2026），添加 `ItemDefinitionGroup` 配置（C++17、`/utf-8`、`/MT`、链接库列表）
- **更新 2** — `build.bat` 重写为 `cl.exe` + `link.exe` 直接编译方式（绕过 MSBuild `VCTargetsPath` 环境变量问题），自动通过 `vswhere` 查找 Visual Studio 安装路径

### 已实现功能

- [x] 固定时间段切换（08:00 亮 / 20:00 暗，可配置）
- [x] 日出日落自动计算（基于经纬度，每日自动更新）
- [x] 跟随 Windows Night Light（联动夜间模式）
- [x] 一键手动切换（按钮 + 热键 + 托盘菜单）
- [x] 系统主题与应用主题独立控制
- [x] 极简 GUI 界面（Win32 原生窗口）
- [x] 事件驱动架构（零轮询）
- [x] 跨午夜场景处理
- [x] 手动覆盖自动恢复
- [x] 配置文件热重载
- [x] 系统托盘常驻
- [x] 首次编译成功 — 输出 x64/Release/LightSwitch.exe (360 KB)
