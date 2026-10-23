param(
    [Parameter(Mandatory=$true)][uri]$ServerUrl,
    [switch]$LocalDevelopment,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if ($ServerUrl.AbsolutePath -ne '/' -or $ServerUrl.Query -or $ServerUrl.Fragment -or $ServerUrl.UserInfo) {
    throw 'Use the server origin, for example https://licences.example.co.za, without a path or credentials.'
}
if ($ServerUrl.Scheme -ne 'https' -and !($LocalDevelopment -and $ServerUrl.Scheme -eq 'http' -and $ServerUrl.IsLoopback)) {
    throw 'HTTPS is required. HTTP is allowed only for loopback development.'
}
if (!$OutputPath) {
    $name = if ($LocalDevelopment) { 'licensing.local.json' } else { 'licensing.json' }
    $OutputPath = Join-Path 'src/SchoolExamScheduler.Desktop' $name
}
if ($LocalDevelopment -and (Split-Path $OutputPath -Leaf) -ne 'licensing.local.json') {
    throw 'Local development configuration must be named licensing.local.json so it cannot be published.'
}
$response = Invoke-RestMethod -Uri ([uri]::new($ServerUrl, '/.well-known/licence-public-key'))
if (!$response.publicKeyPem -or $response.publicKeyPem -notmatch 'BEGIN PUBLIC KEY') { throw 'The server did not return a public signing key.' }
@{
    ServerUrl = $ServerUrl.GetLeftPart([System.UriPartial]::Authority)
    PublicKeyPem = $response.publicKeyPem
    AllowInsecureLoopback = [bool]$LocalDevelopment
} | ConvertTo-Json | Set-Content -Encoding utf8 $OutputPath
Write-Output "Client configuration written to $OutputPath. Rebuild the desktop app. No administrator credentials were copied."
