param(
    [string]$Version = "0.2.0",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$DotnetPath = "dotnet",
    [string]$NpmPath = "npm.cmd",
    [switch]$SkipFrontendBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$frontendRoot = Join-Path $repoRoot "src\DoipSimulator.WebConsole"
$hostProject = Join-Path $repoRoot "src\DoipSimulator.Host\DoipSimulator.Host.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$portableRoot = Join-Path $artifactsRoot "portable"
$packageName = "doip-simulator-$Version-$RuntimeIdentifier-portable"
$packageRoot = Join-Path $portableRoot $packageName
$zipPath = Join-Path $portableRoot "$packageName.zip"
$webDist = Join-Path $frontendRoot "dist"
$webRoot = Join-Path $packageRoot "wwwroot"
$defaultConfig = Join-Path $repoRoot "sample-config\default.simulator.json"

function Invoke-NativeCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory = $repoRoot
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FilePath exited with code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host "Creating portable DOIP Simulator package $Version for $RuntimeIdentifier"

New-Item -ItemType Directory -Force -Path $portableRoot | Out-Null

if (-not $SkipFrontendBuild) {
    if (-not (Test-Path (Join-Path $frontendRoot "node_modules"))) {
        Invoke-NativeCommand $NpmPath @("install") $frontendRoot
    }

    Invoke-NativeCommand $NpmPath @("run", "build") $frontendRoot
}

if (-not (Test-Path (Join-Path $webDist "index.html"))) {
    throw "Frontend dist is missing. Run the script without -SkipFrontendBuild first."
}

if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Invoke-NativeCommand $DotnetPath @(
    "publish",
    $hostProject,
    "-c",
    "Release",
    "-r",
    $RuntimeIdentifier,
    "--self-contained",
    "true",
    "-p:PublishSingleFile=false",
    "-p:Version=$Version",
    "-p:AssemblyVersion=$Version",
    "-p:FileVersion=$Version",
    "-o",
    $packageRoot
)

New-Item -ItemType Directory -Force -Path $webRoot | Out-Null
Copy-Item -Path (Join-Path $webDist "*") -Destination $webRoot -Recurse -Force
Copy-Item -LiteralPath $defaultConfig -Destination (Join-Path $packageRoot "simulator-config.json") -Force

$readme = @"
# DOIP Simulator Portable

This folder is a self-contained Windows x64 build. No .NET SDK, Node.js, npm, or source checkout is required.

## Start

Double-click `Start-DOIP-Simulator.cmd`.

The Web Console opens at:

http://127.0.0.1:5080

## Ports

- Web Console/API: 127.0.0.1:5080
- DoIP UDP/TCP: 13400
- DoIP TLS port in config: 3496 (disabled by default)

## Default ECU

- VIN: LTEST000000000001
- ECU logical address: 0x0E00
- Tester source address whitelist: 0x0E80
- Dynamic sample DIDs: 0xF191 through 0xF197

## Stop

Close the console window, press Ctrl+C in it, or use the shutdown action in the Web Console.

Runtime logs are written under the `logs` folder.
PCAP captures are created by the Web Console when recording is started.
"@

Set-Content -LiteralPath (Join-Path $packageRoot "README.md") -Encoding UTF8 -Value $readme

$startPs1 = @'
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root "doip-simulator.exe"
$config = Join-Path $root "simulator-config.json"
$logs = Join-Path $root "logs"
$eventLog = Join-Path $logs "runtime-events.log"

New-Item -ItemType Directory -Force -Path $logs | Out-Null

Start-Job -ScriptBlock {
    Start-Sleep -Seconds 3
    Start-Process "http://127.0.0.1:5080"
} | Out-Null

& $exe run --listen-address 127.0.0.1 --port 5080 --config $config --event-log $eventLog
'@

Set-Content -LiteralPath (Join-Path $packageRoot "Start-DOIP-Simulator.ps1") -Encoding UTF8 -Value $startPs1

$startCmd = @'
@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-DOIP-Simulator.ps1"
endlocal
'@

Set-Content -LiteralPath (Join-Path $packageRoot "Start-DOIP-Simulator.cmd") -Encoding ASCII -Value $startCmd

Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -Force

Write-Host "Portable package created: $zipPath"
