param([Parameter(Mandatory=$true)][string]$Name, [string[]]$GameArguments, [switch]$Source, [string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = & "$PSScriptRoot/resolve-game-executable.ps1" -Source:$Source -Executable $Executable
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid evidence name' }
$stdout = Join-Path $PSScriptRoot ('release07-' + $Name + '.log')
$stderr = Join-Path $PSScriptRoot ('release07-' + $Name + '.err')
$runRecord = Join-Path $PSScriptRoot ('release07-' + $Name + '.run.json')
# A failed retry must never inherit an earlier successful process record.
if (Test-Path -LiteralPath $runRecord) { Remove-Item -LiteralPath $runRecord }
$runHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$sourceIdentity = [ordered]@{}
if ($Source) {
    $sourceIdentity.commit = (& git -C $projectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source under test' }
    $sourceIdentity.trees = [ordered]@{}
    foreach ($sourceFolder in @('game','native','art_source')) {
        $treeId = (& git -C $projectRoot rev-parse ('HEAD:' + $sourceFolder)).Trim()
        if ($LASTEXITCODE -ne 0) { throw ('Cannot identify source tree: ' + $sourceFolder) }
        $sourceIdentity.trees[$sourceFolder] = $treeId
    }
    & git -c core.safecrlf=false -C $projectRoot diff --quiet HEAD -- game native art_source
    if ($LASTEXITCODE -gt 1) { throw 'Cannot inspect source changes' }
    $sourceIdentity.dirty = ($LASTEXITCODE -eq 1)
    $untrackedInputs = @(& git -C $projectRoot ls-files --others --exclude-standard -- game native art_source)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect untracked source' }
    $sourceIdentity.dirty = $sourceIdentity.dirty -or $untrackedInputs.Count -gt 0
    $scriptArgument = @($GameArguments | Where-Object { $_ -like 'res://*.gd' } | Select-Object -First 1)
    if ($scriptArgument.Count) {
        $sourceIdentity.test_path = $scriptArgument[0]
        $testFile = Join-Path (Join-Path $projectRoot 'game') $scriptArgument[0].Substring(6)
        $sourceIdentity.test_sha256 = (Get-FileHash -LiteralPath $testFile -Algorithm SHA256).Hash
    }
}
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
    [ordered]@{name=$Name; exe_sha256=$runHash; source=[bool]$Source; source_identity=$sourceIdentity; arguments=$GameArguments; started_utc=$runStarted; finished_utc=[DateTime]::UtcNow.ToString('o'); exit_code=$owned.ExitCode; stderr_bytes=(Get-Item -LiteralPath $stderr).Length; passed=$true} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $runRecord -Encoding utf8
} finally {
    # The Godot console executable may own a separate renderer process. Kill
    # only this run's direct child, before the parent, on timeout/error.
    if (-not $owned.HasExited) {
        $ownedChildren = @(Get-CimInstance Win32_Process -Filter ("ParentProcessId=" + $owned.Id) -ErrorAction SilentlyContinue)
        foreach ($ownedChild in $ownedChildren) {
            if ($ownedChild.CreationDate -ge [DateTime]::Parse($runStarted).ToLocalTime().AddSeconds(-1) -and $ownedChild.Name -like 'Godot*') {
                Stop-Process -Id $ownedChild.ProcessId -Force -ErrorAction SilentlyContinue
            }
        }
    }
    if (-not $owned.HasExited) { Stop-Process -Id $owned.Id -Force }
}
