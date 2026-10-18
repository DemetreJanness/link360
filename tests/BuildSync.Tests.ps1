# Exercises publishing against a local bare fixture. Never uses GitHub or cameras.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('artifacts/build-sync-test-' + [guid]::NewGuid().ToString('N'))
$primary = Join-Path $fixture 'primary'
$community = Join-Path $fixture 'community'
$remote = Join-Path $fixture 'remote.git'
New-Item -ItemType Directory -Path $fixture | Out-Null
function G([string]$At,[string[]]$A) {
    $out = @(& git -C $At @A)
    if ($LASTEXITCODE -ne 0) { throw "Fixture git failed: $($A[0])" }
    return $out
}
function Check([bool]$Value,[string]$Reason) { if (!$Value) { throw $Reason } }
function Sync([bool]$ExpectSuccess,[string[]]$A) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $community 'scripts/sync-builds.ps1') @A 2>&1
    $code = $LASTEXITCODE; $ErrorActionPreference = $old
    if (($code -eq 0) -ne $ExpectSuccess) { $out | Out-Host; throw "Unexpected sync result: $code" }
}
G $fixture @('init','--bare',$remote) | Out-Null
G $fixture @('init','-b','codex/local-development',$primary) | Out-Null
G $primary @('config','user.name','LINK360 fixture') | Out-Null
G $primary @('config','user.email','fixture@localhost') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $primary 'scripts'),(Join-Path $primary 'src') | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'scripts/sync-builds.ps1') -Destination (Join-Path $primary 'scripts/sync-builds.ps1')
'Write-Host "Fixture checks passed"; $global:LASTEXITCODE = 0' | Set-Content -LiteralPath (Join-Path $primary 'scripts/test-review-lab.ps1')
'baseline' | Set-Content -LiteralPath (Join-Path $primary 'src/sample.cs')
G $primary @('add','--','scripts','src') | Out-Null
G $primary @('commit','-m','Fixture baseline') | Out-Null
$baseline = (G $primary @('rev-parse','HEAD')) -join ''
G $primary @('remote','add','origin',$remote) | Out-Null
G $primary @('push','origin','HEAD:refs/heads/main','HEAD:refs/heads/codex/tracking-pipeline-efficiency') | Out-Null
G $primary @('worktree','add','-b','codex/community-build',$community) | Out-Null
Sync $true @()
Check (((G $primary @('rev-parse','HEAD')) -join '') -eq $baseline) 'Status changed primary'
Sync $true @('-Publish')
Check (((G $remote @('rev-parse','refs/heads/codex/community-build')) -join '') -eq $baseline) 'Publish missed community'
'dirty' | Set-Content -LiteralPath (Join-Path $primary 'dirty.txt')
Sync $false @('-Publish')
Remove-Item -LiteralPath (Join-Path $primary 'dirty.txt')
'fixture private data' | Set-Content -LiteralPath (Join-Path $community 'src/settings.local.json')
G $community @('add','--','src/settings.local.json') | Out-Null
Sync $false @('-Publish','-CommitMessage','Reject private file')
Check (((G $community @('rev-parse','HEAD')) -join '') -eq $baseline) 'Private path was committed'
G $community @('restore','--staged','--','src/settings.local.json') | Out-Null
Remove-Item -LiteralPath (Join-Path $community 'src/settings.local.json')
'reviewed change' | Set-Content -LiteralPath (Join-Path $community 'src/sample.cs')
G $community @('add','--','src/sample.cs') | Out-Null
Sync $true @('-Publish','-CommitMessage','Reviewed fixture change')
$published = (G $community @('rev-parse','HEAD')) -join ''
Check ($published -ne $baseline) 'Staged commit was not created'
Check (((G $remote @('rev-parse','refs/heads/codex/community-build')) -join '') -eq $published) 'New commit not published'
Check (((G $remote @('rev-parse','refs/heads/main')) -join '') -eq $baseline) 'main changed'
Check (((G $primary @('rev-parse','HEAD')) -join '') -eq $baseline) 'Local development changed'
$tree = (G $community @('rev-parse','HEAD^{tree}')) -join ''
$remoteAdvance = (G $community @('commit-tree',$tree,'-p',$published,'-m','Remote change')) -join ''
G $community @('push','origin',($remoteAdvance+':refs/heads/codex/community-build')) | Out-Null
'local divergent change' | Set-Content -LiteralPath (Join-Path $community 'src/sample.cs')
G $community @('add','--','src/sample.cs') | Out-Null
G $community @('commit','-m','Divergent fixture') | Out-Null
# Give the other branch a pending update to prove the push is atomic.
$localAdvance = (G $primary @('commit-tree',$tree,'-p',$baseline,'-m','Local fixture advance')) -join ''
G $primary @('update-ref','refs/heads/codex/local-development',$localAdvance) | Out-Null
G $primary @('restore','--source=HEAD','--staged','--worktree','--','src/sample.cs') | Out-Null
Sync $false @('-Publish')
Check (((G $remote @('rev-parse','refs/heads/codex/community-build')) -join '') -eq $remoteAdvance) 'Divergent remote overwritten'
Check (((G $remote @('rev-parse','refs/heads/codex/local-development')) -join '') -eq $baseline) 'Atomic rejection updated other branch'
Write-Host 'PASS: status-only, separate refs, staged-only commit, private/dirty rejection, main preservation and atomic non-fast-forward rejection'
Write-Host "Local fixture retained at $fixture"
