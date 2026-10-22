param(
    [string]$Server = 'http://127.0.0.1:5080',
    [string]$Room = 'demo'
)

$ErrorActionPreference = 'Stop'
$script:failures = 0

function Check([string]$label, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Output "PASS  $label" }
    else { Write-Output "FAIL  $label  $detail"; $script:failures++ }
}

function Connect-Ws([string]$name) {
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $uri = [Uri]("$script:WsBase/v1/ws?room=$script:Room&name=$name&device=windows&role=member&client=poke-$script:RunId-$name")
    $ws.ConnectAsync($uri, [Threading.CancellationToken]::None).Wait(5000) | Out-Null
    return $ws
}

function Receive-Json($ws, [int]$timeoutMs = 3000) {
    $buf = New-Object byte[] 16384
    $seg = New-Object System.ArraySegment[byte] -ArgumentList (,$buf)
    $ms = New-Object System.IO.MemoryStream
    $cts = New-Object System.Threading.CancellationTokenSource
    $cts.CancelAfter($timeoutMs)
    try {
        do {
            $res = $ws.ReceiveAsync($seg, $cts.Token).Result
            if ($res.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { return $null }
            $ms.Write($buf, 0, $res.Count)
        } while (-not $res.EndOfMessage)
        return ([Text.Encoding]::UTF8.GetString($ms.ToArray()) | ConvertFrom-Json)
    }
    catch { return $null }
    finally { $cts.Dispose(); $ms.Dispose() }
}

function Send-Json($ws, $obj) {
    $json = $obj | ConvertTo-Json -Depth 12 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $seg = New-Object System.ArraySegment[byte] -ArgumentList (,$bytes)
    $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait(5000) | Out-Null
}

function Receive-Type($ws, [string]$type, [int]$timeoutMs = 3000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($timeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $msg = Receive-Json $ws 500
        if ($null -eq $msg) { continue }
        if ($msg.type -eq $type) { return $msg }
    }
    return $null
}

$serverUri = [Uri]$Server
$wsScheme = if ($serverUri.Scheme -eq 'https') { 'wss' } else { 'ws' }
$script:WsBase = "${wsScheme}://$($serverUri.Authority)"
$script:Room = $Room
$script:RunId = [Guid]::NewGuid().ToString('n').Substring(0, 8)

$alice = Connect-Ws 'Alice'
$aliceWelcome = Receive-Json $alice
$aliceSnap = Receive-Json $alice
Check 'alice welcome' ($null -ne $aliceWelcome -and $aliceWelcome.type -eq 'welcome')
Check 'alice snapshot' ($null -ne $aliceSnap -and $aliceSnap.type -eq 'room.snapshot')
$aliceId = $aliceSnap.payload.members | Where-Object { $_.name -eq 'Alice' } | Select-Object -First 1 -ExpandProperty id

$bob = Connect-Ws 'Bob'
$bobWelcome = Receive-Json $bob
$bobSnap = Receive-Json $bob
Check 'bob welcome' ($null -ne $bobWelcome -and $bobWelcome.type -eq 'welcome')
Check 'bob snapshot has 2 members' ($bobSnap.payload.members.Count -eq 2)
$bobId = $bobSnap.payload.members | Where-Object { $_.name -eq 'Bob' } | Select-Object -First 1 -ExpandProperty id

# Bob 加入会让 Alice 收到 presence.changed 与 member.joined 两条广播
$joined1 = Receive-Json $alice 3000
$joined2 = Receive-Json $alice 3000
Check 'alice sees bob join events' ($null -ne $joined1 -and $null -ne $joined2)

Send-Json $alice @{ v = 1; type = 'poke.send'; id = 'poke-1'; payload = @{ targetId = $bobId } }
$received = Receive-Type $bob 'poke.received'
$sent = Receive-Type $alice 'poke.sent'
Check 'bob receives poke.received' ($null -ne $received)
Check 'poke.received from alice' ($null -ne $received -and $received.payload.from.name -eq 'Alice')
Check 'poke.received targetId' ($null -ne $received -and $received.payload.targetId -eq $bobId)
Check 'alice receives poke.sent' ($null -ne $sent -and $sent.payload.targetId -eq $bobId)

Send-Json $alice @{ v = 1; type = 'poke.send'; id = 'poke-fast'; payload = @{ targetId = $bobId } }
$fastError = Receive-Type $alice 'error'
Check 'poke rate limited' ($null -ne $fastError -and $fastError.payload.code -eq 'slow_down')

Send-Json $alice @{ v = 1; type = 'poke.send'; id = 'poke-self'; payload = @{ targetId = $aliceId } }
$selfError = Receive-Type $alice 'error'
Check 'self poke rejected' ($null -ne $selfError -and $selfError.payload.code -eq 'poke_self')

Send-Json $alice @{ v = 1; type = 'poke.send'; id = 'poke-missing'; payload = @{ targetId = 'u_deadbeef' } }
$missingError = Receive-Type $alice 'error'
Check 'unknown target rejected' ($null -ne $missingError -and $missingError.payload.code -eq 'poke_not_found')

try { $alice.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', [Threading.CancellationToken]::None).Wait(2000) | Out-Null } catch { }
try { $bob.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', [Threading.CancellationToken]::None).Wait(2000) | Out-Null } catch { }

Write-Output "FAILURES=$script:failures"
