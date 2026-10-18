param(
    [Parameter(Mandatory)][ValidateSet('Pan','Tilt','Roll','Zoom','Focus')][string]$Control,
    [Parameter(Mandatory)][int]$Value
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Insta360 Link Controller' -ErrorAction SilentlyContinue) {
    throw 'Close the vendor controller to avoid competing control commands'
}
Add-Type -Path @((Join-Path $PSScriptRoot '../src/native/CameraProbe.cs'),(Join-Path $PSScriptRoot '../src/native/SmoothMotion.cs'))
$readings = @([Link360.CameraProbe]::SetControl($Control, $Value))
if ($readings.Count -ne 1) { throw 'Expected exactly one Insta360 Link camera' }
$readings | ConvertTo-Json -Depth 6
if ($readings[0].Error) { throw $readings[0].Error }
