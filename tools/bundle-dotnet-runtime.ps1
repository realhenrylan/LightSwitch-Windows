#Requires -Version 5.1
<#
.SYNOPSIS
    Adds the .NET runtime to a framework-dependent LightSwitch publish folder.

.DESCRIPTION
    LightSwitch publishes as framework-dependent because the Windows App SDK is
    already bundled separately. This script turns that folder into a portable,
    self-contained layout without needing the .NET runtime pack from NuGet:

      1. Copy shared\Microsoft.NETCore.App\<version> into the publish folder.
      2. Copy host\fxr\<version>\hostfxr.dll next to the app.
      3. Rewrite runtimeconfig.json to use includedFrameworks.
      4. Merge the runtime assets into LightSwitch.deps.json.

    The result runs without DOTNET_ROOT and without a machine-wide .NET install.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $DotnetRoot
)

$ErrorActionPreference = 'Stop'

$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$dotnetRoot = (Resolve-Path -LiteralPath $DotnetRoot).Path
$runtimeRoot = Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App'
if (-not (Test-Path -LiteralPath $runtimeRoot)) {
    throw "Cannot find the .NET runtime folder: $runtimeRoot"
}

$runtimeDir = Get-ChildItem -LiteralPath $runtimeRoot -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $runtimeDir) {
    throw "No .NET runtime found under $runtimeRoot"
}

$runtimeVersion = $runtimeDir.Name
$fxrRoot = Join-Path $dotnetRoot 'host\fxr'
$fxrDir = Get-ChildItem -LiteralPath $fxrRoot -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $fxrDir) {
    throw "No hostfxr found under $fxrRoot"
}

$runtimeConfigPath = Join-Path $publish 'LightSwitch.runtimeconfig.json'
$depsPath = Join-Path $publish 'LightSwitch.deps.json'
if (-not (Test-Path -LiteralPath $runtimeConfigPath)) { throw "Missing $runtimeConfigPath" }
if (-not (Test-Path -LiteralPath $depsPath)) { throw "Missing $depsPath" }

Write-Host "Bundling .NET $runtimeVersion into $publish ..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $runtimeDir.FullName '*') -Destination $publish -Recurse -Force
Copy-Item -LiteralPath (Join-Path $fxrDir.FullName 'hostfxr.dll') -Destination $publish -Force

# runtimeconfig.json: replace "framework" with "includedFrameworks".
$config = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
$runtimeOptions = $config.runtimeOptions
$runtimeOptions.PSObject.Properties.Remove('framework')
$included = @([pscustomobject]@{ name = 'Microsoft.NETCore.App'; version = $runtimeVersion })
$runtimeOptions | Add-Member -NotePropertyName includedFrameworks -NotePropertyValue $included -Force
$json = $config | ConvertTo-Json -Depth 30
[System.IO.File]::WriteAllText($runtimeConfigPath, $json, (New-Object System.Text.UTF8Encoding($false)))

# deps.json: import the runtime's own assets under Microsoft.NETCore.App/<version>.
$app = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$runtimeDepsPath = Join-Path $runtimeDir.FullName 'Microsoft.NETCore.App.deps.json'
$runtimeDeps = Get-Content -LiteralPath $runtimeDepsPath -Raw | ConvertFrom-Json
$runtimeTargetName = $runtimeDeps.runtimeTarget.name
$runtimeTarget = $runtimeDeps.targets.PSObject.Properties[$runtimeTargetName].Value
$runtimeLibraryKey = $runtimeTarget.PSObject.Properties.Name |
    Where-Object { $_ -like 'Microsoft.NETCore.App*' } |
    Select-Object -First 1
if (-not $runtimeLibraryKey) { throw 'No Microsoft.NETCore.App library in the runtime deps file.' }

$libraryName = "Microsoft.NETCore.App/$runtimeVersion"
$appTargetName = $app.runtimeTarget.name
$appTarget = $app.targets.PSObject.Properties[$appTargetName].Value
$appTarget | Add-Member -NotePropertyName $libraryName -NotePropertyValue $runtimeTarget.$runtimeLibraryKey -Force
$app.libraries | Add-Member -NotePropertyName $libraryName -NotePropertyValue $runtimeDeps.libraries.$runtimeLibraryKey -Force
$json = $app | ConvertTo-Json -Depth 60
[System.IO.File]::WriteAllText($depsPath, $json, (New-Object System.Text.UTF8Encoding($false)))

$license = Join-Path $dotnetRoot 'LICENSE.txt'
if (Test-Path -LiteralPath $license) {
    Copy-Item -LiteralPath $license -Destination (Join-Path $publish 'LICENSE-DOTNET.txt') -Force
}
$notices = Join-Path $dotnetRoot 'ThirdPartyNotices.txt'
if (Test-Path -LiteralPath $notices) {
    Copy-Item -LiteralPath $notices -Destination (Join-Path $publish 'THIRD-PARTY-NOTICES-DOTNET.txt') -Force
}

Write-Host "Bundled .NET $runtimeVersion; app runs without DOTNET_ROOT." -ForegroundColor Green
