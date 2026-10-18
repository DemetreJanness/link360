[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$labRoot = Split-Path -Parent $PSScriptRoot
$previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$previousFirstRun = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$previousNoLogo = $env:DOTNET_NOLOGO
$liveCommunitySettings = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'LINK360-Community/settings.local.json'
$liveCommunityHashBefore = if (Test-Path -LiteralPath $liveCommunitySettings) { (Get-FileHash -LiteralPath $liveCommunitySettings).Hash } else { $null }
Push-Location -LiteralPath $labRoot
try {
    $gitDir = & git rev-parse --absolute-git-dir
    if ($LASTEXITCODE -ne 0) { throw 'This command requires the review worktree.' }
    $commonDir = & git rev-parse --path-format=absolute --git-common-dir
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the Git worktree.' }
    if ([IO.Path]::GetFullPath($gitDir) -eq [IO.Path]::GetFullPath($commonDir)) {
        throw 'Run this command in the separate review worktree, not the primary checkout.'
    }

    $artifactRoot = Join-Path $labRoot 'artifacts/review-lab'
    $runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
    $reportDir = Join-Path $artifactRoot $runId
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    $runLog = Join-Path $reportDir 'checks.log'
    $restoreConfig = Join-Path $artifactRoot 'NuGet.offline.config'
    '<configuration><packageSources><clear /></packageSources></configuration>' |
        Set-Content -LiteralPath $restoreConfig -Encoding UTF8
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'

    function Invoke-Check {
        param([string]$Command, [string[]]$Arguments)
        & $Command @Arguments 2>&1 | Tee-Object -FilePath $runLog -Append
        if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE. See $runLog" }
    }

    Invoke-Check 'dotnet' @('restore','tests/Link360.Tests.csproj','--configfile',$restoreConfig,'-p:NuGetAudit=false')
    Invoke-Check 'dotnet' @('build','tests/Link360.Tests.csproj','-c','Release','--no-restore','-o',(Join-Path $artifactRoot 'bin'))
    Invoke-Check (Join-Path $artifactRoot 'bin/LINK360.Tests.exe') @()
    Invoke-Check (Join-Path $artifactRoot 'bin/LINK360.Tests.exe') @('--shared-pipeline-test',(Join-Path $reportDir 'shared-pipeline'))
    $revision = & git rev-parse HEAD
    $branch = & git branch --show-current
    $dirty = @(& git status --porcelain).Count -gt 0
    [ordered]@{ status='passed'; revision=$revision; branch=$branch; workingTreeModified=$dirty; finishedUtc=[DateTime]::UtcNow.ToString('o'); scope='Build and default fixtures only; no application launch or hardware probes' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDir 'result.json') -Encoding UTF8
    Write-Host "Review checks passed. Local report: $reportDir"
}
finally {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousFirstRun
    $env:DOTNET_NOLOGO = $previousNoLogo
    Pop-Location
    $liveCommunityHashAfter = if (Test-Path -LiteralPath $liveCommunitySettings) { (Get-FileHash -LiteralPath $liveCommunitySettings).Hash } else { $null }
    if ($liveCommunityHashBefore -ne $liveCommunityHashAfter) { throw 'Live community settings changed during tests. Test fixtures must use isolated settings files.' }
}
