param([switch]$Import, [string]$Script='house094_contract_test')
$ErrorActionPreference='Stop'
$houseRepo=Split-Path -Parent $PSScriptRoot
$exe='C:/Users/brank/Documents/Codex/2026-09-06/dejame-dormir/work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$info=[Diagnostics.ProcessStartInfo]::new()
$info.FileName=$exe
$info.WorkingDirectory=$houseRepo
$info.Arguments=if($Import){'--headless --path game --editor --import --quit --single-threaded-scene'}else{"--headless --path game --script res://tests/$Script.gd --single-threaded-scene"}
$info.UseShellExecute=$false
$info.CreateNoWindow=$true
$info.RedirectStandardOutput=$true
$info.RedirectStandardError=$true
$p=[Diagnostics.Process]::new();$p.StartInfo=$info
$null=$p.Start()
$o=$p.StandardOutput.ReadToEndAsync();$e=$p.StandardError.ReadToEndAsync()
$finished=$p.WaitForExit(55000)
if(-not $finished){$p.Kill();$p.WaitForExit()}
$out=$o.Result;$err=$e.Result
$prefix=Join-Path $PSScriptRoot "house094-$stamp-$Script"
[IO.File]::WriteAllText($prefix+'.stdout.log',$out)
[IO.File]::WriteAllText($prefix+'.stderr.log',$err)
$failed=(-not $finished)-or $p.ExitCode-ne 0 -or ($out+$err)-match 'SCRIPT ERROR|Parse Error|Compile Error|ERROR:'
[IO.File]::WriteAllText($prefix+'.json',(@{passed=(-not $failed);exit=$p.ExitCode;timeout=(-not $finished);stderr=$err.Length;script=$Script;import=[bool]$Import}|ConvertTo-Json))
($out+$err)-split "`n"|Select-String 'HOUSE094|FAIL|SCRIPT ERROR|Parse Error|Compile Error|ERROR:'|Select-Object -First 45
Write-Output ('REPORT '+$prefix+'.json')
if($failed){exit 1}
exit 0
