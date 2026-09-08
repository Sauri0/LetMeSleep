param([Parameter(Mandatory=$true)][string]$Name, [string[]]$GameArguments, [switch]$Source)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = if ($Source) { Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe' } else { Join-Path $projectRoot 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe' }
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid evidence name' }
$stdout = Join-Path $PSScriptRoot ('release07-' + $Name + '.log')
$stderr = Join-Path $PSScriptRoot ('release07-' + $Name + '.err')
$runRecord = Join-Path $PSScriptRoot ('release07-' + $Name + '.run.json')
# A failed retry must never inherit an earlier successful process record.
if (Test-Path -LiteralPath $runRecord) { Remove-Item -LiteralPath $runRecord }
$runHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$runStarted = [DateTime]::UtcNow.ToString('o')
if ($Source) { $GameArguments = @('--path', ('"' + (Join-Path $projectRoot 'game') + '"')) + $GameArguments }
$owned = Start-Process -FilePath $exe -ArgumentList $GameArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$retainedProcessHandle = $owned.Handle
try {
    if (-not $owned.WaitForExit(55000)) { throw ('Timed out: ' + $Name) }
    $owned.Refresh()
    Get-Content -LiteralPath $stdout -Tail 8
    if ($null -eq $owned.ExitCode -or $owned.ExitCode -ne 0) { throw ('Check failed: ' + $Name + ', unavailable/nonzero exit ' + $owned.ExitCode) }
    if ((Get-Item -LiteralPath $stderr).Length -gt 0) { Get-Content -LiteralPath $stderr -TotalCount 12; throw ('stderr: ' + $Name) }
    if (Select-String -LiteralPath $stdout -Pattern 'SCRIPT ERROR:|^ERROR:|failures=[1-9]' -Quiet) { throw ('Log failure: ' + $Name) }
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $runHash) { throw ('Executable changed: ' + $Name) }
    [ordered]@{name=$Name; exe_sha256=$runHash; source=[bool]$Source; arguments=$GameArguments; started_utc=$runStarted; finished_utc=[DateTime]::UtcNow.ToString('o'); exit_code=$owned.ExitCode; stderr_bytes=(Get-Item -LiteralPath $stderr).Length; passed=$true} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $runRecord -Encoding utf8
} finally {
    if (-not $owned.HasExited) { Stop-Process -Id $owned.Id -Force }
}
