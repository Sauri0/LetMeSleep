param([Parameter(Mandatory=$true)][string]$Name, [string[]]$GameArguments)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $projectRoot 'outputs/Let-me-sleep-0.6.0-Windows/Let-me-sleep.exe'
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid evidence name' }
$stdout = Join-Path $PSScriptRoot ('release06-' + $Name + '.log')
$stderr = Join-Path $PSScriptRoot ('release06-' + $Name + '.err')
$owned = Start-Process -FilePath $exe -ArgumentList $GameArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    if (-not $owned.WaitForExit(55000)) { throw ('Timed out: ' + $Name) }
    $owned.Refresh()
    Get-Content -LiteralPath $stdout -Tail 8
    if ($owned.ExitCode -ne 0) { throw ('Native check failed: ' + $Name + ', exit ' + $owned.ExitCode) }
    if ((Get-Item -LiteralPath $stderr).Length -gt 0) { Get-Content -LiteralPath $stderr -TotalCount 12; throw ('Native stderr: ' + $Name) }
    if (Select-String -LiteralPath $stdout -Pattern 'SCRIPT ERROR:|^ERROR:|failures=[1-9]' -Quiet) { throw ('Native log failure: ' + $Name) }
} finally {
    if (-not $owned.HasExited) { Stop-Process -Id $owned.Id -Force }
}
