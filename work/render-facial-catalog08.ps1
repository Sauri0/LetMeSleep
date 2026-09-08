param(
    [ValidateSet('human','mosquito')][string]$Role='human',
    [int]$FirstPage=0,
    [int]$PageCount=1,
    [string]$OutputDirectory,
    [string]$ExpectedExeSha256,
    [switch]$Source
)
# The release owner must reserve the rendering window before running this file.
# Each page is independently bounded, hash-bound and resumable without marking
# generated images as visually reviewed. No preferences are read or changed.
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $taskRoot 'outputs/0.7-combinaciones' }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$exe=if ($Source) { Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe' } else { Join-Path $taskRoot 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe' }
$exeHash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if (-not $Source -and $exeHash -ne $ExpectedExeSha256) { throw 'Freeze and supply the exact exported EXE hash.' }
$sourceHash=[ordered]@{}
foreach ($relative in @("game/assets/art/characters/$Role/${Role}_lms06.glb",'game/assets/art/characters/shared/character_skin.gd','game/assets/art/characters/shared/facial_expression.gd','game/assets/art/characters/shared/cloth.gdshader','game/scripts/cosmetics.gd','game/scripts/avatar_preview.gd','game/scripts/actor_view.gd','game/scripts/human_pose.gd','game/scripts/mosquito_pose.gd','game/tests/facial_catalog08_gallery.gd')) {
    $sourceHash[$relative]=(Get-FileHash -LiteralPath (Join-Path $taskRoot $relative) -Algorithm SHA256).Hash
}
$totalPages=if ($Role -eq 'human') { 122 } else { 11 }
if ($PageCount -lt 1 -or $FirstPage -lt 0 -or $FirstPage+$PageCount -gt $totalPages) { throw 'Page range exceeds the exact head catalog.' }
for ($page=$FirstPage; $page -lt $FirstPage+$PageCount; $page++) {
    $label='{0}-page-{1:000}' -f $Role,$page
    $reportPath=Join-Path $OutputDirectory ($label+'.json')
    $runPath=Join-Path $OutputDirectory ($label+'.run.json')
    if (Test-Path -LiteralPath $runPath) {
        $previous=Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json -AsHashtable
        $sameSources=$true
        foreach ($relative in $sourceHash.Keys) { if ($previous.source_sha256[$relative] -ne $sourceHash[$relative]) { $sameSources=$false } }
        if (-not $previous.passed -or $previous.exe_sha256 -ne $exeHash -or [bool]$previous.source -ne [bool]$Source -or -not $sameSources) { throw ('Existing evidence belongs to different inputs: '+$label) }
        $previousReport=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        foreach ($sheet in $previousReport.sheets) {
            $sheetPath=Join-Path $OutputDirectory $sheet.file
            if ((Get-FileHash -LiteralPath $sheetPath -Algorithm SHA256).Hash -ne $previous.sheet_sha256[$sheet.file]) { throw ('Changed or missing sheet: '+$sheet.file) }
        }
        Write-Output ('CATALOG_RESUMED '+$label)
        continue
    }
    $arguments=@('--script','res://tests/facial_catalog08_gallery.gd','--max-fps','60','--',('--role='+$Role),('--page='+$page),('"--output='+$OutputDirectory+'"'))
    & "$PSScriptRoot/run-native07.ps1" -Name ('gallery-'+$label) -GameArguments $arguments -Source:$Source
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $report.capture_passed -or $report.failures -ne 0 -or $report.page -ne $page -or $report.role -ne $Role) { throw ('Invalid page report: '+$label) }
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $exeHash) { throw 'EXE changed during gallery.' }
    foreach ($relative in $sourceHash.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $taskRoot $relative) -Algorithm SHA256).Hash -ne $sourceHash[$relative]) { throw ('Source changed during gallery: '+$relative) }
    }
    $sheetHashes=[ordered]@{}
    foreach ($sheet in $report.sheets) { $sheetHashes[$sheet.file]=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $sheet.file) -Algorithm SHA256).Hash }
    [ordered]@{passed=$true;source=[bool]$Source;exe_sha256=$exeHash;source_sha256=$sourceHash;sheet_sha256=$sheetHashes;page=$page;role=$Role;heads=$report.captured_head_count;visual_review_status='pending'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $runPath -Encoding utf8
}
