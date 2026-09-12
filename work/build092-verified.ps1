param([string]$EvidenceName = ('director092-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference = 'Stop'
if ($EvidenceName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid evidence name' }
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Select-String -LiteralPath "$projectRoot/game/project.godot" -Pattern '^config/version="0.9.2"$' -Quiet)) { throw 'This runner is for 0.9.2 only' }
& git -C $projectRoot diff --quiet HEAD -- game native art_source
if ($LASTEXITCODE -ne 0) { throw 'Commit source changes before building' }
if (@(& git -C $projectRoot ls-files --others --exclude-standard -- game native art_source).Count) { throw 'Import and commit source UIDs before building' }
$transcript = Join-Path $PSScriptRoot ($EvidenceName + '.txt')
if (Test-Path -LiteralPath $transcript) { throw 'Evidence already exists' }
$voicePath = Join-Path $PSScriptRoot 'voice09-acoustics-results.json'
$voiceBytes = [IO.File]::ReadAllBytes($voicePath)
Start-Transcript -LiteralPath $transcript | Out-Null
try {
    & (Join-Path $PSScriptRoot 'build.ps1')
} finally {
    # This tracked report had unrelated local edits before release work.
    # Preserve the new test evidence separately and restore those exact bytes.
    if (Test-Path -LiteralPath $voicePath) {
        Copy-Item -LiteralPath $voicePath -Destination (Join-Path $PSScriptRoot ($EvidenceName + '-voice.json'))
    }
    [IO.File]::WriteAllBytes($voicePath,$voiceBytes)
    Stop-Transcript | Out-Null
}
