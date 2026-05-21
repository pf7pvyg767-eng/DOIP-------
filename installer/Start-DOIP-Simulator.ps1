Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$installRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetPath = Join-Path $installRoot "dotnet\dotnet.exe"
$appPath = Join-Path $installRoot "doip-simulator.dll"
$configPath = Join-Path $installRoot "simulator-config.json"
$logRoot = Join-Path $env:LOCALAPPDATA "DOIP Simulator\logs"
$eventLogPath = Join-Path $logRoot "runtime-events.log"
$apiBaseUrl = "http://127.0.0.1:5080"

New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

function Test-Health {
    try {
        $response = Invoke-RestMethod -Uri "$apiBaseUrl/api/health" -TimeoutSec 1
        return $response.status -eq "ok"
    } catch {
        return $false
    }
}

if (-not (Test-Path -LiteralPath $dotnetPath)) {
    throw "Cannot find bundled .NET host: $dotnetPath"
}

if (-not (Test-Path -LiteralPath $appPath)) {
    throw "Cannot find DOIP Simulator assembly: $appPath"
}

if (-not (Test-Path -LiteralPath $configPath)) {
    throw "Cannot find simulator configuration: $configPath"
}

if (-not (Test-Health)) {
    Start-Process `
        -FilePath $dotnetPath `
        -ArgumentList @($appPath, "run", "--config", $configPath, "--event-log", $eventLogPath) `
        -WorkingDirectory $installRoot `
        -WindowStyle Hidden | Out-Null

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(12)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if (Test-Health) {
            break
        }
    }
}

Start-Process $apiBaseUrl | Out-Null
