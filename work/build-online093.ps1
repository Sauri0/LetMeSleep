$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
. ./work/package-metadata.ps1
$version='0.9.3'
if((Get-PackageProtocol -ProjectRoot $root).version -ne $version){throw 'Wrong version'}
& git diff --quiet HEAD -- game native art_source
if($LASTEXITCODE -ne 0 -or @(& git ls-files --others --exclude-standard -- game native art_source).Count){throw 'Commit imported source before building'}
$out=Join-Path $root ('outputs/Let-me-sleep-'+$version+'-Windows')
$exe=Join-Path $out 'Let-me-sleep.exe'
if(Test-Path $exe){throw 'Never overwrite an existing versioned executable'}
Initialize-OnlineBuildConfiguration -ProjectRoot $root
$tests=@('online093_ui_checks','online093_connection_checks','online_invitation_test','online_session_test','online_network_checks','online_network_mtu_checks','online_network_payload_checks','online_packet_codec_test')
$records=@()
foreach($test in $tests){
    $name='online093-source-'+$test
    $argsList=@('--headless','--script',('res://tests/'+$test+'.gd'))
    if($test -eq 'online093_ui_checks'){$argsList+=@('--','--source-contracts')}
    & ./work/run-native07.ps1 -Source -Name $name -GameArguments $argsList
    $records+=Get-Content (Join-Path $PSScriptRoot ('release07-'+$name+'.run.json')) -Raw|ConvertFrom-Json
}
foreach($entry in @('main','client')){
    & ./work/run-native07.ps1 -Source -Name ('online093-parse-'+$entry) -GameArguments @('--headless','--check-only','--script',('res://scripts/'+$entry+'.gd'))
}
[IO.Directory]::CreateDirectory($out)|Out-Null
Get-ChildItem ./distribution -File|Copy-Item -Destination $out
& ./work/run-native07.ps1 -Source -Name online093-export -GameArguments @('--headless','--export-release','"Windows Desktop"',$exe)
foreach($notice in @(@{source='game/addons/lms_opus/licenses';dest='Licencias-voz'},@{source='game/addons/epic-online-services-godot/licenses';dest='Licencias-online'})){
    $dest=Join-Path $out $notice.dest;[IO.Directory]::CreateDirectory($dest)|Out-Null
    Get-ChildItem (Join-Path $root $notice.source) -File|Copy-Item -Destination $dest
}
foreach($test in @(@{name='online093-export-ui';script='online093_ui_checks'},@{name='online093-export-live-host';script='online_network_live_host'})){
    & ./work/run-native07.ps1 -Executable $exe -Name $test.name -GameArguments @('--headless','--script',('res://tests/'+$test.script+'.gd'))
    $records+=Get-Content (Join-Path $PSScriptRoot ('release07-'+$test.name+'.run.json')) -Raw|ConvertFrom-Json
}
Write-PackageMetadata -ProjectRoot $root -OutputDirectory $out -Version $version -HeadlessTests $false -NativeTests $false
$metaPath=Join-Path $out 'BUILD.json';$m=Get-Content $metaPath -Raw|ConvertFrom-Json
$proof=@{mode='targeted_headless';baseline='v0.9.2';source_commit=$m.source_commit;native_run=$false;wan_verified=$false;tests=$records;scope='Focused online and UI contracts plus real EOS host lifecycle. No game window, native visual test, or two-home relay claim.'}
$proof|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'VERIFICATION.json')
$m|Add-Member -NotePropertyName verification -NotePropertyValue @{mode='targeted_headless';report='VERIFICATION.json'}
$m.files|Add-Member -NotePropertyName 'VERIFICATION.json' -NotePropertyValue (Get-FileHash (Join-Path $out 'VERIFICATION.json')).Hash
$m|ConvertTo-Json -Depth 12|Set-Content $metaPath
Test-PackageMetadata -ProjectRoot $root -OutputDirectory $out -Version $version -AllowTargetedHeadless
foreach($legacy in @('Iniciar-servidor.cmd','servidor.cfg')){if(Test-Path (Join-Path $out $legacy)){throw 'Legacy launcher in online-only package'}}
$zip=$out+'.zip';if(Test-Path $zip){throw 'Versioned ZIP exists'}
[IO.Compression.ZipFile]::CreateFromDirectory($out,$zip,[IO.Compression.CompressionLevel]::Optimal,$true)
$hash=(Get-FileHash $zip).Hash
[IO.File]::WriteAllText($zip+'.sha256.txt',$hash+'  '+[IO.Path]::GetFileName($zip)+"`n")
@{version=$version;source_commit=$m.source_commit;zip_sha256=$hash;zip_bytes=(Get-Item $zip).Length;tests=$records.Count;native_run=$false;wan_verified=$false}|ConvertTo-Json|Set-Content ./work/online093-package.json
Get-Content ./work/online093-package.json
