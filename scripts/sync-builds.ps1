[CmdletBinding()]
param(
    [switch]$Publish,
    [string]$CommitMessage,
    [string]$IncomingBranch = 'codex/tracking-pipeline-efficiency'
)
# Fetch/report by default. Publish only the two named branches, never main.
$ErrorActionPreference = 'Stop'
$labRoot = Split-Path -Parent $PSScriptRoot
function Git([string]$Root, [string[]]$Arguments) {
    $output = @(& git.exe -C $Root @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments[0]) failed ($LASTEXITCODE). No reset or force-push was attempted." }
    return $output
}
function Clean([string]$Root) {
    if (@(Git $Root @('status','--porcelain')).Count) { throw "Uncommitted files in $Root. Review and commit them explicitly first." }
}
$branch = (Git $labRoot @('branch','--show-current')) -join ''
if ($branch -ne 'codex/community-build') { throw 'Run this from the codex/community-build worktree.' }
$gitDir = (Git $labRoot @('rev-parse','--absolute-git-dir')) -join ''
$common = (Git $labRoot @('rev-parse','--path-format=absolute','--git-common-dir')) -join ''
if ([IO.Path]::GetFullPath($gitDir) -eq [IO.Path]::GetFullPath($common)) { throw 'A separate community worktree is required.' }
Git $labRoot @('check-ref-format','--branch',$IncomingBranch) | Out-Null
$localRoot = $null; $candidate = $null
foreach ($line in (Git $labRoot @('worktree','list','--porcelain'))) {
    if ($line.StartsWith('worktree ')) { $candidate = $line.Substring(9) }
    if ($line -eq 'branch refs/heads/codex/local-development') { $localRoot = $candidate }
}
if (!$localRoot) { throw 'Cannot find the separate local-development checkout.' }
Git $labRoot @('fetch','--no-tags','origin') | Out-Host
$incoming = 'refs/remotes/origin/' + $IncomingBranch
Git $labRoot @('rev-parse','--verify',($incoming+'^{commit}')) | Out-Null
Write-Host "Incoming changes from $IncomingBranch (+ not applied, - equivalent patch already present):"
Git $labRoot @('cherry','-v','HEAD',$incoming) | Out-Host
if (!$Publish) {
    if ($CommitMessage) { throw 'CommitMessage requires -Publish; nothing committed.' }
    Write-Host 'Status only. No merge, commit, build, camera operation or push. Use -Publish after review.'
    return
}
Clean $localRoot
if ($CommitMessage) {
    if (@(Git $labRoot @('diff','--name-only')).Count -or @(Git $labRoot @('ls-files','--others','--exclude-standard')).Count) {
        throw 'Stage only the reviewed changes; unstaged or untracked files remain. Nothing committed.'
    }
    $staged = @(Git $labRoot @('diff','--cached','--name-only','--diff-filter=ACDMRT'))
    if (!$staged.Count) { throw 'No staged changes to commit.' }
    foreach ($path in $staged) {
        if ($path -notmatch '^(src/|tests/|scripts/|docs/|README\.md$|AGENTS\.md$|\.gitignore$|LICENSE$|THIRD_PARTY_NOTICES\.md$)' -or
            $path -match '(?i)(^|/)(bin|obj|artifacts|diagnostics|captures|recordings|preserved-maps|logs|\.rea-tools)/|\.local\.|\.(mp4|avi|pcapng?|wav|zip|exe|dll|key|pem)$|(^|/)\.env|NEXT-SESSION') {
            throw "Refusing to auto-commit private/generated or unsupported path: $path"
        }
    }
    Git $labRoot @('commit','-m',$CommitMessage) | Out-Host
}
Clean $labRoot
$communityExe = Join-Path $labRoot 'artifacts/review-lab/bin/LINK360.exe'
if (Get-Process LINK360 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $communityExe }) { throw 'Close the community app before building and publishing.' }
$expectedCommunity = (Git $labRoot @('rev-parse','refs/heads/codex/community-build')) -join ''
$expectedLocal = (Git $labRoot @('rev-parse','refs/heads/codex/local-development')) -join ''
& (Join-Path $PSScriptRoot 'test-review-lab.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Community checks failed; nothing pushed.' }
Clean $labRoot; Clean $localRoot
if (((Git $labRoot @('rev-parse','refs/heads/codex/community-build')) -join '') -ne $expectedCommunity -or
    ((Git $labRoot @('rev-parse','refs/heads/codex/local-development')) -join '') -ne $expectedLocal) { throw 'A branch changed during testing; publish stopped.' }
# Atomic push: both refs update or neither does. Non-fast-forward changes stop it.
Git $labRoot @('push','--atomic','origin',($expectedLocal+':refs/heads/codex/local-development'),($expectedCommunity+':refs/heads/codex/community-build')) | Out-Host
Write-Host 'Published both separate source branches. main and private settings were not updated.'
