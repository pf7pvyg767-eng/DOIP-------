param(
    [string]$HostName = "127.0.0.1",
    [int]$Port = 13400,
    [int]$Connections = 20,
    [int]$RequestsPerSecond = 200,
    [int]$DurationSeconds = 10,
    [string]$TesterBaseAddress = "0x0E80",
    [switch]$IncrementTesterAddress,
    [string]$EcuAddress = "0x0E00",
    [string]$Did = "0xF190",
    [int]$TimeoutMilliseconds = 2000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-HexToUInt16 {
    param([string]$Value)
    return [Convert]::ToUInt16($Value.Replace("0x", ""), 16)
}

function New-DoipFrame {
    param(
        [UInt16]$PayloadType,
        [byte[]]$Payload
    )

    $frame = New-Object byte[] (8 + $Payload.Length)
    $frame[0] = 0x02
    $frame[1] = 0xFD
    $frame[2] = [byte](($PayloadType -shr 8) -band 0xFF)
    $frame[3] = [byte]($PayloadType -band 0xFF)
    $length = [UInt32]$Payload.Length
    $frame[4] = [byte](($length -shr 24) -band 0xFF)
    $frame[5] = [byte](($length -shr 16) -band 0xFF)
    $frame[6] = [byte](($length -shr 8) -band 0xFF)
    $frame[7] = [byte]($length -band 0xFF)
    [Array]::Copy($Payload, 0, $frame, 8, $Payload.Length)
    return $frame
}

function Read-Exact {
    param(
        [System.IO.Stream]$Stream,
        [int]$Length
    )

    $buffer = New-Object byte[] $Length
    $offset = 0
    while ($offset -lt $Length) {
        $read = $Stream.Read($buffer, $offset, $Length - $offset)
        if ($read -le 0) {
            throw "Connection closed while reading $Length bytes."
        }

        $offset += $read
    }

    return $buffer
}

function Read-DoipFrame {
    param([System.IO.Stream]$Stream)

    $header = Read-Exact -Stream $Stream -Length 8
    $payloadType = [UInt16](([int]$header[2] -shl 8) -bor [int]$header[3])
    $length = [UInt32](([int]$header[4] -shl 24) -bor ([int]$header[5] -shl 16) -bor ([int]$header[6] -shl 8) -bor [int]$header[7])
    $payload = if ($length -gt 0) { Read-Exact -Stream $Stream -Length ([int]$length) } else { New-Object byte[] 0 }
    return [pscustomobject]@{
        PayloadType = $payloadType
        Payload = $payload
    }
}

function Open-ActivatedConnection {
    param([int]$Index)

    $client = [System.Net.Sockets.TcpClient]::new()
    $client.ReceiveTimeout = $TimeoutMilliseconds
    $client.SendTimeout = $TimeoutMilliseconds
    $client.Connect($HostName, $Port)
    $stream = $client.GetStream()
    $tester = Convert-HexToUInt16 $TesterBaseAddress
    if ($IncrementTesterAddress) {
        $tester = [UInt16]($tester + $Index)
    }
    $activationPayload = New-Object byte[] 7
    $activationPayload[0] = [byte](($tester -shr 8) -band 0xFF)
    $activationPayload[1] = [byte]($tester -band 0xFF)
    $activationPayload[2] = 0x00
    $frame = New-DoipFrame -PayloadType 0x0005 -Payload $activationPayload
    $stream.Write($frame, 0, $frame.Length)
    $response = Read-DoipFrame -Stream $stream
    if ($response.PayloadType -ne 0x0006 -or $response.Payload.Length -lt 5 -or $response.Payload[4] -ne 0x10) {
        throw "Routing activation failed for connection $Index."
    }

    return [pscustomobject]@{
        Client = $client
        Stream = $stream
        Tester = $tester
    }
}

function Send-ReadDid {
    param($Connection)

    $ecu = Convert-HexToUInt16 $EcuAddress
    $didValue = Convert-HexToUInt16 $Did
    $payload = New-Object byte[] 7
    $payload[0] = [byte](($Connection.Tester -shr 8) -band 0xFF)
    $payload[1] = [byte]($Connection.Tester -band 0xFF)
    $payload[2] = [byte](($ecu -shr 8) -band 0xFF)
    $payload[3] = [byte]($ecu -band 0xFF)
    $payload[4] = 0x22
    $payload[5] = [byte](($didValue -shr 8) -band 0xFF)
    $payload[6] = [byte]($didValue -band 0xFF)
    $frame = New-DoipFrame -PayloadType 0x8001 -Payload $payload
    $Connection.Stream.Write($frame, 0, $frame.Length)
    $response = Read-DoipFrame -Stream $Connection.Stream
    $isDiagnosticResponse = $response.PayloadType -eq 0x8001
    $hasUdsPayload = $response.Payload.Length -ge 5
    $isExpectedUdsResponse = $hasUdsPayload -and ($response.Payload[4] -eq 0x62 -or $response.Payload[4] -eq 0x7F)
    if ($VerbosePreference -ne "SilentlyContinue") {
        $sid = if ($hasUdsPayload) { "0x{0:X2}" -f $response.Payload[4] } else { "none" }
        Write-Verbose ("response payloadType=0x{0:X4} length={1} udsSid={2}" -f $response.PayloadType, $response.Payload.Length, $sid)
    }
    if ($isDiagnosticResponse -and $isExpectedUdsResponse) {
        return $true
    }

    $sid = if ($hasUdsPayload) { "0x{0:X2}" -f $response.Payload[4] } else { "none" }
    throw ("Unexpected diagnostic response payloadType=0x{0:X4} length={1} udsSid={2}." -f $response.PayloadType, $response.Payload.Length, $sid)
}

$connectionsList = New-Object System.Collections.Generic.List[object]
$startedAt = Get-Date
$success = 0
$failure = 0
$total = 0
$lastFailure = $null

try {
    for ($index = 0; $index -lt $Connections; $index++) {
        $connectionsList.Add((Open-ActivatedConnection -Index $index))
    }

    $deadline = (Get-Date).AddSeconds($DurationSeconds)
    $intervalMilliseconds = [Math]::Max(1, [int](1000 / $RequestsPerSecond))
    $cursor = 0
    while ((Get-Date) -lt $deadline) {
        $connection = $connectionsList[$cursor % $connectionsList.Count]
        $cursor++
        $total++
        try {
            if (Send-ReadDid -Connection $connection) {
                $success++
            } else {
                $failure++
                $lastFailure = "Unexpected diagnostic response."
            }
        } catch {
            $failure++
            $lastFailure = $_.Exception.Message
        }

        Start-Sleep -Milliseconds $intervalMilliseconds
    }
} finally {
    foreach ($connection in $connectionsList) {
        $connection.Client.Dispose()
    }
}

$duration = ((Get-Date) - $startedAt).TotalSeconds
$actualRps = if ($duration -gt 0) { $total / $duration } else { 0 }
$successRate = if ($total -gt 0) { $success / $total } else { 0 }

[pscustomobject]@{
    host = $HostName
    port = $Port
    targetConnections = $Connections
    establishedConnections = $connectionsList.Count
    targetRequestsPerSecond = $RequestsPerSecond
    durationSeconds = [Math]::Round($duration, 3)
    totalRequests = $total
    successfulResponses = $success
    failedResponses = $failure
    lastFailure = $lastFailure
    successRate = [Math]::Round($successRate, 4)
    achievedRequestsPerSecond = [Math]::Round($actualRps, 3)
} | ConvertTo-Json -Depth 4

if ($connectionsList.Count -lt $Connections -or $failure -gt 0) {
    exit 1
}

exit 0
