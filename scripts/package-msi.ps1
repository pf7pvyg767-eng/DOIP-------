param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$ProductVersion = "0.2.0",
    [string]$WixVersion = "6.0.2",
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = "dotnet"
}
else {
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
}

function Assert-UnderRoot {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside workspace: $fullPath"
    }

    return $fullPath
}

function Reset-Directory {
    param([string]$Path)

    $fullPath = Assert-UnderRoot $Path
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
    return $fullPath
}

function Invoke-Native {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

function Get-WixInvocation {
    param([string]$ToolDirectory)

    $wixDll = Get-ChildItem -LiteralPath $ToolDirectory -Recurse -Filter "wix.dll" |
        Sort-Object FullName |
        Select-Object -First 1
    if ($wixDll) {
        return [pscustomobject]@{
            FilePath = $dotnet
            Prefix = @($wixDll.FullName)
        }
    }

    return [pscustomobject]@{
        FilePath = Join-Path $ToolDirectory "wix.exe"
        Prefix = @()
    }
}

function ConvertTo-WixId {
    param([string]$Value)

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $hash = $sha1.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    }
    finally {
        $sha1.Dispose()
    }
    $suffix = -join ($hash[0..7] | ForEach-Object { $_.ToString("X2") })
    $clean = [Regex]::Replace($Value, "[^A-Za-z0-9_]", "_")
    if ($clean.Length -gt 40) {
        $clean = $clean.Substring(0, 40)
    }

    if ($clean -notmatch "^[A-Za-z_]") {
        $clean = "_$clean"
    }

    return "${clean}_$suffix"
}

