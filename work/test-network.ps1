param(
    [string]$Mode = 'blood',
    [int]$Humans = 1,
    [int]$Mosquitoes = 1,
    [int]$Rounds = 1,
    [int]$Port = 27940,
    [switch]$DisconnectTest,
    [switch]$RematchTest,
    [switch]$Incompatible,
    [switch]$InvitationTest,
    [string]$Executable = '',
    [string]$ClientExecutable = '',
    [switch]$LegacyHandshake,
    [string]$JoinVersion = '0.0.invalid',
    [int]$JoinProtocol = -99
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = if ($Executable) { $Executable } else { Join-Path $projectRoot 'work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe' }
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $Port + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$runDir = Join-Path $PSScriptRoot "network-$Mode-${Humans}v${Mosquitoes}-$runId"
[System.IO.Directory]::CreateDirectory($runDir) | Out-Null
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$processInfo = [System.Collections.Generic.List[object]]::new()
$processResults = [System.Collections.Generic.List[object]]::new()
$reports = @()
$errors = @()
$harnessErrors = [System.Collections.Generic.List[string]]::new()
$expectedClients = if ($Incompatible) { 1 } else { $Humans + $Mosquitoes }
$testFailed = $true
function Start-GameProcess([string]$Label, [string[]]$Extra) {
    $isExternalClient = $Label -ne 'server' -and $ClientExecutable
    $selectedExe = if ($isExternalClient) { $ClientExecutable } else { $godotExe }
    $baseArgs = @('--headless')
    if (-not $Executable -and -not $isExternalClient) { $baseArgs += @('--path', ('"' + (Join-Path $projectRoot 'game') + '"')) }
    $allArgs = $baseArgs + @('--') + $Extra
    $proc = Start-Process -FilePath $selectedExe -ArgumentList $allArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir "$Label.stdout.log") -RedirectStandardError (Join-Path $runDir "$Label.stderr.log")
    $processes.Add($proc)
    $processInfo.Add([ordered]@{process=$proc;label=$Label;kind=$(if ($Label -eq 'server') {'server'} else {'client'});executable=$selectedExe;started_utc=[DateTime]::UtcNow.ToString('o');timed_out=$false})
    return $proc
}
try {
    $server = Start-GameProcess 'server' @('--server', "--port=$Port")
    $serverLog = Join-Path $runDir 'server.stdout.log'
    $serverDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $serverDeadline) {
        if ($server.HasExited) { throw "Server exited before readiness. Inspect $runDir" }
        if ((Test-Path -LiteralPath $serverLog) -and (Select-String -LiteralPath $serverLog -Pattern 'SERVER_READY' -Quiet)) { break }
        Start-Sleep -Milliseconds 100
    }
    if (-not (Select-String -LiteralPath $serverLog -Pattern 'SERVER_READY' -Quiet)) { throw "Server did not become ready. Inspect $runDir" }
    $codeFile = Join-Path $runDir 'room-code.txt'
    $creatorArgs = @('--bot', '--create', '--name=Amigo1', "--humans=$Humans", "--rounds=$Rounds", "--timeout=$($Rounds * 75 + 20)", "--mode=$Mode", "--port=$Port", "--players=$($Humans + $Mosquitoes)", ('--code-file="' + $codeFile + '"'), ('--report="' + (Join-Path $runDir 'human1.json') + '"'))
    if ($DisconnectTest) { $creatorArgs += '--disconnect-test' }
    if ($RematchTest) { $creatorArgs += '--rematch-test' }
    if ($Incompatible -and -not $LegacyHandshake) { $creatorArgs += @('--incompatible', "--join-version=$JoinVersion", "--join-protocol=$JoinProtocol") }
    if ($InvitationTest) { $creatorArgs += '--shared-host=' + [System.Net.Dns]::GetHostName() }
    $creator = Start-GameProcess 'human1' $creatorArgs
    if (-not $Incompatible) {
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath $codeFile) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
        if (-not (Test-Path -LiteralPath $codeFile)) { throw "No room code created. Inspect $runDir" }
        $code = [System.IO.File]::ReadAllText($codeFile)
        for ($i = 2; $i -le $Humans; $i++) {
            $extra = @('--bot', "--name=Amigo$i", "--rounds=$Rounds", "--timeout=$($Rounds * 75 + 20)", "--code=$code", "--port=$Port", ('--report="' + (Join-Path $runDir "human$i.json") + '"'))
            if ($RematchTest) { $extra += '--rematch-test' }
            if ($InvitationTest) { $extra += '--invitation=' + $code }
            if ($DisconnectTest) { $extra += '--disconnect-test' }
            Start-GameProcess "human$i" $extra | Out-Null
        }
        for ($i = 1; $i -le $Mosquitoes; $i++) {
            $extra = @('--bot', "--name=Amigo$($Humans + $i)", "--rounds=$Rounds", "--timeout=$($Rounds * 75 + 20)", "--code=$code", "--port=$Port", ('--report="' + (Join-Path $runDir "mosquito$i.json") + '"'))
            if ($RematchTest) { $extra += '--rematch-test' }
            if ($InvitationTest) { $extra += '--invitation=' + $code }
            if ($DisconnectTest) { $extra += if ($i -eq 1) { '--disconnect-after=6' } else { '--disconnect-test' } }
            Start-GameProcess "mosquito$i" $extra | Out-Null
        }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($Rounds * 75 + 25)
    while ([DateTime]::UtcNow -lt $deadline) {
        $running = @($processes | Where-Object { $_.Id -ne $server.Id -and -not $_.HasExited })
        if ($running.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    }
    foreach ($entry in $processInfo) {
        if ($entry.kind -ne 'client') { continue }
        $entry.process.Refresh()
        if (-not $entry.process.HasExited) { $entry.timed_out = $true }
        else { $entry.process.WaitForExit() } # Drain redirected output before reading reports.
    }
    $reports = @(Get-ChildItem -LiteralPath $runDir -Filter '*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    $errors = @(Get-ChildItem -LiteralPath $runDir -Filter '*.stderr.log' | Where-Object { $_.Length -gt 0 } | ForEach-Object { (Get-Content -LiteralPath $_.FullName -TotalCount 16) -join "`n" })
    $failedReports = @($reports | Where-Object { $_.errors.Count -gt 0 -or -not $_.privacy_ok })
    $testFailed = $reports.Count -ne $expectedClients -or $failedReports.Count -gt 0 -or $errors.Count -gt 0
    if ($Incompatible) {
        $testFailed = $testFailed -or @($reports | Where-Object { -not $_.incompatible_rejected }).Count -gt 0
    }
    if ($DisconnectTest) {
        $intentional = @($reports | Where-Object { $_.intentional_disconnect })
        $survivors = @($reports | Where-Object { -not $_.intentional_disconnect })
        $invalidAbort = @($survivors | Where-Object { -not $_.returned_to_lobby -or $_.result.winner })
        $testFailed = $testFailed -or $intentional.Count -ne 1 -or $survivors.Count -lt 1 -or $invalidAbort.Count -gt 0
    }
    if (-not $Incompatible -and -not $DisconnectTest) {
        $winners = @($reports.result.winner | Sort-Object -Unique)
        $expectedWinner = if ($Mode -eq 'sleep') { 'human' } else { 'mosquito' }
        $testFailed = $testFailed -or $winners.Count -ne 1 -or $winners[0] -ne $expectedWinner
        $incomplete = @($reports | Where-Object { $_.roles_by_round.Count -ne $Rounds -or $_.results.Count -ne $Rounds -or -not $_.lobby_movement_seen -or -not $_.lobby_jump_seen -or -not $_.lobby_crouch_seen -or -not $_.lobby_sprint_seen -or -not $_.cosmetics_synced })
        $testFailed = $testFailed -or $incomplete.Count -gt 0
    }
} catch {
    $testFailed = $true
    $harnessErrors.Add($_.Exception.Message)
} finally {
    foreach ($entry in $processInfo) {
        $proc = $entry.process
        $stopRequested = $false
        $termination = 'natural'
        $exitCode = $null
        $exited = $false
        $cleanupError = ''
        try {
            $proc.Refresh()
            if (-not $proc.HasExited) {
                $stopRequested = $true
                $termination = if ($entry.kind -eq 'server') { 'harness_server_stop' } elseif ($entry.timed_out) { 'client_timeout' } else { 'harness_client_cleanup' }
                Stop-Process -Id $proc.Id -Force
                if (-not $proc.WaitForExit(5000)) { throw "Process $($entry.label) did not stop after cleanup" }
            }
            $proc.WaitForExit()
            $proc.Refresh()
            $exited = $proc.HasExited
            if ($exited) { $exitCode = $proc.ExitCode }
        } catch {
            $cleanupError = $_.Exception.Message
            $harnessErrors.Add($cleanupError)
            $testFailed = $true
        }
        $processResults.Add([ordered]@{label=$entry.label;kind=$entry.kind;pid=$proc.Id;executable=$entry.executable;started_utc=$entry.started_utc;observed_exit_utc=[DateTime]::UtcNow.ToString('o');exited=$exited;exit_code=$exitCode;termination=$termination;stop_requested=$stopRequested;timed_out=[bool]$entry.timed_out;cleanup_error=$cleanupError})
    }
}
# Forced client cleanup, a missing exit code, or a nonzero natural exit is a
# failed run even if the client wrote a successful report before it crashed.
$clientResults = @($processResults | Where-Object { $_.kind -eq 'client' })
$serverResults = @($processResults | Where-Object { $_.kind -eq 'server' })
$badClients = @($clientResults | Where-Object { -not $_.exited -or $null -eq $_.exit_code -or $_.exit_code -ne 0 -or $_.timed_out -or $_.stop_requested -or $_.termination -ne 'natural' })
$badServers = @($serverResults | Where-Object { -not $_.exited -or $null -eq $_.exit_code -or -not $_.stop_requested -or $_.termination -ne 'harness_server_stop' })
$testFailed = $testFailed -or $clientResults.Count -ne $expectedClients -or $serverResults.Count -ne 1 -or $badClients.Count -gt 0 -or $badServers.Count -gt 0 -or $harnessErrors.Count -gt 0
# Read after all owned processes have stopped, including late server stderr.
$errors = @(Get-ChildItem -LiteralPath $runDir -Filter '*.stderr.log' | Where-Object { $_.Length -gt 0 } | ForEach-Object { (Get-Content -LiteralPath $_.FullName -TotalCount 16) -join "`n" })
$testFailed = $testFailed -or $errors.Count -gt 0
[ordered]@{run=$runDir;expectedClients=$expectedClients;reports=$reports;stderr=$errors;harness_errors=$harnessErrors.ToArray();processes=$processResults.ToArray();process_validation=@{passed=($badClients.Count -eq 0 -and $badServers.Count -eq 0 -and $clientResults.Count -eq $expectedClients -and $serverResults.Count -eq 1);client_exit_codes_checked=$true;server_exit_policy='Server must remain running until intentionally stopped by the harness; its forced-stop exit code is recorded, not required to be zero.'}} | ConvertTo-Json -Depth 12
if ($testFailed) { exit 1 }
exit 0
