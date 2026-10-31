# LightSwitch Standalone — 实施计划（v2）

> 基于 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) LightSwitch 模块源码直接复用，从零构建独立可执行程序。
> 设计目标：**极轻量 + 事件驱动 + 极简 GUI**

---

## 一、项目目标

在不安装/依赖 PowerToys 的前提下，复用其 LightSwitch 模块源码，构建一个 Windows 自动深浅色主题切换工具：

- **固定时间段切换**（如 08:00 亮 / 20:00 暗）
- **日出日落自动计算**（基于经纬度）
- **跟随 Windows Night Light**（联动夜间模式）
- **一键手动切换**
- **系统主题与应用主题独立控制**
- **极简 GUI 界面**（Win32 原生窗口，< 10 KB 额外开销）
- **事件驱动架构**（零轮询，CPU 占用 ≈ 0%）

---

## 二、核心原理

Windows 深浅色主题切换的本质是 **修改注册表 + 广播系统消息**。

### 2.1 注册表路径

```
HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize
```

| 键名 | 值 | 含义 |
|------|-----|------|
| `SystemUsesLightTheme` | `1`=亮, `0`=暗 | 系统主题（任务栏、开始菜单） |
| `AppsUseLightTheme` | `1`=亮, `0`=暗 | 应用主题（窗口、控件） |
| `ColorPrevalence` | 切亮色时需重置 | 标题栏颜色继承 |

### 2.2 广播消息

修改注册表后广播通知 Windows 刷新界面：

```cpp
SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
    (LPARAM)L"ImmersiveColorSet", SMTO_ABORTIFHUNG, 5000, nullptr);
SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0,
    SMTO_ABORTIFHUNG, 5000, nullptr);
SendMessageTimeout(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, 0, 0,
    SMTO_ABORTIFHUNG, 5000, nullptr);
```

### 2.3 夜间模式检测

Night Light 状态存储在二进制注册表数据中：

```
HKCU\\...\\CloudStore\\...\\windows.data.bluelightreduction.bluelightreductionstate
```

读取 `Data` 二进制值，第 24 字节（索引 23）=`0x10` 且第 25 字节（索引 24）=`0x00` 时开启。

---

## 三、源码复用策略

直接从 PowerToys 仓库复制源文件，**逐文件复用**，仅做最小化适配（去掉 PowerToys 内部依赖，替换为 Win32 原生实现）。

### 3.1 直接复用的文件（零修改/微调）

| PowerToys 源文件 | 本项目路径 | 修改内容 |
|---|---|---|
| `LightSwitchLib/ThemeHelper.h` | `src/ThemeHelper.h` | 去掉 logger 引用，保留纯 Win32 API 实现 |
| `LightSwitchLib/ThemeHelper.cpp` | `src/ThemeHelper.cpp` | 移除 `Logger::info` 调用，用 `OutputDebugString` 替代 |
| `LightSwitchService/ThemeScheduler.h` | `src/ThemeScheduler.h` | **完全不变** |
| `LightSwitchService/ThemeScheduler.cpp` | `src/ThemeScheduler.cpp` | **完全不变** |
| `LightSwitchService/LightSwitchUtils.h` | `src/LightSwitchUtils.h` | **完全不变** |
| `LightSwitchService/SettingsConstants.h` | `src/SettingsConstants.h` | **完全不变** |
| `LightSwitchService/SettingsObserver.h` | `src/SettingsObserver.h` | **完全不变**（保留接口以备扩展） |
| `LightSwitchService/NightLightRegistryObserver.h` | `src/NightLightRegistryObserver.h` | **完全不变**（纯 Win32 实现，零依赖） |
| `LightSwitchService/NightLightRegistryObserver.cpp` | 合并入 header-only | 已是 header-only，无需 .cpp |

### 3.2 需要适配的文件

