Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ApiBase = "http://127.0.0.1:5080"
$DoipHost = "127.0.0.1"
$DoipPort = 13400
$Tester = [UInt16]0x0E80
$Ecu = [UInt16]0x0E00
$Results = [System.Collections.Generic.List[object]]::new()

function ConvertTo-HexText { param([byte[]]$Bytes) if ($null -eq $Bytes -or $Bytes.Length -eq 0) { return "" } (($Bytes | ForEach-Object { $_.ToString("X2") }) -join " ") }
function Add-Result { param([string]$Name, [bool]$Pass, [string]$Detail) $script:Results.Add([pscustomobject]@{ Name = $Name; Pass = $Pass; Detail = $Detail }) }
function New-DoipFrame {
    param([UInt16]$PayloadType, [byte[]]$Payload)
    $frame = New-Object byte[] (8 + $Payload.Length)
    $frame[0] = 0x02; $frame[1] = 0xFD
    $frame[2] = [byte](($PayloadType -shr 8) -band 0xFF); $frame[3] = [byte]($PayloadType -band 0xFF)
    $length = [UInt32]$Payload.Length
    $frame[4] = [byte](($length -shr 24) -band 0xFF); $frame[5] = [byte](($length -shr 16) -band 0xFF)
    $frame[6] = [byte](($length -shr 8) -band 0xFF); $frame[7] = [byte]($length -band 0xFF)
    [Array]::Copy($Payload, 0, $frame, 8, $Payload.Length)
    return $frame
}
function Read-Exact {
    param([System.IO.Stream]$Stream, [int]$Length)
    $buffer = New-Object byte[] $Length
    $offset = 0
    while ($offset -lt $Length) {
        $read = $Stream.Read($buffer, $offset, $Length - $offset)
        if ($read -le 0) { throw "socket closed while reading $Length bytes" }
        $offset += $read
    }
    return $buffer
}
function Read-DoipFrame {
    param([System.IO.Stream]$Stream)
    $header = Read-Exact -Stream $Stream -Length 8
    $length = ([UInt32]$header[4] -shl 24) -bor ([UInt32]$header[5] -shl 16) -bor ([UInt32]$header[6] -shl 8) -bor [UInt32]$header[7]
    $payload = if ($length -gt 0) { Read-Exact -Stream $Stream -Length ([int]$length) } else { [byte[]]@() }
    return [pscustomobject]@{ PayloadType = ([UInt16]$header[2] -shl 8) -bor [UInt16]$header[3]; Payload = $payload }
}
function Write-Bytes { param([System.IO.Stream]$Stream, [byte[]]$Bytes) $Stream.Write($Bytes, 0, $Bytes.Length) }
function New-DiagnosticPayload {
    param([byte[]]$Uds)
    $payload = New-Object byte[] (4 + $Uds.Length)
    $payload[0] = [byte](($script:Tester -shr 8) -band 0xFF); $payload[1] = [byte]($script:Tester -band 0xFF)
    $payload[2] = [byte](($script:Ecu -shr 8) -band 0xFF); $payload[3] = [byte]($script:Ecu -band 0xFF)
    [Array]::Copy($Uds, 0, $payload, 4, $Uds.Length)
    return $payload
}
function Get-UdsFromDoipPayload {
    param([byte[]]$Payload)
    if ($Payload.Length -lt 4) { return [byte[]]@() }
    $uds = New-Object byte[] ($Payload.Length - 4)
    [Array]::Copy($Payload, 4, $uds, 0, $uds.Length)
    return $uds
}
function Send-Uds {
    param([System.IO.Stream]$Stream, [byte[]]$Uds)
    Write-Bytes -Stream $Stream -Bytes (New-DoipFrame -PayloadType 0x8001 -Payload (New-DiagnosticPayload -Uds $Uds))
    return Get-UdsFromDoipPayload -Payload (Read-DoipFrame -Stream $Stream).Payload
}
function Assert-Prefix {
    param([byte[]]$Actual, [byte[]]$ExpectedPrefix)
    if ($Actual.Length -lt $ExpectedPrefix.Length) { return $false }
    for ($i = 0; $i -lt $ExpectedPrefix.Length; $i++) { if ($Actual[$i] -ne $ExpectedPrefix[$i]) { return $false } }
    return $true
}
function Test-UdpVehicleIdentification {
    $udp = [System.Net.Sockets.UdpClient]::new(0)
    $udp.Client.ReceiveTimeout = 2000
    try {
        $request = New-DoipFrame -PayloadType 0x0001 -Payload ([byte[]]@())
        [void]$udp.Send($request, $request.Length, $script:DoipHost, $script:DoipPort)
        $remote = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0)
        $bytes = $udp.Receive([ref]$remote)
        Add-Result "UDP vehicle identification" ($bytes.Length -ge 40 -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x04) ("response={0}" -f (ConvertTo-HexText $bytes))
    } catch { Add-Result "UDP vehicle identification" $false $_.Exception.Message } finally { $udp.Dispose() }
}
function New-DoipTcpSession {
    param([UInt16]$TesterAddress = $script:Tester)
    $client = [System.Net.Sockets.TcpClient]::new()
    $client.ReceiveTimeout = 5000; $client.SendTimeout = 5000
    $client.Connect($script:DoipHost, $script:DoipPort)
    $stream = $client.GetStream()
    $payload = New-Object byte[] 7
    $payload[0] = [byte](($TesterAddress -shr 8) -band 0xFF); $payload[1] = [byte]($TesterAddress -band 0xFF)
    Write-Bytes -Stream $stream -Bytes (New-DoipFrame -PayloadType 0x0005 -Payload $payload)
    return [pscustomobject]@{ Client = $client; Stream = $stream; RoutingResponse = (Read-DoipFrame -Stream $stream) }
}

