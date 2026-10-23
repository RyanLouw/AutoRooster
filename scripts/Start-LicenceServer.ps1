param([switch]$Setup)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:Licensing__DataPath = Join-Path (Get-Location) 'licence-data'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
if ($Setup) {
    dotnet run --project src/SchoolExamScheduler.LicenceServer --no-launch-profile -- --setup
} else {
    if (!(Test-Path (Join-Path $env:Licensing__DataPath 'server-settings.json'))) {
        throw 'Run ./scripts/Start-LicenceServer.ps1 -Setup first.'
    }
    dotnet run --project src/SchoolExamScheduler.LicenceServer --no-launch-profile -- --urls http://localhost:5080
}
if ($LASTEXITCODE -ne 0) { throw 'Licence server command failed.' }