function ConvertTo-XmlAttribute {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

function Write-DirectoryWix {
    param(
        [System.Text.StringBuilder]$Builder,
        [string]$DirectoryPath,
        [string]$RelativePath,
        [System.Collections.Generic.List[string]]$ComponentIds,
        [int]$Depth = 2
    )

    $indent = "  " * $Depth
    foreach ($file in Get-ChildItem -LiteralPath $DirectoryPath -File | Sort-Object Name) {
        $fileRelativePath = if ($RelativePath) { Join-Path $RelativePath $file.Name } else { $file.Name }
        $componentId = "cmp_$(ConvertTo-WixId $fileRelativePath)"
        $fileId = "fil_$(ConvertTo-WixId $fileRelativePath)"
        $source = ConvertTo-XmlAttribute $file.FullName
        [void]$Builder.AppendLine("$indent<Component Id=`"$componentId`" Guid=`"*`">")
        [void]$Builder.AppendLine("$indent  <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")
        [void]$Builder.AppendLine("$indent</Component>")
        $ComponentIds.Add($componentId)
    }

    foreach ($directory in Get-ChildItem -LiteralPath $DirectoryPath -Directory | Sort-Object Name) {
        $directoryRelativePath = if ($RelativePath) { Join-Path $RelativePath $directory.Name } else { $directory.Name }
        $directoryId = "dir_$(ConvertTo-WixId $directoryRelativePath)"
        $directoryName = ConvertTo-XmlAttribute $directory.Name
        [void]$Builder.AppendLine("$indent<Directory Id=`"$directoryId`" Name=`"$directoryName`">")
        Write-DirectoryWix -Builder $Builder -DirectoryPath $directory.FullName -RelativePath $directoryRelativePath -ComponentIds $ComponentIds -Depth ($Depth + 1)
        [void]$Builder.AppendLine("$indent</Directory>")
    }
}

function Copy-DotnetRuntime {
    param([string]$Destination)

    $sourceRoot = Split-Path -Parent $dotnet
    $runtimeVersion = (& $dotnet --list-runtimes |
        Select-String -Pattern "^Microsoft\.NETCore\.App\s+([0-9.]+)\s+" |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Select-Object -First 1)
    $aspNetVersion = (& $dotnet --list-runtimes |
        Select-String -Pattern "^Microsoft\.AspNetCore\.App\s+([0-9.]+)\s+" |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Select-Object -First 1)

    if ([string]::IsNullOrWhiteSpace($runtimeVersion) -or [string]::IsNullOrWhiteSpace($aspNetVersion)) {
        throw "Could not locate local .NET runtime versions to bundle."
    }

    $destinationFullPath = Assert-UnderRoot $Destination
    New-Item -ItemType Directory -Force -Path $destinationFullPath | Out-Null

    Copy-Item -LiteralPath (Join-Path $sourceRoot "dotnet.exe") -Destination (Join-Path $destinationFullPath "dotnet.exe") -Force
    foreach ($notice in @("LICENSE.txt", "ThirdPartyNotices.txt")) {
        $noticePath = Join-Path $sourceRoot $notice
        if (Test-Path -LiteralPath $noticePath) {
            Copy-Item -LiteralPath $noticePath -Destination (Join-Path $destinationFullPath $notice) -Force
        }
    }

    $fxrSource = Join-Path $sourceRoot "host\fxr\$runtimeVersion"
    $netCoreSource = Join-Path $sourceRoot "shared\Microsoft.NETCore.App\$runtimeVersion"
    $aspNetSource = Join-Path $sourceRoot "shared\Microsoft.AspNetCore.App\$aspNetVersion"

    New-Item -ItemType Directory -Force -Path (Join-Path $destinationFullPath "host\fxr") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $destinationFullPath "shared\Microsoft.NETCore.App") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $destinationFullPath "shared\Microsoft.AspNetCore.App") | Out-Null

    Copy-Item -LiteralPath $fxrSource -Destination (Join-Path $destinationFullPath "host\fxr\$runtimeVersion") -Recurse -Force
    Copy-Item -LiteralPath $netCoreSource -Destination (Join-Path $destinationFullPath "shared\Microsoft.NETCore.App\$runtimeVersion") -Recurse -Force
    Copy-Item -LiteralPath $aspNetSource -Destination (Join-Path $destinationFullPath "shared\Microsoft.AspNetCore.App\$aspNetVersion") -Recurse -Force
}

Push-Location $root
try {
    $artifactsRoot = Join-Path $root "artifacts\installer"
    $publishDir = Reset-Directory (Join-Path $artifactsRoot "publish")
    $msiDir = Reset-Directory (Join-Path $artifactsRoot "msi")
    $wixWorkDir = Reset-Directory (Join-Path $artifactsRoot "wix")
    $wixToolDir = Join-Path $artifactsRoot "tools\wix-$WixVersion"

    & powershell -ExecutionPolicy Bypass -File (Join-Path $root "scripts\generate-app-icon.ps1") -OutputPath (Join-Path $root "src\DoipSimulator.Host\assets\doip-simulator.ico")

    Push-Location (Join-Path $root "src\DoipSimulator.WebConsole")
    try {
        if (Test-Path -LiteralPath "package-lock.json") {
            npm.cmd ci
        } else {
            npm.cmd install
        }

        npm.cmd run build
    }
    finally {
        Pop-Location
    }

    if (-not $SkipTests) {
        & $dotnet test (Join-Path $root "DoipSimulator.sln") --no-restore
    }

    & $dotnet publish (Join-Path $root "src\DoipSimulator.Host\DoipSimulator.Host.csproj") `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained false `
        -p:UseAppHost=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDir

    Copy-DotnetRuntime (Join-Path $publishDir "dotnet")
    Copy-Item -LiteralPath (Join-Path $root "src\DoipSimulator.WebConsole\dist") -Destination (Join-Path $publishDir "wwwroot") -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root "sample-config\default.simulator.json") -Destination (Join-Path $publishDir "simulator-config.json") -Force
    Copy-Item -LiteralPath (Join-Path $root "installer\Start-DOIP-Simulator.ps1") -Destination (Join-Path $publishDir "Start-DOIP-Simulator.ps1") -Force
    Copy-Item -LiteralPath (Join-Path $root "installer\Start-DOIP-Simulator.cmd") -Destination (Join-Path $publishDir "Start-DOIP-Simulator.cmd") -Force

    if (-not (Test-Path -LiteralPath (Join-Path $wixToolDir "wix.exe"))) {
        New-Item -ItemType Directory -Force -Path $wixToolDir | Out-Null
        Invoke-Native $dotnet @("tool", "install", "wix", "--tool-path", $wixToolDir, "--version", $WixVersion)
    }

    $wixInvocation = Get-WixInvocation $wixToolDir
    $installedExtensions = & $wixInvocation.FilePath @(@($wixInvocation.Prefix) + "extension", "list")

    if (($installedExtensions | Out-String) -notmatch "WixToolset\.Firewall\.wixext") {
        Invoke-Native $wixInvocation.FilePath @(@($wixInvocation.Prefix) + "extension", "add", "WixToolset.Firewall.wixext/$WixVersion")
    }

    $componentIds = [System.Collections.Generic.List[string]]::new()
    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
    [void]$builder.AppendLine('  <Fragment>')
    [void]$builder.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')
    Write-DirectoryWix -Builder $builder -DirectoryPath $publishDir -RelativePath "" -ComponentIds $componentIds -Depth 3
    [void]$builder.AppendLine('    </DirectoryRef>')
    [void]$builder.AppendLine('  </Fragment>')
    [void]$builder.AppendLine('  <Fragment>')
    [void]$builder.AppendLine('    <ComponentGroup Id="PublishedFiles">')
    foreach ($componentId in $componentIds) {
        [void]$builder.AppendLine("      <ComponentRef Id=`"$componentId`" />")
    }
    [void]$builder.AppendLine('    </ComponentGroup>')
    [void]$builder.AppendLine('  </Fragment>')
    [void]$builder.AppendLine('</Wix>')

    $generatedFilesWxs = Join-Path $wixWorkDir "PublishedFiles.wxs"
    Set-Content -Path $generatedFilesWxs -Value $builder.ToString() -Encoding UTF8

    $msiPath = Join-Path $msiDir "DOIP-Simulator-$ProductVersion-$RuntimeIdentifier.msi"
    Invoke-Native $wixInvocation.FilePath @(
        @($wixInvocation.Prefix) +
        "build",
        (Join-Path $root "installer\Product.wxs"),
        $generatedFilesWxs,
        "-ext",
        "WixToolset.Firewall.wixext",
        "-arch",
        "x64",
        "-d",
        "SourceRoot=$root",
        "-d",
        "ProductVersion=$ProductVersion",
        "-out",
        $msiPath)

    Write-Output "MSI_CREATED=$msiPath"
}
finally {
    Pop-Location
}
