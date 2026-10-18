param([string]$VoiceRoot)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$VoiceRoot){$VoiceRoot=Join-Path $root '.rea-tools/piper'}
$model=Join-Path $VoiceRoot 'en_GB-cori-high.onnx'
if((Get-FileHash -LiteralPath $model).Hash -ne '470B4DD634C98F8A4850D7626FFC3DFC90774628EEEF6605A6DD8F88F30A5903'){throw 'Voice model checksum mismatch'}
$phrases=[ordered]@{
 'tracking-still.wav'='Tracking test started. Please sit still for fifteen seconds.'
 'tracking-slow.wav'='Please move slowly left and right.'
 'tracking-fast.wav'='Now make a faster sideways movement.'
 'tracking-settle.wav'='Please return to your normal seat and stay still until the test is complete.'
}
$requests=foreach($e in $phrases.GetEnumerator()){@{text=$e.Value;output_file=(Join-Path $root "assets/audio/$($e.Key)")}|ConvertTo-Json -Compress}
$requests|& (Join-Path $VoiceRoot 'runtime/piper/piper.exe') --model $model --json-input --quiet
if($LASTEXITCODE -ne 0){throw 'Local speech generation failed'}
$p=Join-Path $root 'assets/audio/provenance.json';$v=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json -AsHashtable
foreach($e in $phrases.GetEnumerator()){$v.texts[$e.Key]=$e.Value}
$v.files=@(Get-ChildItem -LiteralPath (Join-Path $root 'assets/audio') -Filter '*.wav'|ForEach-Object{@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$v|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding utf8