try { $health = Invoke-RestMethod -Uri "$ApiBase/api/health"; Add-Result "API health" ($health.status -eq "ok") ("status={0} version={1}" -f $health.status, $health.version) } catch { Add-Result "API health" $false $_.Exception.Message }
try { $config = Invoke-RestMethod -Uri "$ApiBase/api/config"; Add-Result "API config" ($config.entity.vin -eq "LTEST000000000001") ("vin={0} logical={1}" -f $config.entity.vin, $config.entity.logicalAddress) } catch { Add-Result "API config" $false $_.Exception.Message }
Test-UdpVehicleIdentification

$deniedSession = $null
try {
    $deniedSession = New-DoipTcpSession -TesterAddress 0x0E81
    $payload = $deniedSession.RoutingResponse.Payload
    $code = if ($payload.Length -ge 5) { $payload[4] } else { 0xFF }
    Add-Result "TCP routing activation denied for non-whitelisted source" ($code -eq 0x00) ("code=0x{0:X2} payload={1}" -f $code, (ConvertTo-HexText $payload))
} catch { Add-Result "TCP routing activation denied for non-whitelisted source" $false $_.Exception.Message } finally { if ($deniedSession) { $deniedSession.Client.Dispose() } }

$session = $null
try {
    $session = New-DoipTcpSession
    $routingPayload = $session.RoutingResponse.Payload
    $routingCode = if ($routingPayload.Length -ge 5) { $routingPayload[4] } else { 0xFF }
    Add-Result "TCP routing activation accepted" ($routingCode -eq 0x10) ("code=0x{0:X2} payload={1}" -f $routingCode, (ConvertTo-HexText $routingPayload))
    Write-Bytes -Stream $session.Stream -Bytes (New-DoipFrame -PayloadType 0x0007 -Payload ([byte[]]@()))
    $alive = Read-DoipFrame -Stream $session.Stream
    Add-Result "DoIP alive check" ($alive.PayloadType -eq 0x0008 -and $alive.Payload.Length -eq 2 -and $alive.Payload[0] -eq 0x0E -and $alive.Payload[1] -eq 0x00) ("type=0x{0:X4} payload={1}" -f $alive.PayloadType, (ConvertTo-HexText $alive.Payload))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x10, 0x03)); Add-Result "UDS DiagnosticSessionControl extended" (Assert-Prefix $resp ([byte[]](0x50, 0x03))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x3E, 0x00)); Add-Result "UDS TesterPresent" (Assert-Prefix $resp ([byte[]](0x7E, 0x00))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x22, 0xF1, 0x90)); $vin = if ($resp.Length -ge 20) { [System.Text.Encoding]::ASCII.GetString($resp[3..($resp.Length - 1)]) } else { "" }; Add-Result "UDS ReadDataByIdentifier 0xF190" ((Assert-Prefix $resp ([byte[]](0x62, 0xF1, 0x90))) -and $vin -eq "LTEST000000000001") ("resp={0} vin={1}" -f (ConvertTo-HexText $resp), $vin)
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x2E, 0xF1, 0x90, 0x4C, 0x54, 0x45, 0x53, 0x54, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31)); Add-Result "UDS WriteDataByIdentifier 0xF190" (Assert-Prefix $resp ([byte[]](0x6E, 0xF1, 0x90))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $seedResp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x27, 0x01)); $seedPass = Assert-Prefix $seedResp ([byte[]](0x67, 0x01)); Add-Result "UDS SecurityAccess request seed" $seedPass ("resp={0}" -f (ConvertTo-HexText $seedResp))
    if ($seedPass -and $seedResp.Length -gt 2) { $seed = $seedResp[2..($seedResp.Length - 1)]; $key = New-Object byte[] $seed.Length; for ($i = 0; $i -lt $seed.Length; $i++) { $key[$i] = $seed[$i] -bxor 0xA5 }; $keyReq = [byte[]]@(0x27, 0x02) + $key; $keyResp = Send-Uds -Stream $session.Stream -Uds $keyReq; Add-Result "UDS SecurityAccess send key" (Assert-Prefix $keyResp ([byte[]](0x67, 0x02))) ("seed={0} key={1} resp={2}" -f (ConvertTo-HexText $seed), (ConvertTo-HexText $key), (ConvertTo-HexText $keyResp)) }
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x31, 0x01, 0x02, 0x01)); Add-Result "UDS RoutineControl start 0x0201" (Assert-Prefix $resp ([byte[]](0x71, 0x01, 0x02, 0x01))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x28, 0x03, 0x03)); Add-Result "UDS CommunicationControl disableRxAndTx" (Assert-Prefix $resp ([byte[]](0x68, 0x03))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x85, 0x02)); Add-Result "UDS ControlDTCSetting off" (Assert-Prefix $resp ([byte[]](0xC5, 0x02))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x19, 0x02, 0xFF)); Add-Result "UDS ReadDTCInformation reportByStatusMask" (Assert-Prefix $resp ([byte[]](0x59, 0x02, 0xFF))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x14, 0xFF, 0xFF, 0xFF)); Add-Result "UDS ClearDiagnosticInformation all group" (Assert-Prefix $resp ([byte[]](0x54))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x34, 0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x10)); Add-Result "UDS RequestDownload default-disabled negative" (Assert-Prefix $resp ([byte[]](0x7F, 0x34, 0x22))) ("resp={0}" -f (ConvertTo-HexText $resp))
    $resp = Send-Uds -Stream $session.Stream -Uds ([byte[]](0x99)); Add-Result "UDS unsupported service negative" (Assert-Prefix $resp ([byte[]](0x7F, 0x99, 0x11))) ("resp={0}" -f (ConvertTo-HexText $resp))
} catch { Add-Result "TCP/UDS session fatal" $false $_.Exception.Message } finally { if ($session) { $session.Client.Dispose() } }

try { $metrics = Invoke-RestMethod -Uri "$ApiBase/api/metrics"; Add-Result "API metrics after traffic" ($null -ne $metrics.collectedAt) ("active={0} totalAccepted={1} udsRps={2}" -f $metrics.connections.active, $metrics.connections.totalAccepted, $metrics.throughput.udsRequestsPerSecond) } catch { Add-Result "API metrics after traffic" $false $_.Exception.Message }

$passed = @($Results | Where-Object { $_.Pass }).Count
$failed = @($Results | Where-Object { -not $_.Pass }).Count
$Results | ForEach-Object { "{0} {1} :: {2}" -f ($(if ($_.Pass) { "PASS" } else { "FAIL" })), $_.Name, $_.Detail }
"SUMMARY pass=$passed fail=$failed total=$($Results.Count)"
if ($failed -gt 0) { exit 1 }
