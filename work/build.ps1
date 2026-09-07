param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath = Join-Path $projectRoot 'game'
$outDir = Join-Path $projectRoot 'outputs/Let-me-sleep-0.3.0-Windows'
[System.IO.Directory]::CreateDirectory($outDir) | Out-Null
if (Test-Path -LiteralPath (Join-Path $projectRoot 'distribution')) {
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'distribution') -File | Copy-Item -Destination $outDir
}
& $godotExe --headless --path $gamePath --editor --import --quit
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed' }
if (-not $SkipTests) {
    & $godotExe --headless --path $gamePath --script res://tests/rules_test.gd
    if ($LASTEXITCODE -ne 0) { throw 'Rules tests failed' }
    & $godotExe --headless --path $gamePath --script res://tests/lobby_rules_test.gd
    if ($LASTEXITCODE -ne 0) { throw 'Lobby rules tests failed' }
    & $godotExe --headless --path $gamePath --script res://tests/cosmetics_test.gd
    if ($LASTEXITCODE -ne 0) { throw 'Cosmetics tests failed' }
    & $godotExe --headless --path $gamePath --script res://tests/visual_checks.gd
    if ($LASTEXITCODE -ne 0) { throw 'Visual geometry tests failed' }
    & $godotExe --headless --path $gamePath --script res://tests/audio_checks.gd
    if ($LASTEXITCODE -ne 0) { throw 'Audio tests failed' }
    foreach ($testName in @('maps_test','locomotion_test','practice_test','invitation_test','network_order_test','ui_navigation_test','preferences_migration_test')) {
        & $godotExe --headless --path $gamePath --script "res://tests/$testName.gd"
        if ($LASTEXITCODE -ne 0) { throw "$testName failed" }
    }
}
& $godotExe --headless --path $gamePath --export-release 'Windows Desktop' (Join-Path $outDir 'Let-me-sleep.exe')
if ($LASTEXITCODE -ne 0) { throw 'Windows export failed' }
$exe = Get-Item -LiteralPath (Join-Path $outDir 'Let-me-sleep.exe')
if ($exe.Length -lt 1000000) { throw 'Exported executable incomplete' }
$fontRoot = Join-Path $gamePath 'assets/fonts'
foreach ($fontNotice in Get-ChildItem -LiteralPath $fontRoot -Recurse -File | Where-Object { $_.Extension -in @('.md','.txt') }) {
    $noticeRelative = [System.IO.Path]::GetRelativePath($fontRoot, $fontNotice.FullName)
    $noticeTarget = Join-Path (Join-Path $outDir 'Licencias-fuentes') $noticeRelative
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($noticeTarget)) | Out-Null
    Copy-Item -LiteralPath $fontNotice.FullName -Destination $noticeTarget
}
Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256
