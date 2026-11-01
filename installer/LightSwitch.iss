; -----------------------------------------------------------------------------
; LightSwitch setup script (Inno Setup 6.5+)
;
; Build with tools\build-installer.ps1 (or build.ps1 -Publish -Installer).
; The script expects a completed self-contained publish folder; it does not
; compile the application itself.
;
; Defines (passed by the build script):
;   MyAppVersion   e.g. 2.0.2
;   MyAppVersion4  e.g. 2.0.2.0 (required by VersionInfoVersion)
;   PublishDir     path to artifacts\LightSwitch-win-x64
;   OutputDir      path to artifacts
; -----------------------------------------------------------------------------

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyAppVersion4
  #define MyAppVersion4 MyAppVersion + ".0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\LightSwitch-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#if !FileExists(PublishDir + "\LightSwitch.exe")
  #error The publish folder does not contain LightSwitch.exe. Run build.ps1 -Publish first, or pass /DPublishDir=<folder>.
#endif

#define MyAppName "LightSwitch"
#define MyAppPublisher "LightSwitch"
#define MyAppExeName "LightSwitch.exe"
#define MyAppUrl "https://github.com/realhenrylan/LightSwitch-Windows"
#define MyAppIssuesUrl MyAppUrl + "/issues"

[Setup]
; Keep this AppId stable: it is the identity used for upgrades and uninstall.
AppId={{32327A9C-EC6D-422C-B57F-85D44505E652}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppIssuesUrl}
AppUpdatesURL={#MyAppUrl}/releases
AppCopyright=Copyright (C) 2026 LightSwitch contributors
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; The app is a per-user application (HKCU + current user's wallpaper), so the
; installer never needs administrator rights. {autopf} resolves to
; %LOCALAPPDATA%\Programs in this mode.
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-setup
SetupIconFile=..\src\LightSwitch.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
LicenseFile=..\LICENSE
AllowNoIcons=yes
; The app lives in the tray and has no visible top-level window most of the
; time, so Restart Manager cannot close it reliably. [Code] stops it instead.
CloseApplications=no
RestartApplications=no
VersionInfoVersion={#MyAppVersion4}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion4}
VersionInfoProductTextVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoOriginalFileName={#MyAppName}-{#MyAppVersion}-setup.exe
VersionInfoCopyright=Copyright (C) 2026 LightSwitch contributors

[Languages]
; Chinese first: it is used when no other language matches the system locale.
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.StartupTask=登录 Windows 时自动启动 LightSwitch（推荐）
english.StartupTask=Start LightSwitch when I sign in (recommended)
chinesesimplified.StartupGroup=启动选项：
english.StartupGroup=Startup options:
chinesesimplified.EnablingStartup=正在启用开机自启...
english.EnablingStartup=Enabling start at sign-in...
chinesesimplified.LaunchApp=启动 LightSwitch
english.LaunchApp=Launch LightSwitch
chinesesimplified.DeleteUserData=是否同时删除 LightSwitch 的设置、日志和缓存？选择“否”将保留，便于以后重装。
english.DeleteUserData=Also delete LightSwitch settings, logs, and cache? Choose No to keep them for a future reinstall.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"

[Files]
; The publish folder already contains the .NET runtime, Windows App SDK,
; XAML artifacts and the app itself. Keep the layout intact.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Windows 主题与壁纸调度器"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "Windows 主题与壁纸调度器"

[Run]
; Enable start at sign-in only for a fresh install. On upgrade the current
; in-app setting is authoritative and must not be overwritten.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--enable-startup"; Flags: runhidden; Check: ShouldEnableStartup; StatusMsg: "{cm:EnablingStartup}"
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Registry]
; Delete the Run entry the app may have created (both fresh and upgraded
; installs), without creating it here: the app's StartupService owns it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "LightSwitch"; Flags: uninsdeletevalue

[UninstallRun]
; Stop the tray process before deleting its files. The uninstaller runs from
; %TEMP%, so it is safe to kill LightSwitch.exe by name.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExeName} /T"; Flags: runhidden; RunOnceId: "StopLightSwitch"

[Code]
var
  FreshInstall: Boolean;

function ShouldEnableStartup: Boolean;
begin
  Result := FreshInstall and WizardIsTaskSelected('startup');
end;

procedure StopLightSwitch;
var
  ResultCode: Integer;
begin
  { taskkill returns 128 when no matching process exists; that is fine. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(400);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    { A fresh install means the target folder does not contain the app yet.
      A portable copy may still be running, so always stop the process: the
      single-instance mutex would otherwise forward the installed app to it. }
    FreshInstall := not FileExists(ExpandConstant('{app}\{#MyAppExeName}'));
    StopLightSwitch;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { Silent uninstalls keep user data without prompting. Interactive
      uninstalls ask, with No as the default. }
    if not UninstallSilent then
    begin
      if MsgBox(ExpandConstant('{cm:DeleteUserData}'), mbConfirmation,
          MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(ExpandConstant('{localappdata}\LightSwitch'), True, True, True);
      end;
    end;
  end;
end;
