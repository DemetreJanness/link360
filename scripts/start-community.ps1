[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$labRoot = Split-Path -Parent $PSScriptRoot
$binary = Join-Path $labRoot 'artifacts/review-lab/bin/LINK360.exe'
$profileProbe = Join-Path $labRoot 'artifacts/review-lab/bin/LINK360.Tests.exe'
if (!(Test-Path -LiteralPath $binary) -or !(Test-Path -LiteralPath $profileProbe)) {
    throw 'Build and test this checkout first with scripts/test-review-lab.ps1.'
}
$profile = (& $profileProbe --community-profile | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0 -or $profile.name -ne 'LINK360 · Community review build') {
    throw 'This output is not the isolated community build. Rebuild the reviewed source first.'
}
Write-Host "Starting the community review build. Settings: $($profile.settings)"
Write-Host 'Close the normal LINK360 app first. Both builds retain the same camera-controller mutex.'
Start-Process -FilePath $binary -WorkingDirectory $labRoot -WindowStyle Normal
