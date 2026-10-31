# 变更日志

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
