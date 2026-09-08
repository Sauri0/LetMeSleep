[CmdletBinding()]
param(
    [string]$Executable = '',
    [switch]$Packed,
    [string]$OutputDirectory = '',
    [ValidateRange(1,243)][int]$HumanChunkSize = 27,
    [ValidateRange(1,42)][int]$MosquitoChunkSize = 21,
    [ValidateRange(1,55)][int]$TimeoutSeconds = 55
)
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Executable)) {
    $Executable = Join-Path $projectRoot 'work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
}
$exePath = (Resolve-Path -LiteralPath $Executable).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot ('work/facial-motion08-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) {
    if (@(Get-ChildItem -LiteralPath $outputPath -Force).Count -gt 0) {
        throw "Output directory must be empty; previous evidence is preserved: $outputPath"
    }
} else {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}
$indexPath = Join-Path $outputPath 'index.json'
$fixturePath = Join-Path $projectRoot 'game/tests/facial_parts08_motion_export.gd'
$report = [ordered]@{
    format = 'LMS_FACIAL_MOTION_08_INDEX'
    version = 1
    complete = $false
    expected_totals = [ordered]@{human = 243; mosquito = 42}
    executable = $exePath
    executable_sha256 = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
    fixture = $fixturePath
    fixture_sha256 = (Get-FileHash -LiteralPath $fixturePath -Algorithm SHA256).Hash
    packed = [bool]$Packed
    max_process_seconds = $TimeoutSeconds
    started_utc = [DateTime]::UtcNow.ToString('o')
    parts = [System.Collections.Generic.List[object]]::new()
    failures = [System.Collections.Generic.List[string]]::new()
    limits = @(
        'Every requested case must be present once across verified parts; no intersection verdict is inferred from a successful export.',
        'Geometry IDs are local to each part; resolve through that part topologies/geometries.',
        'Finite conservative poses and seeded expression witnesses do not prove continuous-motion or full morph-domain clearance.'
    )
}
$seen = [System.Collections.Generic.HashSet[string]]::new()
$referenceSources = $null
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Save-Index {
    $report.updated_utc = [DateTime]::UtcNow.ToString('o')
    [System.IO.File]::WriteAllText($indexPath,($report | ConvertTo-Json -Depth 30),$utf8)
}

function Assert-True([bool]$Value,[string]$Message) {
    if (-not $Value) { throw $Message }
}

