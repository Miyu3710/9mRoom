param(
    [string]$Server = 'http://127.0.0.1:5080',
    [string]$Room = 'demo',
    [int]$GraceSeconds = 3
)

$ErrorActionPreference = 'Stop'
$script:failures = 0
$serverUri = [Uri]$Server
$wsScheme = if ($serverUri.Scheme -eq 'https') { 'wss' } else { 'ws' }
$script:WsBase = "${wsScheme}://$($serverUri.Authority)"
$script:Room = $Room
$script:RunId = [Guid]::NewGuid().ToString('n').Substring(0, 8)

function Check([string]$label, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Output "PASS  $label" }
    else { Write-Output "FAIL  $label  $detail"; $script:failures++ }
}

function Connect-Ws([string]$name, [string]$clientId) {
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $uri = [Uri]("$script:WsBase/v1/ws?room=$script:Room&name=$name&device=windows&role=member&client=$clientId")
    $ws.ConnectAsync($uri, [Threading.CancellationToken]::None).Wait(5000) | Out-Null
    return $ws
}

function Receive-Json($ws, [int]$timeoutMs = 5000) {
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

function Receive-Type($ws, [string]$type, [int]$timeoutMs = 5000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($timeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $msg = Receive-Json $ws 500
        if ($null -eq $msg) { continue }
        if ($msg.type -eq $type) { return $msg }
    }
    return $null
}

function Drain-To-Pong($ws) {
    Send-Json $ws @{ v = 1; type = 'ping'; id = 'drain'; payload = @{} }
    $messages = @()
    while ($true) {
        $msg = Receive-Json $ws 2500
        if ($null -eq $msg) { break }
        if ($msg.type -eq 'pong') { break }
        $messages += $msg
    }
    return $messages
}

function New-Client($name, $clientId) {
    $ws = Connect-Ws $name $clientId
    $welcome = Receive-Json $ws
    $snapshot = Receive-Json $ws
    return @{ Ws = $ws; Welcome = $welcome; Snapshot = $snapshot }
}

$bob = New-Client 'Bob' "bob-reconnect-$script:RunId"
Check 'observer welcome' ($null -ne $bob.Welcome -and $bob.Welcome.type -eq 'welcome')

$clientId = "alice-reconnect-$script:RunId"
$aliceA = New-Client 'Alice' $clientId
$aliceId = $aliceA.Welcome.payload.userId
Check 'alice initial welcome' ($null -ne $aliceId)
$join1 = Receive-Json $bob.Ws
$join2 = Receive-Json $bob.Ws
Check 'observer sees alice join' ($null -ne $join1 -and $null -ne $join2)

# 同一 clientId 新连接替换旧连接：保留 userId，无 left/joined
$aliceB = New-Client 'Alice' $clientId
Check 'resume keeps userId' ($aliceB.Welcome.payload.userId -eq $aliceId) "old=$aliceId new=$($aliceB.Welcome.payload.userId)"
$resumeEvent = Receive-Type $bob.Ws 'presence.changed'
Check 'resume broadcasts presence.changed' ($null -ne $resumeEvent)
$resumeEvents = Drain-To-Pong $bob.Ws
Check 'resume has no member.left' (-not ($resumeEvents | Where-Object { $_.type -eq 'member.left' }))
Check 'resume has no member.joined' (-not ($resumeEvents | Where-Object { $_.type -eq 'member.joined' }))

# 异常断开后，快照应包含 reconnecting 成员
$aliceB.Ws.Abort()
$aliceB.Ws.Dispose()
$reconnecting = Receive-Type $bob.Ws 'presence.changed'
Check 'abnormal close -> reconnecting' ($null -ne $reconnecting -and $reconnecting.payload.member.online -eq $false -and $reconnecting.payload.member.connectionState -eq 'reconnecting')

$charlie = New-Client 'Charlie' "charlie-reconnect-$script:RunId"
$charlieAlice = $charlie.Snapshot.payload.members | Where-Object { $_.id -eq $aliceId } | Select-Object -First 1
Check 'snapshot contains reconnecting member' ($null -ne $charlieAlice -and $charlieAlice.connectionState -eq 'reconnecting' -and $charlieAlice.online -eq $false)
Receive-Json $bob.Ws | Out-Null
Receive-Json $bob.Ws | Out-Null

$aliceC = New-Client 'Alice' $clientId
Check 'reconnect within grace keeps userId' ($aliceC.Welcome.payload.userId -eq $aliceId)
$restored = Receive-Type $bob.Ws 'presence.changed'
Check 'reconnect broadcasts presence.changed online' ($null -ne $restored -and $restored.payload.member.online -eq $true -and $restored.payload.member.connectionState -eq 'online')
$restoreEvents = Drain-To-Pong $bob.Ws
Check 'reconnect has no member.left' (-not ($restoreEvents | Where-Object { $_.type -eq 'member.left' }))
Check 'reconnect has no member.joined' (-not ($restoreEvents | Where-Object { $_.type -eq 'member.joined' }))

# 超过恢复窗口（测试用短窗口）只收到一次 member.left
$aliceC.Ws.Abort()
$aliceC.Ws.Dispose()
$expiring = Receive-Type $bob.Ws 'presence.changed'
Check 'close before expiry reconnecting' ($null -ne $expiring -and $expiring.payload.member.connectionState -eq 'reconnecting')
Start-Sleep -Seconds ($GraceSeconds + 1)
$left = Receive-Type $bob.Ws 'member.left'
Check 'expired member leaves once' ($null -ne $left -and $left.payload.id -eq $aliceId)
$afterLeft = Drain-To-Pong $bob.Ws
Check 'no duplicate member.left' (-not ($afterLeft | Where-Object { $_.type -eq 'member.left' }))

# 显式 leave：立即 left；随后同 clientId 必须按新成员加入
$aliceD = New-Client 'Alice' $clientId
$newId = $aliceD.Welcome.payload.userId
Check 'expired reconnect creates new member' ($newId -ne $aliceId)
Receive-Json $bob.Ws | Out-Null
Receive-Json $bob.Ws | Out-Null
Send-Json $aliceD.Ws @{ v = 1; type = 'leave'; id = 'leave-1'; payload = @{} }
$explicitLeft = Receive-Type $bob.Ws 'member.left'
Check 'explicit leave broadcasts left' ($null -ne $explicitLeft -and $explicitLeft.payload.id -eq $newId)
Start-Sleep -Milliseconds 300
$aliceD.Ws.Dispose()

$aliceE = New-Client 'Alice' $clientId
Check 'after explicit leave reconnect is new member' ($aliceE.Welcome.payload.userId -ne $newId)
$joinedAfterLeave = Receive-Type $bob.Ws 'member.joined'
Check 'after explicit leave gets member.joined' ($null -ne $joinedAfterLeave -and $joinedAfterLeave.payload.member.id -eq $aliceE.Welcome.payload.userId)

foreach ($client in @($bob, $aliceA, $aliceE, $charlie)) {
    try { $client.Ws.Abort(); $client.Ws.Dispose() } catch { }
}

Write-Output "FAILURES=$script:failures"
