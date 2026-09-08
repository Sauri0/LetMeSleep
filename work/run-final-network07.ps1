param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedExeSHA256,
    [ValidateSet('blood','tasks','survival','disconnect','invalid','legacy06')][string[]]$Scenario = @('blood','tasks','survival','disconnect','invalid','legacy06'),
    [string]$ReportPrefix = 'release07-network',
    [string]$Executable
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$exe = & "$PSScriptRoot/resolve-game-executable.ps1" -Executable $Executable
$legacyExe = Join-Path $taskRoot 'outputs/Let-me-sleep-0.6.0-Windows/Let-me-sleep.exe'
$harness = Join-Path $PSScriptRoot 'test-network.ps1'
$shellExe = (Get-Process -Id $PID).Path
$expectedHash = $ExpectedExeSHA256.ToUpperInvariant()
if ($ReportPrefix -notmatch '^[A-Za-z0-9_-]+$') { throw 'ReportPrefix must be a plain filename prefix.' }
if (@($Scenario | Sort-Object -Unique).Count -ne $Scenario.Count) { throw 'Duplicate scenario requested.' }
function Assert-FrozenExe {
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Frozen EXE hash differs from the explicitly supplied delivery hash.' }
}
function Assert-Fields($Value, [string[]]$Fields, [string]$Label) {
    if ($null -eq $Value) { throw "$Label missing object" }
    foreach ($field in $Fields) {
        if ($Value.PSObject.Properties.Name -notcontains $field) { throw "$Label missing field $field" }
    }
}
function Assert-True([bool]$Value, [string]$Label) { if (-not $Value) { throw $Label } }
function Write-Json([string]$Path, $Value) {
    [System.IO.File]::WriteAllText($Path,($Value | ConvertTo-Json -Depth 24)+[Environment]::NewLine,[System.Text.UTF8Encoding]::new($false))
}
Assert-FrozenExe
if ($Scenario -contains 'legacy06' -and -not (Test-Path -LiteralPath $legacyExe)) { throw 'The actual 0.6 EXE is required for the legacy scenario.' }
$runLabel = 'network-final07-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')
$wrapperDir = Join-Path $PSScriptRoot $runLabel
[System.IO.Directory]::CreateDirectory($wrapperDir) | Out-Null
$exeEvidence = [ordered]@{path=$exe;sha256=$expectedHash;bytes=(Get-Item -LiteralPath $exe).Length}
$cases = @(
    @{Name='blood';Mode='blood';Humans=1;Mosquitoes=1;Rounds=2;InvitationTest=$true;RematchTest=$true;Port=28701},
    @{Name='tasks';Mode='sleep';Humans=1;Mosquitoes=1;Rounds=1;Port=28702},
    @{Name='survival';Mode='survival';Humans=4;Mosquitoes=12;Rounds=1;Port=28703},
    @{Name='disconnect';Mode='blood';Humans=1;Mosquitoes=1;Rounds=1;DisconnectTest=$true;Port=28704},
    @{Name='invalid';Mode='blood';Humans=1;Mosquitoes=1;Rounds=1;Incompatible=$true;Port=28705},
    @{Name='legacy06';Mode='blood';Humans=1;Mosquitoes=1;Rounds=1;Incompatible=$true;LegacyHandshake=$true;ClientExecutable=$legacyExe;Port=28706}
)
$summary = [ordered]@{exe=$exeEvidence;harness_sha256=(Get-FileHash -LiteralPath $harness -Algorithm SHA256).Hash;started_utc=[DateTime]::UtcNow.ToString('o');requested=$Scenario;cases=@();passed=$false;limits=@('ENet processes on one PC; not an independent WAN test.','These rounds validate lobby/roles/results/privacy; separate combat and throw fixtures validate six tools and projectile actions.','Validates the PowerShell harness and each naturally exited Godot client code; timeouts/forced client cleanup fail. The server is intentionally stopped by the harness: its recorded forced-stop code is not required to be zero.')}
foreach ($case in $cases) {
    if ($Scenario -notcontains $case.Name) { continue }
    Assert-FrozenExe
    $name = $case.Name
    $stdout = Join-Path $wrapperDir ($name+'.stdout.json')
    $stderr = Join-Path $wrapperDir ($name+'.stderr.log')
    $caseSummary = [ordered]@{scenario=$name;port=$case.Port;started_utc=[DateTime]::UtcNow.ToString('o');passed=$false;failure='';wrapper_exit=$null}
    $proc = $null
    $record = $null
    Write-Output "NETWORK_BEGIN $name port=$($case.Port) sha256=$expectedHash"
    try {
        # A separate PowerShell process gives a fresh, explicit exit status.
        # Never infer success from a caller's possibly stale LASTEXITCODE.
        $arguments = @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',('"'+$harness+'"'),'-Executable',('"'+$exe+'"'))
        foreach ($key in ($case.Keys | Sort-Object)) {
            if ($key -eq 'Name') { continue }
            $arguments += '-'+$key
            if ($case[$key] -isnot [bool]) { $arguments += '"'+[string]$case[$key]+'"' }
        }
        $proc = Start-Process -FilePath $shellExe -ArgumentList $arguments -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $deadline = [DateTime]::UtcNow.AddSeconds([int]$case.Rounds*75+65)
        $lastProgress = [DateTime]::UtcNow
        while (-not $proc.WaitForExit(500)) {
            if ([DateTime]::UtcNow -gt $deadline) { throw "$name wrapper timeout" }
            if (([DateTime]::UtcNow-$lastProgress).TotalSeconds -ge 15) {
                Write-Output "NETWORK_RUNNING $name"
                $lastProgress = [DateTime]::UtcNow
            }
        }
        $proc.WaitForExit()
        $caseSummary.wrapper_exit = $proc.ExitCode
        Assert-FrozenExe
        # Preserve child-process evidence even when the harness returns failure.
        if ((Get-Item -LiteralPath $stdout).Length -gt 0) {
            $record = Get-Content -LiteralPath $stdout -Raw | ConvertFrom-Json
            if ($record.PSObject.Properties.Name -contains 'processes') { $caseSummary.processes = @($record.processes) }
        }
        Assert-True ($proc.ExitCode -eq 0) "$name harness exited $($proc.ExitCode); inspect $wrapperDir"
        Assert-True ((Get-Item -LiteralPath $stderr).Length -eq 0) "$name PowerShell stderr is not empty"
        Assert-Fields $record @('run','expectedClients','reports','stderr','harness_errors','processes','process_validation') $name
        $reports = @($record.reports)
        $expectedClients = if ($case.Incompatible) { 1 } else { $case.Humans+$case.Mosquitoes }
        Assert-True ($record.expectedClients -eq $expectedClients -and $reports.Count -eq $expectedClients) "$name report count mismatch"
        Assert-True (@($record.stderr).Count -eq 0) "$name reported engine stderr"
        Assert-True (@($record.harness_errors).Count -eq 0) "$name harness capture/cleanup errors"
        $clientProcesses = @($record.processes | Where-Object { $_.kind -eq 'client' })
        $serverProcesses = @($record.processes | Where-Object { $_.kind -eq 'server' })
        Assert-True ($clientProcesses.Count -eq $expectedClients -and $serverProcesses.Count -eq 1 -and @($record.processes).Count -eq $expectedClients+1) "$name process evidence count mismatch"
        Assert-True (@($record.processes.pid | Sort-Object -Unique).Count -eq $expectedClients+1) "$name duplicate process evidence"
        foreach ($child in @($record.processes)) {
            Assert-Fields $child @('label','kind','pid','executable','started_utc','observed_exit_utc','exited','exit_code','termination','stop_requested','timed_out','cleanup_error') $name
            Assert-True ($child.exited -eq $true -and $null -ne $child.exit_code -and -not $child.cleanup_error) "$name missing/failed process exit capture for $($child.label)"
        }
        foreach ($child in $clientProcesses) {
            Assert-True ($child.exit_code -eq 0 -and $child.termination -eq 'natural' -and $child.stop_requested -eq $false -and $child.timed_out -eq $false) "$name client $($child.label) did not exit normally with code0"
        }
        Assert-True ($serverProcesses[0].termination -eq 'harness_server_stop' -and $serverProcesses[0].stop_requested -eq $true -and $serverProcesses[0].timed_out -eq $false) "$name server exit was not an intentional harness shutdown"
        Assert-True ($record.process_validation.passed -eq $true -and $record.process_validation.client_exit_codes_checked -eq $true) "$name harness process validation failed"
        # Scan after the child script's finally block, including late server output.
        $engineLogs = @(Get-ChildItem -LiteralPath $record.run -Filter '*.log')
        Assert-True ($engineLogs.Count -ge ($expectedClients+1)*2) "$name missing client/server logs"
        foreach ($log in $engineLogs) {
            if ($log.Name -like '*.stderr.log') { Assert-True ($log.Length -eq 0) "$name engine stderr: $($log.FullName)" }
            if (Select-String -LiteralPath $log.FullName -Pattern '(?m)SCRIPT ERROR:|^ERROR:|^FAIL(?:\s|:)|failures=[1-9]' -Quiet) { throw "$name engine failure text: $($log.FullName)" }
        }
        foreach ($report in $reports) {
            Assert-Fields $report @('errors','privacy_ok','privacy_failures','privacy_pending','privacy_matched','privacy_deferred','private_packets','result') $name
            Assert-True (@($report.errors).Count -eq 0 -and $report.privacy_ok -eq $true -and @($report.privacy_failures).Count -eq 0 -and $report.privacy_pending -eq 0) "$name privacy/errors barrier failed"
            Assert-True ($report.privacy_matched -eq $report.private_packets) "$name private packets were not all matched"
            if ($case.Incompatible) {
                Assert-Fields $report @('incompatible_rejected') $name
                Assert-True ($report.incompatible_rejected -eq $true -and $report.private_packets -eq 0) "$name incompatible client accepted or received private data"
            } elseif (-not $case.DisconnectTest) {
                Assert-Fields $report @('results','roles_by_round','lobby_movement_seen','lobby_jump_seen','lobby_crouch_seen','lobby_sprint_seen','cosmetics_synced') $name
                Assert-True (@($report.results).Count -eq $case.Rounds -and @($report.roles_by_round).Count -eq $case.Rounds) "$name incomplete round history"
                foreach ($field in @('lobby_movement_seen','lobby_jump_seen','lobby_crouch_seen','lobby_sprint_seen','cosmetics_synced')) { Assert-True ($report.$field -eq $true) "$name missing $field" }
                $winner = if ($case.Mode -eq 'sleep') { 'human' } else { 'mosquito' }
                foreach ($result in @($report.results)) { Assert-True ($result.winner -eq $winner) "$name inconsistent winner in round history" }
                Assert-True ($report.result.winner -eq $winner) "$name last winner mismatch"
                if ($name -eq 'blood') { Assert-True ($report.returned_to_lobby -eq $true) 'blood rematch did not return to lobby' }
            }
        }
        if ($case.DisconnectTest) {
            $intentional = @($reports | Where-Object { $_.intentional_disconnect -eq $true })
            $survivors = @($reports | Where-Object { $_.intentional_disconnect -ne $true })
            Assert-True ($intentional.Count -eq 1 -and $survivors.Count -eq 1) 'disconnect requires one intentional departure and one observer'
            foreach ($report in $survivors) { Assert-True ($report.returned_to_lobby -eq $true -and -not $report.result.winner) 'disconnect must abort to lobby without winner' }
        } elseif (-not $case.Incompatible) {
            for ($round=0; $round -lt $case.Rounds; $round++) {
                $humans = @($reports | Where-Object { $_.roles_by_round[$round] -eq 'human' }).Count
                $mosquitoes = @($reports | Where-Object { $_.roles_by_round[$round] -eq 'mosquito' }).Count
                Assert-True ($humans -eq $case.Humans -and $mosquitoes -eq $case.Mosquitoes) "$name server-assigned role totals mismatch in round $round"
            }
        }
        $record | Add-Member -NotePropertyName exe -NotePropertyValue $exeEvidence -Force
        $record | Add-Member -NotePropertyName validation -NotePropertyValue ([ordered]@{passed=$true;scenario=$name;port=$case.Port;harness_exit=$proc.ExitCode;all_client_exit_codes_zero=$true;server_intentionally_stopped=$true;all_round_results_checked=$true;all_private_packets_matched=$true;raw_logs_checked_after_cleanup=$true;completed_utc=[DateTime]::UtcNow.ToString('o')}) -Force
        if ($case.ClientExecutable) { $record | Add-Member -NotePropertyName client_exe -NotePropertyValue ([ordered]@{path=$case.ClientExecutable;sha256=(Get-FileHash -LiteralPath $case.ClientExecutable -Algorithm SHA256).Hash}) -Force }
        $canonical = Join-Path $PSScriptRoot ($ReportPrefix+'-'+$name+'.json')
        if (Test-Path -LiteralPath $canonical) { Copy-Item -LiteralPath $canonical -Destination (Join-Path $wrapperDir ($name+'.previous.json')) }
        Write-Json $canonical $record
        $caseSummary.passed = $true
        $caseSummary.report = $canonical
        $caseSummary.run = $record.run
        $caseSummary.private_matched = ($reports | Measure-Object -Property privacy_matched -Sum).Sum
        $caseSummary.private_deferred = ($reports | Measure-Object -Property privacy_deferred -Sum).Sum
        Write-Output "NETWORK_PASS $name clients=$expectedClients private_matched=$($caseSummary.private_matched) deferred=$($caseSummary.private_deferred) pending=0"
    } catch {
        $caseSummary.failure = $_.Exception.Message
        # A failed repeat must not leave an older successful canonical report
        # available to a finalizer that has not yet inspected this summary.
        $canonical = Join-Path $PSScriptRoot ($ReportPrefix+'-'+$name+'.json')
        $previous = Join-Path $wrapperDir ($name+'.previous.json')
        if ((Test-Path -LiteralPath $canonical) -and -not (Test-Path -LiteralPath $previous)) { Copy-Item -LiteralPath $canonical -Destination $previous }
        Write-Json $canonical ([ordered]@{run=$(if ($null -ne $record) {$record.run} else {$wrapperDir});expectedClients=$(if ($case.Incompatible) {1} else {$case.Humans+$case.Mosquitoes});reports=@();processes=$(if ($null -ne $record) {@($record.processes)} else {@()});stderr=@($caseSummary.failure);exe=$exeEvidence;validation=@{passed=$false;scenario=$name;harness_exit=$caseSummary.wrapper_exit}})
        Write-Output "NETWORK_FAIL $name $($caseSummary.failure)"
    } finally {
        if ($null -ne $proc -and -not $proc.HasExited) {
            # Only children started by this owned harness process are stopped.
            Get-CimInstance Win32_Process -Filter "ParentProcessId=$($proc.Id)" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
        $caseSummary.completed_utc = [DateTime]::UtcNow.ToString('o')
        $summary.cases += $caseSummary
        Write-Json (Join-Path $wrapperDir 'summary.json') $summary
    }
}
Assert-FrozenExe
$summary.completed_utc = [DateTime]::UtcNow.ToString('o')
$summary.passed = @($summary.cases | Where-Object { -not $_.passed }).Count -eq 0 -and $summary.cases.Count -eq $Scenario.Count
Write-Json (Join-Path $wrapperDir 'summary.json') $summary
Write-Output "NETWORK_MATRIX passed=$($summary.passed) scenarios=$($summary.cases.Count) summary=$wrapperDir/summary.json"
if (-not $summary.passed) { exit 1 }
exit 0
