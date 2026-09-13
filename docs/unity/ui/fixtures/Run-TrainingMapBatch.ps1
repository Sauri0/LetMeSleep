param([string]$ProjectPath='N:/LetMeSleep/Repository/unity')
$ErrorActionPreference='Stop'
$batchRoot='N:/LetMeSleep/Validation/Higgsfield/TrainingUI'
$batchOutput=Join-Path $batchRoot ('run-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $batchOutput | Out-Null
$batchEditor='N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe'
$batchDll="$batchRoot/bin/HiggsfieldTrainingUIBatch.dll"
$batchConfig="$batchRoot/config.json"
foreach($batchFile in @($batchEditor,$batchDll,$batchConfig)){if(!(Test-Path -LiteralPath $batchFile)){throw "Missing $batchFile"}}
$batchInfo=[System.Diagnostics.ProcessStartInfo]::new()
$batchInfo.FileName=$batchEditor;$batchInfo.UseShellExecute=$false;$batchInfo.CreateNoWindow=$true
foreach($batchArg in @('-batchmode','-projectPath',$ProjectPath,'-monitor','1','-executeMethod',
    'LetMeSleep.Content.Editor.Higgsfield.HiggsfieldExternalFixture.Run',
    '-higgsfieldFixtureAssembly',$batchDll,'-higgsfieldFixtureConfig',$batchConfig,
    '-higgsfieldFixtureOutput',$batchOutput,'-logFile',"$batchOutput/editor.log")){$batchInfo.ArgumentList.Add($batchArg)}
$batchProcess=[System.Diagnostics.Process]::Start($batchInfo)
Write-Output "Batch PID $($batchProcess.Id). Output $batchOutput"
# Root runs only with exclusive project/GPU slot. No -quit or -nographics.
if(!$batchProcess.WaitForExit(240000)){
    throw "Batch still running: PID $($batchProcess.Id). Inspect log/process and cleanup; no second launch. $batchOutput"
}
if(Test-Path -LiteralPath "$batchOutput/batch.json"){Get-Content -LiteralPath "$batchOutput/batch.json" -Raw}
if($batchProcess.ExitCode -ne 0){throw "Native batch failed ($($batchProcess.ExitCode)); inspect $batchOutput"}
if(!(Test-Path -LiteralPath "$batchOutput/batch.json")){throw 'No fixture receipt produced.'}
$batchReceipt=Get-Content -LiteralPath "$batchOutput/batch.json" -Raw|ConvertFrom-Json
if($batchReceipt.state -ne 'captured-awaiting-visual-review'){throw 'Receipt is not complete; do not infer success from editor exit.'}
Write-Output "Review training-720-RT.png and training-1080-RT.png in $batchOutput"