| 源文件 | 适配策略 |
|---|---|
| `LightSwitchService/LightSwitchStateManager.h/.cpp` | 保留核心状态机逻辑，去掉 PowerToys 设置系统依赖，改用本地 JSON + `RegNotifyChangeKeyValue` |
| `LightSwitchService/LightSwitchSettings.h/.cpp` | 重写：去掉 `PowerToysSettings::PowerToyValues`，改用 `std::ifstream` / nlohmann/json 直接读写 `config.json` |
| `LightSwitchService/LightSwitchService.cpp` | 重写入口：去掉 Windows Service 注册，改为普通 Win32 进程 + 消息循环 |
| `LightSwitchModuleInterface/dllmain.cpp` | 只复用热键解析逻辑和 `ToggleTheme()` |

### 3.3 剔除的 PowerToys 依赖

| PowerToys 依赖 | 替换方案 |
|---|---|
| `Logger` / `logger.h` | `OutputDebugStringW` + 可选日志文件 |
| `PowerToysSettings::PowerToyValues` | nlohmann/json |
| `FileWatcher`（内部类） | `RegNotifyChangeKeyValue` / `FindFirstChangeNotification` |
| `common/SettingsAPI/` | 全量删除 |
| `powertoys_gpo` | 全量删除 |
| `Trace` / ETW | 全量删除 |
| `WinHookEventIDs` | 全量删除 |
| Windows Service | 改为普通用户态进程 |
| `PowertoyModuleIface` | 全量删除 |

---

## 四、架构设计

### 4.1 事件驱动模型（零轮询）

