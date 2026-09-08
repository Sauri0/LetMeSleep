#Requires -Version 7.0
param(
    [string]$PlanPath=(Join-Path $PSScriptRoot 'gallery09-plan.json'),
    [int]$FirstJob=0,
    [ValidateRange(1,144)][int]$MaxJobs=1,
    [switch]$Execute
)
# Offline listing is the default. Execute only after the shared native/import
# reservation is granted. One owned Godot process at a time, <=55 seconds each.
$ErrorActionPreference='Stop'
$plan=Get-Content -LiteralPath $PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$planHash=(Get-FileHash -LiteralPath $PlanPath -Algorithm SHA256).Hash.ToLowerInvariant()
$validator=Join-Path $PSScriptRoot 'gallery09-manifest.py'
if ($FirstJob -lt 0 -or $FirstJob -ge $plan.jobs.Count) { throw 'Job index outside full plan.' }
$last=[Math]::Min($plan.jobs.Count,$FirstJob+$MaxJobs)
foreach ($job in $plan.jobs[$FirstJob..($last-1)]) {
    if (-not $Execute) { Write-Output ('PLANNED {0}: {1}, heads {2}-{3}, {4} tiles, review pending' -f $job.index,$job.id,$job.first,$job.last,$job.expected_tiles); continue }
    & python $validator verify-inputs --plan $PlanPath
    if ($LASTEXITCODE -ne 0) { throw 'Frozen inputs changed.' }
    $folder=[IO.Path]::GetFullPath($job.output)
    $recordPath=Join-Path $folder 'run.json'
    if (Test-Path -LiteralPath $recordPath) {
        $previous=Get-Content -LiteralPath $recordPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        if (-not $previous.passed) { throw ('Prior failed attempt preserved; use a new output plan: '+$job.id) }
        $priorValidation=Get-Content -LiteralPath (Join-Path $folder 'validated.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        if ($previous.exe_sha256 -ne $plan.exe_sha256 -or $previous.plan_sha256 -ne $planHash) { throw 'Existing run uses another executable or frozen plan.' }
        foreach ($relative in $plan.source_sha256.Keys) {
            if ($priorValidation.source_sha256[$relative] -ne $plan.source_sha256[$relative]) { throw ('Existing capture used another source/import: '+$relative) }
        }
        foreach ($sheet in $priorValidation.sheets) {
            if ((Get-FileHash -LiteralPath (Join-Path $folder $sheet.file) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sheet.sha256) { throw ('Changed existing sheet: '+$sheet.file) }
        }
        if ((Get-FileHash -LiteralPath (Join-Path $folder $job.expected_report) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $priorValidation.report_sha256) { throw 'Changed existing page JSON.' }
        & python $validator validate-page --plan $PlanPath --job $job.index
        if ($LASTEXITCODE -ne 0) { throw 'Existing capture does not satisfy full plan.' }
        Write-Output ('RESUMED_CAPTURE_ONLY '+$job.id)
        continue
    }
    if (Test-Path -LiteralPath $folder) { throw ('Unrecorded prior outputs preserved; choose a new plan: '+$folder) }
    $active=Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Godot*' -or $_.ProcessName -like 'Let-me-sleep*' }
    if ($active) { throw 'Another Godot/game process exists. Coordinate the native reservation; do not kill it.' }
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $argsList=@('--path',('"'+(Join-Path $plan.root 'game')+'"'),'--rendering-method','gl_compatibility','--script','res://tests/facial_catalog08_gallery.gd','--max-fps','60','--',('--role='+$job.role),('--page='+$job.page),('--states='+($job.states -join ',')),('"--output='+$folder+'"'))
    if ($null -ne $job.head) { $argsList+=('--head='+$job.head) }
    $started=[DateTime]::UtcNow
    $record=[ordered]@{job=$job.id;plan_sha256=$planHash;exe_sha256=$plan.exe_sha256;source_mode=$true;started_utc=$started.ToString('o');arguments=$argsList;timeout_seconds=55;passed=$false;visual_review_status='pending'}
    $owned=$null
    try {
        $owned=Start-Process -FilePath $plan.exe -ArgumentList $argsList -WorkingDirectory $plan.root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'native.log') -RedirectStandardError (Join-Path $folder 'native.err')
        $handle=$owned.Handle
        if (-not $owned.WaitForExit(55000)) { throw 'Page exceeded 55 seconds.' }
        $owned.Refresh();$record.exit_code=$owned.ExitCode
        if ($null -eq $owned.ExitCode -or $owned.ExitCode -ne 0) { throw 'Nonzero or unavailable Godot exit code.' }
        if ((Get-Item -LiteralPath (Join-Path $folder 'native.err')).Length -gt 0) { throw 'Godot stderr is nonempty.' }
        if (Select-String -LiteralPath (Join-Path $folder 'native.log') -Pattern 'SCRIPT ERROR:|^ERROR:|failures=[1-9]' -Quiet) { throw 'Godot reported a failure.' }
        & python $validator validate-page --plan $PlanPath --job $job.index
        if ($LASTEXITCODE -ne 0) { throw 'Capture or source validation failed.' }
        $record.passed=$true
    } catch {
        $record.failure=$_.Exception.Message
        throw
    } finally {
        if ($null -ne $owned -and -not $owned.HasExited) { Stop-Process -Id $owned.Id -Force }
        $record.finished_utc=[DateTime]::UtcNow.ToString('o')
        $record.elapsed_seconds=([DateTime]::UtcNow-$started).TotalSeconds
        $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    }
    Write-Output ('CAPTURED {0} {1:N2}s; visual review pending' -f $job.id,$record.elapsed_seconds)
}
