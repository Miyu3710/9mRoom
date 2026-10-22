# Build Windows client (CLI probe + WPF app)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
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
    throw "Local .NET SDK not found: $dotnet"
}

Push-Location $PSScriptRoot
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'design\generate-tokens.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'Token generation failed'
    }

    & $dotnet build '.\src\NineMRoom.Client.Cli\NineMRoom.Client.Cli.csproj' -c Debug @args
    if ($LASTEXITCODE -ne 0) {
        throw 'CLI build failed'
    }

    & $dotnet build '.\src\NineMRoom.Client.App\NineMRoom.Client.App.csproj' -c Debug @args
    if ($LASTEXITCODE -ne 0) {
        throw 'WPF build failed'
    }
}
finally {
    Pop-Location
}
