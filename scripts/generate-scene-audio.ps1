$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$model = Join-Path $root '.rea-tools/piper/en_GB-cori-high.onnx'
if ((Get-FileHash -LiteralPath $model).Hash -ne '470B4DD634C98F8A4850D7626FFC3DFC90774628EEEF6605A6DD8F88F30A5903') { throw 'Voice model checksum mismatch' }
$phrases = [ordered]@{
 'scene-leave.wav' = 'Please leave the room. The countdown begins now.'
 'scene-start.wav' = 'Scanning the room now. Please stay outside until I say the scan is complete.'
 'scene-complete.wav' = 'Room scan complete. You may return now. Done, thank you.'
 'scene-failed.wav' = 'The room scan could not complete. You may return now. Please check the message on screen.'
 'scene-cancelled.wav' = 'Room scan cancelled. You may return now.'
 'count-30.wav' = 'Thirty.'
 'count-20.wav' = 'Twenty.'
 'count-10.wav' = 'Ten.'
 'count-5.wav' = 'Five.'
 'count-4.wav' = 'Four.'
 'count-3.wav' = 'Three.'
 'count-2.wav' = 'Two.'
 'count-1.wav' = 'One.'
}
$requests = foreach ($entry in $phrases.GetEnumerator()) {
 @{ text=$entry.Value; output_file=(Join-Path $root "assets/audio/$($entry.Key)") } | ConvertTo-Json -Compress
}
$requests | & (Join-Path $root '.rea-tools/piper/runtime/piper/piper.exe') --model $model --json-input --quiet
if ($LASTEXITCODE -ne 0) { throw 'Local speech generation failed' }
$provenancePath = Join-Path $root 'assets/audio/provenance.json'
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json -AsHashtable
foreach ($entry in $phrases.GetEnumerator()) { $provenance.texts[$entry.Key] = $entry.Value }
$provenance.files = @(Get-ChildItem -LiteralPath (Join-Path $root 'assets/audio') -Filter '*.wav' | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$provenance | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $provenancePath -Encoding utf8
