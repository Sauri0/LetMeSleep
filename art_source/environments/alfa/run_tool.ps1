param([string]$Stage='export',[string]$Executable,[string[]]$Arguments,[int]$TimeoutSeconds=55)
$ErrorActionPreference='Stop'
$alfaOutput=Join-Path $PSScriptRoot 'validation'
New-Item -ItemType Directory -Force $alfaOutput | Out-Null
$info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=$Executable;$info.WorkingDirectory=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
foreach($arg in $Arguments){$info.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::new();$process.StartInfo=$info;$started=[DateTime]::UtcNow;$null=$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$timedOut=-not $process.WaitForExit($TimeoutSeconds*1000)
if($timedOut){$process.Kill($true);$process.WaitForExit()}
$out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult();[IO.File]::WriteAllText((Join-Path $alfaOutput ($Stage+'.log')),$out);[IO.File]::WriteAllText((Join-Path $alfaOutput ($Stage+'.err')),$err)
$result=@{stage=$Stage;pid=$process.Id;started_utc=$started.ToString('o');finished_utc=[DateTime]::UtcNow.ToString('o');exit_code=$process.ExitCode;timeout=$timedOut;stderr_bytes=[Text.Encoding]::UTF8.GetByteCount($err);arguments=$Arguments}
$result | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $alfaOutput ($Stage+'.run.json'));$result | ConvertTo-Json -Compress -Depth 4
$out -split "`n" | Select-Object -Last 8
if($err){$err -split "`n" | Select-Object -Last 8}
exit $process.ExitCode
