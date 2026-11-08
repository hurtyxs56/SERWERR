param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$Config)
$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path -Parent $Config
Start-Transcript -LiteralPath (Join-Path $serverRoot 'supervisor.log') -Append | Out-Null
try { & (Join-Path $PSScriptRoot 'Start-AutoHost.ps1') -GameDir $GameDir -Config $Config }
catch { Write-Host ('BLAD NADZORCY: ' + $_.Exception.Message); exit 1 }
finally { Stop-Transcript | Out-Null }
