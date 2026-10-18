$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot '../src/desktop/Link360.Desktop.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'LINK360 build failed' }
$env:LINK360_OBS_DIR=Join-Path (Split-Path $PSScriptRoot) '../OBS'
$binary=Join-Path $PSScriptRoot '../src/desktop/bin/Release/net9.0-windows/LINK360.exe'
Start-Process -FilePath $binary -WindowStyle Normal
