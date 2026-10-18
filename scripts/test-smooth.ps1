param([ValidateRange(500,6000)][int]$DurationMs=1400)
$ErrorActionPreference='Stop'
$binary=Join-Path $PSScriptRoot '../src/desktop/bin/Release/net9.0-windows/LINK360.exe'
function Start-Worker($request) {
    $info=[System.Diagnostics.ProcessStartInfo]::new($binary,'--worker')
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardInput=$true;$info.RedirectStandardOutput=$true
    $p=[System.Diagnostics.Process]::Start($info)
    $p.StandardInput.WriteLine(($request|ConvertTo-Json -Compress -Depth 5));$p.StandardInput.Close()
    return $p
}
function Complete-Worker($p) {
    try {
        $output=$p.StandardOutput.ReadToEndAsync()
        if (!$p.WaitForExit(8000)) { $p.Kill($true); throw 'Motion exceeded deadline' }
        $result=$output.GetAwaiter().GetResult()|ConvertFrom-Json
        if($p.ExitCode -ne 0 -or $result.Error){throw $result.Error}
        return $result
    } finally {$p.Dispose()}
}
$initial=Complete-Worker (Start-Worker @{})
$pose=@{}
foreach($name in @('Pan','Tilt','Zoom')) {$pose[$name]=($initial.Controls|Where-Object Name -eq $name).Value}
$target=@{Pan=$pose.Pan+8;Tilt=$pose.Tilt+6;Zoom=$pose.Zoom+40}
foreach($name in @('Pan','Tilt','Zoom')) {
    $c=$initial.Controls|Where-Object Name -eq $name
    if($target[$name] -gt $c.Max) {$target[$name]=$pose[$name]-($target[$name]-$pose[$name])}
}
try {
    node (Join-Path $PSScriptRoot 'capture-link.mjs') .rea-tools/smooth-before.png
    $p=Start-Worker @{Target=$target;DurationMs=$DurationMs}
    Start-Sleep -Milliseconds ([int]($DurationMs/2))
    node (Join-Path $PSScriptRoot 'capture-link.mjs') .rea-tools/smooth-middle.png
    $result=Complete-Worker $p
    if($result.MotionCommands -lt 4 -or $result.MotionElapsedMs -lt 1100) {throw 'Transition did not use a timed command sequence'}
    Start-Sleep -Milliseconds 400
    node (Join-Path $PSScriptRoot 'capture-link.mjs') .rea-tools/smooth-end.png
    $result|ConvertTo-Json -Depth 6|Set-Content .rea-tools/smooth-result.json
    'PASS: live eased transition and endpoint readback'
    $cancelled=Start-Worker @{Target=$pose;DurationMs=3000}
    Start-Sleep -Milliseconds 500
    $cancelled.Kill($true);$cancelled.WaitForExit();$cancelled.Dispose()
    $after=Complete-Worker (Start-Worker @{})
    $after|ConvertTo-Json -Depth 6|Set-Content .rea-tools/smooth-cancelled-state.json
    if(($after.Controls|Where-Object Name -eq Zoom).Value -eq $pose.Zoom){throw 'Cancelled transition unexpectedly reached endpoint'}
    'PASS: terminated motion did not complete or replay its target'
} finally {
    $restored=Complete-Worker (Start-Worker @{Target=$pose;DurationMs=1400})
    $restored|ConvertTo-Json -Depth 6|Set-Content .rea-tools/smooth-restored-state.json
}
