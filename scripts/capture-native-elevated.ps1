$ErrorActionPreference='Stop'
$taskWorkspace=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $taskWorkspace
$taskNode=(Get-Command node -ErrorAction Stop).Source
$taskStamp=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$env:UV_THREADPOOL_SIZE='16'
& $taskNode scripts/capture-native-controls.mjs 45 1> ".rea-tools/native-capture-$taskStamp-status.txt" 2> ".rea-tools/native-capture-$taskStamp-errors.txt"
