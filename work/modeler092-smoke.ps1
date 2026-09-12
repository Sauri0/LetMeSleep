param([int]$BudgetSeconds = 120, [switch]$SkipImport, [switch]$FurnishingFirst, [switch]$FurnishingOnly, [int]$Seed = 1)
$ErrorActionPreference = 'Stop'
if ($BudgetSeconds -lt 1 -or $BudgetSeconds -gt 180) { throw 'Budget must be 1..180 seconds.' }
$repoPath = Split-Path -Parent $PSScriptRoot
$enginePath = 'C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$occupied = Get-Process -Name '*Godot*','*blender*' -ErrorAction SilentlyContinue
if ($occupied) { throw 'Godot/Blender is already running; no process started.' }
$runStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$watch = [Diagnostics.Stopwatch]::StartNew()
$steps = @(
    @{ name='import'; args='--headless --path game --editor --import --quit --single-threaded-scene' },
    @{ name='contracts'; args='--headless --path game --script res://tests/modeler092_contract_test.gd --single-threaded-scene' },
    @{ name='structure'; args='--headless --path game --script res://tests/modeler092_structure_test.gd --single-threaded-scene' },
    @{ name='furnishing'; args="--headless --path game --script res://tests/modeler092_furnishing_test.gd --single-threaded-scene -- --seed=$Seed --report=res://../work/modeler092-furnishing-seed-$Seed.json" }
)
if ($SkipImport) { $steps=@($steps | Where-Object { $_.name -ne 'import' }) }
if ($FurnishingFirst) { $steps=@($steps | Where-Object { $_.name -eq 'furnishing' }) + @($steps | Where-Object { $_.name -ne 'furnishing' }) }
if ($FurnishingOnly) { $steps=@($steps | Where-Object { $_.name -eq 'furnishing' }) }
$records = [Collections.Generic.List[object]]::new()
$passed = $true
foreach ($step in $steps) {
    $remaining = $BudgetSeconds * 1000 - [int]$watch.ElapsedMilliseconds
    if ($remaining -le 0) { $passed=$false; break }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName=$enginePath
    $info.WorkingDirectory=$repoPath
    $info.Arguments=$step.args
    $info.UseShellExecute=$false
    $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true
    $info.RedirectStandardError=$true
    $process=[Diagnostics.Process]::new()
    $process.StartInfo=$info
    $null=$process.Start()
    $outputRead=$process.StandardOutput.ReadToEndAsync()
    $errorRead=$process.StandardError.ReadToEndAsync()
    $finished=$process.WaitForExit([Math]::Min(55000,$remaining))
    if (-not $finished) { $process.Kill(); $process.WaitForExit() }
    $stdout=$outputRead.Result
    $stderr=$errorRead.Result
    $prefix=Join-Path $PSScriptRoot ('modeler092-smoke-'+$runStamp+'-'+$step.name)
    [IO.File]::WriteAllText($prefix+'.stdout.log',$stdout)
    [IO.File]::WriteAllText($prefix+'.stderr.log',$stderr)
    $errorText=($stdout+$stderr) -match 'SCRIPT ERROR|Parse Error|Compile Error|ERROR:'
    $record=@{ step=$step.name; exit=$process.ExitCode; timeout=(-not $finished); error_text=$errorText; stderr_chars=$stderr.Length; elapsed_total_ms=$watch.ElapsedMilliseconds; stdout=$prefix+'.stdout.log'; stderr=$prefix+'.stderr.log' }
    $records.Add($record)
    Write-Output ($record | ConvertTo-Json -Compress)
    ($stdout+$stderr) -split "`n" | Select-String -Pattern 'MODELER|FAIL|SCRIPT ERROR|Parse Error|Compile Error|ERROR:' | Select-Object -First 25
    if (-not $finished -or $process.ExitCode -ne 0 -or $errorText) { $passed=$false; break }
}
$result=@{ passed=($passed -and $records.Count -eq $steps.Count); steps=$records.ToArray(); budget_seconds=$BudgetSeconds; elapsed_ms=$watch.ElapsedMilliseconds }
$report=Join-Path $PSScriptRoot ('modeler092-smoke-'+$runStamp+'.json')
[IO.File]::WriteAllText($report,($result | ConvertTo-Json -Depth 8))
Write-Output ('REPORT '+$report)
if (-not $result.passed) { exit 1 }
exit 0
