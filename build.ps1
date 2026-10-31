#Requires -Version 5.1
<#
.SYNOPSIS
    Builds, tests and optionally publishes LightSwitch for Windows.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Test
    .\build.ps1 -Publish
    .\build.ps1 -Publish -SelfContained
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $Test,

    [switch] $Publish,

    [switch] $SelfContained,

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
    if (Test-Path $local) {
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

    throw '未找到 .NET SDK 10。请安装 .NET 10 SDK：https://dotnet.microsoft.com/download/dotnet/10.0'
}

$dotnet = Resolve-Dotnet
Write-Host "dotnet: $dotnet" -ForegroundColor DarkGray
Write-Host "Building LightSwitch ($Configuration) ..." -ForegroundColor Cyan

& $dotnet build (Join-Path $root 'LightSwitch.slnx') -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "构建失败（exit $LASTEXITCODE）。"
}

$appOutput = Join-Path $root "src\LightSwitch.App\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64"
Write-Host "应用输出：$appOutput" -ForegroundColor Green

if ($Test) {
    Write-Host 'Running tests ...' -ForegroundColor Cyan
    & $dotnet run --project (Join-Path $root 'tests\LightSwitch.Tests\LightSwitch.Tests.csproj') `
        -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "测试失败（exit $LASTEXITCODE）。"
    }
}

if ($Publish) {
    $output = if ($OutputDirectory) {
        $OutputDirectory
    }
    else {
        Join-Path $root 'artifacts\LightSwitch-win-x64'
    }

    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }

    Write-Host "Publishing to $output (self-contained=$($SelfContained.IsPresent)) ..." -ForegroundColor Cyan
    $publishArgs = @(
        'publish',
        (Join-Path $root 'src\LightSwitch.App\LightSwitch.App.csproj'),
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', $(if ($SelfContained) { 'true' } else { 'false' }),
        '-p:WindowsAppSDKSelfContained=true',
        '-o', $output
    )

    & $dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "发布失败（exit $LASTEXITCODE）。"
    }

    Write-Host "发布完成：$output" -ForegroundColor Green
    if (-not $SelfContained) {
        Write-Host '提示：该目录需要目标机器安装 .NET 10 Desktop Runtime。' -ForegroundColor Yellow
    }
}
