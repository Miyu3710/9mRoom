$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\env.ps1"
Push-Location $PSScriptRoot
try {
    if ($args.Count -eq 0) { & .\gradlew.bat assembleDebug }
    else { & .\gradlew.bat @args }
    if ($LASTEXITCODE -ne 0) { throw "Gradle failed: $LASTEXITCODE" }
} finally { Pop-Location }
