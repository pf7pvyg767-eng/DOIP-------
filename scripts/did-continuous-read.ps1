param(
    [string]$DoipHost = "127.0.0.1",
    [int]$DoipPort = 13400,
    [int]$Tester = 0x0E80,
    [int]$Ecu = 0x0E00,
    [string[]]$Dids = @("0xF191", "0xF192", "0xF193", "0xF194", "0xF195", "0xF196", "0xF197"),
    [int]$IntervalMs = 500,
    [int]$DurationSeconds = 120,
    [string]$ConfigPath = "sample-config/default.simulator.json",
    [string]$ApiBaseUrl = "",
    [switch]$SkipApiPreflight
)

$ErrorActionPreference = "Stop"

trap {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

function ConvertTo-Hex {
    param([byte[]]$Bytes)
    ($Bytes | ForEach-Object { $_.ToString("X2") }) -join " "
}

function ConvertFrom-DidText {
    param([string]$Value)

    $text = $Value.Trim()
    if ($text.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        $text = $text.Substring(2)
    }

    if ($text.Length -ne 4 -or $text -notmatch '^[0-9a-fA-F]{4}$') {
        throw "DID '$Value' must be a 16-bit hexadecimal value such as 0xF193."
    }

    return [Convert]::ToUInt16($text, 16)
}

function New-DoipFrame {
    param(
        [UInt16]$PayloadType,
        [byte[]]$Payload = [byte[]]::new(0)
    )

    $frame = [byte[]]::new(8 + $Payload.Length)
    $frame[0] = 0x02
    $frame[1] = 0xFD
    $frame[2] = [byte](($PayloadType -shr 8) -band 0xFF)
    $frame[3] = [byte]($PayloadType -band 0xFF)
    $length = [UInt32]$Payload.Length
    $frame[4] = [byte](($length -shr 24) -band 0xFF)
    $frame[5] = [byte](($length -shr 16) -band 0xFF)
    $frame[6] = [byte](($length -shr 8) -band 0xFF)
    $frame[7] = [byte]($length -band 0xFF)
    if ($Payload.Length -gt 0) {
        [Array]::Copy($Payload, 0, $frame, 8, $Payload.Length)
    }

    return $frame
}

function Read-Exact {
    param(
        [System.IO.Stream]$Stream,
        [int]$Length
    )

    $buffer = [byte[]]::new($Length)
    $offset = 0
    while ($offset -lt $Length) {
        $count = $Stream.Read($buffer, $offset, $Length - $offset)
        if ($count -le 0) {
            throw "Unexpected end of stream while reading $Length bytes."
        }
        $offset += $count
    }

    return $buffer
}

function Read-DoipFrame {
    param([System.IO.Stream]$Stream)

    $header = Read-Exact -Stream $Stream -Length 8
    $payloadType = [UInt16](([int]$header[2] -shl 8) -bor [int]$header[3])
    $length = [UInt32](([int]$header[4] -shl 24) -bor ([int]$header[5] -shl 16) -bor ([int]$header[6] -shl 8) -bor [int]$header[7])
    if ($length -gt 1048576) {
        throw "Unexpected DoIP payload length $length."
    }

    $payload = if ($length -gt 0) { Read-Exact -Stream $Stream -Length ([int]$length) } else { [byte[]]::new(0) }
    return [pscustomobject]@{
        PayloadType = $payloadType
        Payload = $payload
    }
}

function New-AddressBytes {
    param(
        [int]$Source,
        [int]$Target
    )

    return [byte[]]@(
        [byte](($Source -shr 8) -band 0xFF),
        [byte]($Source -band 0xFF),
        [byte](($Target -shr 8) -band 0xFF),
        [byte]($Target -band 0xFF)
    )
}

function New-DoipTcpClient {
    $client = [System.Net.Sockets.TcpClient]::new()
    $connect = $client.BeginConnect($DoipHost, $DoipPort, $null, $null)
    if (-not $connect.AsyncWaitHandle.WaitOne(3000)) {
        $client.Dispose()
        throw "Timed out connecting to ${DoipHost}:$DoipPort."
    }

    $client.EndConnect($connect)
    $client.ReceiveTimeout = 3000
    $client.SendTimeout = 3000
    return $client
}

function Invoke-RoutingActivation {
    param([System.IO.Stream]$Stream)

    $payload = [byte[]]@(
        [byte](($Tester -shr 8) -band 0xFF),
        [byte]($Tester -band 0xFF),
        0x00,
        0x00,
        0x00,
        0x00,
        0x00
    )
    $frame = New-DoipFrame -PayloadType 0x0005 -Payload $payload
    $Stream.Write($frame, 0, $frame.Length)
    $response = Read-DoipFrame -Stream $Stream
    if ($response.PayloadType -ne 0x0006) {
        throw ("Expected Routing Activation response 0x0006, got 0x{0:X4}." -f $response.PayloadType)
    }
    if ($response.Payload.Length -lt 5 -or $response.Payload[4] -ne 0x10) {
        throw ("Routing Activation was not accepted: {0}" -f (ConvertTo-Hex $response.Payload))
    }
}

function Invoke-UdsRequest {
    param(
        [System.IO.Stream]$Stream,
        [byte[]]$Payload
    )

    $addresses = New-AddressBytes -Source $Tester -Target $Ecu
    $diagnosticPayload = [byte[]]::new($addresses.Length + $Payload.Length)
    [Array]::Copy($addresses, 0, $diagnosticPayload, 0, $addresses.Length)
    [Array]::Copy($Payload, 0, $diagnosticPayload, $addresses.Length, $Payload.Length)
    $frame = New-DoipFrame -PayloadType 0x8001 -Payload $diagnosticPayload
    $Stream.Write($frame, 0, $frame.Length)

    while ($true) {
        $response = Read-DoipFrame -Stream $Stream
        if ($response.PayloadType -eq 0x8002) {
            continue
        }
        if ($response.PayloadType -ne 0x8001) {
            throw ("Unexpected DoIP payload type 0x{0:X4}." -f $response.PayloadType)
        }
        if ($response.Payload.Length -lt 5) {
            throw ("Short diagnostic response: {0}" -f (ConvertTo-Hex $response.Payload))
        }
        return $response.Payload[4..($response.Payload.Length - 1)]
    }
}

function Read-Did {
    param(
        [System.IO.Stream]$Stream,
        [UInt16]$Did
    )

    $request = [byte[]]@(
        0x22,
        [byte](($Did -shr 8) -band 0xFF),
        [byte]($Did -band 0xFF)
    )
    $response = Invoke-UdsRequest -Stream $Stream -Payload $request
    if ($response.Length -ge 3 -and $response[0] -eq 0x7F -and $response[1] -eq 0x22) {
        $nrc = $response[2]
        $reason = switch ($nrc) {
            0x31 { "RequestOutOfRange: this DID is not configured on DoIP ${DoipHost}:$DoipPort." }
            0x33 { "SecurityAccessDenied: unlock the required security level before reading this DID." }
            default { "NegativeResponse NRC 0x$($nrc.ToString("X2"))." }
        }

        throw ("ECU rejected DID 0x{0:X4}: {1} Raw response: {2}" -f $Did, $reason, (ConvertTo-Hex $response))
    }

    if ($response.Length -lt 3 -or $response[0] -ne 0x62 -or $response[1] -ne $request[1] -or $response[2] -ne $request[2]) {
        throw ("Unexpected DID 0x{0:X4} response: {1}" -f $Did, (ConvertTo-Hex $response))
    }

    if ($response.Length -eq 3) {
        return [byte[]]::new(0)
    }

    return $response[3..($response.Length - 1)]
}

function ConvertTo-UnsignedBigEndian {
    param([byte[]]$Bytes)

    $value = [UInt64]0
    foreach ($byte in $Bytes) {
        $value = ($value -shl 8) -bor $byte
    }
    return $value
}

function ConvertTo-NumericBigEndian {
    param(
        [byte[]]$Bytes,
        [string]$NumericType
    )

    $unsigned = ConvertTo-UnsignedBigEndian $Bytes
    switch ($NumericType.ToLowerInvariant()) {
        "int16" {
            if ($Bytes.Length -ne 2) { return $unsigned }
            if ($unsigned -ge 0x8000) { return [Int64]($unsigned - 0x10000) }
            return [Int64]$unsigned
        }
        "int32" {
            if ($Bytes.Length -ne 4) { return $unsigned }
            if ($unsigned -ge 0x80000000) { return [Int64]($unsigned - 0x100000000) }
            return [Int64]$unsigned
        }
        default {
            return $unsigned
        }
    }
}

function Get-DidNumericTypeMap {
    param([string]$Path)

    $map = @{}
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -Path $Path)) {
        return $map
    }

    $config = Get-Content -Path $Path -Raw | ConvertFrom-Json
    foreach ($didConfig in @($config.uds.dids)) {
        if ($null -eq $didConfig.identifier -or $null -eq $didConfig.valueProvider -or
            [string]::IsNullOrWhiteSpace($didConfig.valueProvider.numericType)) {
            continue
        }

        $identifier = ConvertFrom-DidText $didConfig.identifier
        $map[[int]$identifier] = [string]$didConfig.valueProvider.numericType
    }

    return $map
}

