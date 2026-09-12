$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputsDir = Join-Path $projectRoot 'outputs'
$versionLine = Select-String -LiteralPath (Join-Path $projectRoot 'game/project.godot') -Pattern '^config/version="([0-9]+\.[0-9]+\.[0-9]+(?:-(?:alfa|beta|omega|delta|gamma))?)"$'
if (-not $versionLine) { throw 'Project version missing or invalid' }
$packageVersion = $versionLine.Matches[0].Groups[1].Value
$packageName = 'Let-me-sleep-' + $packageVersion + '-Windows'
$packageDir = Join-Path $outputsDir $packageName
$zipPath = Join-Path $outputsDir ($packageName + '.zip')
if (Test-Path -LiteralPath $zipPath) {
    throw 'The versioned ZIP already exists; inspect it before packaging again.'
}
. (Join-Path $PSScriptRoot 'package-metadata.ps1')
Test-PackageMetadata -ProjectRoot $projectRoot -OutputDirectory $packageDir -Version $packageVersion
[System.IO.Compression.ZipFile]::CreateFromDirectory($packageDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
[System.IO.File]::WriteAllText($zipPath + '.sha256.txt', $hash.Hash + '  ' + [System.IO.Path]::GetFileName($zipPath) + "`n")
Get-Item -LiteralPath $zipPath | Select-Object FullName,Length
