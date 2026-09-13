param(
    [string]$Central='N:/LetMeSleep/Repository',
    [string]$Assembly='N:/LetMeSleep/Validation/Higgsfield/MapChecks/20260913-072404-892/bin/Debug/netstandard2.1/HiggsfieldMapChecks_20260913_072404_892.dll',
    [string]$Config='N:/LetMeSleep/Validation/Higgsfield/MapChecks/20260913-072404-892/checks.json',
    [string]$Output='N:/LetMeSleep/Validation/Higgsfield/HumanMotorCandidate/baseline-step24-01'
)
$ErrorActionPreference='Stop'
$relative='unity/Assets/LetMeSleep/Gameplay.Unity/UnityGameplayWorld.cs'
$path=Join-Path $Central $relative
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity already active'}
$expected=(& git -C $Central rev-parse ('8e9a150:'+$relative)).Trim()
$actual=(& git -C $Central hash-object --path=$relative $path).Trim()
if($actual -ne $expected){throw 'Central file differs from integrated candidate; refusing substitution'}
New-Item -ItemType Directory -Path $Output -Force|Out-Null
$backup=Join-Path $Output 'UnityGameplayWorld.candidate.bytes'
if(Test-Path -LiteralPath $backup){throw 'Backup already exists; use a fresh evidence output'}
[IO.File]::WriteAllBytes($backup,[IO.File]::ReadAllBytes($path))
$before=(Get-FileHash -LiteralPath $path).Hash
$record=[ordered]@{status='BACKED_UP';candidateCommit='8e9a150';candidateBlob=$expected;baselineCommit='8e9a150^';path=$path;backup=$backup;beforeSha256=$before;afterSha256=$null;restored=$false;pid=$null;exitCode=$null}
$receipt=Join-Path $Output 'restore-guard.json'
$record|ConvertTo-Json|Set-Content -LiteralPath $receipt
$process=$null
try {
    $baseline=(& git -C $Central show ('8e9a150^:'+$relative)) -join "`n"
    if($LASTEXITCODE -ne 0){throw 'Cannot retrieve parent motor'}
    [IO.File]::WriteAllText($path,$baseline+"`n",[Text.UTF8Encoding]::new($false))
    $parentBlob=(& git -C $Central rev-parse ('8e9a150^:'+$relative)).Trim()
    if((& git -C $Central hash-object --path=$relative $path).Trim() -ne $parentBlob){throw 'Temporary baseline source mismatch'}
    $record.status='BASELINE_RUNNING';$record.baselineBlob=$parentBlob
    $process=Start-Process -FilePath 'N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-nographics','-noaudio','-projectPath',($Central+'/unity'),'-executeMethod','LetMeSleep.Content.Editor.Higgsfield.HiggsfieldExternalFixture.Run','-higgsfieldFixtureAssembly',$Assembly,'-higgsfieldFixtureConfig',$Config,'-higgsfieldFixtureOutput',$Output,'-logFile',($Output+'/unity.log'),'-quit')
    $record.pid=$process.Id;$record|ConvertTo-Json|Set-Content -LiteralPath $receipt
    Write-Output ('BASELINE PID='+$process.Id+'; guarded restore='+$receipt)
    if(!$process.WaitForExit(180000)){ $process.Kill();$process.WaitForExit();throw 'Own baseline Unity exceeded180s and was stopped before restoration' }
    $record.exitCode=$process.ExitCode
}
finally {
    # Never restore source while our Unity process is still using the baseline.
    if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
    [IO.File]::WriteAllBytes($path,[IO.File]::ReadAllBytes($backup))
    $record.afterSha256=(Get-FileHash -LiteralPath $path).Hash
    $record.restored=$record.afterSha256 -eq $before
    $record.status=if($record.restored){'CANDIDATE_RESTORED_EXACT_BYTES'}else{'RESTORE_FAILED'}
    $record|ConvertTo-Json|Set-Content -LiteralPath $receipt
    if(!$record.restored){throw 'Candidate restore SHA mismatch'}
    Write-Output ('RESTORED SHA256='+$record.afterSha256+'; Unity exit='+$record.exitCode)
}
