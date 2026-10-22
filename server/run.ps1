# 启动本地开发服务端
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
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'

New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:APPDATA, $env:LOCALAPPDATA, $env:TMP | Out-Null

$dotnet = Join-Path $tools 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw "找不到本地 .NET SDK：$dotnet。见 README 的「构建环境」一节。"
}

Push-Location $PSScriptRoot
try {
    & $dotnet run -c Debug @args
}
finally {
    Pop-Location
}
