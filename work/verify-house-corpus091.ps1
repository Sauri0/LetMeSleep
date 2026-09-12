param(
    [string]$EvidencePrefix = ('house-corpus-'+(Get-Date -Format 'yyyyMMdd-HHmmss')),
    [switch]$RepeatCorpus,
    [ValidateSet(1,2,4,5,10,20,25,50,100)][int]$BatchSize = 100
)
$ErrorActionPreference = 'Stop'
if ($EvidencePrefix -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a plain evidence prefix.' }
$root = Split-Path $PSScriptRoot -Parent
$game = Join-Path $root 'game'
$engine = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
if (@(Get-Process -Name 'Godot*','Let-me-sleep*','blender*' -ErrorAction SilentlyContinue).Count) {
    throw 'Corpus deferred: another game, renderer or editor is running.'
}
$output = Join-Path $PSScriptRoot $EvidencePrefix
if (Test-Path -LiteralPath $output) { throw 'Evidence exists; choose another prefix.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$sourceCommit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision.' }
$files = @('game/tests/procedural09_corpus_checks.gd')
foreach ($name in @('procedural_house','house_validation','furniture_blueprint','navigation_geometry','door_geometry','pickup_placement','pickup_support_geometry')) {
    $files += 'game/scripts/'+$name+'.gd'
}
$sourceHashes = [ordered]@{}
foreach ($file in $files) { $sourceHashes[$file] = (Get-FileHash -LiteralPath (Join-Path $root $file)).Hash }
$runs = @()
$passes = if ($RepeatCorpus) { 2 } else { 1 }
$batchCount = 1000 / $BatchSize
for ($pass = 0; $pass -lt $passes; $pass++) {
    foreach ($batch in 0..($batchCount-1)) {
        $name = 'batch-{0:D2}{1}' -f $batch, $(if ($pass) { '-repeat' } else { '' })
        $arguments = @('--headless','--path',$game,'--script','res://tests/procedural09_corpus_checks.gd','--',('--batch='+$batch),('--batch-size='+$BatchSize),('--output='+$output))
        if ($pass) { $arguments += '--repeat' }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $engine
        $start.WorkingDirectory = $root
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $start
        $started = [DateTime]::UtcNow
        $timedOut = $false
        try {
            if (-not $process.Start()) { throw 'Cannot start corpus batch.' }
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(55000)) {
                $timedOut = $true
                $process.Kill($true)
                $process.WaitForExit()
            }
            $exitCode = $process.ExitCode
            $outText = $stdout.Result
            $errText = $stderr.Result
            [IO.File]::WriteAllText((Join-Path $output ($name+'.stdout.log')),$outText)
            [IO.File]::WriteAllText((Join-Path $output ($name+'.stderr.log')),$errText)
        } finally {
            $process.Dispose()
        }
        $record = [ordered]@{batch=$batch; repeat=[bool]$pass; started_utc=$started.ToString('o'); finished_utc=[DateTime]::UtcNow.ToString('o'); exit_code=$exitCode; timeout=$timedOut; stderr_bytes=[Text.Encoding]::UTF8.GetByteCount($errText)}
        [IO.File]::WriteAllText((Join-Path $output ($name+'.run.json')),($record | ConvertTo-Json))
        $runs += $record
        if ($timedOut -or $exitCode -ne 0 -or $errText.Length -gt 0 -or $outText -match 'SCRIPT ERROR:|(?m)^ERROR:|failures=[1-9]') {
            throw ('Corpus batch failed: '+$name+'; evidence preserved in '+$output)
        }
        Write-Output ($outText.Trim().Split("`n")[-1])
    }
}
$layouts = [Collections.Generic.HashSet[string]]::new()
$seeds = [Collections.Generic.HashSet[int]]::new()
$allCases = @()
foreach ($batch in 0..($batchCount-1)) {
    $name = 'batch-{0:D2}' -f $batch
    $data = Get-Content -LiteralPath (Join-Path $output ($name+'.json')) -Raw | ConvertFrom-Json
    if ($data.batch_size -ne $BatchSize -or $data.count -ne $BatchSize -or $data.cases.Count -ne $BatchSize -or $data.failures.Count -ne 0) { throw ('Incomplete corpus batch '+$name) }
    foreach ($property in $data.source_hashes.PSObject.Properties) {
        if ($sourceHashes['game/scripts/'+$property.Name+'.gd'] -ne $property.Value) { throw 'Corpus source hash differs from the measured source.' }
    }
    foreach ($case in $data.cases) {
        if ($case.errors.Count -ne 0 -or -not $seeds.Add([int]$case.seed) -or -not $layouts.Add([string]$case.layout_signature)) { throw 'Corpus has errors, repeated seeds or repeated structural layouts.' }
        $allCases += $case
    }
    if ($RepeatCorpus) {
        $repeat = Get-Content -LiteralPath (Join-Path $output ($name+'-repeat.json')) -Raw | ConvertFrom-Json
        if ($repeat.batch_size -ne $BatchSize -or $repeat.count -ne $BatchSize -or $repeat.cases.Count -ne $BatchSize -or $repeat.failures.Count -ne 0) { throw 'Incomplete repeat batch.' }
        foreach ($index in 0..($BatchSize-1)) {
            if ($repeat.cases[$index].seed -ne $data.cases[$index].seed -or $repeat.cases[$index].fingerprint -ne $data.cases[$index].fingerprint -or $repeat.cases[$index].layout_signature -ne $data.cases[$index].layout_signature) { throw 'Independent processes generated different geometry for the same seed.' }
        }
    }
}
foreach ($file in $files) {
    if ((Get-FileHash -LiteralPath (Join-Path $root $file)).Hash -ne $sourceHashes[$file]) { throw 'Source changed during corpus verification.' }
}
$summary = [ordered]@{source_commit=$sourceCommit; source_sha256=$sourceHashes; seeds=$seeds.Count; distinct_structural_layouts=$layouts.Count; independent_repeats=$(if ($RepeatCorpus) { $seeds.Count } else { 0 }); failures=0; runs=$runs; scope='Source geometry, layout diversity and validation only; no rendering, performance or WAN certification.'}
[IO.File]::WriteAllText((Join-Path $output 'summary.json'),($summary | ConvertTo-Json -Depth 8))
Write-Output ('Corpus verified: '+$seeds.Count+' seeds; independent repeat='+[bool]$RepeatCorpus+'; '+$output)
