param([string]$DataDir=$PSScriptRoot)
$ErrorActionPreference='Stop'
$DataDir=(Resolve-Path -LiteralPath $DataDir).Path
$status=Get-Content -LiteralPath (Join-Path $DataDir 'status.json') -Raw | ConvertFrom-Json
if ($status.phase -eq 'stopped' -or $status.phase -eq 'failed') { throw 'Serwer jest już zatrzymany lub zgłosił błąd.' }
$process=Get-Process -Id $status.pid -ErrorAction Stop
if ($process.ProcessName -ne 'StellarDrive') { throw 'PID nie należy do StellarDrive.' }
if (((Get-Date).ToUniversalTime()-([DateTime]$status.utc).ToUniversalTime()).TotalSeconds -gt 180) { throw 'Status serwera jest nieaktualny. Nie wysłano polecenia.' }
Set-Content -LiteralPath (Join-Path $DataDir 'supervisor.stop') -Value 'planned stop' -Encoding UTF8
Add-Content -LiteralPath (Join-Path $DataDir 'commands.txt') -Value 'stop' -Encoding UTF8
Write-Host 'Wysłano zapis i zatrzymanie. Poczekaj na phase=stopped w status.json. Nie zamykaj procesu siłą.'
