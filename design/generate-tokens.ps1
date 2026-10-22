param(
    [string]$Tokens = (Join-Path $PSScriptRoot 'tokens.json'),
    [string]$OutXaml = (Join-Path $PSScriptRoot '..\clients\windows\src\NineMRoom.Client.App\Themes\Tokens.xaml')
)

$ErrorActionPreference = 'Stop'

function ConvertTo-Pascal([string]$value) {
    if ([string]::IsNullOrEmpty($value)) { return '' }
    return $value.Substring(0, 1).ToUpperInvariant() + $value.Substring(1)
}

$data = Get-Content -Path $Tokens -Raw -Encoding UTF8 | ConvertFrom-Json
$OutXaml = [IO.Path]::GetFullPath($OutXaml)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutXaml) | Out-Null

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"')
[void]$sb.AppendLine('                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"')
[void]$sb.AppendLine('                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">')
[void]$sb.AppendLine('  <!-- Generated from design/tokens.json. Do not edit. -->')

foreach ($property in $data.color.PSObject.Properties) {
    $key = ConvertTo-Pascal $property.Name
    [void]$sb.AppendLine(('  <Color x:Key="Color{0}">{1}</Color>' -f $key, $property.Value))
    [void]$sb.AppendLine(('  <SolidColorBrush x:Key="Brush{0}" Color="{{StaticResource Color{0}}}" />' -f $key))
}

[void]$sb.AppendLine(('  <FontFamily x:Key="FontUi">{0}</FontFamily>' -f $data.font.familyUi))
[void]$sb.AppendLine(('  <FontFamily x:Key="FontMono">{0}</FontFamily>' -f $data.font.familyMono))

foreach ($property in $data.font.size.PSObject.Properties) {
    [void]$sb.AppendLine(('  <sys:Double x:Key="FontSize{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), $property.Value))
}

foreach ($property in $data.space.PSObject.Properties) {
    [void]$sb.AppendLine(('  <sys:Double x:Key="Space{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), $property.Value))
}

foreach ($property in $data.radius.PSObject.Properties) {
    [void]$sb.AppendLine(('  <CornerRadius x:Key="Radius{0}">{1}</CornerRadius>' -f (ConvertTo-Pascal $property.Name), $property.Value))
}

foreach ($property in $data.layout.PSObject.Properties) {
    [void]$sb.AppendLine(('  <sys:Double x:Key="Layout{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), $property.Value))
}

foreach ($property in $data.motion.PSObject.Properties) {
    [void]$sb.AppendLine(('  <sys:Double x:Key="Motion{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), $property.Value))
}

[void]$sb.AppendLine('</ResourceDictionary>')

$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($OutXaml, $sb.ToString(), $utf8)
Write-Output "generated $OutXaml"
