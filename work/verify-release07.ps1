param([ValidateSet('native','network','performance')][string]$Suite, [string]$Executable, [string]$EvidencePrefix)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$exe=& "$PSScriptRoot/resolve-game-executable.ps1" -Executable $Executable
if (-not $EvidencePrefix) { $EvidencePrefix='candidate-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff') }
if ($EvidencePrefix -notmatch '^[A-Za-z0-9_-]+$') { throw 'EvidencePrefix must be a plain filename prefix.' }
$evidenceDirectory=Join-Path $PSScriptRoot $EvidencePrefix
if (Test-Path -LiteralPath $evidenceDirectory) { throw 'Prior evidence exists; choose a fresh EvidencePrefix.' }
[IO.Directory]::CreateDirectory($evidenceDirectory) | Out-Null
# Keep previous release logs and captures intact. The existing bounded runner
# records the executable hash for every invocation.
function Invoke-CandidateCheck {
    param([string]$Name, [string[]]$GameArguments)
    & "$PSScriptRoot/run-native07.ps1" -Executable $exe -Name ($EvidencePrefix+'-'+$Name) -GameArguments (@('--audio-driver','Dummy')+$GameArguments)
}
if ($Suite -eq 'native') {
    foreach ($test in @('v07_character_rig_checks','v07_character_client_checks','selected07_actor_checks','selected07_facial_envelope_checks','facial_parts08_checks','facial_blink08_checks','customization08_checks','house07_checks','house07_liso_checks','video07_checks','doors07_client_checks','hud_input06_checks','camera_turn_checks','ui_navigation_test','throw_client07_checks','tool07_visual_checks','pickup07_support_checks','frame07_joinery_test','house07_occlusion_checks')) {
        $nativeArguments=@('--script',("res://tests/$test.gd"),'--max-fps','60')
        if($test -eq 'customization08_checks'){$nativeArguments+=@('--','--screens',('"--screens-output='+$evidenceDirectory+'/customization"'))}
        Invoke-CandidateCheck -Name $test -GameArguments $nativeArguments
    }
    Invoke-CandidateCheck -Name occlusion-transitions -GameArguments @('--script','res://tests/house07_occlusion_checks.gd','--','--transitions')
    Invoke-CandidateCheck -Name network-throw -GameArguments @('--headless','--script','res://tests/network07_throw_checks.gd')
    Invoke-CandidateCheck -Name network08_cosmetics_checks -GameArguments @('--headless','--script','res://tests/network08_cosmetics_checks.gd')
    Invoke-CandidateCheck -Name cosmetics_catalog08_test -GameArguments @('--headless','--script','res://tests/cosmetics_catalog08_test.gd','--',('--catalog='+$evidenceDirectory+'/cosmetics-catalog08.json'))
    $catalogReportPath=Join-Path $evidenceDirectory 'cosmetics-catalog08.json'
    $catalogReport=Get-Content -LiteralPath $catalogReportPath -Raw | ConvertFrom-Json -AsHashtable
    $catalogReport['exe_sha256']=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    $catalogReport['external_cosmetics_source_sha256']=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'game/scripts/cosmetics.gd') -Algorithm SHA256).Hash
    $catalogReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $catalogReportPath -Encoding utf8
    Invoke-CandidateCheck -Name selected07_mesh_checks -GameArguments @('--script','res://tests/selected07_mesh_checks.gd','--','--production','--verify','--tools')
    Invoke-CandidateCheck -Name house07_lighting_probe -GameArguments @('--script','res://tests/house07_lighting_probe.gd','--','--production','--candidate-only','--energy=0.55','--atlas=2048')
    foreach ($role in @('human','mosquito')) {
        foreach ($mode in @('blood','survival','sleep')) {
            $practiceLabel='practice-'+$role+'-'+$mode
            Invoke-CandidateCheck -Name $practiceLabel -GameArguments @('--','--no-microphone','--practice-checks',('--check-role='+$role),('--check-mode='+$mode),('--check-report='+$evidenceDirectory+'/'+$practiceLabel+'.json'),('--check-output='+$evidenceDirectory+'/'+$practiceLabel+'-states'))
        }
    }
    Invoke-CandidateCheck -Name hosting -GameArguments @('--','--no-microphone','--hosting-checks',('--report='+$evidenceDirectory+'/hosting.json'))
    Invoke-CandidateCheck -Name combat -GameArguments @('--headless','--script','res://tests/network07_combat_checks.gd')
    foreach ($test in @('hud09_coalescing_checks','skin09_visibility_checks','human09_presentation_test','stun_help_test','surface09_test','emote09_authority_test','procedural09_test','map_tasks09_test','voice09_acoustics_checks','voice09_pcm_test','network09_map_social_checks','network09_view_ack_checks','network09_voice_checks')) {
        Invoke-CandidateCheck -Name $test -GameArguments @('--headless','--script',("res://tests/$test.gd"))
    }
    Invoke-CandidateCheck -Name voice09_session_checks -GameArguments @('--headless','--frame-delay','2','--script','res://tests/voice09_session_checks.gd')
    foreach ($test in @('glasses09_fit_checks','actor09_legacy_geometry_test','appendage09_visual_checks','emote09_pose_checks','furniture09_blueprint_checks','voice09_visual_checks','menu09_ui_checks','social09_client_checks','surface09_client_checks','surface_view09_client_checks','voice_input09_ui_checks','voice_context09_ui_checks')) {
        $testOutput=Join-Path $evidenceDirectory $test
        if ($test -in @('voice09_visual_checks','glasses09_fit_checks')) { $testOutput+='.json' }
        $outputFlag = if ($test -eq 'actor09_legacy_geometry_test') { '--report=' } else { '--output=' }
        if ($test -eq 'actor09_legacy_geometry_test') { $testOutput += '.json' }
        Invoke-CandidateCheck -Name $test -GameArguments @('--script',("res://tests/$test.gd"),'--',($outputFlag+$testOutput))
    }
    $motionOutput=Join-Path $evidenceDirectory 'facial-motion'
    & "$PSScriptRoot/run-facial-motion08.ps1" -Packed -Executable $exe -OutputDirectory $motionOutput
    $motionIndex=Get-Content -LiteralPath (Join-Path $motionOutput 'index.json') -Raw | ConvertFrom-Json
    if(-not $motionIndex.complete -or $motionIndex.case_count -ne 285 -or $motionIndex.executable_sha256 -ne (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash){throw 'Incomplete or mismatched final motion export.'}
    Copy-Item -LiteralPath (Join-Path $motionOutput 'index.json') -Destination (Join-Path $evidenceDirectory 'facial-motion-index.json')
}
if ($Suite -eq 'performance') {
    $measuredExeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    foreach ($case in @(@(2,1080,$false),@(16,1080,$false),@(16,1440,$false),@(16,2160,$false),@(16,1080,$true))) {
        $label='performance-'+$case[0]+'-'+$case[1]
        if ($case[2]) { $label+='-natural-doors' }
        $performanceArguments=@('--script','res://tests/performance07_live.gd','--','--map-id=house-v1-1',('--population='+$case[0]),('--resolution='+$case[1]),('--report='+$evidenceDirectory+'/'+$label+'.json'))
        if ($case[2]) { $performanceArguments+='--natural-doors' }
        Invoke-CandidateCheck -Name $label -GameArguments $performanceArguments
        if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $measuredExeHash) { throw 'Executable changed during measurement' }
        $reportPath = Join-Path $evidenceDirectory ($label+'.json')
        $measuredReport = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -AsHashtable
        $measuredReport['exe_sha256'] = $measuredExeHash
        $measuredReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reportPath -Encoding utf8
    }
}
if ($Suite -eq 'network') {
    & "$PSScriptRoot/run-final-network07.ps1" -Executable $exe -ReportPrefix ($EvidencePrefix+'-network') -ExpectedExeSHA256 (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
}
