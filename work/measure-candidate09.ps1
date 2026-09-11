param([string]$Executable, [string]$MapId = 'house-v2-1', [string]$EvidencePrefix = ('perf-rc09-'+(Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if($MapId -notmatch '^house(-v[1-9][0-9]*-[1-9][0-9]*)?$'){throw 'Invalid map ID.'}
if($EvidencePrefix -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a plain filename prefix.'}
$exe=& "$PSScriptRoot/resolve-game-executable.ps1" -Executable $Executable
$engineProcesses=@(Get-Process -Name 'Godot*','Let-me-sleep*' -ErrorAction SilentlyContinue)
if($engineProcesses.Count){throw 'Measurement deferred: a game/editor is already running. No process was stopped or changed.'}
$evidence=Join-Path $PSScriptRoot $EvidencePrefix
if(Test-Path -LiteralPath $evidence){throw 'Evidence already exists; choose a new prefix.'}
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$exeHash=(Get-FileHash -LiteralPath $exe).Hash
$results=@()
# The second case isolates population cost; the third diagnoses shadow cost.
# These are benchmark settings, never changes to saved player preferences.
foreach($case in @(@{name='two-players';count=2;shadows=-1},@{name='sixteen-players';count=16;shadows=-1},@{name='sixteen-without-shadows';count=16;shadows=0})){
    $report=Join-Path $evidence ($case.name+'.json')
    $arguments=@('--audio-driver','Dummy','--script','res://tests/performance07_live.gd','--',('--map-id='+$MapId),'--natural-doors','--resolution=1080',('--population='+$case.count),('--report='+$report))
    if($case.shadows -ge 0){$arguments+=('--diagnostic-shadows='+$case.shadows)}
    & "$PSScriptRoot/run-native07.ps1" -Executable $exe -Name ($EvidencePrefix+'-'+$case.name) -GameArguments $arguments
    if((Get-FileHash -LiteralPath $exe).Hash -ne $exeHash){throw 'Executable changed during measurement.'}
    $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if($data.samples -lt 30 -or $data.fps_limit -ne 0 -or $data.vsync -ne 'disabled' -or $data.rendered_map_id -ne $data.authoritative_map_id -or $data.rendered_map_id -ne $MapId){throw 'Incomplete or mismatched performance scenario.'}
    $results+=@{case=$case.name;report=([IO.Path]::GetFileName($report));frame_ms=$data.frame_ms;physics_ms=$data.physics_ms;render_cpu_ms=$data.render_cpu_ms;render_gpu_ms=$data.render_gpu_ms;quality=$data.video_quality;samples=$data.samples}
}
@{exe_sha256=$exeHash;map_id=$MapId;cases=$results;scope='Local host plus bots, same generated map, 1080p, unlimited FPS. No WAN or minimum-hardware certification. Shadow-off is diagnostic only.'} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $evidence 'summary.json')
Write-Output ('Performance evidence: '+$evidence)