```
┌─────────────────────────────────────────────────────────┐
│                    LightSwitch.exe                       │
│                                                         │
│  ┌──────────────┐    ┌──────────────────────────────┐   │
│  │  极简 GUI     │    │       核心调度引擎             │   │
│  │  (Win32 窗口)  │───│  LightSwitchStateManager     │   │
│  │  + 托盘图标   │    │  (来自 PowerToys 源码)       │   │
│  └──────────────┘    └──────────┬───────────────────┘   │
│                                 │                       │
│         ┌───────────────────────┼───────────────────┐   │
│         │     WaitForMultipleObjects (事件驱动)      │   │
│         │                                           │   │
│         ├─ 注册表变更通知 (RegNotifyChangeKeyValue)  │   │
│         ├─ 配置文件变更 (FindFirstChangeNotification)│   │
│         ├─ 整点分钟信号 (waitable timer)             │   │
│         ├─ 手动切换事件 (Event object)               │   │
│         └─ 退出事件                                 │   │
│         └───────────────────────────────────────────┘   │
│                                                         │
│  ┌──────────────┐    ┌──────────────────────────────┐   │
│  │ ThemeHelper  │    │     ThemeScheduler           │   │
│  │ (注册表读写) │    │   (日出日落天文算法)          │   │
│  └──────────────┘    └──────────────────────────────┘   │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │ NightLightRegistryObserver                      │   │
│  │ (注册表监听，Night Light 变化时自动通知)         │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

**关键设计决策**：

1. **不使用 `Sleep(60s)` 轮询** — 改为 `WaitForMultipleObjects` + 可等待定时器，CPU 占用 = 0
2. **不使用 Windows Service** — 普通用户态进程（无需管理员），内存占用 < 2 MB
3. **不使用任何 GUI 框架** — 纯 Win32 API 创建窗口，无 .NET / MFC / Qt 依赖
4. **单进程架构** — 不像 PowerToys 那样分 ModuleInterface（DLL）+ Service（EXE），而是合并为一个 EXE

### 4.2 内存与性能目标

| 指标 | 目标 | PowerToys 对比 |
|------|------|----------------|
| 进程内存占用 | **< 2 MB** | ~15-30 MB（含 .NET 运行时） |
| CPU 空闲占用 | **0%** | < 0.1%（但有 .NET 后台线程） |
| 二进制体积 | **< 100 KB** | ~500 KB（多个 DLL + EXE） |
| 启动时间 | **瞬时** | ~1-2s（加载 .NET 运行时） |
| 外部依赖 | **零**（除 VC++ 运行时） | 依赖 .NET 6+、PowerToys 基础设施 |
| GUI 开销 | **< 10 KB** | ~50+ MB（整个 Settings 页面） |

### 4.3 调度模式

来自 PowerToys 的 `ScheduleMode` 枚举（直接复用）：

```cpp
enum class ScheduleMode {
    Off,            // 关闭自动调度
    FixedHours,     // 固定时间段（如 08:00–20:00）
    SunsetToSunrise, // 日出日落模式
    FollowNightLight // 跟随 Windows Night Light
};
```

---

## 五、极简 GUI 设计

### 5.1 主窗口布局

一个 Win32 对话框，尺寸约 360×320 px，不可缩放：

```
┌──────────────────────────────────────┐
│  LightSwitch — 自动深浅色切换    ─ □ × │
├──────────────────────────────────────┤
│                                      │
│  调度模式:  [固定时间段 ▾]           │
│                                      │
│  ┌─ 系统主题  ☑                     │
│  └─ 应用主题  ☑                     │
│                                      │
│  亮色时间:  [08:00]  暗色时间: [20:00] │
│                                      │
│  纬度: [  39.9  ]  经度: [ 116.4  ]  │
│                                      │
│  ┌──────────────────────────────┐    │
│  │  [☀ 手动切换为亮色]          │    │
│  │  [☾ 手动切换为暗色]          │    │
│  └──────────────────────────────┘    │
│                                      │
│  当前状态: ☀ 亮色模式 (自动调度中)    │
│                                      │
└──────────────────────────────────────┘
```

### 5.2 系统托盘图标

- 双击托盘图标 → 显示/隐藏主窗口
- 右键菜单：
  ```
  ┌──────────────────────┐
  │  切换为亮色/暗色      │
  │──────────────────────┤
  │  启用/禁用自动调度    │
  │──────────────────────┤
  │  打开设置窗口         │
  │  退出                 │
  └──────────────────────┘
  ```
- 托盘图标颜色随当前主题变化（☀ 亮 / ☾ 暗）
- 工具提示显示当前状态："LightSwitch — 亮色模式 (自动)"

### 5.3 交互逻辑

| 控件 | 行为 |
|------|------|
| 调度模式下拉框 | 切换模式时立即评估是否需要切换 |
| 系统/应用主题复选框 | 独立控制是否修改对应注册表项 |
| 亮色/暗色时间选择器 | 修改后自动保存到 config.json |
| 纬度/经度输入框 | 仅在日出日落模式下启用，回车即保存 |
| 手动切换按钮 | 点击后立即切换并保存手动覆盖状态 |
| 窗口关闭按钮 | 隐藏到托盘（不退出程序） |

### 5.4 Win32 控件选型

仅使用标准 Win32 控件（`Comctl32.dll` / `User32.dll`）：

| 控件类型 | Win32 类 | 用途 |
|----------|----------|------|
| 下拉选择框 | `ComboBox` | 调度模式选择 |
| 复选框 | `Button` (BS_CHECKBOX) | 系统/应用主题控制 |
| 时间选择器 | `SysDateTimePick32` | 亮色/暗色时间设置 |
| 编辑框 | `Edit` | 纬度/经度输入 |
| 按钮 | `Button` | 手动切换、保存 |
| 静态文本 | `Static` | 状态显示、标签 |
| 托盘图标 | `Shell_NotifyIcon` | 系统托盘 |
| 弹出菜单 | `TrackPopupMenu` | 右键菜单 |

---

## 六、文件结构

```
Light-Dark mode/
├─── PLAN.md                  # 本文档 — 实施计划
├─── README.md                # 项目说明
├─── CHANGELOG.md             # 变更日志
├─── LightSwitch.vcxproj       # MSBuild 项目文件
├─── LightSwitch.rc           # 资源文件（图标 + 版本信息）
├─── build.bat                # 构建脚本
├─── install.bat              # 安装/卸载脚本
├─── config.json              # 运行时配置文件（自动生成）
├─── .gitignore
├─── resources/
│   ├─── LightSwitch.ico        # 程序图标
│   └─── app.manifest           # 兼容性清单（Windows 10/11）
├─── src/                     # 源文件目录
│   ├─── pch.h                   # 预编译头
│   ├─── main.cpp                # 入口函数 + WinMain + 消息循环
│   │
│   │   # —— 来自 PowerToys 的源文件（直接复用） ——
│   ├─── ThemeHelper.h        # [PowerToys] 注册表读写
│   ├─── ThemeHelper.cpp      # [PowerToys] 主题切换实现
│   ├─── ThemeScheduler.h     # [PowerToys] 日出日落计算头文件
│   ├─── ThemeScheduler.cpp   # [PowerToys] 天文算法
│   ├─── LightSwitchUtils.h   # [PowerToys] ShouldBeLight / GetNowMinutes
│   ├─── SettingsConstants.h  # [PowerToys] 设置枚举
│   ├─── SettingsObserver.h   # [PowerToys] 观察者接口
│   ├─── NightLightRegistryObserver.h # [PowerToys] 注册表监听
│   │
│   │   # —— 适配/重写的文件 ——
│   ├─── LightSwitchStateManager.h  # [适配] 状态机
│   ├─── LightSwitchStateManager.cpp # [适配] 核心调度逻辑
│   ├─── ConfigManager.h            # [新写] JSON 配置管理
│   ├─── ConfigManager.cpp          # [新写] 配置读写
│   │
│   │   # —— GUI 界面 ——
│   ├─── MainWindow.h         # [新写] 主窗口封装
│   ├─── MainWindow.cpp       # [新写] Win32 窗口 + 控件
│   ├─── TrayIcon.h           # [新写] 系统托盘封装
│   ├─── TrayIcon.cpp         # [新写] 托盘图标 + 右键菜单
│   └─── resource.h           # 资源 ID 定义
└─── build/                  # 构建输出（gitignore）
```

---

## 七、构建系统

### 7.1 项目文件（`LightSwitch.vcxproj`）

直接使用 MSBuild（Visual Studio 2022），与 PowerToys 构建系统一致：

**关键配置：**
- 平台工具集：`v143` (VS 2022)
- C++ 标准：`/std:c++17`
- 配置类型：`.exe`
- 字符集：Unicode
- 优化：`/O1`（最小体积）
- 链接：`/SUBSYSTEM:WINDOWS`（窗口应用）
- 运行时库：`/MT`（静态链接 CRT，无需 vc_redist）
- 依赖：仅 `kernel32.lib`, `user32.lib`, `comctl32.lib`, `advapi32.lib`, `shell32.lib`

**不需要的外部依赖：**
- ❌ nlohmann-json（用手写轻量解析器替代）
- ❌ 通过 vcpkg 管理的任何第三方库
- ❌ .NET / CLR

### 7.2 构建步骤

```powershell
# 使用 Visual Studio 2022 开发者命令提示符
build.bat

