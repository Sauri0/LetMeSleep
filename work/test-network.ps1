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
function Start-GameProcess([string]$Label, [string[]]$Extra) {
    $isExternalClient = $Label -ne 'server' -and $ClientExecutable
    $selectedExe = if ($isExternalClient) { $ClientExecutable } else { $godotExe }
    $baseArgs = @('--headless')
    if (-not $Executable -and -not $isExternalClient) { $baseArgs += @('--path', ('"' + (Join-Path $projectRoot 'game') + '"')) }
    $allArgs = $baseArgs + @('--') + $Extra
    $proc = Start-Process -FilePath $selectedExe -ArgumentList $allArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir "$Label.stdout.log") -RedirectStandardError (Join-Path $runDir "$Label.stderr.log")
    $processes.Add($proc)
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
    $reports = @(Get-ChildItem -LiteralPath $runDir -Filter '*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    $errors = @(Get-ChildItem -LiteralPath $runDir -Filter '*.stderr.log' | Where-Object { $_.Length -gt 0 } | ForEach-Object { (Get-Content -LiteralPath $_.FullName -TotalCount 16) -join "`n" })
    [ordered]@{ run = $runDir; expectedClients = $(if ($Incompatible) { 1 } else { $Humans + $Mosquitoes }); reports = $reports; stderr = $errors } | ConvertTo-Json -Depth 12
    $expectedClients = if ($Incompatible) { 1 } else { $Humans + $Mosquitoes }
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
} finally {
    foreach ($proc in $processes) { if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force } }
}
if ($testFailed) { exit 1 }
