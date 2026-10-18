$ErrorActionPreference='Stop'
$binary=Join-Path $PSScriptRoot '../src/desktop/bin/Release/net9.0-windows/LINK360.exe'
function Invoke-CameraWorker($request) {
    $info=[System.Diagnostics.ProcessStartInfo]::new($binary,'--worker')
    $info.UseShellExecute=$false
    $info.CreateNoWindow=$true
    $info.RedirectStandardInput=$true
    $info.RedirectStandardOutput=$true
    $process=[System.Diagnostics.Process]::Start($info)
    try {
        $process.StandardInput.WriteLine(($request | ConvertTo-Json -Compress))
        $process.StandardInput.Close()
        $output=$process.StandardOutput.ReadToEndAsync()
        if (!$process.WaitForExit(8000)) { $process.Kill($true); throw 'Worker timed out' }
        return ($output.GetAwaiter().GetResult() | ConvertFrom-Json)
    } finally { $process.Dispose() }
}
$reading=Invoke-CameraWorker @{Control=$null;Value=0}
if ($reading.Error -or $reading.Name -ne 'Insta360 Link') { throw 'Read protocol failed' }
$zoom=$reading.Controls | Where-Object Name -eq Zoom
$invalid=Invoke-CameraWorker @{Control='Zoom';Value=($zoom.Max+$zoom.Step)}
if ($invalid.Error -notmatch 'manual range and step') { throw 'Invalid range was accepted' }
$unsupported=Invoke-CameraWorker @{Control='Unknown';Value=0}
if ($unsupported.Error -notmatch 'Unsupported control') { throw 'Unknown control was accepted' }
$after=Invoke-CameraWorker @{Control=$null;Value=0}
if (($after.Controls | Where-Object Name -eq Zoom).Value -ne $zoom.Value) { throw 'Invalid request altered zoom' }
$settingsFile=Join-Path (Split-Path $binary) 'settings.local.json'
$settings=Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
if ($settings.Version -ne 1 -or $settings.Presets.Count -ne 4 -or $settings.Presets[0].Name -ne 'Main') { throw 'Preset persistence failed' }
'PASS: compiled worker protocol, unsupported/range rejection, unchanged zoom, local preset persistence'
