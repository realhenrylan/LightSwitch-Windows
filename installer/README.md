# Windows installer

This folder contains the [Inno Setup](https://jrsoftware.org/isinfo.php) script for LightSwitch.

## Build

Install Inno Setup 6.5 or newer, then run from the repository root:

```powershell
.\build.ps1 -Publish -Installer
```

Or reuse an existing publish folder:

```powershell
.\tools\build-installer.ps1 -SkipPublish
```

The setup executable and its SHA256 file are written to `artifacts\`:

```text
artifacts\LightSwitch-<version>-setup.exe
artifacts\LightSwitch-<version>-setup.exe.sha256
```

## Behavior

- Per-user install; no administrator rights. The default folder is `%LOCALAPPDATA%\Programs\LightSwitch`.
- A Start Menu shortcut is created. The desktop shortcut is optional.
- Run at startup is checked by default on a fresh install. It is applied through `LightSwitch.exe --enable-startup`; upgrades leave the existing setting alone.
- Uninstall is registered under Settings > Apps and removes the install folder, shortcuts, and the HKCU Run value.
- Uninstall keeps `%LOCALAPPDATA%\LightSwitch` (settings, logs, cache) by default. An interactive uninstall can delete it; a silent uninstall always keeps it.
- Silent install / uninstall examples:

```powershell
Start-Process .\LightSwitch-<version>-setup.exe -Wait -ArgumentList `
  '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-'

Start-Process "$env:LOCALAPPDATA\Programs\LightSwitch\unins000.exe" -Wait -ArgumentList `
  '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART'
```

## Languages

`ChineseSimplified.isl` is the official Simplified Chinese language file from the
[Inno Setup source repository](https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl).
Keep its header intact when updating it. The installer shows Chinese first and
English second.