function Get-InferredApiBaseUrl {
    if (-not [string]::Equals($DoipHost, "127.0.0.1", [StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::Equals($DoipHost, "localhost", [StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    $apiPort = 5080
    if ($DoipPort -ge 13400 -and $DoipPort -le 13420) {
        $apiPort = 5080 + ($DoipPort - 13400)
    }

    return "http://127.0.0.1:$apiPort"
}

function Get-ResponseItems {
    param($Response)

    if ($null -eq $Response) {
        return @()
    }

    if ($Response.PSObject.Properties.Name -contains "value") {
        return @($Response.value)
    }

    return @($Response)
}

function Invoke-ApiPreflight {
    param([UInt16[]]$RequestedDids)

    if ($SkipApiPreflight) {
        return
    }

    $baseUrl = if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) { Get-InferredApiBaseUrl } else { $ApiBaseUrl.TrimEnd("/") }
    if ([string]::IsNullOrWhiteSpace($baseUrl)) {
        return
    }

    try {
        $summary = Invoke-RestMethod -Uri "$baseUrl/api/runtime/summary" -TimeoutSec 2
        $didResponse = Invoke-RestMethod -Uri "$baseUrl/api/dids" -TimeoutSec 2
        $configured = @{}
        foreach ($item in (Get-ResponseItems $didResponse)) {
            if ($item.did) {
                $configured[[string]$item.did] = $true
            }
        }

        $missing = @()
        foreach ($did in $RequestedDids) {
            $didText = "0x{0:X4}" -f $did
            if (-not $configured.ContainsKey($didText)) {
                $missing += $didText
            }
        }

        $reportedDoipPort = [int]$summary.doipTcpPort
        if ($reportedDoipPort -ne $DoipPort) {
            Write-Host "Warning: $baseUrl reports DoIP TCP port $reportedDoipPort, but this script will connect to $DoipPort." -ForegroundColor Yellow
        }

        if ($missing.Count -gt 0) {
            $configuredText = if ($configured.Count -gt 0) { ($configured.Keys | Sort-Object) -join ", " } else { "none" }
            $message = @(
                "API preflight failed: $baseUrl does not configure requested DID(s): $($missing -join ', ').",
                "Configured DID(s) on that runtime: $configuredText.",
                "The current live dynamic-DID development instance uses: powershell -ExecutionPolicy Bypass -File .\scripts\did-continuous-read.ps1 -DoipPort 13401 -DurationSeconds 0",
                "If you intend to test port $DoipPort, restart/load the simulator with sample-config/default.simulator.json, or pass -SkipApiPreflight to bypass this check."
            ) -join " "
            throw $message
        }

        Write-Host "API preflight OK: $baseUrl reports DoIP TCP $reportedDoipPort and requested DID(s) are configured." -ForegroundColor Green
    } catch {
        $message = $_.Exception.Message
        if ($message.StartsWith("API preflight failed:", [StringComparison]::OrdinalIgnoreCase)) {
            throw
        }

        Write-Host "Warning: API preflight could not query $baseUrl ($message). Continuing with raw DoIP." -ForegroundColor Yellow
    }
}

$didValues = $Dids |
    ForEach-Object { $_ -split "," } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { ConvertFrom-DidText $_ }

if ($didValues.Count -eq 0) {
    throw "At least one DID is required."
}

$numericTypeByDid = Get-DidNumericTypeMap $ConfigPath
Invoke-ApiPreflight $didValues

$client = $null
$stream = $null
$deadline = if ($DurationSeconds -gt 0) { [DateTimeOffset]::UtcNow.AddSeconds($DurationSeconds) } else { [DateTimeOffset]::MaxValue }

Write-Host "Connecting to DoIP ${DoipHost}:$DoipPort tester=0x$($Tester.ToString("X4")) ecu=0x$($Ecu.ToString("X4"))"
Write-Host "Reading DIDs: $((($didValues | ForEach-Object { '0x{0:X4}' -f $_ }) -join ', '))"
Write-Host "Interval: ${IntervalMs}ms Duration: $DurationSeconds second(s). Use Ctrl+C to stop."
if ($numericTypeByDid.Count -gt 0) {
    Write-Host "Numeric decode config: $ConfigPath"
}

try {
    $client = New-DoipTcpClient
    $stream = $client.GetStream()
    Invoke-RoutingActivation -Stream $stream
    Write-Host "Routing Activation accepted." -ForegroundColor Green

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        foreach ($did in $didValues) {
            try {
                $value = Read-Did -Stream $stream -Did $did
                $numericType = $numericTypeByDid[[int]$did]
                if ([string]::IsNullOrWhiteSpace($numericType)) {
                    $numericType = "uint"
                }

                $numeric = if ($value.Length -gt 0 -and $value.Length -le 8) {
                    ConvertTo-NumericBigEndian -Bytes $value -NumericType $numericType
                } else {
                    $null
                }

                $numericText = if ($null -ne $numeric) { " numeric=$numeric type=$numericType" } else { "" }
                Write-Host ("{0:HH:mm:ss.fff} DID 0x{1:X4} raw={2}{3}" -f (Get-Date), $did, (ConvertTo-Hex $value), $numericText)
            } catch {
                Write-Host ("{0:HH:mm:ss.fff} DID 0x{1:X4} ERROR {2}" -f (Get-Date), $did, $_.Exception.Message) -ForegroundColor Red
            }
        }

        Start-Sleep -Milliseconds $IntervalMs
    }
} finally {
    if ($stream) {
        $stream.Dispose()
    }
    if ($client) {
        $client.Dispose()
    }
}
