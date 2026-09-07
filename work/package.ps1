$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputsDir = Join-Path $projectRoot 'outputs'
$packageDir = Join-Path $outputsDir 'Dejame-dormir-0.2.0-Windows'
$zipPath = Join-Path $outputsDir 'Dejame-dormir-0.2.0-Windows.zip'
if (Test-Path -LiteralPath $zipPath) {
    $zipPath = Join-Path $outputsDir ('Dejame-dormir-0.2.0-Windows-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
}
[System.IO.Compression.ZipFile]::CreateFromDirectory($packageDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
[System.IO.File]::WriteAllText($zipPath + '.sha256.txt', $hash.Hash + '  ' + [System.IO.Path]::GetFileName($zipPath) + "`n")
Get-Item -LiteralPath $zipPath | Select-Object FullName,Length
