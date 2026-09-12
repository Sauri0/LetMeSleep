param([switch]$SkipTests, [switch]$SkipHeadlessTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath = Join-Path $projectRoot 'game'
$buildWorkRoot = $PSScriptRoot
. (Join-Path $PSScriptRoot 'online-package.ps1')
Initialize-OnlineBuildConfiguration -ProjectRoot $projectRoot
function Invoke-CheckedHeadless {
    param([string]$CheckName, [string[]]$GameArguments)
    $errorLog = Join-Path $buildWorkRoot ('build-' + $CheckName + '.err')
    $outputLog = Join-Path $buildWorkRoot ('build-' + $CheckName + '.log')
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $godotExe
    $start.WorkingDirectory = $projectRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in (@('--headless','--path',$gamePath) + $GameArguments)) { $start.ArgumentList.Add($argument) }
    $checked = [System.Diagnostics.Process]::new()
    $checked.StartInfo = $start
    try {
        if (-not $checked.Start()) { throw ('Cannot start check: ' + $CheckName) }
        $stdout = $checked.StandardOutput.ReadToEndAsync()
        $stderr = $checked.StandardError.ReadToEndAsync()
        $timedOut = -not $checked.WaitForExit(55000)
        if ($timedOut) { $checked.Kill($true); $checked.WaitForExit() }
        $nativeExitCode = $checked.ExitCode
        [System.IO.File]::WriteAllText($outputLog,$stdout.Result)
        [System.IO.File]::WriteAllText($errorLog,$stderr.Result)
        if ($timedOut) { throw ('Check timed out: ' + $CheckName + '; logs preserved') }
    } finally {
        $checked.Dispose()
    }
    # Scripts can log an error and still exit zero; require both channels clean.
    if ($nativeExitCode -ne 0 -or (Get-Item -LiteralPath $errorLog).Length -gt 0 -or (Select-String -LiteralPath $outputLog -Pattern 'SCRIPT ERROR:|^ERROR:|failures=[1-9]' -Quiet)) {
        Get-Content -LiteralPath $errorLog -TotalCount 12
        throw ('Godot check failed: ' + $CheckName + ', exit ' + $nativeExitCode)
    }
    Get-Content -LiteralPath $outputLog -Tail 1
}
$versionLine = Select-String -LiteralPath (Join-Path $gamePath 'project.godot') -Pattern '^config/version="([0-9]+\.[0-9]+\.[0-9]+)"$'
if (-not $versionLine) { throw 'Project version missing or invalid' }
$buildVersion = $versionLine.Matches[0].Groups[1].Value
$outDir = Join-Path $projectRoot ('outputs/Let-me-sleep-' + $buildVersion + '-Windows')
if (Test-Path -LiteralPath (Join-Path $outDir 'Let-me-sleep.exe')) {
    throw 'A versioned executable already exists. Choose a new project version before building; historical deliveries must remain reproducible.'
}
[System.IO.Directory]::CreateDirectory($outDir) | Out-Null
if (Test-Path -LiteralPath (Join-Path $projectRoot 'distribution')) {
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'distribution') -File | Copy-Item -Destination $outDir
}
# Godot 4.5.2 can race GDExtension teardown on a fresh editor import.
# Delay applies only to this import process, never the game or measurements.
Invoke-CheckedHeadless -CheckName 'import' -GameArguments @('--editor','--import','--quit','--frame-delay','800')
foreach ($entryPoint in @('scripts/main','scripts/client','tests/network_bot','tests/practice_ui_checks','tests/gameplay_demo','tests/gameplay06_demo','tests/camera_turn_checks','tests/hud_input06_checks','tests/hosting_checks','tests/gameplay07_demo','tests/performance07_live','tests/network07_combat_checks','tests/video07_checks','tests/doors07_client_checks','tests/throw_client07_checks','tests/tool07_visual_checks','tests/house07_occlusion_checks','tests/network07_throw_checks','tests/tools07_demo','tests/customization07_demo','tests/facial_catalog08_gallery','tests/facial_parts08_motion_export','tests/network08_cosmetics_checks')) {
    Invoke-CheckedHeadless -CheckName ('parse-' + $entryPoint.Replace('/','-')) -GameArguments @('--check-only','--script',"res://$entryPoint.gd")
}
if (-not $SkipTests) {
    if (-not $SkipHeadlessTests) {
    foreach ($initialTest in @('rules_test','lobby_rules_test','cosmetics_test','cosmetics_catalog08_test','network08_cosmetics_checks','visual_checks','audio_checks','house07_lighting_test')) {
        Invoke-CheckedHeadless -CheckName $initialTest -GameArguments @('--script',"res://tests/$initialTest.gd")
    }
    foreach ($testName in @('maps_test','route_tests','locomotion_test','focus_combat_test','manual_defense_test','stun_help_test','online_packet_codec_test','task_deadline_test','practice_test','invitation_test','network_order_test','network_connection_test','network_privacy_audit_test','contact_orientation06_test','house06_checks','music06_test','preferences_migration_test','doors07_test','door_navigation07_test','attack07_test','arena_spatial07_test','pose_cache07_test','barriers07_test','v07_character_motion_checks','v07_character_cache_checks','video_preferences07_test','bot_scheduler07_test','network07_combat_checks','house07_joints_test','house09_ceiling_test','house09_surfaces_test','v07_facial_state_checks','mosquito_impact07_test','throw_authority07_test','throw_input07_checks','audio07_tools_checks','frame07_joinery_test','pickup07_support_checks','tool_reach07_test','network07_throw_checks')) {
        Invoke-CheckedHeadless -CheckName $testName -GameArguments @('--script',"res://tests/$testName.gd")
    }
    foreach ($testName in @('audio09_redundancy_checks','hud09_coalescing_checks','skin09_visibility_checks','human09_presentation_test','surface09_test','surface_view09_test','emote09_authority_test','procedural09_test','geometry09_cache_checks','map_tasks09_test','voice09_acoustics_checks','voice09_pcm_test','audio09_listener_checks','network09_map_social_checks','network09_view_ack_checks','network09_voice_checks','door09_equivalence_test','door09_body_query_test','navigation09_connections_test')) {
        Invoke-CheckedHeadless -CheckName $testName -GameArguments @('--script',"res://tests/$testName.gd")
    }
    foreach ($testName in @('modeler092_contract_test','modeler092_structure_test','modeler091_layout_test','environment091_stair_lighting_test','environment091_v2_finish_test','review091_online_cosmetics_contract')) {
        Invoke-CheckedHeadless -CheckName $testName -GameArguments @('--script',"res://tests/$testName.gd")
    }
    # Physical route following exceeds the per-process deadline as one corpus.
    # Keep every seed and check, with one bounded process/report per house.
    foreach ($houseSeed in @(1,2,7,31,97,257,997,2026,65537,1234567,2147483646)) {
        $furnishingReport = Join-Path $buildWorkRoot ('build-modeler092-furnishing-' + $houseSeed + '.json')
        Invoke-CheckedHeadless -CheckName ('modeler092_furnishing_test-' + $houseSeed) -GameArguments @('--script','res://tests/modeler092_furnishing_test.gd','--',('--seed=' + $houseSeed),('--report=' + $furnishingReport))
        $houseReport = Join-Path $buildWorkRoot ('build-review092-house-' + $houseSeed + '.json')
        Invoke-CheckedHeadless -CheckName ('review092_house_contract-' + $houseSeed) -GameArguments @('--script','res://tests/review092_house_contract.gd','--',('--seed=' + $houseSeed),('--report=' + $houseReport))
        $approachReport = Join-Path $buildWorkRoot ('build-review092-approaches-' + $houseSeed + '.json')
        Invoke-CheckedHeadless -CheckName ('review092_functional_approach_test-' + $houseSeed) -GameArguments @('--script','res://tests/review092_functional_approach_test.gd','--',('--seed=' + $houseSeed),('--report=' + $approachReport))
    }
    Invoke-CheckedHeadless -CheckName 'voice09_session_checks' -GameArguments @('--audio-driver','Dummy','--frame-delay','2','--script','res://tests/voice09_session_checks.gd')
    foreach ($onlineTest in @('online_invitation_test','online_session_test','online_network_checks','online_network_mtu_checks','online_network_payload_checks','online_pair_integration')) {
        Invoke-CheckedHeadless -CheckName $onlineTest -GameArguments @('--script',"res://tests/$onlineTest.gd")
    }
    Invoke-CheckedHeadless -CheckName 'online_network_live_host' -GameArguments @('--script','res://tests/online_network_live_host.gd','--',('--config=' + (Join-Path $gamePath 'eos.local.cfg')))
    }
    # Skin baking requires a rendering backend; Godot's headless dummy backend
    # cannot register the skeleton used by this actual-deformed-mesh test.
    # UI checks also need real mouse capture, unavailable in the dummy backend.
    foreach ($nativeTest in @('camera_turn_checks','mosquito092_camera_checks','motion091_attachment_test','review091_motion_combat_contract','ui091_customization_checks','motion091_presentation_test','online_main_lifecycle','glasses09_fit_checks','actor09_legacy_geometry_test','v07_character_rig_checks','v07_character_client_checks','selected07_mesh_checks','selected07_actor_checks','selected07_facial_envelope_checks','facial_parts08_checks','facial_blink08_checks','customization08_checks','house07_checks','house07_lighting_probe','house07_liso_checks','ui_navigation_test','video07_checks','doors07_client_checks','throw_client07_checks','tool07_visual_checks','house07_occlusion_checks','appendage09_visual_checks','emote09_pose_checks','furniture09_blueprint_checks','voice09_visual_checks','menu09_ui_checks','social09_client_checks','surface09_client_checks','surface_view09_client_checks','voice_input09_ui_checks','voice_context09_ui_checks')) {
    $rigLog = Join-Path $PSScriptRoot ('build-' + $nativeTest + '.log')
    $rigError = Join-Path $PSScriptRoot ('build-' + $nativeTest + '.err')
    $nativeArguments = @('--path', ('"' + $gamePath + '"'), '--script', ('res://tests/' + $nativeTest + '.gd'))
    if ($nativeTest -eq 'camera_turn_checks') { $nativeArguments += '--verbose' }
    if ($nativeTest -eq 'selected07_mesh_checks') { $nativeArguments += @('--','--production','--verify','--tools') }
    if ($nativeTest -eq 'house07_lighting_probe') { $nativeArguments += @('--','--production','--candidate-only','--energy=0.55','--atlas=2048') }
    $rig = Start-Process -FilePath $godotExe -ArgumentList $nativeArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $rigLog -RedirectStandardError $rigError
    $retainedNativeHandle = $rig.Handle
    try {
        if (-not $rig.WaitForExit(55000)) { throw ('Native check timed out: ' + $nativeTest) }
        $rig.Refresh()
        if ($null -eq $rig.ExitCode -or $rig.ExitCode -ne 0 -or (Get-Item -LiteralPath $rigError).Length -gt 0 -or (Select-String -LiteralPath $rigLog -Pattern 'SCRIPT ERROR:|^ERROR:|failures=[1-9]' -Quiet)) { throw ('Native check failed: ' + $nativeTest + '; inspect work/build logs') }
        Get-Content -LiteralPath $rigLog -Tail 1
    } finally {
        if (-not $rig.HasExited) { Stop-Process -Id $rig.Id -Force }
    }
    }
    foreach ($style in 0..2) {
        $cornerOutput = Join-Path $projectRoot ('outputs/build-' + $buildVersion + '/actor-corner-style' + $style)
        & (Join-Path $PSScriptRoot 'run-native07.ps1') -Source -Name ('build-corner09-style' + $style) -GameArguments @('--script','res://tests/surface09_actor_corner_checks.gd','--',('--footwear=' + $style),('"--output=' + $cornerOutput + '"'))
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
$codecNotices = Join-Path $gamePath 'addons/lms_opus/licenses'
$codecNoticeOutput = Join-Path $outDir 'Licencias-voz'
[System.IO.Directory]::CreateDirectory($codecNoticeOutput) | Out-Null
Get-ChildItem -LiteralPath $codecNotices -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $codecNoticeOutput $_.Name)
}
$codecSource = Join-Path $gamePath 'addons/lms_opus/bin/lms_opus.windows.x86_64.dll'
$codecSourceHash = (Get-FileHash -LiteralPath $codecSource -Algorithm SHA256).Hash
$codecCopies = @(Get-ChildItem -LiteralPath $outDir -Recurse -File -Filter 'lms_opus.windows.x86_64.dll')
if ($codecCopies.Count -eq 0) { throw 'Opus extension DLL was not exported' }
foreach ($codecCopy in $codecCopies) {
    if ((Get-FileHash -LiteralPath $codecCopy.FullName -Algorithm SHA256).Hash -ne $codecSourceHash) { throw 'Exported Opus DLL differs from verified source binary' }
}
. (Join-Path $PSScriptRoot 'package-metadata.ps1')
$onlineNotices = Join-Path $gamePath 'addons/epic-online-services-godot/licenses'
$onlineNoticeOutput = Join-Path $outDir 'Licencias-online'
[IO.Directory]::CreateDirectory($onlineNoticeOutput) | Out-Null
Get-ChildItem -LiteralPath $onlineNotices -File | Copy-Item -Destination $onlineNoticeOutput
Get-OnlinePackageFiles -ProjectRoot $projectRoot -OutputDirectory $outDir | Out-Null
Write-PackageMetadata -ProjectRoot $projectRoot -OutputDirectory $outDir -Version $buildVersion -HeadlessTests (-not $SkipTests -and -not $SkipHeadlessTests) -NativeTests (-not $SkipTests)
Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256
