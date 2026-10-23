param(
    [string]$Repo = 'Miyu3710/9mRoom',
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $root 'docs\releases\releases.json'
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    throw 'GitHub CLI (gh) not found. Install and run gh auth login first.'
}

$raw = & $gh.Source api "repos/$Repo/releases?per_page=100"
if (-not $raw) {
    throw 'gh api returned no data.'
}

$releases = $raw | ConvertFrom-Json
$rows = foreach ($release in $releases) {
    if ($release.draft) {
        continue
    }

    $tag = [string]$release.tag_name
    $platform = if ($tag.StartsWith('windows-v')) {
        'windows'
    } elseif ($tag.StartsWith('server-v')) {
        'server'
    } elseif ($tag.StartsWith('android-v')) {
        'android'
    } else {
        continue
    }

    [pscustomobject]@{
        platform = $platform
        name = [string]$release.name
        tag = $tag
        publishedAt = [string]$release.published_at
        url = [string]$release.html_url
        prerelease = [bool]$release.prerelease
        body = [string]$release.body
        assets = @($release.assets | ForEach-Object {
            [pscustomobject]@{
                name = [string]$_.name
                size = [int64]$_.size
                url = [string]$_.browser_download_url
            }
        })
    }
}

$rows = @($rows | Sort-Object publishedAt -Descending)
$json = $rows | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
    $Output,
    $json,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote $($rows.Count) releases to $Output"
