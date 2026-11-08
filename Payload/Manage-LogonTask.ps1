param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$TaskName,[ValidateSet('Enable','Disable')][string]$Mode)
$ErrorActionPreference='Stop'
if($TaskName -notmatch '^StellarDrive AutoHost [A-F0-9]{12}$'){throw 'Nieprawidłowa nazwa zadania.'}
try {
 if($Mode -eq 'Disable') {if(Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue){Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false};exit 0}
 $GameDir=(Resolve-Path -LiteralPath $GameDir).Path
 $root=Join-Path $GameDir 'StellarDriveServer'
 $user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
 $action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ('-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "'+(Join-Path $root 'Supervisor-Entry.ps1')+'" -GameDir "'+$GameDir+'" -Config "'+(Join-Path $root 'autohost.json')+'"')
 $trigger=New-ScheduledTaskTrigger -AtLogOn -User $user
 $trigger.Delay='PT60S'
 $principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
 Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
 exit 0
} catch {Write-Error $_;exit 1}

