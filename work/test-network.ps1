param(
    [string]$Mode = 'blood',
    [int]$Humans = 1,
    [int]$Mosquitoes = 2,
    [int]$Port = 27940,
    [switch]$DisconnectTest,
    [switch]$RematchTest,
    [switch]$Incompatible,
    [string]$Executable = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$godotExe = if ($Executable) { $Executable } else { Join-Path $projectRoot 'work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe' }
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$runDir = Join-Path $PSScriptRoot "network-$Mode-${Humans}v${Mosquitoes}-$runId"
[System.IO.Directory]::CreateDirectory($runDir) | Out-Null
$baseArgs = @('--headless')
if (-not $Executable) { $baseArgs += @('--path', ('"' + (Join-Path $projectRoot 'game') + '"')) }
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function Start-GameProcess([string]$Label, [string[]]$Extra) {
    $allArgs = $baseArgs + @('--') + $Extra
    $proc = Start-Process -FilePath $godotExe -ArgumentList $allArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir "$Label.stdout.log") -RedirectStandardError (Join-Path $runDir "$Label.stderr.log")
    $processes.Add($proc)
    return $proc
}
try {
    $server = Start-GameProcess 'server' @('--server', "--port=$Port")
    Start-Sleep -Milliseconds 600
    $codeFile = Join-Path $runDir 'room-code.txt'
    $creatorArgs = @('--bot', '--create', '--role=human', '--name=Humano1', "--mode=$Mode", "--port=$Port", "--players=$($Humans + $Mosquitoes)", ('--code-file="' + $codeFile + '"'), ('--report="' + (Join-Path $runDir 'human1.json') + '"'))
    if ($DisconnectTest) { $creatorArgs += '--disconnect-test' }
    if ($RematchTest) { $creatorArgs += '--rematch-test' }
    if ($Incompatible) { $creatorArgs += '--incompatible' }
    $creator = Start-GameProcess 'human1' $creatorArgs
    if (-not $Incompatible) {
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath $codeFile) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
        if (-not (Test-Path -LiteralPath $codeFile)) { throw "No room code created. Inspect $runDir" }
        $code = [System.IO.File]::ReadAllText($codeFile)
        for ($i = 2; $i -le $Humans; $i++) {
            $extra = @('--bot', '--role=human', "--name=Humano$i", "--code=$code", "--port=$Port", ('--report="' + (Join-Path $runDir "human$i.json") + '"'))
            if ($DisconnectTest) { $extra += '--disconnect-test' }
            Start-GameProcess "human$i" $extra | Out-Null
        }
        for ($i = 1; $i -le $Mosquitoes; $i++) {
            $extra = @('--bot', '--role=mosquito', "--name=Mosquito$i", "--code=$code", "--port=$Port", ('--report="' + (Join-Path $runDir "mosquito$i.json") + '"'))
            if ($DisconnectTest) { $extra += if ($i -eq 1) { '--disconnect-after=6' } else { '--disconnect-test' } }
            Start-GameProcess "mosquito$i" $extra | Out-Null
        }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(55)
    while ([DateTime]::UtcNow -lt $deadline) {
        $running = @($processes | Where-Object { $_.Id -ne $server.Id -and -not $_.HasExited })
        if ($running.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    }
    $reports = @(Get-ChildItem -LiteralPath $runDir -Filter '*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    $errors = @(Get-ChildItem -LiteralPath $runDir -Filter '*.stderr.log' | Where-Object { $_.Length -gt 0 } | ForEach-Object { (Get-Content -LiteralPath $_.FullName -TotalCount 16) -join "`n" })
    [ordered]@{ run = $runDir; expectedClients = $(if ($Incompatible) { 1 } else { $Humans + $Mosquitoes }); reports = $reports; stderr = $errors } | ConvertTo-Json -Depth 12
} finally {
    foreach ($proc in $processes) { if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force } }
}
