param([string]$Godot = '', [string]$Report = '', [switch]$Packaged)
$ErrorActionPreference = 'Stop'
$rootPath = Split-Path $PSScriptRoot -Parent
if (-not $Godot) { $Godot = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe' }
if (-not $Report) { $Report = Join-Path $PSScriptRoot 'host-parent05.json' }
$testPort = 28627
$logPath = Join-Path $PSScriptRoot 'host-parent05.log'
$errorPath = Join-Path $PSScriptRoot 'host-parent05-error.log'
$parentHelper = $null
$serverChild = $null
try {
    $shellPath = (Get-Process -Id $PID).Path
    $parentHelper = Start-Process -FilePath $shellPath -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 45' -WindowStyle Hidden -PassThru
    $arguments = @('--headless')
    if (-not $Packaged) { $arguments += @('--path',(Join-Path $rootPath 'game')) }
    $arguments += @('--','--server',('--port=' + $testPort),('--parent-pid=' + $parentHelper.Id))
    $serverChild = Start-Process -FilePath $Godot -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $logPath -RedirectStandardError $errorPath
    $until = (Get-Date).AddSeconds(8)
    $ready = $false
    while ((Get-Date) -lt $until -and -not $serverChild.HasExited) {
        if ((Test-Path -LiteralPath $logPath) -and (Select-String -LiteralPath $logPath -Pattern 'SERVER_READY' -Quiet)) { $ready = $true; break }
        Start-Sleep -Milliseconds 100
    }
    if (-not $ready) { throw 'Owned server did not confirm listening' }
    Stop-Process -Id $parentHelper.Id
    $stopped = $serverChild.WaitForExit(5000)
    if (-not $stopped) { throw 'Child remained alive after owned parent exited' }
    $udp = [Net.Sockets.UdpClient]::new($testPort)
    $udp.Dispose()
    $record = @{ ready=$ready; parentCrashStopsChild=$stopped; portReleased=$true; childExit=$serverChild.ExitCode }
    $record | ConvertTo-Json | Set-Content -LiteralPath $Report -Encoding utf8
    $record | ConvertTo-Json
} finally {
    foreach ($owned in @($serverChild,$parentHelper)) {
        if ($null -ne $owned -and -not $owned.HasExited) { Stop-Process -Id $owned.Id }
    }
}
