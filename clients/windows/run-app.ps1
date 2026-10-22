# Run the Windows WPF client
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$exe = Join-Path $PSScriptRoot 'src\NineMRoom.Client.App\bin\Debug\net8.0-windows10.0.19041.0\NineMRoom.Client.App.exe'

if (-not (Test-Path $exe)) {
    throw "Not built yet. Run .\build.ps1 first."
}

$env:DOTNET_ROOT = Join-Path $root '.tools\dotnet'

& $exe @args
