param(
    [Parameter(Mandatory=$true)][string]$Stage,
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string[]]$Arguments,
    [int]$TimeoutSeconds=180
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskPrefix=Join-Path $PSScriptRoot ('character091-'+$Stage)
$taskInfo=[System.Diagnostics.ProcessStartInfo]::new()
$taskInfo.FileName=$Executable
$taskInfo.WorkingDirectory=$taskRoot
$taskInfo.UseShellExecute=$false
$taskInfo.CreateNoWindow=$true
$taskInfo.RedirectStandardOutput=$true
$taskInfo.RedirectStandardError=$true
foreach($taskArgument in $Arguments){$taskInfo.ArgumentList.Add($taskArgument)}
$taskProcess=[System.Diagnostics.Process]::new()
$taskProcess.StartInfo=$taskInfo
$taskStarted=[DateTime]::UtcNow
$null=$taskProcess.Start()
$taskStdout=$taskProcess.StandardOutput.ReadToEndAsync()
$taskStderr=$taskProcess.StandardError.ReadToEndAsync()
$taskTimedOut=-not $taskProcess.WaitForExit($TimeoutSeconds*1000)
if($taskTimedOut){$taskProcess.Kill($true);$taskProcess.WaitForExit()}
$taskOut=$taskStdout.GetAwaiter().GetResult()
$taskErr=$taskStderr.GetAwaiter().GetResult()
[IO.File]::WriteAllText($taskPrefix+'.log',$taskOut)
[IO.File]::WriteAllText($taskPrefix+'.err',$taskErr)
$taskResult=[ordered]@{stage=$Stage;pid=$taskProcess.Id;started_utc=$taskStarted.ToString('o');finished_utc=[DateTime]::UtcNow.ToString('o');exit_code=$taskProcess.ExitCode;timeout=$taskTimedOut;stderr_bytes=[Text.Encoding]::UTF8.GetByteCount($taskErr);executable=$Executable;arguments=$Arguments}
$taskResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($taskPrefix+'.run.json')
$taskResult | ConvertTo-Json -Compress -Depth 5
$taskOut -split "`n" | Select-Object -Last 12
if($taskErr){$taskErr -split "`n" | Select-Object -Last 12}
if($taskTimedOut){exit 124}
exit $taskProcess.ExitCode
