$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$godotExe=Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath=Join-Path $projectRoot 'game'
$buildWorkRoot=$PSScriptRoot
$tokens=$null
$parseErrors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'build.ps1'),[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count){throw 'Build script parse failed'}
$definition=$ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-CheckedHeadless'},$true)
if(-not $definition){throw 'Build wrapper missing'}
. ([scriptblock]::Create($definition.Extent.Text))
Invoke-CheckedHeadless -CheckName 'wrapper-version-probe' -GameArguments @('--version')
$probe=Join-Path $PSScriptRoot 'intentional_build_error07.gd'
if(Test-Path -LiteralPath $probe){throw 'Probe collision'}
@'
extends SceneTree
func _initialize()->void:
    push_error("INTENTIONAL_BUILD_WRAPPER_PROBE")
    quit(0)
'@ | Set-Content -LiteralPath $probe -Encoding utf8
try {
    $rejected=$false
    try {
        Invoke-CheckedHeadless -CheckName 'wrapper-error-probe' -GameArguments @('--script',$probe)
    } catch {
        if($_.Exception.Message -notmatch 'Godot check failed: wrapper-error-probe, exit 0'){throw}
        $rejected=$true
    }
    if(-not $rejected){throw 'Build wrapper accepted an error with exit zero'}
    Write-Output 'BUILD_WRAPPER_PASS clean_exit_accepted=true logged_error_exit_zero_rejected=true'
} finally {
    Remove-Item -LiteralPath $probe
}
