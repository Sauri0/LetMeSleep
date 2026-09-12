$ErrorActionPreference = 'Stop'
$hudBase = 'C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir'
$hudMotion = Split-Path -Parent $PSScriptRoot
$hudTemporary = Join-Path $hudBase 'game/tests/motion091_hud_coalescing_isolated.gd'
if (Test-Path -LiteralPath $hudTemporary) { throw 'Isolated fixture already exists; refusing to overwrite.' }
$hudPrefix = Join-Path $PSScriptRoot 'motion091-hud-fixture-r1'
Copy-Item -LiteralPath (Join-Path $hudMotion 'game/tests/hud09_coalescing_checks.gd') -Destination $hudTemporary
try {
    $hudStart = [Diagnostics.ProcessStartInfo]::new()
    $hudStart.FileName = Join-Path $hudBase 'work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
    $hudStart.WorkingDirectory = $hudBase
    $hudStart.UseShellExecute = $false
    $hudStart.CreateNoWindow = $true
    $hudStart.RedirectStandardOutput = $true
    $hudStart.RedirectStandardError = $true
    foreach ($hudArgument in @('--path', (Join-Path $hudBase 'game'), '--audio-driver', 'Dummy', '--script', 'res://tests/motion091_hud_coalescing_isolated.gd', '--', ('--report=' + $hudPrefix + '.json'))) { $hudStart.ArgumentList.Add($hudArgument) }
    $hudProcess = [Diagnostics.Process]::new()
    $hudProcess.StartInfo = $hudStart
    $hudStarted = [DateTime]::UtcNow
    $null = $hudProcess.Start()
    $hudOut = $hudProcess.StandardOutput.ReadToEndAsync()
    $hudErr = $hudProcess.StandardError.ReadToEndAsync()
    $hudFinished = $hudProcess.WaitForExit(25000)
    if (-not $hudFinished) { $hudProcess.Kill($true); $hudProcess.WaitForExit() }
    $hudStdout = $hudOut.GetAwaiter().GetResult()
    $hudStderr = $hudErr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText($hudPrefix + '.stdout.log', $hudStdout)
    [IO.File]::WriteAllText($hudPrefix + '.stderr.log', $hudStderr)
    $hudResult = [ordered]@{ base=$hudBase; base_commit=(& git -C $hudBase rev-parse HEAD); started_utc=$hudStarted.ToString('o'); finished_utc=[DateTime]::UtcNow.ToString('o'); exit_code=$hudProcess.ExitCode; timed_out=(-not $hudFinished); stderr_bytes=[Text.Encoding]::UTF8.GetByteCount($hudStderr) }
    $hudResult | ConvertTo-Json | Set-Content -LiteralPath ($hudPrefix + '.run.json')
    $hudResult | ConvertTo-Json
} finally {
    Remove-Item -LiteralPath $hudTemporary
}
if (-not $hudFinished -or $hudProcess.ExitCode -ne 0 -or $hudStderr.Length -gt 0) { exit 1 }
