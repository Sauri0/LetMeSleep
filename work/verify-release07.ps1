param([ValidateSet('native','network','performance')][string]$Suite)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $taskRoot 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe'
if ($Suite -eq 'native') {
    foreach ($test in @('v07_character_rig_checks','v07_character_client_checks','house07_checks','video07_checks','doors07_client_checks','hud_input06_checks','camera_turn_checks','ui_navigation_test')) {
        & "$PSScriptRoot/run-native07.ps1" -Name $test -GameArguments @('--script',("res://tests/$test.gd"),'--max-fps','60')
    }
    & "$PSScriptRoot/run-native07.ps1" -Name practice -GameArguments @('--','--practice-checks',('--check-report='+$PSScriptRoot+'/release07-practice.json'),('--check-output='+$PSScriptRoot+'/release07-practice-states'))
    & "$PSScriptRoot/run-native07.ps1" -Name hosting -GameArguments @('--','--hosting-checks',('--report='+$PSScriptRoot+'/release07-hosting.json'))
    & "$PSScriptRoot/run-native07.ps1" -Name combat -GameArguments @('--headless','--script','res://tests/network07_combat_checks.gd')
}
if ($Suite -eq 'performance') {
    foreach ($case in @(@(1,1080),@(16,1080),@(16,1440),@(16,2160))) {
        $label='performance-'+$case[0]+'-'+$case[1]
        & "$PSScriptRoot/run-native07.ps1" -Name $label -GameArguments @('--script','res://tests/performance07_live.gd','--',('--population='+$case[0]),('--resolution='+$case[1]),('--report='+$PSScriptRoot+'/release07-'+$label+'.json'))
    }
}
if ($Suite -eq 'network') {
    $cases=@(
        @{Name='blood';Mode='blood';Humans=1;Mosquitoes=1;Rounds=2;InvitationTest=$true;RematchTest=$true;Port=28701},
        @{Name='tasks';Mode='sleep';Humans=1;Mosquitoes=1;Port=28702},
        @{Name='survival';Mode='survival';Humans=4;Mosquitoes=12;Port=28703},
        @{Name='disconnect';Mode='blood';Humans=1;Mosquitoes=1;DisconnectTest=$true;Port=28704},
        @{Name='invalid';Mode='blood';Incompatible=$true;Port=28705},
        @{Name='legacy06';Mode='blood';Incompatible=$true;LegacyHandshake=$true;ClientExecutable=(Join-Path $taskRoot 'outputs/Let-me-sleep-0.6.0-Windows/Let-me-sleep.exe');Port=28706}
    )
    foreach ($case in $cases) {
        $label=$case.Name
        $case.Remove('Name')
        & "$PSScriptRoot/test-network.ps1" @case -Executable $exe > (Join-Path $PSScriptRoot ('release07-network-'+$label+'.json'))
        if ($LASTEXITCODE -ne 0) { throw ('Network case failed: '+$label) }
        Write-Output ('Network PASS '+$label)
    }
}
