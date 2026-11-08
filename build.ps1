param([string]$DotNet='dotnet')
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
 # Windows PowerShell 5.1 otherwise decodes UTF-8 scripts as the system ANSI codepage.
 $utf8Bom = New-Object System.Text.UTF8Encoding($true)
 foreach ($script in Get-ChildItem Payload/*.ps1) {
   [IO.File]::WriteAllText($script.FullName, [IO.File]::ReadAllText($script.FullName), $utf8Bom)
 }
 & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File scripts/Validate-PowerShell51.ps1 -Directory (Join-Path $PSScriptRoot 'Payload')
 if ($LASTEXITCODE -ne 0) { throw 'Windows PowerShell 5.1 syntax validation failed' }
 $evidence=Get-Content -LiteralPath 'Payload/evidence.json' -Raw | ConvertFrom-Json
 $hash=(Get-FileHash -LiteralPath 'Payload/StellarDrive.AutoHost.dll' -Algorithm SHA256).Hash
 if($hash -ne $evidence.dllSha256 -or $hash -ne '3B794EF574676FC4A71E79C21716A32D2A10F86D8932668244EBDCAE6D4BD19E'){throw 'Niezgodny DLL / evidence.'}
 if((Get-FileHash 'Payload/REPORT.md' -Algorithm SHA256).Hash -ne $evidence.reportSha256){throw 'Niezgodny raport moda.'}
 & $DotNet publish Manager/Manager.csproj -c Release -o artifacts/manager
 if($LASTEXITCODE -ne 0){throw 'Błąd budowania panelu.'}
 & $DotNet publish Setup/Setup.csproj -c Release -o artifacts/setup
 if($LASTEXITCODE -ne 0){throw 'Błąd budowania instalatora.'}
 New-Item -ItemType Directory -Path dist -Force | Out-Null
 Copy-Item artifacts/manager/StellarDrive-Server-Manager.exe dist/
 Copy-Item artifacts/setup/StellarDrive-Server-Setup.exe dist/
 Copy-Item README.md,CHANGELOG.md,LICENSE.txt,autohost.example.json dist/
 Copy-Item Payload/StellarDrive.AutoHost.dll,Payload/evidence.json,Payload/REPORT.md,Payload/TEST-NA-VPS.md dist/
 Get-ChildItem dist/* -File | Where-Object Name -NE 'checksums.sha256' | ForEach-Object {('{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$_.Name)} | Set-Content dist/checksums.sha256 -Encoding ascii
} finally {Pop-Location}
