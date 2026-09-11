param(
    [ValidateSet('import', 'presentation', 'legacy', 'camera', 'manual', 'visual', 'attachment', 'contact')]
    [string]$Mode = 'presentation',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')]
    [string]$RunName = 'run1'
)
$ErrorActionPreference = 'Stop'
# Only execute after Director grants the shared engine slot.
$motionRepo = Split-Path -Parent $PSScriptRoot
$motionEngine = 'C:\Users\brank\Documents\Codex\2026-09-06\dejame-dormir\work\tools\godot-4.5.2\Godot_v4.5.2-stable_win64_console.exe'
$motionPrefix = Join-Path $PSScriptRoot "motion091-$Mode-$RunName"
$motionArgs = [System.Collections.Generic.List[string]]::new()
if ($Mode -eq 'import') {
    foreach ($item in @('--headless', '--path', (Join-Path $motionRepo 'game'), '--editor', '--import', '--quit', '--frame-delay', '800')) { $motionArgs.Add($item) }
} else {
    $motionScripts = @{
        presentation = 'motion091_presentation_test.gd'
        legacy = 'human09_presentation_test.gd'
        camera = 'camera_turn_checks.gd'
        manual = 'manual_defense_test.gd'
        visual = 'motion091_camera_visual.gd'
        attachment = 'motion091_attachment_test.gd'
        contact = 'contact_orientation06_test.gd'
    }
    foreach ($item in @('--path', (Join-Path $motionRepo 'game'), '--audio-driver', 'Dummy', '--script', ('res://tests/' + $motionScripts[$Mode]), '--', ('--report=' + $motionPrefix + '.json'))) { $motionArgs.Add($item) }
}
$motionStart = [System.Diagnostics.ProcessStartInfo]::new()
$motionStart.FileName = $motionEngine
$motionStart.WorkingDirectory = $motionRepo
$motionStart.UseShellExecute = $false
$motionStart.CreateNoWindow = $true
$motionStart.RedirectStandardOutput = $true
$motionStart.RedirectStandardError = $true
foreach ($item in $motionArgs) { $motionStart.ArgumentList.Add($item) }
$motionProcess = [System.Diagnostics.Process]::new()
$motionProcess.StartInfo = $motionStart
$motionStartedAt = [DateTime]::UtcNow
$null = $motionProcess.Start()
$motionOut = $motionProcess.StandardOutput.ReadToEndAsync()
$motionErr = $motionProcess.StandardError.ReadToEndAsync()
$motionFinished = $motionProcess.WaitForExit(55000)
if (-not $motionFinished) { $motionProcess.Kill($true); $motionProcess.WaitForExit() }
$motionStdout = $motionOut.GetAwaiter().GetResult()
$motionStderr = $motionErr.GetAwaiter().GetResult()
[IO.File]::WriteAllText($motionPrefix + '.stdout.log', $motionStdout)
[IO.File]::WriteAllText($motionPrefix + '.stderr.log', $motionStderr)
$motionResult = [ordered]@{
    mode = $Mode
    cwd = $motionRepo
    started_utc = $motionStartedAt.ToString('o')
    finished_utc = [DateTime]::UtcNow.ToString('o')
    exit_code = $motionProcess.ExitCode
    timed_out = -not $motionFinished
    stderr_bytes = [Text.Encoding]::UTF8.GetByteCount($motionStderr)
    stdout_log = $motionPrefix + '.stdout.log'
    stderr_log = $motionPrefix + '.stderr.log'
}
$motionResult | ConvertTo-Json | Set-Content -LiteralPath ($motionPrefix + '.run.json')
$motionResult | ConvertTo-Json
if (-not $motionFinished -or $motionProcess.ExitCode -ne 0) { exit 1 }
