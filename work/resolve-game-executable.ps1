param([switch]$Source, [string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if ($Source -and $Executable) { throw 'Choose either source Godot or an explicit executable.' }
if ($Source) {
    $candidate = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
} elseif ($Executable) {
    $candidate = $Executable
} else {
    $version = Select-String -LiteralPath (Join-Path $projectRoot 'game/project.godot') -Pattern '^config/version="([0-9]+\.[0-9]+\.[0-9]+(?:-(?:alfa|beta|omega|delta|gamma))?)"$'
    if (@($version).Count -ne 1) { throw 'Project version missing or ambiguous.' }
    $candidate = Join-Path $projectRoot ('outputs/Let-me-sleep-' + $version.Matches[0].Groups[1].Value + '-Windows/Let-me-sleep.exe')
}
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw ('Executable does not exist: ' + $candidate) }
(Get-Item -LiteralPath $candidate).FullName
