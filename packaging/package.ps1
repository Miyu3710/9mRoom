# Build 9mRoom Windows packages
# Output: dist/9mRoom-1.0.0-win-x64.zip, dist/9mRoom-Setup-1.0.0.msi, dist/SHA256SUMS.txt
param(
    [string]$Version = '1.0.0',
    [switch]$Sign,
    [string]$PfxPath,
    [string]$PfxPassword = $env:NINEMROOM_SIGN_PFX_PASSWORD,
    [string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$tools = Join-Path $root '.tools'
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'publish'

$env:DOTNET_CLI_HOME = Join-Path $tools 'home'
$env:NUGET_PACKAGES = Join-Path $tools 'nuget'
$env:APPDATA = Join-Path $tools 'appdata'
$env:LOCALAPPDATA = Join-Path $tools 'localappdata'
$env:TMP = Join-Path $tools 'temp'
$env:TEMP = $env:TMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:APPDATA, $env:LOCALAPPDATA, $env:TMP | Out-Null

$dotnet = Join-Path $tools 'dotnet\dotnet.exe'
$wix = Join-Path $tools 'wix5\wix.exe'
if (-not (Test-Path $wix)) {
    $wix = Join-Path $tools 'wix\wix.exe'
}
$uiExtension = Join-Path $root '.wix\extensions\WixToolset.UI.wixext\5.0.2\wixext5\WixToolset.UI.wixext.dll'
$icon = Join-Path $PSScriptRoot '9mRoom.ico'
$license = Join-Path $PSScriptRoot 'license.rtf'
$uiLocalization = Join-Path $PSScriptRoot 'WixUI_zh-CN.wxl'

if (-not (Test-Path $dotnet)) { throw "Local .NET SDK not found: $dotnet" }
if (-not (Test-Path $wix)) { throw "WiX not found: $wix (install with dotnet tool install wix --tool-path .tools/wix5)" }
if (-not (Test-Path $uiExtension)) {
    throw 'WiX UI extension not found. Run: .\.tools\wix5\wix.exe extension add WixToolset.UI.wixext/5.0.2'
}
if (-not (Test-Path $icon)) { throw "Installer icon not found: $icon" }
if (-not (Test-Path $license)) { throw "License RTF not found: $license" }
if (-not (Test-Path $uiLocalization)) { throw "Chinese WiX UI localization not found: $uiLocalization" }

$distFull = [System.IO.Path]::GetFullPath($dist)
if (-not $distFull.StartsWith([System.IO.Path]::GetFullPath($root), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean dist outside workspace: $distFull"
}

if (Test-Path $distFull) {
    Remove-Item $distFull -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publish | Out-Null

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'design\generate-tokens.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Token generation failed' }

Push-Location (Join-Path $root 'clients\windows')
try {
    & $dotnet publish '.\src\NineMRoom.Client.App\NineMRoom.Client.App.csproj' `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}
finally {
    Pop-Location
}

Copy-Item (Join-Path $root 'clients\windows\9mRoom.cmd') $publish -Force

if ($Sign) {
    $appSignArgs = @{
        Path = $publish
        TimestampUrl = $TimestampUrl
    }
    if ($PfxPath) { $appSignArgs.PfxPath = $PfxPath }
    if ($PfxPassword) { $appSignArgs.PfxPassword = $PfxPassword }
    if ($Thumbprint) { $appSignArgs.Thumbprint = $Thumbprint }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sign.ps1') @appSignArgs
    if ($LASTEXITCODE -ne 0) { throw 'Application signing failed' }
}

$msi = Join-Path $dist "9mRoom-Setup-$Version.msi"
& $wix build (Join-Path $PSScriptRoot 'installer.wxs') `
    -arch x64 `
    -ext $uiExtension `
    -loc $uiLocalization `
    -d "Version=$Version" `
    -d "PublishDir=$publish" `
    -d "IconPath=$icon" `
    -d "LicensePath=$license" `
    -d "TaskKillPath=$(Join-Path $env:SystemRoot 'System32\taskkill.exe')" `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw 'WiX MSI build failed' }

Get-ChildItem $dist -Filter '*.wixpdb' | Remove-Item -Force

if ($Sign) {
    $signArgs = @{
        Path = $dist
        TimestampUrl = $TimestampUrl
    }
    if ($PfxPath) { $signArgs.PfxPath = $PfxPath }
    if ($PfxPassword) { $signArgs.PfxPassword = $PfxPassword }
    if ($Thumbprint) { $signArgs.Thumbprint = $Thumbprint }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sign.ps1') @signArgs
    if ($LASTEXITCODE -ne 0) { throw 'Signing failed' }
}
else {
    Write-Warning 'Unsigned build. Pass -Sign with a code-signing certificate before public release.'
}

$zip = Join-Path $dist "9mRoom-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal -Force

$serverDir = Join-Path $dist 'server-linux-x64'
& $dotnet publish (Join-Path $root 'server\NineMRoom.Server.csproj') `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -o $serverDir
if ($LASTEXITCODE -ne 0) { throw 'server Linux publish failed' }

Copy-Item (Join-Path $PSScriptRoot 'run-server.sh') $serverDir -Force
Copy-Item (Join-Path $PSScriptRoot 'update-server.sh') $serverDir -Force

$serverArchive = Join-Path $dist "9mRoom-Server-$Version-linux-x64.tar.gz"
& tar.exe -czf $serverArchive -C $serverDir .
if ($LASTEXITCODE -ne 0) { throw 'server Linux archive failed' }

$hashes = @()
foreach ($file in @($zip, $msi, $serverArchive)) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash
    $hashes += "$hash  $([System.IO.Path]::GetFileName($file))"
}

$windowsSha = (Get-FileHash $msi -Algorithm SHA256).Hash
$serverSha = (Get-FileHash $serverArchive -Algorithm SHA256).Hash

$windowsManifest = Join-Path $dist 'update-windows.json'
[ordered]@{
    platform = 'windows'
    version = $Version
    tag = "windows-v$Version"
    release = "https://github.com/Miyu3710/9mRoom/releases/tag/windows-v$Version"
    windows = [ordered]@{
        asset = [System.IO.Path]::GetFileName($msi)
        sha256 = $windowsSha
        portable = [System.IO.Path]::GetFileName($zip)
        portableSha256 = (Get-FileHash $zip -Algorithm SHA256).Hash
    }
} | ConvertTo-Json -Depth 5 | Set-Content $windowsManifest -Encoding UTF8

$serverManifest = Join-Path $dist 'update-server.json'
[ordered]@{
    platform = 'server'
    version = $Version
    tag = "server-v$Version"
    release = "https://github.com/Miyu3710/9mRoom/releases/tag/server-v$Version"
    linux = [ordered]@{
        asset = [System.IO.Path]::GetFileName($serverArchive)
        sha256 = $serverSha
        entry = 'run-server.sh'
    }
} | ConvertTo-Json -Depth 5 | Set-Content $serverManifest -Encoding UTF8

# 兼容仍读取旧清单的客户端；新客户端使用各自平台的清单。

[ordered]@{
    version = $Version
    tag = "windows-v$Version"
    release = "https://github.com/Miyu3710/9mRoom/releases/tag/windows-v$Version"
    windows = [ordered]@{
        asset = [System.IO.Path]::GetFileName($msi)
        sha256 = $windowsSha
    }
    linux = [ordered]@{
        asset = [System.IO.Path]::GetFileName($serverArchive)
        sha256 = $serverSha
        entry = 'run-server.sh'
    }
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dist 'update.json') -Encoding UTF8
$windowsManifestHash = (Get-FileHash $windowsManifest -Algorithm SHA256).Hash
$serverManifestHash = (Get-FileHash $serverManifest -Algorithm SHA256).Hash
$manifestHash = (Get-FileHash (Join-Path $dist 'update.json') -Algorithm SHA256).Hash
$hashes += "$windowsManifestHash  update-windows.json"
$hashes += "$serverManifestHash  update-server.json"
$hashes += "$manifestHash  update.json"
$hashes | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII

Get-ChildItem $dist | Select-Object Name, Length
Write-Host "Packages written to $dist"
