param([switch]$TestZoom, [switch]$TestPanTilt)
$ErrorActionPreference = 'Stop'
Add-Type -Path @((Join-Path $PSScriptRoot '../src/native/CameraProbe.cs'),(Join-Path $PSScriptRoot '../src/native/SmoothMotion.cs'))
if ($TestZoom -and $TestPanTilt) { throw 'Choose one test mode' }
if ($TestZoom -or $TestPanTilt) {
    if (Get-Process -Name 'Insta360 Link Controller' -ErrorAction SilentlyContinue) { throw 'Close the vendor controller first' }
    if ($TestPanTilt) { $readings = @([Link360.CameraProbe]::TestPanTilt()) }
    else { $readings = @([Link360.CameraProbe]::TestZoom()) }
    $readings | ConvertTo-Json -Depth 6
    foreach ($reading in $readings) { if ($reading.Error) { throw $reading.Error } }
} else {
    [Link360.CameraProbe]::Read() | ConvertTo-Json -Depth 6
}
