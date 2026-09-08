param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedExeSha256,
    [switch]$RecordOnly
)

# Run only after the release owner reserves GPU/CPU and freezes this exact EXE.
# The menu fixture temporarily uses a documented mix, then restores preferences.
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $taskRoot 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe'
$actualSha=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if ($actualSha -ne $ExpectedExeSha256) { throw 'The executable does not match the frozen SHA256.' }
$runId='release07-movie-'+(Get-Date -Format 'yyyyMMdd-HHmmss')
$captureDir=Join-Path $PSScriptRoot $runId
$previewDir=Join-Path $taskRoot 'outputs/0.7-preview'
New-Item -ItemType Directory -Force -Path $captureDir,$previewDir | Out-Null
$prefsPath=Join-Path $env:APPDATA 'Godot/app_userdata/Let me sleep/preferences.cfg'
$prefsBefore=if(Test-Path -LiteralPath $prefsPath){(Get-FileHash -LiteralPath $prefsPath -Algorithm SHA256).Hash}else{'absent'}
$prefsExisted=Test-Path -LiteralPath $prefsPath
[byte[]]$prefsOriginalBytes=@()
if($prefsExisted){$prefsOriginalBytes=[IO.File]::ReadAllBytes($prefsPath)}
$captures=@()

try {
foreach($case in @(
    @{Name='tools-demo';Fixture='tools07_demo';ExpectedChecks=15;File='Let-me-sleep-0.7-objetos.mp4'},
    @{Name='customization-demo';Fixture='customization07_demo';ExpectedChecks=27;File='Let-me-sleep-0.7-tu-pinta.mp4'}
)) {
    $movie=Join-Path $captureDir ($case.Name+'.avi')
    $report=Join-Path $PSScriptRoot ('release07-'+$case.Name+'.json')
    $stdout=Join-Path $PSScriptRoot ('release07-'+$case.Name+'.log')
    $stderr=Join-Path $PSScriptRoot ('release07-'+$case.Name+'.err')
    $argsList=@('--script',('res://tests/'+$case.Fixture+'.gd'),'--fixed-fps','30','--write-movie',('"'+$movie+'"'),'--',('"--demo-report='+$report+'"'))
    $owned=Start-Process -FilePath $exe -ArgumentList $argsList -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    try {
        # Poll this owned process in short bounded waits; MovieWriter is not a
        # performance run and can take longer than the movie's screen time.
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while(-not $owned.WaitForExit(10000)) {
            if($timer.Elapsed.TotalSeconds -gt 180) { throw ('Capture timed out: '+$case.Name) }
        }
        $owned.Refresh()
        if($owned.ExitCode -ne 0) { throw ('Fixture exit '+$owned.ExitCode+': '+$case.Name) }
        if((Get-Item -LiteralPath $stderr).Length -gt 0) { throw ('Fixture stderr: '+$stderr) }
        if(Select-String -LiteralPath $stdout -Pattern 'SCRIPT ERROR:|^ERROR:|failures=[1-9]' -Quiet) { throw ('Fixture log failure: '+$stdout) }
        $data=Get-Content -Raw -LiteralPath $report | ConvertFrom-Json
        $failureCount=if($case.Name -eq 'customization-demo'){@($data.failures).Count}else{[int]$data.failures}
        if($failureCount -ne 0 -or $data.checks -ne $case.ExpectedChecks) { throw ('Unexpected checks: '+$case.Name) }
        if(-not $data.staged_control_demo -or $data.preferences_modified) { throw ('Invalid demonstration metadata: '+$case.Name) }
        if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $actualSha) { throw 'EXE changed during capture.' }
        $media=(& ffprobe -v error -show_streams -show_format -of json $movie | Out-String) | ConvertFrom-Json
        if($LASTEXITCODE -ne 0) { throw ('Could not inspect capture: '+$movie) }
        $video=@($media.streams | Where-Object codec_type -eq 'video')[0]
        $audio=@($media.streams | Where-Object codec_type -eq 'audio')[0]
        if($video.width -ne 1280 -or $video.height -ne 720 -or $video.avg_frame_rate -ne '30/1' -or $audio.channels -ne 2) { throw ('Unexpected capture streams: '+$movie) }
        $capture=[ordered]@{file=$movie;output_file=$case.File;exe_sha256=$actualSha;fixture=$case.Fixture;duration_s=[double]$media.format.duration;video_codec=$video.codec_name;audio_codec=$audio.codec_name;fps=30;resolution=@(1280,720);audio_replaced=$false;benchmark=$false;prepared_menu_mix=($case.Name -eq 'customization-demo')}
        $data | Add-Member -NotePropertyName capture -NotePropertyValue $capture -Force
        $data | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $report -Encoding utf8
        $captures+=$capture
        Get-Content -LiteralPath $stdout -Tail 3
    } finally {
        if(-not $owned.HasExited) { Stop-Process -Id $owned.Id -Force }
    }
}
$prefsAfter=if(Test-Path -LiteralPath $prefsPath){(Get-FileHash -LiteralPath $prefsPath -Algorithm SHA256).Hash}else{'absent'}
if($prefsBefore -ne $prefsAfter) { throw 'Preferences changed during the recording window.' }
} finally {
    # Also restore after a failed or timed-out fixture, before propagating error.
    if($prefsExisted){[IO.File]::WriteAllBytes($prefsPath,$prefsOriginalBytes)}
    elseif(Test-Path -LiteralPath $prefsPath){Remove-Item -LiteralPath $prefsPath}
}
$record=[ordered]@{run=$runId;exe_sha256=$actualSha;preferences_sha256_before=$prefsBefore;preferences_sha256_after=$prefsAfter;captures=$captures;label='Prepared situations using real Client input and authority; MovieWriter audio; not a performance benchmark'}
$record | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'release07-video-capture.json') -Encoding utf8
if($RecordOnly) { Write-Output ('CAPTURE_READY '+$captureDir); exit 0 }

foreach($capture in $captures){
$output=Join-Path $previewDir $capture.output_file
& ffmpeg -hide_banner -loglevel error -y -i $capture.file -map '0:v:0' -map '0:a:0' -c:v libx264 -threads 4 -preset medium -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart $output
if($LASTEXITCODE -ne 0) { throw 'FFmpeg could not assemble the final movie.' }
& ffprobe -v error -show_entries 'stream=codec_name,codec_type,width,height,avg_frame_rate,channels,sample_rate:format=duration,size' -of json $output
if($LASTEXITCODE -ne 0) { throw 'Final MP4 inspection failed.' }
Write-Output ('VIDEO_READY '+$output)
}
