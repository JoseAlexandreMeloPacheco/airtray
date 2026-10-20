param([switch]$Verify)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet build ./AirTray/AirTray.csproj -c Release -o ./artifacts/airtray
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close AirTray if its executable is in use.' }
    if ($Verify) {
        dotnet ./artifacts/airtray/AirTray.dll --self-test-protocol
        if ($LASTEXITCODE -ne 0) { throw 'Protocol regression tests failed.' }
        dotnet ./artifacts/airtray/AirTray.dll --self-test-ui ./artifacts/functional-ui
        if ($LASTEXITCODE -ne 0) { throw 'UI regression tests failed.' }
    }
} finally { Pop-Location }
