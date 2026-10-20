$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if (!(Test-Path -LiteralPath '.git')) { throw 'Run git init first.' }
    git add -- .gitignore .gitattributes README.md README.en.md LICENSE SECURITY.md THIRD_PARTY.md 'Abrir AirTray.cmd' AirTray scripts docs
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage files.' }
    $files = @(git -c core.quotepath=false ls-files --cached)
    if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'Could not inspect the Git index.' }
    $rootFiles = @('.gitignore','.gitattributes','README.md','README.en.md','LICENSE','SECURITY.md','THIRD_PARTY.md','Abrir AirTray.cmd')
    foreach ($file in $files) {
        $allowed = $file -in $rootFiles -or $file -match '^AirTray/[^/]+\.(cs|xaml|csproj)$' -or $file -match '^AirTray/Assets/(air-states-transparent\.png|README\.md)$' -or $file -match '^scripts/(Build|Prepare-GitHub|Package-AirTray)\.ps1$' -or $file -match '^docs/(repository-description\.md|audit\.md|screenshots/control-(on|off)\.png)$'
        if (!$allowed) { throw "Unexpected staged file: $file" }
    }
    git diff --cached --check
    if ($LASTEXITCODE -ne 0) { throw 'Staged whitespace errors found.' }
    git diff --cached --stat
    Write-Host 'PASS: functional source allowlist checked. Review credentials separately before publishing. No commit or upload was made.'
} finally { Pop-Location }