try {
    foreach ($role in @('human','mosquito')) {
        $total = [int]$report.expected_totals[$role]
        $chunkSize = if ($role -eq 'human') { $HumanChunkSize } else { $MosquitoChunkSize }
        for ($start = 0; $start -lt $total; $start += $chunkSize) {
            $count = [Math]::Min($chunkSize,$total-$start)
            $label = '{0}-{1:D3}-{2:D3}' -f $role,$start,($start+$count-1)
            $partPath = Join-Path $outputPath ($label + '.json')
            $stdoutPath = Join-Path $outputPath ($label + '.stdout.log')
            $stderrPath = Join-Path $outputPath ($label + '.stderr.log')
            $arguments = [System.Collections.Generic.List[string]]::new()
            if (-not $Packed) { $arguments.Add('--path'); $arguments.Add('"' + (Join-Path $projectRoot 'game') + '"') }
            $arguments.Add('--script'); $arguments.Add('res://tests/facial_parts08_motion_export.gd')
            $arguments.Add('--'); $arguments.Add('--roles=' + $role)
            $arguments.Add('--case-start=' + $start); $arguments.Add('--case-count=' + $count)
            $arguments.Add('"--output=' + $partPath + '"')
            $part = [ordered]@{label=$label; role=$role; start=$start; count=$count; file=$partPath; stdout=$stdoutPath; stderr=$stderrPath; verified=$false; pid=$null; exit_code=$null; timed_out=$false; elapsed_seconds=$null}
            $report.parts.Add($part)
            Save-Index
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $ownedProcess = Start-Process -FilePath $exePath -ArgumentList $arguments.ToArray() -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
            # Windows PowerShell 5 may lose the native exit status if the lazy
            # process handle is first opened after termination. Retain it now.
            $retainedHandle = $ownedProcess.Handle
            $part.pid = $ownedProcess.Id
            if (-not $ownedProcess.WaitForExit($TimeoutSeconds*1000)) {
                $part.timed_out = $true
                Stop-Process -Id $ownedProcess.Id -ErrorAction SilentlyContinue
                [void]$ownedProcess.WaitForExit(3000)
            }
            if ($ownedProcess.HasExited) { $ownedProcess.WaitForExit() }
            if ($ownedProcess.HasExited) { $part.exit_code = $ownedProcess.ExitCode }
            $watch.Stop(); $part.elapsed_seconds = $watch.Elapsed.TotalSeconds
            Save-Index
            Assert-True (-not $part.timed_out) ('Process timeout: ' + $label)
            Assert-True ($null -ne $part.exit_code -and [int]$part.exit_code -eq 0) ('Nonzero or unavailable process exit: ' + $label)
            Assert-True ((Get-Item -LiteralPath $stderrPath).Length -eq 0) ('Nonempty stderr: ' + $label)
            $stdout = Get-Content -LiteralPath $stdoutPath -Raw
            Assert-True ($stdout -match 'FACIAL_MOTION08_RESULT .* failures=0 ') ('Missing success marker: ' + $label)
            Assert-True (-not ($stdout -match 'FACIAL_MOTION08_FAIL|SCRIPT ERROR|ERROR:')) ('Failure output: ' + $label)
            $sidecarPath = $partPath + '.index.json'
            Assert-True (Test-Path -LiteralPath $sidecarPath) ('Missing coverage sidecar: ' + $label)
            $sidecar = Get-Content -LiteralPath $sidecarPath -Raw | ConvertFrom-Json
            Assert-True ($sidecar.format -eq 'LMS_FACIAL_MOTION_08_PART') ('Unexpected sidecar format: ' + $label)
            Assert-True ([bool]$sidecar.coverage.span_complete -and @($sidecar.summary.failures).Count -eq 0) ('Unverified export span: ' + $label)
            Assert-True ([int]$sidecar.summary.cases -eq $count) ('Incorrect exported case count: ' + $label)
            Assert-True ([double]$sidecar.summary.max_base_error_m -le [double]$sidecar.summary.base_tolerance_m) ('Native skinning mismatch: ' + $label)
            $range = $sidecar.coverage.requested_ranges.$role
            Assert-True ([int]$range.start -eq $start -and [int]$range.end_exclusive -eq ($start+$count) -and [int]$range.total -eq $total) ('Incorrect requested span: ' + $label)
            $ids = @($sidecar.coverage.case_ids)
            Assert-True ($ids.Count -eq $count) ('Incorrect case ID count: ' + $label)
            for ($i=0; $i -lt $count; $i++) {
                $expectedId = $role + '-' + ($start+$i)
                Assert-True ([string]$ids[$i] -eq $expectedId) ('Incorrect case ID: ' + $label)
                Assert-True ($seen.Add($expectedId)) ('Duplicate case ID: ' + $expectedId)
            }
            $sources = $sidecar.source_sha256 | ConvertTo-Json -Compress -Depth 10
            if ($null -eq $referenceSources) { $referenceSources = $sources; $report.source_sha256 = $sidecar.source_sha256 }
            Assert-True ($sources -ceq $referenceSources) ('Sources changed between parts: ' + $label)
            $hash = (Get-FileHash -LiteralPath $partPath -Algorithm SHA256).Hash
            Assert-True ($hash -ieq [string]$sidecar.sha256) ('Part SHA mismatch: ' + $label)
            Assert-True ((Get-Item -LiteralPath $partPath).Length -eq [long]$sidecar.bytes) ('Part size mismatch: ' + $label)
            $part.sha256 = $hash; $part.bytes = [long]$sidecar.bytes
            $part.coverage = $sidecar.coverage; $part.summary = $sidecar.summary
            $part.index = $sidecarPath; $part.verified = $true
            $ownedProcess.Dispose()
            Save-Index
            Write-Output ('FACIAL_MOTION08_PART_OK {0} cases={1} seconds={2:N2} bytes={3}' -f $label,$count,$part.elapsed_seconds,$part.bytes)
        }
    }
    Assert-True ($seen.Count -eq 285) 'Combined case union is incomplete'
    Assert-True ((Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash -eq $report.executable_sha256) 'Executable changed during export'
    Assert-True ((Get-FileHash -LiteralPath $fixturePath -Algorithm SHA256).Hash -eq $report.fixture_sha256) 'Fixture changed during export'
    $report.complete = $true
    $report.case_count = $seen.Count
    $report.finished_utc = [DateTime]::UtcNow.ToString('o')
    Save-Index
    Write-Output ('FACIAL_MOTION08_INDEX_OK cases=285 parts={0} index={1}' -f $report.parts.Count,$indexPath)
    exit 0
} catch {
    $report.failures.Add($_.Exception.Message)
    $report.complete = $false
    Save-Index
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
