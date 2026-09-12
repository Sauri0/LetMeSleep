param([string]$OutputDirectory = '', [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'outputs/launcher-1.1.0' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$core = Join-Path $repo 'launcher/Updater.cs'
$location = Join-Path $repo 'launcher/InstallLocation.cs'
$gameVersion = Join-Path $repo 'launcher/GameVersion.cs'
$refs = @('/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$branding = @(('/win32icon:' + (Join-Path $repo 'launcher/assets/launcher.ico')),('/resource:' + (Join-Path $repo 'launcher/assets/launcher-logo.png') + ',LauncherLogo'),('/resource:' + (Join-Path $repo 'launcher/assets/launcher.ico') + ',LauncherIcon'))
& $compiler /nologo /optimize+ /target:winexe /platform:x64 '/r:System.Windows.Forms.dll' '/r:System.Drawing.dll' @refs @branding ('/out:' + (Join-Path $OutputDirectory 'Let-me-sleep-Launcher.exe')) $core $location $gameVersion (Join-Path $repo 'launcher/Launcher.cs') (Join-Path $repo 'launcher/LauncherTheme.cs')
if ($LASTEXITCODE -ne 0) { throw 'Launcher compile failed' }
if (-not $SkipTests) {
    $testExe = Join-Path $repo 'work/updater-tests.exe'
    & $compiler /nologo /optimize+ /target:exe /platform:x64 @refs ('/out:' + $testExe) $core $location $gameVersion (Join-Path $repo 'launcher/UpdaterTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Updater test compile failed' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Updater tests failed' }
}
Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'Let-me-sleep-Launcher.exe')
