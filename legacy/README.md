# v1.x（C++ / Win32 + GDI）归档

这里保存的是 LightSwitch **v1.1.0** 的原始 C++ 实现，仅用于查阅与历史归档，**不再维护**。

v2.0.0 已用 C# + WinUI 3 + Windows App SDK + Fluent Design 重写，功能、配置迁移和使用方式请回到仓库根目录的 [README](../README.md)。

## 内容

| 路径 | 说明 |
|------|------|
| `src/` | v1 的 C++ 源码（状态机、主题、日出日落、自绘界面、托盘） |
| `resources/` | v1 的图标与 `app.manifest` |
| `LightSwitch.vcxproj` | v1 的 MSBuild 工程（C++17，x64） |
| `LightSwitch.rc` | v1 的图标与版本资源 |
| `build.bat` / `install.bat` | v1 的构建、注册开机自启脚本 |
| `PLAN-v1.md` | v1 的设计与实施记录（仅本地保留，不纳入版本库） |

## 构建 v1（可选）

需要 Visual Studio 2022/2026 的「使用 C++ 的桌面开发」工作负载：

```powershell
cd legacy
.\build.bat
```

输出通常在 `legacy\x64\Release\LightSwitch.exe`。工程按相对路径引用 `src\` 与 `resources\`，因此整个 `legacy/` 目录可以独立打开。

v1 的 `config.json` 位于 exe 同目录。首次运行 v2 时，如果 `%LOCALAPPDATA%\LightSwitch\config.json` 不存在，v2 会尝试从 exe 同目录的旧 `config.json` 导入设置（包括旧的 `sunrise_offset` / `sunset_offset` 字段名）。