# 也可直接用 MSBuild
msbuild LightSwitch.vcxproj /p:Configuration=Release /p:Platform=x64

# 输出
x64\Release\LightSwitch.exe
```

### 7.3 依赖清单

```
kernel32.lib         → 进程/线程/注册表 API
user32.lib           → 窗口/消息/控件
comctl32.lib         → SysDateTimePick32 时间选择器
advapi32.lib         → RegNotifyChangeKeyValue
shell32.lib          → Shell_NotifyIcon（托盘图标）
```

**零外部依赖，仅需 VC++ 运行时（所有 Windows 10/11 系统自带）。**

---

## 八、调度引擎设计

### 8.1 主消息循环

```
WinMain()
  │
  ├─ 创建主窗口（隐藏启动，仅显示托盘图标）
  ├─ 加载 config.json
  ├─ 注册系统热键（Ctrl+Alt+Shift+D 切换主题）
  │
  └─ 消息循环 (GetMessage / DispatchMessage)
       │
       ├─ WM_TIMER (每分钟一次)
       │    └─ LightSwitchStateManager::OnTick()
       │
       ├─ 注册表变更通知 (WM_USER + REG_NOTIFY)
       │    └─ NightLightRegistryObserver 回调
       │    └─ LightSwitchStateManager::OnNightLightChange()
       │
       ├─ 配置文件变更 (WM_USER + FILE_CHANGE)
       │    └─ 重新加载 config.json
       │    └─ LightSwitchStateManager::OnSettingsChanged()
       │
       ├─ 热键消息 (WM_HOTKEY)
       │    └─ ToggleTheme()
       │    └─ LightSwitchStateManager::OnManualOverride()
       │
       └─ 托盘图标消息 (WM_TRAYICON)
            └─ 双击 → 显示/隐藏主窗口
            └─ 右键 → 弹出菜单
