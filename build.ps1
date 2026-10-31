#Requires -Version 5.1
<#
.SYNOPSIS
    Builds, tests and publishes LightSwitch for Windows.

.DESCRIPTION
    Publishing is self-contained by default: the publish folder includes the
    Windows App SDK and a private copy of the .NET runtime, so the target
    machine does not need any runtime installed.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Test
    .\build.ps1 -Publish
    .\build.ps1 -Publish -FrameworkDependent
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $Test,

    [switch] $Publish,

    # Produce a smaller folder that requires the .NET 10 Desktop Runtime.
    [switch] $FrameworkDependent,

    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Resolve-Dotnet {
    $candidates = @()
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }

    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet10\dotnet.exe'
    if (Test-Path -LiteralPath $local) {
        $candidates += $local
    }

    foreach ($candidate in $candidates) {
        try {
            $version = & $candidate --version 2>$null
            if ($LASTEXITCODE -eq 0 -and $version -match '^10\.') {
                return $candidate
            }
        }
        catch {
            # Try the next candidate.
        }
    }

    throw 'Cannot find .NET SDK 10. Install it from https://dotnet.microsoft.com/download/dotnet/10.0'
}

$dotnet = Resolve-Dotnet
Write-Host "dotnet: $dotnet" -ForegroundColor DarkGray
Write-Host "Building LightSwitch ($Configuration) ..." -ForegroundColor Cyan

& $dotnet build (Join-Path $root 'LightSwitch.slnx') -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit $LASTEXITCODE)."
}

$appOutput = Join-Path $root "src\LightSwitch.App\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64"
Write-Host "App output: $appOutput" -ForegroundColor Green

if ($Test) {
    Write-Host 'Running tests ...' -ForegroundColor Cyan
    & $dotnet run --project (Join-Path $root 'tests\LightSwitch.Tests\LightSwitch.Tests.csproj') `
        -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed (exit $LASTEXITCODE)."
    }
}

if ($Publish) {
    $output = if ($OutputDirectory) {
        $OutputDirectory
    }
    else {
        Join-Path $root 'artifacts\LightSwitch-win-x64'
    }

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    $mode = if ($FrameworkDependent) { 'framework-dependent' } else { 'self-contained' }
    Write-Host "Publishing to $output ($mode) ..." -ForegroundColor Cyan

    & $dotnet publish (Join-Path $root 'src\LightSwitch.App\LightSwitch.App.csproj') `
        -c $Configuration `
        -r win-x64 `
        --self-contained false `
        -p:WindowsAppSDKSelfContained=true `
        -o $output
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed (exit $LASTEXITCODE)."
    }

    if (-not $FrameworkDependent) {
        $bundler = Join-Path $root 'tools\bundle-dotnet-runtime.ps1'
        if (-not (Test-Path -LiteralPath $bundler)) {
            throw "Missing bundler: $bundler"
        }
        $dotnetRoot = Split-Path -Parent $dotnet
        & $bundler -PublishDirectory $output -DotnetRoot $dotnetRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Bundling .NET failed (exit $LASTEXITCODE)."
        }
    }

    $files = Get-ChildItem -LiteralPath $output -Recurse -File
    $size = ($files | Measure-Object -Property Length -Sum).Sum
    Write-Host ("Publish complete: {0} files, {1:N1} MB" -f $files.Count, ($size / 1MB)) -ForegroundColor Green

    if ($FrameworkDependent) {
        Write-Host 'This build requires the .NET 10 Desktop Runtime on the target machine.' -ForegroundColor Yellow
    }
    else {
        Write-Host 'This build is portable: no .NET runtime installation is required.' -ForegroundColor Green
    }
}
