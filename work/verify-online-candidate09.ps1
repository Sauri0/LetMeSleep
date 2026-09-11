param([string]$Executable, [string]$EvidencePrefix = ('candidate-'+(Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference='Stop'
if($EvidencePrefix -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a plain evidence prefix.'}
$exe=& "$PSScriptRoot/resolve-game-executable.ps1" -Executable $Executable
$running=@(Get-Process -Name 'Godot*','Let-me-sleep*' -ErrorAction SilentlyContinue)
if($running.Count){throw 'Candidate checks deferred: a game/editor is already running.'}
foreach($test in @('online_main_lifecycle','online_pair_integration','online_network_checks','online_session_test','ui_navigation_test','doors07_client_checks','v07_character_client_checks')) {
    & "$PSScriptRoot/run-native07.ps1" -Executable $exe -Name ($EvidencePrefix+'-'+$test) -GameArguments @('--audio-driver','Dummy','--script',('res://tests/'+$test+'.gd'))
}
foreach($role in @('human','mosquito')) {
    & "$PSScriptRoot/run-native07.ps1" -Executable $exe -Name ($EvidencePrefix+'-practice-'+$role) -GameArguments @('--audio-driver','Dummy','--','--no-microphone','--practice-checks',('--check-role='+$role),'--check-mode=blood',('--check-report='+$PSScriptRoot+'/'+$EvidencePrefix+'-practice-'+$role+'.json'))
}
'CANDIDATE_EXE_CHECKS passed=9 scope=local-fixtures-and-bots WAN=false'