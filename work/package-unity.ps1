param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [string]$Version='0.9.4-alfa',
    [string]$OutputDirectory='N:/LetMeSleep/Artifacts/packages'
)
$ErrorActionPreference='Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+-(alfa|beta|omega|delta|gamma)$') { throw 'Invalid playtest version.' }
$buildPath=[IO.Path]::GetFullPath($BuildDirectory)
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
$required=@('Let-me-sleep.exe','UnityPlayer.dll','Let-me-sleep_Data/globalgamemanagers','Let-me-sleep_Data/StreamingAssets/online.local.json')
foreach($relative in $required) { if(!(Test-Path -LiteralPath (Join-Path $buildPath $relative) -PathType Leaf)) { throw "Incomplete Unity build: $relative" } }
if(Get-ChildItem -LiteralPath $buildPath -Recurse -File -Filter 'GfxPluginNativeRender-x64.dll') { throw 'Unused native overlay helper must be excluded by build policy.' }
$packageName="Let-me-sleep-$Version-Windows"
$stage=Join-Path $outputPath ('staging-'+[guid]::NewGuid().ToString('N'))
$package=Join-Path $stage $packageName
[IO.Directory]::CreateDirectory($package)|Out-Null
foreach($item in Get-ChildItem -LiteralPath $buildPath) {
    if($item.Name -match '^(build-receipt\.json|.*_BurstDebugInformation_DoNotShip|.*_BackUpThisFolder_ButDontShipItWithYourGame)$' -or $item.Extension -eq '.pdb') { continue }
    Copy-Item -LiteralPath $item.FullName -Destination $package -Recurse
}
$files=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName) {
    $relative=$file.FullName.Substring($package.Length+1).Replace('\','/')
    $files[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest=[ordered]@{version=$Version;executable='Let-me-sleep.exe';engine='Unity';files=$files}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $package 'BUILD.json') -Encoding utf8
$zip=Join-Path $outputPath ($packageName+'.zip')
if(Test-Path -LiteralPath $zip) { throw 'Package already exists; choose a fresh output directory to retain previous evidence.' }
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $packageName.zip"|Set-Content -LiteralPath ($zip+'.sha256.txt') -Encoding ascii
[pscustomobject]@{Package=$zip;Sha256=$hash;Files=$files.Count;Staging=$stage}
