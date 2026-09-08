param([Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$Source,[string]$ExpectedExeSha256)
# Run only while holding the shared native-render/import reservation.
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new witness directory; preserve prior evidence.'}
$exe=if($Source){Join-Path $PSScriptRoot 'tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'}else{Join-Path $taskRoot 'outputs/Let-me-sleep-0.7.0-Windows/Let-me-sleep.exe'}
$exeHash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if(-not $Source -and $exeHash -ne $ExpectedExeSha256){throw 'Supply the exact candidate EXE hash.'}
$sources=[ordered]@{}
foreach($relative in @('game/assets/art/characters/human/human_lms06.glb','game/assets/art/characters/mosquito/mosquito_lms06.glb','game/assets/art/characters/shared/character_skin.gd','game/assets/art/characters/shared/facial_expression.gd','game/scripts/avatar_preview.gd','game/scripts/cosmetics.gd','game/tests/facial_catalog08_gallery.gd')){
    $sources[$relative]=(Get-FileHash -LiteralPath (Join-Path $taskRoot $relative) -Algorithm SHA256).Hash
}
$cases=@(
    @{name='human-brow-hair';role='human';head=18;states='neutral,brow-up,blink-half,blink'},
    @{name='human-eyes-1';role='human';head=1;states='neutral,blink-half,blink'},
    @{name='human-eyes-2';role='human';head=2;states='neutral,blink-half,blink'},
    @{name='human-mustache-mouth';role='human';head=324;states='neutral,smile'},
    @{name='human-beard-jaw';role='human';head=1944;states='neutral,mouth-open'},
    @{name='mosquito-goggles-mouth';role='mosquito';head=162;states='neutral,mouth-open-smile'},
    @{name='mosquito-eyes-1';role='mosquito';head=1;states='neutral,blink-half,blink'},
    @{name='mosquito-eyes-2';role='mosquito';head=2;states='neutral,blink-half,blink'},
    @{name='mosquito-brows-0';role='mosquito';head=0;states='neutral,brow-up,brow-down,brow-up-blink,brow-down-blink'},
    @{name='mosquito-brows-1';role='mosquito';head=9;states='neutral,brow-up,brow-down,brow-up-blink,brow-down-blink'},
    @{name='mosquito-brows-2';role='mosquito';head=18;states='neutral,brow-up,brow-down,brow-up-blink,brow-down-blink'}
)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$records=@()
foreach($case in $cases){
    $folder=Join-Path $OutputDirectory $case.name
    $name='witness08-'+$case.name
    $arguments=@('--script','res://tests/facial_catalog08_gallery.gd','--max-fps','60','--',('--role='+$case.role),('--head='+$case.head),('--states='+$case.states),('"--output='+$folder+'"'))
    & "$PSScriptRoot/run-native07.ps1" -Name $name -GameArguments $arguments -Source:$Source
    foreach($relative in $sources.Keys){if((Get-FileHash -LiteralPath (Join-Path $taskRoot $relative) -Algorithm SHA256).Hash -ne $sources[$relative]){throw ('Source changed: '+$relative)}}
    if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $exeHash){throw 'Executable changed.'}
    $reportPath=Join-Path $folder ('{0}-page-{1:000}.json' -f $case.role,[int][Math]::Floor($case.head/24))
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if(-not $report.capture_passed -or -not $report.actor_content_checked -or $report.captured_head_count -ne 1){throw ('Incomplete witness: '+$case.name)}
    $images=[ordered]@{}
    foreach($sheet in $report.sheets){$images[$sheet.file]=(Get-FileHash -LiteralPath (Join-Path $folder $sheet.file) -Algorithm SHA256).Hash}
    foreach($suffix in @('log','err','run.json')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('release07-'+$name+'.'+$suffix)) -Destination (Join-Path $folder ('native.'+$suffix))}
    $records+=@{name=$case.name;role=$case.role;head_ordinal=$case.head;appearance=$report.records[0].appearance;states=$report.states;views=$report.views;checks=$report.checks;captured_views=$report.captured_views.Count;image_sha256=$images}
}
$modelFolder=Join-Path $OutputDirectory 'source-models'
[IO.Directory]::CreateDirectory($modelFolder) | Out-Null
foreach($role in @('human','mosquito')){
    $relative="game/assets/art/characters/$role/${role}_lms06.glb"
    $target=Join-Path $modelFolder ($role+'_lms06.glb')
    Copy-Item -LiteralPath (Join-Path $taskRoot $relative) -Destination $target
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $sources[$relative]){throw 'Model snapshot changed.'}
}
[ordered]@{status='visual_review_pending';source_build=[bool]$Source;exe_sha256=$exeHash;source_sha256=$sources;cases=$records;scope='Actual imported geometry, eight views per state. Capture checks do not certify fit. Corrective weights are recorded in each case report; all prior witness directories remain unchanged.'} | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding utf8
