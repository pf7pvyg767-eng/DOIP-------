param(
    [string]$WebConsoleRoot = ".\src\DoipSimulator.WebConsole"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path $WebConsoleRoot
$requiredFiles = @(
    "src\components\RuntimeCockpitPanel.vue",
    "src\components\ConnectionStepList.vue",
    "src\components\ConnectionStepDetail.vue",
    "src\components\EvidenceSummaryGrid.vue",
    "src\connectionWorkflow.ts"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $root $file
    if (-not (Test-Path $path)) {
        Write-Host "FAIL missing $file" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS found $file" -ForegroundColor Green
}

$dashboard = Get-Content (Join-Path $root "src\views\DashboardView.vue") -Raw
$labels = @(
    "RuntimeCockpitPanel",
    "cockpitSnapshot",
    "confirmRuntimeShutdown"
)

foreach ($label in $labels) {
    if ($dashboard -notmatch [regex]::Escape($label)) {
        Write-Host "FAIL DashboardView missing $label" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS DashboardView contains $label" -ForegroundColor Green
}

$cockpit = Get-Content (Join-Path $root "src\components\RuntimeCockpitPanel.vue") -Raw
$cockpitLabels = @(
    "Diagnostic connection workflow",
    "ConnectionStepList",
    "ConnectionStepDetail",
    "EvidenceSummaryGrid"
)

foreach ($label in $cockpitLabels) {
    if ($cockpit -notmatch [regex]::Escape($label)) {
        Write-Host "FAIL RuntimeCockpitPanel missing $label" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS RuntimeCockpitPanel contains $label" -ForegroundColor Green
}

Push-Location $root
try {
    npm.cmd run build
} finally {
    Pop-Location
}
