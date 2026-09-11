$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root 'outputs/Let-me-sleep-0.9.0-Windows/Let-me-sleep.exe'
foreach($test in @('online_main_lifecycle','online_pair_integration','online_network_checks','online_session_test','ui_navigation_test','doors07_client_checks','v07_character_client_checks')) {
    & "$PSScriptRoot/run-native07.ps1" -Executable $exe -Name ('sep11-rc09-'+$test) -GameArguments @('--audio-driver','Dummy','--script',('res://tests/'+$test+'.gd'))
}
foreach($role in @('human','mosquito')) {
    & "$PSScriptRoot/run-native07.ps1" -Executable $exe -Name ('sep11-rc09-practice-'+$role) -GameArguments @('--audio-driver','Dummy','--','--no-microphone','--practice-checks',('--check-role='+$role),'--check-mode=blood',('--check-report='+$PSScriptRoot+'/sep11-practice-'+$role+'.json'))
}
'CANDIDATE_EXE_CHECKS passed=9'
