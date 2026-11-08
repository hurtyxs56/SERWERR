param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -ne 5) { throw 'This check must use Windows PowerShell 5.1' }
$scripts = @(Get-ChildItem -LiteralPath $Directory -Filter '*.ps1')
if ($scripts.Count -eq 0) { throw 'No scripts found' }
foreach ($script in $scripts) {
 $bytes = [IO.File]::ReadAllBytes($script.FullName)
 if ($bytes.Length -lt 3 -or $bytes[0] -ne 239 -or $bytes[1] -ne 187 -or $bytes[2] -ne 191) { throw ('Missing UTF-8 BOM: ' + $script.Name) }
 $tokens = $null
 $parseErrors = $null
 $null = [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
 if ($parseErrors.Count -gt 0) { throw ($script.Name + ': ' + ($parseErrors.Message -join '; ')) }
 Write-Output ('PASS: UTF-8 BOM and PowerShell 5.1 syntax: ' + $script.Name)
}
# Parse only. Never execute server scripts or start the game in this check.