```

### 8.2 状态管理

直接从 PowerToys 复用的 `LightSwitchStateManager` 状态机（核心逻辑不变）：

```cpp
struct LightSwitchState
{
    ScheduleMode lastAppliedMode = ScheduleMode::Off;
    bool isManualOverride = false;    // 用户手动覆盖模式
    bool isSystemLightActive = false; // 当前系统主题缓存
    bool isAppsLightActive = false;   // 当前应用主题缓存
    bool isNightLightActive = false;  // Night Light 状态缓存
    int lastEvaluatedDay = -1;        // 日出日落模式：缓存日期
    int lastTickMinutes = -1;         // 上次评估时间

    int effectiveLightMinutes = 0;   // 解析后的亮色边界
    int effectiveDarkMinutes = 0;    // 解析后的暗色边界
};
```

**关键特性——手动覆盖自动恢复（来自 PowerToys 的核心行为）：**

当用户手动切换主题后，调度引擎进入 `isManualOverride = true` 状态。此后在每次 `OnTick()` 时检查是否跨越了亮/暗时间边界，如果跨边界则自动清除 `isManualOverride` 标志，恢复自动调度。

### 8.3 跨午夜处理

直接复用 PowerToys 的 `ShouldBeLight()` 函数：

```cpp
// 当 lightTime > darkTime 时表示跨越午夜
// 例如 lightTime=22:00(1320), darkTime=06:00(360)
// 此时在 22:00-06:00 之间为亮色模式
```

---

## 九、配置文件格式

### config.json

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
  "sunset_offset": 0
}
```

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `scheduleMode` | string | `"FixedHours"` | `Off` / `FixedHours` / `SunsetToSunrise` / `FollowNightLight` |
| `changeSystem` | bool | `true` | 是否控制系统主题 |
| `changeApps` | bool | `true` | 是否控制应用主题 |
| `lightTime` | int | `480` | 亮色时间（距午夜分钟数，08:00） |
| `darkTime` | int | `1200` | 暗色时间（距午夜分钟数，20:00） |
| `latitude` | string | `"39.9"` | 纬度（十进制度数） |
| `longitude` | string | `"116.4"` | 经度（十进制度数） |
| `sunrise_offset` | int | `0` | 日出偏移量（分钟） |
| `sunset_offset` | int | `0` | 日落偏移量（分钟） |

---

## 十、与 PowerToys LightSwitch 的行为差异

| 行为 | PowerToys 实现 | 本项目实现 | 原因 |
|------|---------------|-----------|------|
| **进程模型** | DLL (ModuleInterface) + Service (EXE) | 单 EXE | 简化架构，节省内存 |
| **调度触发** | Sleep(60s) 轮询 + 事件 | WaitForMultipleObjects 事件驱动 | CPU 零占用 |
| **设置存储** | `PowerToysSettings::PowerToyValues` | 手写 JSON 解析 | 消除 PowerToys 依赖 |
| **配置文件** | `settings.json`（含 schema） | `config.json` | 简化结构 |
| **设置变更通知** | `FileWatcher` 内部类 + debounce 线程 | `FindFirstChangeNotification` + 窗口消息 | 减少一个线程 |
| **日志** | `Logger` (ETW + 文件) | `OutputDebugString` | 轻量化，零文件 I/O |
| **热键** | PowerToys Runner 管理 | `RegisterHotKey` | 直接注册，无需中间层 |
| **GUI** | 通过 PowerToys Settings 页面（.NET WPF） | 纯 Win32 对话框 | 极简，零额外运行时 |
| **主题通知** | 通过 Event 通知 PowerDisplay 模块 | **不需要** | 无 PowerDisplay 依赖 |
| **开机自启** | 通过 PowerToys Runner | 通过 `HKCU\\...\\Run` 或 schtasks | 用户可选择设置 |

