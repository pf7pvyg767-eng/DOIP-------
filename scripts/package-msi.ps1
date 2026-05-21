param(
    [string]$Version = "0.1.0",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SkipFrontendBuild,
    [switch]$AcceptWixEula
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$frontendRoot = Join-Path $repoRoot "src\DoipSimulator.WebConsole"
$hostProject = Join-Path $repoRoot "src\DoipSimulator.Host\DoipSimulator.Host.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish\doip-simulator-$Version-$RuntimeIdentifier"
$installerRoot = Join-Path $artifactsRoot "installer"
$wxsPath = Join-Path $installerRoot "DoipSimulator-$Version.wxs"
$msiPath = Join-Path $installerRoot "DoipSimulator-$Version-$RuntimeIdentifier.msi"
$webDist = Join-Path $frontendRoot "dist"
$webRoot = Join-Path $publishRoot "wwwroot"

function Convert-ToWixId {
    param([string]$Value)
    $id = [Regex]::Replace($Value, "[^A-Za-z0-9_\.]", "_")
    if ($id -notmatch "^[A-Za-z_]") {
        $id = "id_$id"
    }

    if ($id.Length -gt 70) {
        $hash = [Math]::Abs($Value.GetHashCode()).ToString("X")
        $id = $id.Substring(0, 60) + "_" + $hash
    }

    return $id
}

function Convert-ToXmlAttribute {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseFull = (Resolve-Path -LiteralPath $BasePath).ProviderPath.TrimEnd("\") + "\"
    $targetFull = (Resolve-Path -LiteralPath $TargetPath).ProviderPath
    $baseUri = [Uri]$baseFull
    $targetUri = [Uri]$targetFull
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace("/", "\")
}

function Invoke-NativeCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE"
    }
}

Write-Host "Packaging DOIP Simulator $Version for $RuntimeIdentifier"

New-Item -ItemType Directory -Force -Path $artifactsRoot, $installerRoot | Out-Null

if (-not $SkipFrontendBuild) {
    Push-Location $frontendRoot
    try {
        Invoke-NativeCommand "npm" @("run", "build")
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path (Join-Path $webDist "index.html"))) {
    throw "Frontend dist is missing. Run npm run build first or remove -SkipFrontendBuild."
}

if (Test-Path $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

Invoke-NativeCommand "dotnet" @(
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
    $publishRoot
)

New-Item -ItemType Directory -Force -Path $webRoot | Out-Null
Copy-Item -Path (Join-Path $webDist "*") -Destination $webRoot -Recurse -Force

$exePath = Join-Path $publishRoot "doip-simulator.exe"
if (-not (Test-Path $exePath)) {
    throw "Published executable not found: $exePath"
}

$allDirectories = Get-ChildItem -LiteralPath $publishRoot -Directory -Recurse | Sort-Object FullName
$directoryIds = @{
    (Resolve-Path $publishRoot).Path = "INSTALLFOLDER"
}

foreach ($directory in $allDirectories) {
    $relative = Get-RelativePath $publishRoot $directory.FullName
    $directoryIds[$directory.FullName] = Convert-ToWixId "dir_$relative"
}

$directoryChildren = @{}
foreach ($directory in $allDirectories) {
    $parent = Split-Path $directory.FullName -Parent
    if (-not $directoryChildren.ContainsKey($parent)) {
        $directoryChildren[$parent] = New-Object System.Collections.Generic.List[object]
    }

    $directoryChildren[$parent].Add($directory)
}

function Write-WixDirectory {
    param(
        [System.Text.StringBuilder]$Builder,
        [string]$Path,
        [int]$Indent
    )

    if (-not $directoryChildren.ContainsKey($Path)) {
        return
    }

    $pad = " " * $Indent
    foreach ($child in $directoryChildren[$Path]) {
        $id = $directoryIds[$child.FullName]
        $name = Convert-ToXmlAttribute $child.Name
        [void]$Builder.AppendLine("$pad<Directory Id=`"$id`" Name=`"$name`">")
        Write-WixDirectory -Builder $Builder -Path $child.FullName -Indent ($Indent + 2)
        [void]$Builder.AppendLine("$pad</Directory>")
    }
}

$directoryXml = [System.Text.StringBuilder]::new()
Write-WixDirectory -Builder $directoryXml -Path (Resolve-Path $publishRoot).Path -Indent 10

$componentXml = [System.Text.StringBuilder]::new()
$componentRefsXml = [System.Text.StringBuilder]::new()
$index = 0
foreach ($file in Get-ChildItem -LiteralPath $publishRoot -File -Recurse | Sort-Object FullName) {
    $index++
    $relative = Get-RelativePath $publishRoot $file.FullName
    $componentId = Convert-ToWixId "cmp_$index`_$relative"
    $fileId = Convert-ToWixId "fil_$index`_$relative"
    $source = Convert-ToXmlAttribute $file.FullName
    $directoryId = $directoryIds[(Split-Path $file.FullName -Parent)]
    [void]$componentXml.AppendLine("      <Component Id=`"$componentId`" Directory=`"$directoryId`" Guid=`"*`">")
    [void]$componentXml.AppendLine("        <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")
    [void]$componentXml.AppendLine("      </Component>")
    [void]$componentRefsXml.AppendLine("      <ComponentRef Id=`"$componentId`" />")
}

$upgradeCode = "aaf5f278-81ab-4ab9-9d88-7efe113d8f21"
$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="DOIP Simulator" Manufacturer="DOIP Simulator" Version="$Version" UpgradeCode="$upgradeCode" Scope="perUser">
    <MajorUpgrade DowngradeErrorMessage="A newer version of DOIP Simulator is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <StandardDirectory Id="LocalAppDataFolder">
      <Directory Id="INSTALLFOLDER" Name="DOIP Simulator">
$directoryXml      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="DOIP Simulator" />
    </StandardDirectory>

    <Component Id="StartMenuShortcutComponent" Directory="ApplicationProgramsFolder" Guid="0e77f8d1-1e57-447b-bb5f-09e63a9a94d7">
      <Shortcut Id="StartMenuShortcut" Name="DOIP Simulator" Target="[INSTALLFOLDER]doip-simulator.exe" Arguments="run" WorkingDirectory="INSTALLFOLDER" />
      <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall" />
      <RegistryValue Root="HKCU" Key="Software\DOIP Simulator" Name="installed" Type="integer" Value="1" KeyPath="yes" />
    </Component>

    <Feature Id="MainFeature" Title="DOIP Simulator" Level="1">
      <ComponentRef Id="StartMenuShortcutComponent" />
$componentRefsXml    </Feature>

$componentXml  </Package>
</Wix>
"@

Set-Content -LiteralPath $wxsPath -Encoding UTF8 -Value $wxs

if ($AcceptWixEula) {
    Invoke-NativeCommand "wix" @("eula", "accept", "wix7")
}

Invoke-NativeCommand "wix" @("build", $wxsPath, "-arch", "x64", "-o", $msiPath)

Write-Host "MSI created: $msiPath"
Write-Host "After installation, run 'DOIP Simulator' from the Start Menu and open http://127.0.0.1:5080/"
