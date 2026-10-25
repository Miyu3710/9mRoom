param(
    [string]$Server = 'http://127.0.0.1:5080',
    [string]$Room = 'demo'
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
    $uri = [Uri]("$script:WsBase/v1/ws?room=$script:Room&name=$name&device=windows&role=member&client=avatar-$script:RunId-$clientId")
    $ws.ConnectAsync($uri, [Threading.CancellationToken]::None).Wait(5000) | Out-Null
    return $ws
}

function Receive-Json($ws, [int]$timeoutMs = 5000) {
    $buf = New-Object byte[] 262144
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

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 16,16
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::FromArgb(40, 120, 200))
$graphics.Dispose()
$memory = New-Object System.IO.MemoryStream
$bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Jpeg)
$bitmap.Dispose()
$jpeg = $memory.ToArray()
$memory.Dispose()
$avatarData = [Convert]::ToBase64String($jpeg)

$alice = Connect-Ws 'Alice' 'alice'
$aliceWelcome = Receive-Json $alice
$aliceSnap = Receive-Json $alice
Check 'alice welcome' ($null -ne $aliceWelcome)
Check 'alice snapshot' ($null -ne $aliceSnap)

$bob = Connect-Ws 'Bob' 'bob'
$bobWelcome = Receive-Json $bob
$bobSnap = Receive-Json $bob
Check 'bob welcome' ($null -ne $bobWelcome)
Receive-Json $alice | Out-Null
Receive-Json $alice | Out-Null

Send-Json $alice @{
    v = 1
    type = 'avatar.update'
    id = 'avatar-1'
    payload = @{ mime = 'image/jpeg'; width = 16; height = 16; data = $avatarData }
}
$aliceAvatar = Receive-Type $alice 'member.avatar'
$bobAvatar = Receive-Type $bob 'member.avatar'
Check 'alice receives own avatar event' ($null -ne $aliceAvatar -and $aliceAvatar.payload.memberId -eq $aliceWelcome.payload.userId)
Check 'bob receives avatar event' ($null -ne $bobAvatar -and $bobAvatar.payload.memberId -eq $aliceWelcome.payload.userId)
Check 'avatar hash is sha256' ($null -ne $bobAvatar -and $bobAvatar.payload.avatar.hash.Length -eq 64)
Check 'avatar data round-trips' ($null -ne $bobAvatar -and $bobAvatar.payload.avatar.data -eq $avatarData)

$charlie = Connect-Ws 'Charlie' 'charlie'
$charlieWelcome = Receive-Json $charlie
$charlieSnap = Receive-Json $charlie
$charlieAlice = $charlieSnap.payload.members | Where-Object { $_.id -eq $aliceWelcome.payload.userId } | Select-Object -First 1
Check 'snapshot has avatar hash only' ($null -ne $charlieAlice -and $charlieAlice.hasAvatar -eq $true -and $charlieAlice.avatarHash.Length -eq 64)
$charlieAvatar = Receive-Type $charlie 'member.avatar'
Check 'new member receives room avatar' ($null -ne $charlieAvatar -and $charlieAvatar.payload.memberId -eq $aliceWelcome.payload.userId)

Send-Json $alice @{ v = 1; type = 'avatar.update'; id = 'avatar-bad'; payload = @{ mime = 'text/plain'; data = $avatarData } }
$bad = Receive-Type $alice 'error'
Check 'unsupported mime rejected' ($null -ne $bad -and $bad.payload.code -eq 'bad_avatar')

foreach ($ws in @($alice, $bob, $charlie)) {
    try { $ws.Abort(); $ws.Dispose() } catch { }
}

Write-Output "FAILURES=$script:failures"
