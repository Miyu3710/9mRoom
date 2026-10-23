param(
    [string]$Tokens = (Join-Path $PSScriptRoot 'tokens.json'),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\clients\windows\src\NineMRoom.Client.App\Themes')
)

$ErrorActionPreference = 'Stop'

function ConvertTo-Pascal([string]$value) {
    if ([string]::IsNullOrEmpty($value)) { return '' }
    return $value.Substring(0, 1).ToUpperInvariant() + $value.Substring(1)
}

function Format-Number($value) {
    return ([double]$value).ToString('0.####', [Globalization.CultureInfo]::InvariantCulture)
}

function Write-TokenFile {
    param(
        [string]$Path,
        [object]$Colors,
        [object]$Status
    )

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"')
    [void]$sb.AppendLine('                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"')
    [void]$sb.AppendLine('                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">')
    [void]$sb.AppendLine('  <!-- Generated from design/tokens.json by design/generate-tokens.ps1. Do not edit. -->')

    foreach ($property in $Colors.PSObject.Properties) {
        $key = ConvertTo-Pascal $property.Name
        [void]$sb.AppendLine(('  <Color x:Key="Color{0}">{1}</Color>' -f $key, $property.Value))
        [void]$sb.AppendLine(('  <SolidColorBrush x:Key="Brush{0}" Color="{{StaticResource Color{0}}}" />' -f $key))
    }

    foreach ($property in $Status.PSObject.Properties) {
        $key = 'Status' + (ConvertTo-Pascal $property.Name)
        [void]$sb.AppendLine(('  <Color x:Key="Color{0}">{1}</Color>' -f $key, $property.Value))
        [void]$sb.AppendLine(('  <SolidColorBrush x:Key="Brush{0}" Color="{{StaticResource Color{0}}}" />' -f $key))
    }

    foreach ($property in $data.overlay.PSObject.Properties) {
        $key = 'Overlay' + (ConvertTo-Pascal $property.Name)
        [void]$sb.AppendLine(('  <Color x:Key="Color{0}">{1}</Color>' -f $key, $property.Value))
        [void]$sb.AppendLine(('  <SolidColorBrush x:Key="Brush{0}" Color="{{StaticResource Color{0}}}" />' -f $key))
    }

    [void]$sb.AppendLine(('  <FontFamily x:Key="FontUi">{0}</FontFamily>' -f $data.font.familyUi))
    [void]$sb.AppendLine(('  <FontFamily x:Key="FontMono">{0}</FontFamily>' -f $data.font.familyMono))

    foreach ($property in $data.font.type.PSObject.Properties) {
        $key = 'Type' + (ConvertTo-Pascal $property.Name)
        [void]$sb.AppendLine(('  <sys:Double x:Key="{0}Size">{1}</sys:Double>' -f $key, (Format-Number $property.Value.size)))
        [void]$sb.AppendLine(('  <sys:Double x:Key="{0}Line">{1}</sys:Double>' -f $key, (Format-Number $property.Value.line)))
        [void]$sb.AppendLine(('  <FontWeight x:Key="{0}Weight">{1}</FontWeight>' -f $key, $property.Value.weight))
    }

    foreach ($property in $data.space.PSObject.Properties) {
        [void]$sb.AppendLine(('  <sys:Double x:Key="Space{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), (Format-Number $property.Value)))
    }

    foreach ($property in $data.shape.PSObject.Properties) {
        [void]$sb.AppendLine(('  <CornerRadius x:Key="Shape{0}">{1}</CornerRadius>' -f (ConvertTo-Pascal $property.Name), (Format-Number $property.Value)))
    }

    foreach ($property in $data.size.PSObject.Properties) {
        [void]$sb.AppendLine(('  <sys:Double x:Key="Size{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), (Format-Number $property.Value)))
    }

    foreach ($property in $data.state.PSObject.Properties) {
        [void]$sb.AppendLine(('  <sys:Double x:Key="State{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), (Format-Number $property.Value)))
    }

    foreach ($property in $data.motion.PSObject.Properties) {
        [void]$sb.AppendLine(('  <sys:Double x:Key="Motion{0}">{1}</sys:Double>' -f (ConvertTo-Pascal $property.Name), (Format-Number $property.Value)))
    }

    [void]$sb.AppendLine('</ResourceDictionary>')

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($Path, $sb.ToString(), $utf8)
    Write-Output "generated $Path"
}

$data = Get-Content -Path $Tokens -Raw -Encoding UTF8 | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-TokenFile -Path (Join-Path $OutDir 'Tokens.xaml') -Colors $data.color -Status $data.status
Write-TokenFile -Path (Join-Path $OutDir 'TokensLight.xaml') -Colors $data.lightColor -Status $data.lightStatus
