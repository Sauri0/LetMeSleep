param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath = Join-Path $projectRoot 'game'
$buildWorkRoot = $PSScriptRoot
function Invoke-CheckedHeadless {
    param([string]$CheckName, [string[]]$GameArguments)
    $errorLog = Join-Path $buildWorkRoot ('build-' + $CheckName + '.err')
    $savedPreference = $ErrorActionPreference
    try {
        # A Godot script can log an error and still return zero. Inspect stderr
        # as well as its exit code before allowing a distributable export.
        $ErrorActionPreference = 'Continue'
        & $godotExe --headless --path $gamePath @GameArguments 2> $errorLog
        $nativeExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $savedPreference
    }
    if ($nativeExitCode -ne 0 -or (Get-Item -LiteralPath $errorLog).Length -gt 0) {
        Get-Content -LiteralPath $errorLog -TotalCount 12
        throw ('Godot check failed: ' + $CheckName + ', exit ' + $nativeExitCode)
    }
}
$versionLine = Select-String -LiteralPath (Join-Path $gamePath 'project.godot') -Pattern '^config/version="([0-9]+\.[0-9]+\.[0-9]+)"$'
if (-not $versionLine) { throw 'Project version missing or invalid' }
$buildVersion = $versionLine.Matches[0].Groups[1].Value
$outDir = Join-Path $projectRoot ('outputs/Let-me-sleep-' + $buildVersion + '-Windows')
[System.IO.Directory]::CreateDirectory($outDir) | Out-Null
if (Test-Path -LiteralPath (Join-Path $projectRoot 'distribution')) {
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'distribution') -File | Copy-Item -Destination $outDir
}
Invoke-CheckedHeadless -CheckName 'import' -GameArguments @('--editor','--import','--quit')
foreach ($entryPoint in @('scripts/main','scripts/client','tests/network_bot','tests/practice_ui_checks','tests/gameplay_demo','tests/gameplay06_demo','tests/camera_turn_checks','tests/hud_input06_checks','tests/hosting_checks','tests/gameplay07_demo','tests/performance07_live','tests/network07_combat_checks','tests/video07_checks','tests/doors07_client_checks')) {
    Invoke-CheckedHeadless -CheckName ('parse-' + $entryPoint.Replace('/','-')) -GameArguments @('--check-only','--script',"res://$entryPoint.gd")
}
if (-not $SkipTests) {
    foreach ($initialTest in @('rules_test','lobby_rules_test','cosmetics_test','visual_checks','audio_checks','house07_lighting_test')) {
        Invoke-CheckedHeadless -CheckName $initialTest -GameArguments @('--script',"res://tests/$initialTest.gd")
    }
    foreach ($testName in @('maps_test','route_tests','locomotion_test','focus_combat_test','manual_defense_test','stun_help_test','online_packet_codec_test','task_deadline_test','practice_test','invitation_test','network_order_test','network_connection_test','network_privacy_audit_test','contact_orientation06_test','house06_checks','music06_test','preferences_migration_test','doors07_test','door_navigation07_test','attack07_test','arena_spatial07_test','pose_cache07_test','barriers07_test','v07_character_motion_checks','v07_character_cache_checks','video_preferences07_test','bot_scheduler07_test','network07_combat_checks','house07_joints_test','v07_facial_state_checks','mosquito_impact07_test')) {
        Invoke-CheckedHeadless -CheckName $testName -GameArguments @('--script',"res://tests/$testName.gd")
    }
    # Skin baking requires a rendering backend; Godot's headless dummy backend
    # cannot register the skeleton used by this actual-deformed-mesh test.
    # UI checks also need real mouse capture, unavailable in the dummy backend.
    foreach ($nativeTest in @('v07_character_rig_checks','v07_character_client_checks','selected07_mesh_checks','selected07_actor_checks','selected07_facial_envelope_checks','house07_checks','house07_lighting_probe','ui_navigation_test','video07_checks','doors07_client_checks')) {
    $rigLog = Join-Path $PSScriptRoot ('build-' + $nativeTest + '.log')
    $rigError = Join-Path $PSScriptRoot ('build-' + $nativeTest + '.err')
    $nativeArguments = @('--path', ('"' + $gamePath + '"'), '--script', ('res://tests/' + $nativeTest + '.gd'))
    if ($nativeTest -eq 'selected07_mesh_checks') { $nativeArguments += @('--','--production','--verify') }
    if ($nativeTest -eq 'house07_lighting_probe') { $nativeArguments += @('--','--production','--candidate-only','--energy=0.55','--atlas=2048') }
    $rig = Start-Process -FilePath $godotExe -ArgumentList $nativeArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $rigLog -RedirectStandardError $rigError
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
Invoke-CheckedHeadless -CheckName 'export' -GameArguments @('--export-release','Windows Desktop',(Join-Path $outDir 'Let-me-sleep.exe'))
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
