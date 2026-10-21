$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & "$PSScriptRoot/Build.ps1" -Verify
    dotnet publish ./AirTray/AirTray.csproj -c Release -r win-x64 --self-contained false -o ./artifacts/release/airtray-windows-x64 -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $releaseDir = Join-Path $projectRoot 'artifacts/release/airtray-windows-x64'
    $documentation = @('README.md','README.en.md','LICENSE','SECURITY.md','THIRD_PARTY.md','docs/audit.md','docs/repository-description.md','docs/screenshots/control-off.png','docs/screenshots/control-on.png','AirTray/Assets/README.md')
    foreach ($relative in $documentation) {
        $destination = Join-Path $releaseDir $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $relative -Destination $destination -Force
    }
    $expected = @('AirTray.exe','AirTray.dll','AirTray.deps.json','AirTray.runtimeconfig.json') + $documentation
    $actual = @(Get-ChildItem -LiteralPath $releaseDir -File -Recurse | ForEach-Object { $_.FullName.Substring($releaseDir.Length+1).Replace('\','/') })
    if (@($actual | Where-Object { $_ -notin $expected }).Count -ne 0 -or @($expected | Where-Object { $_ -notin $actual }).Count -ne 0) { throw 'Release file list mismatch; inspect before packaging.' }
    Compress-Archive -Path (Join-Path $releaseDir '*') -DestinationPath ./artifacts/release/AirTray-0.3.1-windows-x64.zip -Force
    Get-FileHash ./artifacts/release/AirTray-0.3.1-windows-x64.zip -Algorithm SHA256
} finally { Pop-Location }