---

## 十一、GUI 与后台的交互

GUI 与核心调度引擎运行在**同一进程、同一线程**（Win32 消息循环既是 GUI 消息泵也是调度引擎的驱动）：

```
Win32 消息循环
    │
    ├── WM_COMMAND (用户点击按钮/选择下拉)
    │       ↓
    │   ConfigManager::SetXXX() → 更新 config.json
    │       ↓
    │   LightSwitchStateManager::OnSettingsChanged()
    │       ↓
    │   EvaluateAndApplyIfNeeded() → 必要时切换主题
    │       ↓
    │   GUI 状态栏更新
    │
    ├── WM_TIMER (每分钟)
    │       ↓
    │   LightSwitchStateManager::OnTick()
    │       ↓
    │   若状态变化 → UpdateStatusBar()
    │
    ├── WM_TRAYICON (托盘操作)
    │       ↓
    │   切换主题 / 显示窗口 / 退出
    │
    └── WM_HOTKEY (全局热键)
            ↓
        ToggleTheme()
```

**不引入额外线程** — GUI 刷新、配置读写、主题切换都在消息循环中同步完成。唯一可能需要线程的是 `NightLightRegistryObserver`（它自身即包含一个后台线程用于 `RegNotifyChangeKeyValue`，这是 PowerToys 源码自带的，直接复用）。

---

## 十二、实施步骤

### Phase 1 — 源码移植（复用 PowerToys 文件）

1. 从 PowerToys 仓库复制以下文件到 `src/`：
   - `ThemeHelper.h` / `ThemeHelper.cpp`
   - `ThemeScheduler.h` / `ThemeScheduler.cpp`
   - `LightSwitchUtils.h`
   - `SettingsConstants.h`
   - `SettingsObserver.h`
   - `NightLightRegistryObserver.h`
2. 对 `ThemeHelper.cpp` 做最小修改：去掉 `Logger` 引用，替换为 `OutputDebugStringW`
3. 创建 `pch.h` 预编译头，包含所有公共 Windows 头文件

### Phase 2 — 配置管理（ConfigManager）

1. 实现手写 JSON 解析器
2. 实现 `ConfigManager`：读/写 `config.json`，变更通知
3. 使用 `FindFirstChangeNotification` 监听文件变化

### Phase 3 — 状态机适配（LightSwitchStateManager）

1. 复制 `LightSwitchStateManager.h/.cpp` 从 PowerToys
2. 替换 PowerToys 设置系统为 `ConfigManager`
3. 去掉 `NotifyPowerDisplayThemeChanged` 方法
4. 保留核心状态评估逻辑

### Phase 4 — GUI 实现

1. 创建资源文件 `resource.h`（定义控件 ID）
2. 实现 `TrayIcon` 类（`Shell_NotifyIcon` + 右键菜单）
3. 实现 `MainWindow` 类
4. 实现 `WinMain`：创建窗口 → 注册热键 → 消息循环

### Phase 5 — 构建与测试

1. 编写 `LightSwitch.vcxproj` 项目文件
2. 配置 Release x64 编译
3. 验证所有功能

---

## 十三、测试矩阵

| 测试场景 | 预期结果 |
|----------|----------|
| 固定时间 08:00–20:00 | 08:00 切亮色，20:00 切暗色 |
| 日出日落模式（北京 39.9,116.4） | 按当天天文时间切换 |
| 跟随 Night Light | Night Light 开 → 暗色，关 → 亮色 |
| 手动切换 → 跨越时间边界 | 自动恢复调度 |
| 仅系统主题关闭 | 只切换应用主题，系统主题不变 |
| 修改 config.json 后保存 | 热重载，立即生效 |
| 跨午夜（22:00–06:00） | 22:00 亮色，06:00 暗色 |
| 系统主题 → 手动覆盖 → 回正 | 状态机正确追踪 |
| 窗口关闭 | 隐藏到托盘，进程继续运行 |
| 右键菜单退出 | 进程完全退出 |

