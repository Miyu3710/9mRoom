# 构建服务端（Debug）
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $root '.tools'

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
if (-not (Test-Path $dotnet)) {
    throw "找不到本地 .NET SDK：$dotnet"
}

Push-Location $PSScriptRoot
try {
    & $dotnet build .\NineMRoom.Server.csproj -c Debug @args
}
finally {
    Pop-Location
}
