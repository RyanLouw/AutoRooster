$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet restore SchoolExamScheduler.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
dotnet build SchoolExamScheduler.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet test SchoolExamScheduler.sln -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
dotnet publish src/SchoolExamScheduler.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
New-Item -ItemType Directory -Force artifacts/win-x64/prerequisites | Out-Null
$runtime = Join-Path (Get-Location) 'artifacts/win-x64/prerequisites/vc_redist.x64.exe'
Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $runtime
$signature = Get-AuthenticodeSignature $runtime
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
    Remove-Item $runtime
    throw 'Microsoft Visual C++ redistributable signature validation failed'
}
Copy-Item scripts/Install-VisualCppRuntime.ps1 artifacts/win-x64/Install-VisualCppRuntime.ps1
Write-Output 'Published artifacts/win-x64. Keep the complete folder together.'
