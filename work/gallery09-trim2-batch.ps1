#Requires -Version 7.0
param(
    [ValidateRange(0,11)][int]$Batch=0,
    [switch]$Execute
)
# One explicit batch only. Stops after at most twelve jobs for coordinated review.
$ErrorActionPreference='Stop'
$planPath=Join-Path $PSScriptRoot 'gallery09-trim2-plan.json'
$plan=Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($plan.jobs.Count -ne 144 -or $plan.neutral_pages -ne 133 -or $plan.witness_jobs -ne 11) { throw 'Incomplete exhaustive gallery plan.' }
if (-not $plan.source_sha256.ContainsKey('game/scripts/human_presentation.gd')) { throw 'HumanPresentation dependency is missing.' }
$first=$Batch*12
$runner=Join-Path $PSScriptRoot 'gallery09-run.ps1'
if (-not $Execute) {
    & $runner -PlanPath $planPath -FirstJob $first -MaxJobs 12
    Write-Output ('BATCH_PREPARED_ONLY {0}; no Godot launched' -f $Batch)
    return
}
$started=[DateTime]::UtcNow
$failure=$null
try {
    & $runner -PlanPath $planPath -FirstJob $first -MaxJobs 12 -Execute
} catch {
    $failure=$_.Exception.Message
} finally {
    $rows=@()
    foreach ($job in $plan.jobs[$first..($first+11)]) {
        $recordPath=Join-Path $job.output 'run.json'
        $row=[ordered]@{index=$job.index;job=$job.id;kind=$job.kind;capture_status='pending';visual_review_status='pending'}
        if (Test-Path -LiteralPath $recordPath) {
            $record=Get-Content -LiteralPath $recordPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
            $row.capture_status=if ($record.passed) {'validated'} else {'failed'}
            $row.elapsed_seconds=$record.elapsed_seconds
            if ($record.ContainsKey('failure')) { $row.failure=$record.failure }
        }
        $rows+=$row
    }
    $folder=Split-Path -Parent $plan.jobs[0].output
    $path=Join-Path $folder ('batch-{0:D2}.json' -f $Batch)
    # Preserve every batch attempt, including a failed attempt before a retry.
    if (Test-Path -LiteralPath $path) { $path=Join-Path $folder ('batch-{0:D2}-{1}.json' -f $Batch,[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
    [ordered]@{schema=1;batch=$Batch;first_job=$first;last_job=$first+11;plan_sha256=(Get-FileHash -LiteralPath $planPath -Algorithm SHA256).Hash.ToLowerInvariant();started_utc=$started.ToString('o');elapsed_seconds=([DateTime]::UtcNow-$started).TotalSeconds;failure=$failure;jobs=$rows;automatic_next_batch=$false;visual_review_status='pending'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
    Write-Output ('BATCH_STOPPED_FOR_REVIEW {0}; report={1}' -f $Batch,$path)
}
if ($null -ne $failure) { throw $failure }
