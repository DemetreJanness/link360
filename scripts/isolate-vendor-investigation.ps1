$ErrorActionPreference='Stop'
$taskWorkspace=Split-Path $PSScriptRoot -Parent
$taskResult=Join-Path $taskWorkspace '.rea-tools/vendor-isolation-result.json'
try {
 $taskTargets=@('C:\Program Files\Insta360 Link Controller\Insta360 Link Controller.exe','C:\Program Files\Insta360 Link Controller\crashpad_handler.exe')
 $taskRules=@()
 foreach($taskTarget in $taskTargets){
  if(!(Test-Path -LiteralPath $taskTarget -PathType Leaf)){throw "Missing target: $taskTarget"}
  $taskName='LINK360 local investigation - '+[IO.Path]::GetFileNameWithoutExtension($taskTarget)
  $taskRule=Get-NetFirewallRule -DisplayName $taskName -ErrorAction SilentlyContinue
  if(!$taskRule){$taskRule=New-NetFirewallRule -DisplayName $taskName -Group 'LINK360 local investigation' -Direction Outbound -Program $taskTarget -Action Block -Enabled True -Profile Any}
  $taskPrograms=@($taskRule | Get-NetFirewallApplicationFilter)
  if($taskRule.Action -ne 'Block' -or $taskRule.Direction -ne 'Outbound' -or $taskRule.Enabled -ne 'True' -or $taskPrograms.Program -notcontains $taskTarget){throw "Scoped rule failed verification: $taskName"}
  $taskRules+=@{Name=$taskName;Program=$taskTarget;Action='Block'}
 }
 $taskProfiles=@(Get-NetFirewallProfile | Select-Object Name,Enabled)
 if(@($taskProfiles | Where-Object {!$_.Enabled}).Count -gt 0){throw 'A Windows Firewall profile is disabled; vendor network isolation cannot be assumed.'}
 @{Success=$true;Rules=$taskRules;Profiles=$taskProfiles} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $taskResult
}catch { @{Success=$false;Error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath $taskResult;exit 1 }
