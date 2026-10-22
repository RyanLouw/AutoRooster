$ErrorActionPreference = 'Stop'
$runtime = Join-Path $PSScriptRoot 'prerequisites/vc_redist.x64.exe'
if (-not (Test-Path $runtime)) { throw 'The Microsoft redistributable is missing. Rebuild the complete package with scripts/Publish-Windows.ps1 while online.' }
$signature = Get-AuthenticodeSignature $runtime
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw 'Microsoft redistributable signature validation failed' }
$process = Start-Process -FilePath $runtime -ArgumentList '/install','/passive','/norestart' -Verb RunAs -Wait -PassThru
if ($process.ExitCode -notin @(0,1638,3010)) { throw "Visual C++ runtime installation failed: $($process.ExitCode)" }
Write-Output 'Microsoft Visual C++ x64 runtime is installed. A restart may be required when the installer reports code 3010.'
