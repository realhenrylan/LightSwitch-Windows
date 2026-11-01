#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the LightSwitch setup executable with Inno Setup.

.DESCRIPTION
    By default the application is published first (self-contained win-x64), then
    installer\LightSwitch.iss is compiled with Inno Setup 6 into
    artifacts\LightSwitch-<version>-setup.exe.

    Inno Setup 6.5 or newer must be installed. The compiler is located on PATH,
    in the per-user Programs folder, in Program Files, or through its uninstall
    registry entry.

.EXAMPLE
    .\tools\build-installer.ps1

.EXAMPLE
    .\tools\build-installer.ps1 -SkipPublish -Version 2.0.2

.EXAMPLE
    .\tools\build-installer.ps1 -IsccPath "C:\Program Files\Inno Setup 6\ISCC.exe"
#>
[CmdletBinding()]
param(
    # Folder produced by build.ps1 -Publish. Defaults to artifacts\LightSwitch-win-x64.
    [string] $PublishDirectory,

    # Folder that receives the setup executable. Defaults to artifacts.
    [string] $OutputDirectory,

    # Overrides the version from Directory.Build.props.
    [string] $Version,

    # Explicit path to ISCC.exe.
    [string] $IsccPath,

    # Reuse an existing publish folder instead of running build.ps1 -Publish.
    [switch] $SkipPublish,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-ProjectVersion {
    param([string] $RepositoryRoot)

    $propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
    if (-not (Test-Path -LiteralPath $propsPath)) {
        throw "Missing $propsPath; cannot determine the version."
    }

    $raw = Get-Content -LiteralPath $propsPath -Raw
    $match = [regex]::Match($raw, '<Version>\s*([^<]+?)\s*</Version>')
    if (-not $match.Success) {
        throw "No <Version> element found in $propsPath."
    }

    return $match.Groups[1].Value.Trim()
}

function Resolve-Iscc {
    param([string] $ExplicitPath)

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "ISCC.exe was not found at '$ExplicitPath'."
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $candidates = @()

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }

    if ($env:LOCALAPPDATA) {
        $candidates += (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    }

    if (${env:ProgramFiles}) {
        $candidates += (Join-Path ${env:ProgramFiles} 'Inno Setup 6\ISCC.exe')
    }

    if (${env:ProgramFiles(x86)}) {
        $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $uninstallKeys = @(
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )

    foreach ($key in $uninstallKeys) {
        try {
            $entry = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
            if ($entry.InstallLocation) {
                $candidate = Join-Path $entry.InstallLocation 'ISCC.exe'
                if (Test-Path -LiteralPath $candidate) {
                    return (Resolve-Path -LiteralPath $candidate).Path
                }
            }
        }
        catch {
            # Key does not exist; try the next one.
        }
    }

    throw @'
Cannot find the Inno Setup 6 compiler (ISCC.exe).
Install it with one of:
  winget install --id JRSoftware.InnoSetup -e
  choco install innosetup
or pass -IsccPath <path to ISCC.exe>.
'@
}

if (-not $Version) {
    $Version = Get-ProjectVersion -RepositoryRoot $root
}

if ($Version -notmatch '^\d+\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$') {
    throw "'$Version' is not a valid version number."
}

$version4 = if ($Version -match '^\d+\.\d+\.\d+\.\d+$') { $Version } else { "$Version.0" }

$publish = if ($PublishDirectory) {
    $PublishDirectory
}
else {
    Join-Path $root 'artifacts\LightSwitch-win-x64'
}

$output = if ($OutputDirectory) {
    $OutputDirectory
}
else {
    Join-Path $root 'artifacts'
}

if (-not $SkipPublish) {
    Write-Host "Publishing LightSwitch $Version ..." -ForegroundColor Cyan
    & (Join-Path $root 'build.ps1') -Publish -Configuration $Configuration -OutputDirectory $publish
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing failed (exit $LASTEXITCODE)."
    }
}

$publish = (Resolve-Path -LiteralPath $publish).Path
$appExe = Join-Path $publish 'LightSwitch.exe'
if (-not (Test-Path -LiteralPath $appExe)) {
    throw "The publish folder '$publish' does not contain LightSwitch.exe. Run build.ps1 -Publish first."
}

# A running copy would keep files locked while Inno Setup packages them.
Get-Process -Name 'LightSwitch' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($publish, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object {
        Write-Host "Stopping running instance: $($_.Path)" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force
    }

$iss = Join-Path $root 'installer\LightSwitch.iss'
if (-not (Test-Path -LiteralPath $iss)) {
    throw "Missing installer script: $iss"
}

$languageFile = Join-Path $root 'installer\ChineseSimplified.isl'
if (-not (Test-Path -LiteralPath $languageFile)) {
    throw "Missing installer/ChineseSimplified.isl. See installer/README.md for the download URL."
}

$iscc = Resolve-Iscc -ExplicitPath $IsccPath
Write-Host "ISCC: $iscc" -ForegroundColor DarkGray

if (-not (Test-Path -LiteralPath $output)) {
    New-Item -ItemType Directory -Path $output -Force | Out-Null
}
$output = (Resolve-Path -LiteralPath $output).Path

$setup = Join-Path $output "LightSwitch-$Version-setup.exe"
if (Test-Path -LiteralPath $setup) {
    Remove-Item -LiteralPath $setup -Force
}

Write-Host "Compiling setup ($Version) ..." -ForegroundColor Cyan
& $iscc `
    '/Qp' `
    "/DMyAppVersion=$Version" `
    "/DMyAppVersion4=$version4" `
    "/DPublishDir=$publish" `
    "/DOutputDir=$output" `
    $iss
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed (exit $LASTEXITCODE). See the messages above."
}

if (-not (Test-Path -LiteralPath $setup)) {
    throw "ISCC reported success but '$setup' does not exist."
}

$item = Get-Item -LiteralPath $setup
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
$checksumFile = "$setup.sha256"
# Standard sha256sum format so the file can be verified on other machines.
Set-Content -LiteralPath $checksumFile -Value "$hash  $($item.Name)" -Encoding ASCII

Write-Host ''
Write-Host ("Setup:    {0}" -f $setup) -ForegroundColor Green
Write-Host ("Size:     {0:N1} MB" -f ($item.Length / 1MB)) -ForegroundColor Green
Write-Host ("SHA256:   {0}" -f $hash) -ForegroundColor Green
Write-Host ("Checksum: {0}" -f $checksumFile) -ForegroundColor DarkGray
