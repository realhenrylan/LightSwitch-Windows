# LightSwitch 路线图

本文件记录 v2.0.0 已完成的工作、当前限制与后续计划。v1 的历史计划保留在 [`legacy/PLAN-v1.md`](legacy/PLAN-v1.md)。

## 当前版本：v2.0.0

### 目标

1. 在原有主题调度能力上，增加**浅色 / 深色分别设置壁纸**，参考 [AutoDarkMode](https://github.com/AutoDarkMode/Windows-Auto-Night-Mode)。
2. 使用 **C# + WinUI 3 + Windows App SDK + Fluent Design** 重构界面与运行时，同时保留**托盘常驻**，并尽量控制资源占用。

### 已完成

- [x] Core 库：配置、主题注册表、Night Light、固定时间/日出日落调度、手动覆盖、自启、日志。
- [x] 壁纸服务：`IDesktopWallpaper` 多显示器应用、六种填充方式、状态去重、COM 失败回退 `SystemParametersInfo`。
- [x] 壁纸与主题管线联动：自动调度、手动切换、托盘菜单、全局热键、`--apply light|dark`。
- [x] WinUI 3 Fluent 界面：Mica（Win11）、深浅色资源、卡片布局、自定义标题栏、壁纸缩略图。
- [x] 托盘：隐藏窗口 + `Shell_NotifyIcon` V4 + 原生菜单；关闭/最小化到托盘；主窗口懒创建。
- [x] 单实例、命令行、`--startup` 静默启动、开机自启注册表项。
- [x] 配置迁移：`%LOCALAPPDATA%\LightSwitch\config.json`，首次运行导入旧配置。
- [x] 测试：零依赖控制台 harness，30 个用例全部通过。
- [x] 构建脚本：`build.ps1` / `build.bat`，支持 `-Test` 与 `-Publish`。
- [x] 文档：README、CHANGELOG、legacy 归档说明。

### 验收基线

| 项目 | 结果 |
| --- | --- |
| 构建 | `dotnet build LightSwitch.slnx -c Release`，0 警告 0 错误 |
| 测试 | 30 / 30 通过 |
| 托盘静默启动 | 约 91.6 MB 工作集 / 64.4 MB 私有内存 / 34 线程 |
| 主窗口打开 | 约 147–162 MB 工作集 / 101 MB 私有内存 |
| 输出体积 | 自包含约 170 MB / 461 个文件；框架依赖约 93 MB / 269 个文件 |
| 单实例 | 第二实例退出码 0，主实例窗口被唤起 |
| 系统要求 | Windows 10 1809+ / Windows 11 x64；默认发布无需 .NET；Mica 仅 Win11 |

## 后续计划

### v2.1 — 壁纸能力补全

- [ ] 按显示器分别设置浅色/深色壁纸（参考 ADM 的 Individual 模式）。
- [ ] 纯色背景与 Windows Spotlight 支持。
- [ ] 壁纸文件变化监听（图片被移动/删除时给出提示并保留原设置）。
- [ ] 壁纸历史与一键恢复。

### v2.2 — 轻量化与稳定性

- [ ] 评估将托盘与调度移入独立轻量进程或 Windows 服务，主 UI 按需启动，降低常驻内存。
- [ ] 使用 `Microsoft.WindowsAppSDK` 的 trimming/readiness 能力，减少输出体积。
- [ ] 配置目录热重载（带防抖，避免覆盖 UI 未保存编辑）。
- [ ] 崩溃恢复与更细的诊断日志（可选 ETW）。
- [ ] 高对比度、屏幕阅读器与键盘导航专项检查。

### v2.3 — 分发

- [ ] MSIX / 安装器，提供开始菜单快捷方式与卸载入口。
- [ ] 代码签名与 GitHub Release 自动化。
- [ ] ARM64 构建。
- [ ] 可选：winget / Scoop 清单。

## 非目标

- 不复制 AutoDarkMode 的全部功能（如 Office 主题、鼠标指针、系统强调色、壁纸轮播）。
- 不引入后台服务或驱动；保持单用户、免管理员权限。
- 不为追求 v1 的「< 2 MB」而牺牲 Fluent 界面与可维护性；内存基线在 README 中如实披露。
