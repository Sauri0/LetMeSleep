$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$headlessCommit = '79d8b10'
$transcript = Join-Path $PSScriptRoot 'director091-build2-transcript.txt'
$text = [IO.File]::ReadAllText($transcript)
if (-not $text.Contains('ONLINE_LIVE_HOST_RESULT checks=15 failures=0') -or
    -not $text.Contains('MOTION091_ATTACHMENT_RESULT checks=781 failures=0')) {
    throw 'The completed headless phase and transition to native checks are not recorded.'
}
$version = [IO.File]::ReadAllText((Join-Path $root 'game/project.godot'))
if ($version -notmatch 'config/version="0\.9\.1"') { throw 'This recovery applies only to 0.9.1.' }
function Assert-UnchangedHeadlessSource {
    $changed = @(& git -C $root diff --name-only $headlessCommit HEAD -- game native art_source work/build.ps1)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compare the tested source.' }
    foreach ($file in $changed) {
        if ($file -ne 'game/tests/actor09_legacy_geometry_test.gd') {
            throw ('Headless evidence cannot be reused after changing ' + $file)
        }
    }
    & git -C $root diff --quiet HEAD -- game native art_source work/build.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Uncommitted build source.' }
    if (@(& git -C $root ls-files --others --exclude-standard -- game native art_source).Count) {
        throw 'Untracked build source.'
    }
}
Assert-UnchangedHeadlessSource
$startCommit = (& git -C $root rev-parse HEAD).Trim()
# Re-run every native gate. The completed headless suite remains valid because
# only its unrelated native-only legacy comparison fixture has changed.
& (Join-Path $PSScriptRoot 'build.ps1') -SkipHeadlessTests
Assert-UnchangedHeadlessSource
if ((& git -C $root rev-parse HEAD).Trim() -ne $startCommit) { throw 'Source moved during native validation.' }
$output = Join-Path $root 'outputs/Let-me-sleep-0.9.1-Windows'
$metadata = Get-Content -LiteralPath (Join-Path $output 'BUILD.json') -Raw | ConvertFrom-Json
if ($metadata.source_dirty -or -not $metadata.checks.native) { throw 'Native phase is incomplete.' }
. (Join-Path $PSScriptRoot 'package-metadata.ps1')
Write-PackageMetadata -ProjectRoot $root -OutputDirectory $output -Version '0.9.1' -HeadlessTests $true -NativeTests $true
$metadata = Get-Content -LiteralPath (Join-Path $output 'BUILD.json') -Raw | ConvertFrom-Json
$provenance = [ordered]@{
    headless_source_commit=(& git -C $root rev-parse $headlessCommit).Trim()
    headless_transcript_sha256=(Get-FileHash -LiteralPath $transcript).Hash
    native_source_commit=$startCommit
    only_source_difference='game/tests/actor09_legacy_geometry_test.gd'
    reason='Full headless phase passed before a native comparison fixture failed. Runtime, assets and every headless fixture are unchanged; every native gate was rerun.'
}
$metadata | Add-Member -NotePropertyName verification_provenance -NotePropertyValue $provenance
[IO.File]::WriteAllText((Join-Path $output 'BUILD.json'),($metadata | ConvertTo-Json -Depth 10)+[Environment]::NewLine)
$provenance | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'director091-verification-provenance.json')
