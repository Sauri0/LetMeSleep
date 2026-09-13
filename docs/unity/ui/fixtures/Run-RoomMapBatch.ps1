param([string]$ProjectPath='N:/LetMeSleep/Repository/unity')
$ErrorActionPreference='Stop'
$batchRoot='N:/LetMeSleep/Validation/Higgsfield/RoomUI'
$batchOutput=Join-Path $batchRoot ('run-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $batchOutput | Out-Null
$batchEditor='N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe'
$batchDll="$batchRoot/bin/HiggsfieldRoomUIBatch.dll"
$batchConfig="$batchRoot/config.json"
foreach($batchFile in @($batchEditor,$batchDll,$batchConfig)){if(!(Test-Path -LiteralPath $batchFile)){throw "Missing $batchFile"}}
$batchInfo=[System.Diagnostics.ProcessStartInfo]::new()
$batchInfo.FileName=$batchEditor;$batchInfo.UseShellExecute=$false;$batchInfo.CreateNoWindow=$true
foreach($batchArg in @('-batchmode','-projectPath',$ProjectPath,'-monitor','1','-executeMethod',
    'LetMeSleep.Content.Editor.Higgsfield.HiggsfieldExternalFixture.Run',
    '-higgsfieldFixtureAssembly',$batchDll,'-higgsfieldFixtureConfig',$batchConfig,
    '-higgsfieldFixtureOutput',$batchOutput,'-logFile',"$batchOutput/editor.log")){$batchInfo.ArgumentList.Add($batchArg)}
$originalSettingsFile=Join-Path $ProjectPath 'ProjectSettings/EditorSettings.asset'
$originalSettingsHash=(Get-FileHash -LiteralPath $originalSettingsFile -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{path=$originalSettingsFile;sha256=$originalSettingsHash}|ConvertTo-Json|Set-Content -LiteralPath "$batchOutput/editor-settings-before.json" -Encoding utf8
$batchProcess=[System.Diagnostics.Process]::Start($batchInfo)
Write-Output "Batch PID $($batchProcess.Id). Output $batchOutput"
# Root runs only with exclusive project/GPU slot. No -quit or -nographics.
if(!$batchProcess.WaitForExit(240000)){
    throw "Batch still running: PID $($batchProcess.Id). Inspect log/process and cleanup; no second launch. $batchOutput"
}
if(!(Test-Path -LiteralPath "$batchOutput/batch.json")){throw "No fixture receipt produced; inspect $batchOutput"}
$batchReceipt=Get-Content -LiteralPath "$batchOutput/batch.json" -Raw|ConvertFrom-Json
$sourceChecks=@(foreach($source in $batchReceipt.sourceFiles){
    $current=(Get-FileHash -LiteralPath $source.path -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{path=$source.path;before=$source.sha256;after=$current;unchanged=($current -eq $source.sha256)}
})
$finalSettingsHash=(Get-FileHash -LiteralPath $originalSettingsFile -Algorithm SHA256).Hash.ToLowerInvariant()
$postExit=[ordered]@{exitCode=$batchProcess.ExitCode;sources=$sourceChecks;editorSettingsBefore=$originalSettingsHash;
    editorSettingsAfter=$finalSettingsHash;editorSettingsUnchanged=($originalSettingsHash -eq $finalSettingsHash);
    optionsRestored=$batchReceipt.optionsRestored;scope='Post-process disk hashes; no automatic source restoration.'}
$postExit|ConvertTo-Json -Depth 6|Set-Content -LiteralPath "$batchOutput/post-exit.json" -Encoding utf8
Get-Content -LiteralPath "$batchOutput/batch.json" -Raw
if($batchProcess.ExitCode -ne 0){throw "Native batch failed ($($batchProcess.ExitCode)); inspect $batchOutput"}
if(!$batchReceipt.sourcesUnchanged -or !$batchReceipt.optionsRestored -or !$postExit.editorSettingsUnchanged -or @($sourceChecks|Where-Object {!$_.unchanged}).Count -gt 0){
    throw "Isolation check failed; preserve diffs and inspect receipts. No automatic restoration. $batchOutput"
}
if($batchReceipt.state -ne 'captured-awaiting-visual-review'){throw 'Receipt is not complete; do not infer success from editor exit.'}
Write-Output "Review room-720-RT.png and room-1080-RT.png in $batchOutput"