---

## 十四、注意事项

1. **权限**：修改 `HKEY_CURRENT_USER` 无需管理员权限。
2. **兼容性**：Windows 10 1809+ / Windows 11。
3. **天文算法局限**：极圈以内（纬度 `|lat| >= 66.5°`）可能出现极昼/极夜，算法会返回边界值。
4. **无外部依赖**：唯一需要的运行时是 VC++ 运行时（链接为 `/MT` 静态链接）。
5. **与 PowerToys 共存**：本程序与 PowerToys LightSwitch 互不干扰（使用独立配置文件）。

---

## 十五、参考来源

- [PowerToys — LightSwitch 源码](https://github.com/microsoft/PowerToys/tree/main/src/modules/LightSwitch)（主仓库：[microsoft/PowerToys](https://github.com/microsoft/PowerToys)）
  - `LightSwitchLib/ThemeHelper.cpp/.h` — 主题读写（直接复用）
  - `LightSwitchService/ThemeScheduler.cpp/.h` — 日出日落计算（直接复用）
  - `LightSwitchService/LightSwitchStateManager.cpp/.h` — 状态调度（适配后复用）
  - `LightSwitchService/LightSwitchUtils.h` — 工具函数（直接复用）
  - `LightSwitchService/SettingsConstants.h` — 设置枚举（直接复用）
  - `LightSwitchService/NightLightRegistryObserver.h` — 注册表监听（直接复用）
  - `LightSwitchService/SettingsObserver.h` — 观察者模式（直接复用）
  - `LightSwitchModuleInterface/dllmain.cpp` — 热键与切换逻辑（参考）
  - `LightSwitchService/LightSwitchService.cpp` — 服务入口（架构参考）

---

## 十六、下一步行动

当前项目已完成 **Phase 1–5 的全部编码**，下一步是：

### ① 安装编译器并编译

```powershell
# 安装 Visual Studio 2022 Build Tools（~1.5 GB）
# https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022
# 勾选「使用 C++ 的桌面开发」

# 在开发者命令提示符中编译
cd D:\GitHub\Light-Dark mode
build.bat

# 输出：x64\Release\LightSwitch.exe
```

### ② 功能验证

编译后按[测试矩阵](#十三测试矩阵)逐项验证。

### ③ 安装到开机自启

```powershell
install.bat   # 选 1 → 注册到 HKCU 开机启动
```

### ④ 可选迭代

| 方向 | 说明 |
|------|------|
| 🐛 Bug 修复 | 根据实际使用反馈修复问题 |
| 📦 打包发布 | 用 Inno Setup 制作安装包，发布 GitHub Release |
| 🎨 美化图标 | 用 PowerToys 原版 `LightSwitch.ico`（256×256 多分辨率）替换 |
| 🌐 自动获取经纬度 | 调用 Windows Location API 或 IP 定位 |
| 🔧 新增功能 | 多语言、多计划配置、日志文件等 |

---

## 十七、v1.1.0 界面重构（2026-10-31）

> 本节替代第五节中「极简 Win32 GUI」的坐标式布局描述；功能设计不变。

- **技术栈**：纯 Win32 + GDI + ComCtl32（Windows SDK），不引入 WinUI / Windows App SDK / Qt，保持零第三方依赖
- **窗口**：固定尺寸 420×540 逻辑像素，卡片式布局；双缓冲绘制；Per-Monitor V2 高 DPI；深色标题栏通过 `DwmSetWindowAttribute` 动态调用
- **控件**：分段模式选择、开关、圆角输入框、操作按钮、状态区全部自绘；时间/坐标保留原生 `EDIT` 子窗口
- **新增文件**：`src/UiTheme.h`（配色 / 绘制工具 / 字体 / DPI）
- **行为不变**：配置格式、调度逻辑、托盘菜单、热键、命令行 `/toggle`、README 功能说明均未改变

---

*计划编制日期：2026-07-21 | 版本：v2（极轻量 + 源码复用 + 极简 GUI）*
*最后更新：2026-10-31 — v1.1.0 界面重构（Win32 + GDI 自绘界面）*