param([Parameter(Mandatory=$true)][string]$GameDir,[string]$Config=(Join-Path $PSScriptRoot 'autohost.json'))
$ErrorActionPreference='Stop'
$GameDir=(Resolve-Path -LiteralPath $GameDir).Path
$Config=(Resolve-Path -LiteralPath $Config).Path
$script=Join-Path $PSScriptRoot 'Start-AutoHost.ps1'
foreach($path in @($GameDir,$Config,$script)){if($path.Contains('"')){throw 'Niedozwolony znak w ścieżce.'}}
$user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ('-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "'+$script+'" -GameDir "'+$GameDir+'" -Config "'+$Config+'"')
$trigger=New-ScheduledTaskTrigger -AtLogOn -User $user
$trigger.Delay='PT60S'
$principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
# Intentionally no -Force: an existing task must not be silently replaced.
Register-ScheduledTask -TaskName 'StellarDrive AutoHost' -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Write-Host 'Zadanie startuje po logowaniu użytkownika (opóźnienie 60 s). Steam musi być uruchomiony i zalogowany w tej sesji. Nie jest to usługa działająca przed logowaniem.'
