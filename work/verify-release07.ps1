param([ValidateSet('native','network','performance')][string]$Suite)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $taskRoot 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe'
if ($Suite -eq 'native') {
    foreach ($test in @('v07_character_rig_checks','v07_character_client_checks','selected07_actor_checks','selected07_facial_envelope_checks','facial_parts08_checks','facial_blink08_checks','customization08_checks','house07_checks','house07_liso_checks','video07_checks','doors07_client_checks','hud_input06_checks','camera_turn_checks','ui_navigation_test','throw_client07_checks','tool07_visual_checks','pickup07_support_checks','frame07_joinery_test','house07_occlusion_checks')) {
        $nativeArguments=@('--script',("res://tests/$test.gd"),'--max-fps','60')
        if($test -eq 'customization08_checks'){$nativeArguments+=@('--','--screens',('"--screens-output='+$taskRoot+'/outputs/0.7-personalizacion-final"'))}
        & "$PSScriptRoot/run-native07.ps1" -Name $test -GameArguments $nativeArguments
    }
    & "$PSScriptRoot/run-native07.ps1" -Name occlusion-transitions -GameArguments @('--script','res://tests/house07_occlusion_checks.gd','--','--transitions')
    & "$PSScriptRoot/run-native07.ps1" -Name network-throw -GameArguments @('--headless','--script','res://tests/network07_throw_checks.gd')
    & "$PSScriptRoot/run-native07.ps1" -Name network08_cosmetics_checks -GameArguments @('--headless','--script','res://tests/network08_cosmetics_checks.gd')
    & "$PSScriptRoot/run-native07.ps1" -Name cosmetics_catalog08_test -GameArguments @('--headless','--script','res://tests/cosmetics_catalog08_test.gd','--',('--catalog='+$PSScriptRoot+'/cosmetics-catalog08.json'))
    $catalogReportPath=Join-Path $PSScriptRoot 'cosmetics-catalog08.json'
    $catalogReport=Get-Content -LiteralPath $catalogReportPath -Raw | ConvertFrom-Json -AsHashtable
    $catalogReport['exe_sha256']=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    $catalogReport['external_cosmetics_source_sha256']=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'game/scripts/cosmetics.gd') -Algorithm SHA256).Hash
    $catalogReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $catalogReportPath -Encoding utf8
    & "$PSScriptRoot/run-native07.ps1" -Name selected07_mesh_checks -GameArguments @('--script','res://tests/selected07_mesh_checks.gd','--','--production','--verify','--tools')
    & "$PSScriptRoot/run-native07.ps1" -Name house07_lighting_probe -GameArguments @('--script','res://tests/house07_lighting_probe.gd','--','--production','--candidate-only','--energy=0.55','--atlas=2048')
    & "$PSScriptRoot/run-native07.ps1" -Name practice -GameArguments @('--','--practice-checks',('--check-report='+$PSScriptRoot+'/release07-practice.json'),('--check-output='+$PSScriptRoot+'/release07-practice-states'))
    & "$PSScriptRoot/run-native07.ps1" -Name hosting -GameArguments @('--','--hosting-checks',('--report='+$PSScriptRoot+'/release07-hosting.json'))
    & "$PSScriptRoot/run-native07.ps1" -Name combat -GameArguments @('--headless','--script','res://tests/network07_combat_checks.gd')
    $motionOutput=Join-Path $PSScriptRoot ('release07-facial-motion-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    & "$PSScriptRoot/run-facial-motion08.ps1" -Packed -Executable $exe -OutputDirectory $motionOutput
    $motionIndex=Get-Content -LiteralPath (Join-Path $motionOutput 'index.json') -Raw | ConvertFrom-Json
    if(-not $motionIndex.complete -or $motionIndex.case_count -ne 285 -or $motionIndex.executable_sha256 -ne (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash){throw 'Incomplete or mismatched final motion export.'}
    Copy-Item -LiteralPath (Join-Path $motionOutput 'index.json') -Destination (Join-Path $PSScriptRoot 'release07-facial-motion-index.json')
}
if ($Suite -eq 'performance') {
    $measuredExeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    foreach ($case in @(@(1,1080),@(16,1080),@(16,1440),@(16,2160))) {
        $label='performance-'+$case[0]+'-'+$case[1]
        & "$PSScriptRoot/run-native07.ps1" -Name $label -GameArguments @('--script','res://tests/performance07_live.gd','--',('--population='+$case[0]),('--resolution='+$case[1]),('--report='+$PSScriptRoot+'/release07-'+$label+'.json'))
        if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $measuredExeHash) { throw 'Executable changed during measurement' }
        $reportPath = Join-Path $PSScriptRoot ('release07-'+$label+'.json')
        $measuredReport = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -AsHashtable
        $measuredReport['exe_sha256'] = $measuredExeHash
        $measuredReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reportPath -Encoding utf8
    }
}
if ($Suite -eq 'network') {
    & "$PSScriptRoot/run-final-network07.ps1" -ExpectedExeSHA256 (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
}
