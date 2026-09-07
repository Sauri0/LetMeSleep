param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath = Join-Path $projectRoot 'game'
$outDir = Join-Path $projectRoot 'outputs/Dejame-dormir-0.1.0-Windows'
[System.IO.Directory]::CreateDirectory($outDir) | Out-Null
& $godotExe --headless --path $gamePath --editor --import --quit
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed' }
if (-not $SkipTests) {
    & $godotExe --headless --path $gamePath --script res://tests/rules_test.gd
    if ($LASTEXITCODE -ne 0) { throw 'Rules tests failed' }
}
& $godotExe --headless --path $gamePath --export-release 'Windows Desktop' (Join-Path $outDir 'Dejame-dormir.exe')
if ($LASTEXITCODE -ne 0) { throw 'Windows export failed' }
$exe = Get-Item -LiteralPath (Join-Path $outDir 'Dejame-dormir.exe')
if ($exe.Length -lt 1000000) { throw 'Exported executable incomplete' }
Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256
