# Sign 9mRoom release artifacts with a code-signing certificate.
# Requires either a PFX or a certificate thumbprint in CurrentUser\My.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$PfxPath,
    [string]$PfxPassword = $env:NINEMROOM_SIGN_PFX_PASSWORD,
    [string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

if (-not $PfxPath -and -not $Thumbprint) {
    throw 'Missing signing certificate: pass -PfxPath or -Thumbprint (or set NINEMROOM_SIGN_PFX_PASSWORD for a PFX).'
}

$temporaryThumbprint = $null
$cert = $null

if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PFX not found: $PfxPath" }
    if (-not $PfxPassword) { throw 'PFX password is required (or set NINEMROOM_SIGN_PFX_PASSWORD).' }

    $secure = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
    $imported = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $secure
    $temporaryThumbprint = $imported.Thumbprint
    $cert = Get-Item "Cert:\CurrentUser\My\$temporaryThumbprint"
}
else {
    $cert = Get-Item "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction Stop
}

try {
    if (-not $cert.HasPrivateKey) {
        throw "Certificate $($cert.Thumbprint) has no private key."
    }

    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if (-not $signtool) {
        $signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
    }

    $targets = @()
    $targets += Get-ChildItem (Join-Path $Path 'NineMRoom.Client.App.exe') -ErrorAction SilentlyContinue
    $targets += Get-ChildItem (Join-Path $Path 'publish\NineMRoom.Client.App.exe') -ErrorAction SilentlyContinue
    $targets += Get-ChildItem (Join-Path $Path '*.msi') -ErrorAction SilentlyContinue

    if ($targets.Count -eq 0) {
        throw "No artifacts to sign under $Path"
    }

    if ($signtool) {
        foreach ($file in $targets) {
            Write-Host "Signing $($file.FullName)"
            & $signtool.FullName sign /sha1 $cert.Thumbprint /tr $TimestampUrl /td sha256 /fd sha256 $file.FullName
            if ($LASTEXITCODE -ne 0) { throw "signtool failed for $($file.FullName)" }
        }
    }
    else {
        foreach ($file in $targets) {
            Write-Host "Signing (Authenticode) $($file.FullName)"
            $result = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert -TimestampServer $TimestampUrl -HashAlgorithm SHA256
            if ($result.Status -eq 'UnknownError') {
                throw "Authenticode signing failed for $($file.FullName)"
            }
        }
    }

    foreach ($file in $targets) {
        $signature = Get-AuthenticodeSignature $file.FullName
        Write-Host "$($file.Name): $($signature.Status) / $($signature.SignerCertificate.Subject)"
    }
}
finally {
    if ($temporaryThumbprint) {
        Remove-Item "Cert:\CurrentUser\My\$temporaryThumbprint" -Force -ErrorAction SilentlyContinue
    }
}
