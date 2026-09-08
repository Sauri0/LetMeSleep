$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildWorkRoot = $PSScriptRoot
$godotExe = Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
$gamePath = Join-Path $projectRoot 'game'
$parseTokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'build.ps1'),[ref]$parseTokens,[ref]$parseErrors)
if ($parseErrors.Count) { throw 'Build script has syntax errors' }
$definition = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-CheckedHeadless' },$false)
if ($null -eq $definition) { throw 'Build wrapper missing' }
# Evaluate only this function; never execute the export or versioned-output code.
. ([ScriptBlock]::Create($definition.Extent.Text))
Invoke-CheckedHeadless -CheckName 'wrapper09-version' -GameArguments @('--version')
$rejected = $false
try {
    Invoke-CheckedHeadless -CheckName 'wrapper09-negative' -GameArguments @('--check-only','--script','res://tests/this_fixture_intentionally_does_not_exist.gd')
} catch {
    $rejected = $_.Exception.Message -like 'Godot check failed: wrapper09-negative*'
}
if (-not $rejected) { throw 'Wrapper failed to reject failed Godot script' }
Write-Output 'BUILD_WRAPPER09 version PASS; intentional missing-script failure correctly rejected; no export performed.'
