param(
    [string]$ApiBase = "http://127.0.0.1:5080",
    [string]$DoipHost = "127.0.0.1",
    [int]$DoipPort = 13400,
    [int]$Tester = 0x0E00,
    [int]$Ecu = 0x1000,
    [switch]$SkipShutdown
)

$ErrorActionPreference = "Stop"
$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail = ""
    )

    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $line = if ([string]::IsNullOrWhiteSpace($Detail)) {
        "{0} {1}" -f $status, $Name
    } else {
        "{0} {1} :: {2}" -f $status, $Name, $Detail
    }

    if ($Passed) {
        Write-Host $line -ForegroundColor Green
    } else {
        Write-Host $line -ForegroundColor Red
    }

    $results.Add([pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
    }) | Out-Null
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    try {
        $detail = & $Action
        Add-Result -Name $Name -Passed $true -Detail ([string]$detail)
    } catch {
        Add-Result -Name $Name -Passed $false -Detail $_.Exception.Message
    }
}

function ConvertTo-Hex {
    param([byte[]]$Bytes)
    ($Bytes | ForEach-Object { $_.ToString("X2") }) -join " "
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
    $payloadType = [UInt16](($header[2] -shl 8) -bor $header[3])
    $length = [UInt32](($header[4] -shl 24) -bor ($header[5] -shl 16) -bor ($header[6] -shl 8) -bor $header[7])
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

function Invoke-DoipUdpDiscovery {
    $udp = [System.Net.Sockets.UdpClient]::new()
    try {
        $udp.Client.ReceiveTimeout = 2000
        $target = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Parse($DoipHost), $DoipPort)
        $request = New-DoipFrame -PayloadType 0x0001
        [void]$udp.Send($request, $request.Length, $target)
        $remote = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0)
        $response = $udp.Receive([ref]$remote)
        if ($response.Length -lt 8) {
            throw "Short UDP response."
        }
        $payloadType = [UInt16](($response[2] -shl 8) -bor $response[3])
        if ($payloadType -ne 0x0004) {
            throw ("Expected vehicle announcement 0x0004, got 0x{0:X4}." -f $payloadType)
        }
        return "vehicle announcement from $($remote.Address):$($remote.Port)"
    } finally {
        $udp.Dispose()
    }
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
    return "accepted"
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

Write-Host "Phase 2 functional smoke"
Write-Host "API: $ApiBase"
Write-Host "DoIP: ${DoipHost}:$DoipPort tester=0x$($Tester.ToString("X4")) ecu=0x$($Ecu.ToString("X4"))"

Invoke-Step "API health" {
    $health = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/health" -TimeoutSec 5
    "status=$($health.status)"
}

Invoke-Step "Runtime summary" {
    $summary = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/runtime/summary" -TimeoutSec 5
    "api=$($summary.api.baseUrl) doip=$($summary.doip.tcpEndpoint)"
}

Invoke-Step "UDP vehicle discovery" {
    Invoke-DoipUdpDiscovery
}

$tcpClient = $null
$stream = $null

Invoke-Step "TCP routing activation" {
    $script:tcpClient = New-DoipTcpClient
    $script:stream = $script:tcpClient.GetStream()
    Invoke-RoutingActivation -Stream $script:stream
}

Invoke-Step "Static DID F190 read" {
    $response = Invoke-UdsRequest -Stream $script:stream -Payload ([byte[]]@(0x22, 0xF1, 0x90))
    if ($response.Length -lt 4 -or $response[0] -ne 0x62 -or $response[1] -ne 0xF1 -or $response[2] -ne 0x90) {
        throw ("Unexpected DID response: {0}" -f (ConvertTo-Hex $response))
    }
    ConvertTo-Hex $response
}

Invoke-Step "Update DID F190 to sine provider" {
    $body = @{
        valueProvider = @{
            type = "sine"
            encoding = "uint16"
            amplitude = 10
            offset = 100
            periodMs = 1000
        }
        persist = $false
    } | ConvertTo-Json -Depth 5
    $updated = Invoke-RestMethod -Method Put -Uri "$ApiBase/api/dids/F190/provider" -Body $body -ContentType "application/json" -TimeoutSec 5
    "provider=$($updated.valueProvider.type)"
}

Start-Sleep -Milliseconds 250

Invoke-Step "Dynamic DID F190 read" {
    $response = Invoke-UdsRequest -Stream $script:stream -Payload ([byte[]]@(0x22, 0xF1, 0x90))
    if ($response.Length -ne 5 -or $response[0] -ne 0x62 -or $response[1] -ne 0xF1 -or $response[2] -ne 0x90) {
        throw ("Unexpected dynamic DID response: {0}" -f (ConvertTo-Hex $response))
    }
    ConvertTo-Hex $response
}

Invoke-Step "DID sample API" {
    $sample = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/dids/F190/sample" -TimeoutSec 5
    if ($null -eq $sample.numericValue) {
        throw "Sample did not include numericValue."
    }
    "numericValue=$($sample.numericValue) provider=$($sample.providerType)"
}

if ($stream) {
    $stream.Dispose()
}
if ($tcpClient) {
    $tcpClient.Dispose()
}

if ($SkipShutdown) {
    Add-Result -Name "Runtime shutdown" -Passed $true -Detail "skipped"
} else {
    Invoke-Step "Runtime shutdown" {
        $shutdown = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/runtime/shutdown" -TimeoutSec 5
        "accepted=$($shutdown.accepted)"
    }
}

$failed = @($results | Where-Object { -not $_.Passed })
$passedCount = $results.Count - $failed.Count
Write-Host ("Summary: {0}/{1} passed" -f $passedCount, $results.Count)

if ($failed.Count -gt 0) {
    exit 1
}
