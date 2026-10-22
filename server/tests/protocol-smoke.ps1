$ErrorActionPreference = 'Stop'
$script:failures = 0

function Connect-Ws([string]$name) {
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $uri = [Uri]("ws://127.0.0.1:5080/v1/ws?room=test&name=$name&device=windows&role=member")
    $ws.ConnectAsync($uri, [Threading.CancellationToken]::None).Wait(5000) | Out-Null
    return $ws
}

function Receive-Message($ws, [int]$timeoutMs = 5000) {
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
        $text = [Text.Encoding]::UTF8.GetString($ms.ToArray())
        return ($text | ConvertFrom-Json)
    }
    catch {
        return $null
    }
    finally {
        $cts.Dispose()
        $ms.Dispose()
    }
}

function Send-Json($ws, $obj) {
    $json = $obj | ConvertTo-Json -Depth 12 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $seg = New-Object System.ArraySegment[byte] -ArgumentList (,$bytes)
    $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait(5000) | Out-Null
}

function Check([string]$label, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Output "PASS  $label" }
    else { Write-Output "FAIL  $label  $detail"; $script:failures++ }
}

$statusA = @{
    layout    = 'inline'
    separator = ' | '
    blocks    = @(
        @{ id = 'a'; type = 'app'; template = '{app}'; data = @{ appId = 'vscode'; category = 'coding' } },
        @{ id = 'u'; type = 'uptime'; template = '{uptime}'; data = @{ startedAt = 1729989780000 } }
    )
}

$alice = Connect-Ws 'Alice'
$welcome = Receive-Message $alice
Check 'alice gets welcome' ($null -ne $welcome -and $welcome.type -eq 'welcome')
$snap = Receive-Message $alice
Check 'alice gets snapshot with self' ($null -ne $snap -and $snap.type -eq 'room.snapshot' -and $snap.payload.members.Count -eq 1)

Send-Json $alice @{ v = 1; type = 'presence.update'; id = 't1'; payload = @{ mode = 'focusing'; status = $statusA } }
$selfChanged = Receive-Message $alice
Check 'alice sees own presence.changed' ($null -ne $selfChanged -and $selfChanged.type -eq 'presence.changed' -and $selfChanged.payload.member.online -eq $true)
Check 'status blocks round-trip' ($null -ne $selfChanged -and $selfChanged.payload.member.status.blocks.Count -eq 2)

$bob = Connect-Ws 'Bob'
$bobWelcome = Receive-Message $bob
$bobSnap = Receive-Message $bob
Check 'bob snapshot has 2 members' ($null -ne $bobSnap -and $bobSnap.payload.members.Count -eq 2)
$aliceSeesBob = Receive-Message $alice
Check 'alice notified of bob join' ($null -ne $aliceSeesBob -and $aliceSeesBob.type -eq 'presence.changed' -and $aliceSeesBob.payload.member.name -eq 'Bob')

$statusB = @{
    layout    = 'inline'
    separator = ' | '
    blocks    = @(
        @{ id = 't'; type = 'text'; template = '{text}'; data = @{ text = 'bob here' } }
    )
}
Send-Json $bob @{ v = 1; type = 'presence.update'; id = 't2'; payload = @{ mode = 'focusing'; status = $statusB } }
$bobSelf = Receive-Message $bob
$aliceSeesBobUpdate = Receive-Message $alice
Check 'bob sees own update' ($null -ne $bobSelf -and $bobSelf.payload.member.status.blocks[0].data.text -eq 'bob here')
Check 'alice receives bob update' ($null -ne $aliceSeesBobUpdate -and $aliceSeesBobUpdate.payload.member.status.blocks[0].data.text -eq 'bob here')

$big = @{ v = 1; type = 'presence.update'; id = 't3'; payload = @{ status = @{ layout = 'inline'; blocks = @(1..9 | ForEach-Object { @{ type = 'text'; template = '{text}'; data = @{ text = "x$_" } } }) } } }
Send-Json $bob $big
$err = Receive-Message $bob
Check 'server rejects 9 blocks' ($null -ne $err -and $err.type -eq 'error' -and $err.payload.code -eq 'bad_status')

Send-Json $bob @{ v = 1; type = 'ping'; id = 't4'; payload = @{} }
$pong = Receive-Message $bob
Check 'ping returns pong with server time' ($null -ne $pong -and $pong.type -eq 'pong' -and $pong.payload.serverTime -gt 0)

try {
    $alice.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', [Threading.CancellationToken]::None).Wait(3000) | Out-Null
}
catch {
    # 服务端可能已先行结束连接；离开广播仍应到达 bob
}
$bobSeesLeft = Receive-Message $bob
Check 'bob notified of alice leave' ($null -ne $bobSeesLeft -and $bobSeesLeft.type -eq 'member.left')
try {
    $bob.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', [Threading.CancellationToken]::None).Wait(3000) | Out-Null
}
catch { }

Write-Output "FAILURES=$script:failures"
