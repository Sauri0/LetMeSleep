$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'package-metadata.ps1')
$fixtureRoot = Join-Path $PSScriptRoot ('package-metadata09-test-'+[guid]::NewGuid().ToString('N'))
$output = Join-Path $fixtureRoot 'output'
foreach ($dir in @('game/scripts','game/addons/lms_opus/bin','game/addons/lms_opus/licenses','output/Licencias-voz')) {
    [System.IO.Directory]::CreateDirectory((Join-Path $fixtureRoot $dir)) | Out-Null
}
function Put([string]$Relative,[string]$Value) { [System.IO.File]::WriteAllText((Join-Path $fixtureRoot $Relative),$Value) }
Put 'game/project.godot' 'config/version="0.9.0"'
Put 'game/scripts/network.gd' 'const VERSION := "0.9.0"'
Put 'game/scripts/invitation.gd' "const PREFIX := `"DD5-`"`nconst PROTOCOL := 9`n"
Put 'game/addons/lms_opus/bin/lms_opus.windows.x86_64.dll' 'fixture DLL, never executable'
foreach ($name in @('Opus-COPYING.txt','godot-cpp-LICENSE.md')) {
    Put ('game/addons/lms_opus/licenses/'+$name) 'fixture notice'
    Put ('output/Licencias-voz/'+$name) 'fixture notice'
}
Put 'output/Let-me-sleep.exe' 'fixture EXE, never executable'
Put 'output/lms_opus.windows.x86_64.dll' 'fixture DLL, never executable'
foreach ($name in @('LEEME.md','LEEME.html','PRUEBAS.md')) { Put ('output/'+$name) 'Fixture version 0.9.0, invitation DD5-.' }
& git -C $fixtureRoot init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Fixture git init failed.' }
& git -C $fixtureRoot add -- game
& git -C $fixtureRoot -c user.name='Metadata test' -c user.email='metadata@example.invalid' -c commit.gpgsign=false commit --quiet -m 'Fixture source'
if ($LASTEXITCODE -ne 0) { throw 'Fixture commit failed.' }
$results = [System.Collections.Generic.List[object]]::new()
function WriteValid { Write-PackageMetadata -ProjectRoot $fixtureRoot -OutputDirectory $output -Version '0.9.0' -HeadlessTests $true -NativeTests $true }
function CheckValid { Test-PackageMetadata -ProjectRoot $fixtureRoot -OutputDirectory $output -Version '0.9.0' }
function MustReject([string]$Name,[scriptblock]$Action) {
    $rejected=$false
    try { & $Action } catch { $rejected=$true }
    if (-not $rejected) { throw ('Accepted invalid fixture: '+$Name) }
    $results.Add(@{name=$Name;passed=$true})
}
WriteValid
CheckValid
$results.Add(@{name='valid committed package';passed=$true})
$originalManifest = [System.IO.File]::ReadAllText((Join-Path $output 'BUILD.json'))
foreach ($field in @('version','protocol','source_dirty','checks')) {
    $changed=$originalManifest | ConvertFrom-Json
    switch ($field) {
        'version' { $changed.version='0.6.0' }
        'protocol' { $changed.protocol=6 }
        'source_dirty' { $changed.source_dirty=$true }
        'checks' { $changed.checks.native=$false }
    }
    Put 'output/BUILD.json' ($changed | ConvertTo-Json -Depth 10)
    MustReject $field { CheckValid }
}
Put 'output/BUILD.json' $originalManifest
Put 'output/Let-me-sleep.exe' 'tampered EXE'
MustReject 'changed executable' { CheckValid }
Put 'output/Let-me-sleep.exe' 'fixture EXE, never executable'
Put 'output/lms_opus.windows.x86_64.dll' 'wrong DLL before metadata creation'
MustReject 'wrong DLL before writer' { WriteValid }
MustReject 'wrong DLL at package time' { CheckValid }
Put 'output/lms_opus.windows.x86_64.dll' 'fixture DLL, never executable'
Put 'output/Licencias-voz/Opus-COPYING.txt' 'changed notice'
MustReject 'changed notice' { CheckValid }
Put 'output/Licencias-voz/Opus-COPYING.txt' 'fixture notice'
Put 'output/LEEME.md' 'Old invitation DD4-'
WriteValid
MustReject 'stale guide even with matching hash' { CheckValid }
Put 'output/LEEME.md' 'Fixture version 0.9.0, invitation DD5-.'
Put 'game/scripts/untracked_runtime.gd' 'extends Node'
WriteValid
MustReject 'untracked exported source' { CheckValid }
# Remove only the exact test file written above, leaving the whole fixture as evidence.
Remove-Item -LiteralPath (Join-Path $fixtureRoot 'game/scripts/untracked_runtime.gd')
WriteValid
Put 'game/scripts/network.gd' 'const VERSION := "0.8.0"'
MustReject 'network/project mismatch' { WriteValid }
Put 'game/scripts/network.gd' 'const VERSION := "0.9.0"'
CheckValid
$results.Add(@{name='restored fixture remains valid';passed=$true})
[ordered]@{checks=$results.Count; failures=0; fixture=$fixtureRoot; results=$results; scope='Offline metadata validation using inert fixture files; no engine, export or ZIP.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'package-metadata09-tests.json') -Encoding utf8
"Package metadata: $($results.Count) checks PASS; no engine, export or ZIP."
