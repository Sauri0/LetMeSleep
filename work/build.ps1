param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath = Join-Path $projectRoot 'game'
$versionLine = Select-String -LiteralPath (Join-Path $gamePath 'project.godot') -Pattern '^config/version="([0-9]+\.[0-9]+\.[0-9]+)"$'
if (-not $versionLine) { throw 'Project version missing or invalid' }
$buildVersion = $versionLine.Matches[0].Groups[1].Value
$outDir = Join-Path $projectRoot ('outputs/Let-me-sleep-' + $buildVersion + '-Windows')
[System.IO.Directory]::CreateDirectory($outDir) | Out-Null
if (Test-Path -LiteralPath (Join-Path $projectRoot 'distribution')) {
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'distribution') -File | Copy-Item -Destination $outDir
}
& $godotExe --headless --path $gamePath --editor --import --quit
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed' }
foreach ($entryPoint in @('scripts/main','scripts/client','tests/network_bot','tests/practice_ui_checks','tests/gameplay_demo','tests/gameplay06_demo','tests/camera_turn_checks','tests/hud_input06_checks','tests/hosting_checks')) {
    & $godotExe --headless --path $gamePath --check-only --script "res://$entryPoint.gd"
    if ($LASTEXITCODE -ne 0) { throw "$entryPoint parse failed" }
}
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
    foreach ($testName in @('maps_test','route_tests','locomotion_test','focus_combat_test','manual_defense_test','stun_help_test','online_packet_codec_test','task_deadline_test','practice_test','invitation_test','network_order_test','network_connection_test','network_privacy_audit_test','contact_orientation06_test','house06_checks','music06_test','preferences_migration_test')) {
        & $godotExe --headless --path $gamePath --script "res://tests/$testName.gd"
        if ($LASTEXITCODE -ne 0) { throw "$testName failed" }
    }
    # Skin baking requires a rendering backend; Godot's headless dummy backend
    # cannot register the skeleton used by this actual-deformed-mesh test.
    # UI checks also need real mouse capture, unavailable in the dummy backend.
    foreach ($nativeTest in @('v06_character_rig_checks','ui_navigation_test')) {
    $rigLog = Join-Path $PSScriptRoot ('build-' + $nativeTest + '.log')
    $rigError = Join-Path $PSScriptRoot ('build-' + $nativeTest + '.err')
    $rig = Start-Process -FilePath $godotExe -ArgumentList @('--path', ('"' + $gamePath + '"'), '--script', ('res://tests/' + $nativeTest + '.gd')) -WindowStyle Hidden -PassThru -RedirectStandardOutput $rigLog -RedirectStandardError $rigError
    try {
        if (-not $rig.WaitForExit(55000)) { throw ('Native check timed out: ' + $nativeTest) }
        $rig.Refresh()
        if ($rig.ExitCode -ne 0 -or (Get-Item -LiteralPath $rigError).Length -gt 0) { throw ('Native check failed: ' + $nativeTest + '; inspect work/build logs') }
        Get-Content -LiteralPath $rigLog -Tail 1
    } finally {
        if (-not $rig.HasExited) { Stop-Process -Id $rig.Id -Force }
    }
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
foreach ($audioNotice in @('CREDITS.txt','LICENSE-VSCO2CE-CC0.txt')) {
    $noticeSource = Join-Path (Join-Path $gamePath 'assets/audio') $audioNotice
    $noticeTarget = Join-Path (Join-Path $outDir 'Licencias-audio') $audioNotice
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($noticeTarget)) | Out-Null
    Copy-Item -LiteralPath $noticeSource -Destination $noticeTarget
}
Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256
