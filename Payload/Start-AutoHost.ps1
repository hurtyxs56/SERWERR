param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [string]$Config = (Join-Path $PSScriptRoot 'autohost.json'),
 [int]$MaxRestarts = 3,
 [int]$WindowMinutes = 15
)
$ErrorActionPreference = 'Stop'
if ($MaxRestarts -lt 0 -or $MaxRestarts -gt 10 -or $WindowMinutes -lt 1) { throw 'Nieprawidłowy limit restartów.' }
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$Config = (Resolve-Path -LiteralPath $Config).Path
$exe = Join-Path $GameDir 'StellarDrive.exe'
$data = Split-Path -Parent $Config
$stopFlag = Join-Path $data 'supervisor.stop'
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if (-not $cfg.AutoHost) { throw 'AutoHost jest wyłączony w konfiguracji.' }
foreach ($required in @($exe,(Join-Path $GameDir 'Mods\StellarDrive.AutoHost.dll'),(Join-Path $GameDir 'MelonLoader\net35\MelonLoader.dll'),$cfg.WorldPath)) {
 if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Brak pliku: $required" }
}
$hash = (Get-FileHash -LiteralPath (Join-Path $GameDir 'StellarDrive_Data\Managed\Assembly-CSharp.dll') -Algorithm SHA256).Hash
if ($hash -ne '25D3F704C52EF689EA04E1EDDDCDB014914D8CBCE071F88871066A6B1375715B') { throw 'Inna wersja gry. Wymagane Demo 0.8.2.' }
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Najpierw uruchom Steam w tej samej sesji Windows i zaloguj konto z Demo.' }
if (Get-Process StellarDrive -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) { throw 'Ta kopia gry jest już uruchomiona.' }
$appIdFile = Join-Path $GameDir 'steam_appid.txt'
if (Test-Path -LiteralPath $appIdFile) {
 if ((Get-Content -LiteralPath $appIdFile -Raw).Trim() -ne '4689730') { throw 'steam_appid.txt wskazuje inny produkt. Nie zmieniono go.' }
} else {
 # Official Demo AppID for direct executable launch; native Steam entitlement checks remain enabled.
 [IO.File]::WriteAllText($appIdFile,'4689730',[Text.Encoding]::ASCII)
}
# Refuse stale stop requests instead of silently deleting administrator intent.
if (Test-Path -LiteralPath $stopFlag) { throw "Pozostał znacznik planowanego zatrzymania: $stopFlag. Przenieś go do archiwum przed ponownym uruchomieniem." }
$commands = Join-Path $data 'commands.txt'
if ((Test-Path -LiteralPath $commands) -and (Get-Item -LiteralPath $commands).Length -gt 0) { throw 'Oczekujące commands.txt — sprawdź polecenia przed startem.' }
$supervisorLock = [IO.File]::Open((Join-Path $data 'supervisor.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$failures = New-Object 'System.Collections.Generic.List[DateTime]'
try {
 while ($true) {
  if (Test-Path -LiteralPath $stopFlag) { break }
  $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $log = Join-Path $data "player-$stamp.log"
  # All interpolated arguments are paths passed to a process, not evaluated shell commands.
  if ($Config.Contains('"') -or $log.Contains('"')) { throw 'Niedozwolony znak w ścieżce.' }
  $arguments = '-batchmode -nographics --melonloader.hideconsole --melonloader.disablestartscreen --autohost-config="' + $Config + '" -logFile "' + $log + '"'
  $process = Start-Process -FilePath $exe -WorkingDirectory $GameDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
  Write-Host "AutoHost PID $($process.Id), log $log"
  $process.WaitForExit()
  $process.Refresh()
  if ($process.ExitCode -eq 0 -or (Test-Path -LiteralPath $stopFlag)) { break }
  # Exit code 2 is a configuration/startup failure; restarting cannot fix a bad hash or missing Steam.
  if ($process.ExitCode -eq 2) { throw 'Błąd startu AutoHost. Sprawdź status.json i log; restart wstrzymany.' }
  $now = Get-Date
  $failures.Add($now)
  for ($i=$failures.Count-1; $i -ge 0; $i--) { if (($now-$failures[$i]).TotalMinutes -gt $WindowMinutes) { $failures.RemoveAt($i) } }
  if ($failures.Count -gt $MaxRestarts) { throw "Przekroczony limit $MaxRestarts restartów w $WindowMinutes minut. Sprawdź logi." }
  $delay = [Math]::Min(60,10*[Math]::Pow(2,$failures.Count-1))
  Write-Warning "Proces zakończył się kodem $($process.ExitCode). Ponowny start za $delay s."
  Start-Sleep -Seconds $delay
 }
} finally { $supervisorLock.Dispose() }
