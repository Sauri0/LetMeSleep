#Requires -Version 7.0
param(
    [string]$PlanPath=(Join-Path $PSScriptRoot 'gallery09-fit4-framed-plan.json'),
    [switch]$Execute
)
# Offline by default. Execute only in the assigned native window after the
# fit4-framed consumer gate: this wrapper captures exactly three pages, never all 133.
$ErrorActionPreference='Stop'
$validator=Join-Path $PSScriptRoot 'gallery09-manifest.py'
$runner=Join-Path $PSScriptRoot 'gallery09-run.ps1'
$plan=Get-Content -LiteralPath $PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$selected=@(0,122,132)
$expected=@('neutral-human-000','neutral-mosquito-000','neutral-mosquito-010')
if ($plan.views.Count -ne 8 -or $plan.neutral_pages -ne 133) { throw 'Calibration requires the full eight-view plan.' }
& python -B $validator verify-inputs --plan $PlanPath
if ($LASTEXITCODE -ne 0) { throw 'Plan inputs or imported models changed. Prepare a new frozen plan.' }
$rows=@()
for ($index=0;$index -lt $selected.Count;$index++) {
    $job=$plan.jobs[$selected[$index]]
    if ($job.id -ne $expected[$index]) { throw 'Unexpected calibration index/domain.' }
    & $runner -PlanPath $PlanPath -FirstJob $job.index -MaxJobs 1 -Execute:$Execute
    if ($Execute) {
        $run=Get-Content -LiteralPath (Join-Path $job.output 'run.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        $validated=Get-Content -LiteralPath (Join-Path $job.output 'validated.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        $rows+= [ordered]@{job=$job.id;elapsed_seconds=$run.elapsed_seconds;heads=$validated.head_count;tiles=$validated.tile_count;sheets=$validated.sheets.Count;capture_status=$validated.capture_status;visual_review_status='pending'}
    }
}
if ($Execute) {
    # The unmeasured partial human page is conservatively charged as a full one.
    $estimate=122*$rows[0].elapsed_seconds+10*$rows[1].elapsed_seconds+$rows[2].elapsed_seconds
    [ordered]@{schema=1;scope='Three native source pages only; neutral full-catalog timing is extrapolated, visual approval remains pending';plan_sha256=(Get-FileHash -LiteralPath $PlanPath -Algorithm SHA256).Hash.ToLowerInvariant();rows=$rows;neutral_extrapolated_seconds=$estimate;review_time_included=$false;all_neutral_pages_captured=$false;visual_review_status='pending'} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'gallery09-fit4-framed-calibration-result.json') -Encoding UTF8
    Write-Output ('CALIBRATED_THREE_PAGES estimate-neutral={0:N1}s; visual review pending' -f $estimate)
} else {
    Write-Output 'CALIBRATION_PREPARED_ONLY: 3 pages / 51 heads / 408 views / 12 PNG sheets. No Godot launched.'
}
