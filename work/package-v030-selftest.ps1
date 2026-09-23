# Self-test of work/package-v030.ps1 (and walkthrough-v030.ps1 -VerifyOnly) on synthetic build directories, no Unity.
# The "valid" build is shaped like a real non-Development Mono player: its Managed folder carries
# Unity.Pipeline.Attributes.dll (com.unity.pipeline keeps it unconstrained) and unreferenced assemblies such as
# Unity.Timeline.dll. Pass -ReleaseAssembliesReport <report.json> from WindowsAlfaBuild.VerifyReleaseAssemblies to
# add a case whose Managed folder is exactly the predicted release set of the current commit.
# Output: <OutputRoot>/run-<UTC>/selftest.json; throws on any failed check.
param(
    [string]$Script = (Join-Path $PSScriptRoot 'package-v030.ps1'),
    [string]$Walkthrough = (Join-Path $PSScriptRoot 'walkthrough-v030.ps1'),
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/V030/QA/package-selftest',
    [string]$ReleaseAssembliesReport
)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $root | Out-Null
# Managed names of the 0.2.0 Development player minus what UNITY_EDITOR || DEVELOPMENT_BUILD constraints drop in Release.
$releaseManaged = @('Assembly-CSharp.dll', 'LetMeSleep.Bootstrap.dll', 'LetMeSleep.Online.dll', 'Newtonsoft.Json.dll',
    'Unity.InputSystem.dll', 'Unity.Pipeline.Attributes.dll', 'Unity.RenderPipelines.Core.Runtime.dll',
    'Unity.RenderPipelines.Universal.Runtime.dll', 'Unity.TextMeshPro.dll', 'Unity.Timeline.dll', 'UnityEngine.CoreModule.dll', 'mscorlib.dll')
function New-Build([string]$name, [hashtable]$receiptOverrides = @{}, [string[]]$bootExtra = @(), [switch]$LocalPath, [string[]]$Managed = $releaseManaged) {
    $b = Join-Path $root $name
    foreach ($d in 'Let-me-sleep_Data/StreamingAssets/EOS', 'Let-me-sleep_Data/Managed', 'MonoBleedingEdge/EmbedRuntime', 'Let-me-sleep_BurstDebugInformation_DoNotShip/Data/Plugins', 'Let-me-sleep_BackUpThisFolder_ButDontShipItWithYourGame') {
        New-Item -ItemType Directory -Force (Join-Path $b $d) | Out-Null
    }
    Set-Content (Join-Path $b 'Let-me-sleep.exe') 'exe'; Set-Content (Join-Path $b 'UnityPlayer.dll') 'player'
    Set-Content (Join-Path $b 'UnityCrashHandler64.exe') 'crash'; Set-Content (Join-Path $b 'GUIA-DE-PRUEBA.md') '# Guia'
    Set-Content (Join-Path $b 'Let-me-sleep_Data/globalgamemanagers') 'ggm'
    Set-Content (Join-Path $b 'Let-me-sleep_Data/boot.config') (@('gfx-enable-gfx-jobs=1', 'wait-for-native-debugger=0', 'build-guid=abc') + $bootExtra)
    Set-Content (Join-Path $b 'Let-me-sleep_Data/StreamingAssets/online.local.json') '{"productId":"x"}'
    Set-Content (Join-Path $b 'Let-me-sleep_Data/StreamingAssets/EOS/eos_windows_config.json') ($(if ($LocalPath) { '{"cache":"N:\LetMeSleep\Private"}' } else { '{"flags":1}' }))
    foreach ($dll in $Managed) { Set-Content (Join-Path $b "Let-me-sleep_Data/Managed/$dll") 'asm' }
    Set-Content (Join-Path $b 'Let-me-sleep_Data/Managed/Assembly-CSharp.pdb') 'pdb'
    Set-Content (Join-Path $b 'UnityPlayer.pdb') 'pdb'
    Set-Content (Join-Path $b 'Let-me-sleep_BurstDebugInformation_DoNotShip/Data/Plugins/lib_burst_generated.txt') 'N:\LetMeSleep\Worktrees\x'
    Set-Content (Join-Path $b 'Let-me-sleep_BackUpThisFolder_ButDontShipItWithYourGame/x.cpp') 'cpp'
    $receipt = [ordered]@{ result = 'Succeeded'; unity = '6000.3.24f1'; utc = '2026-09-23T12:00:00Z'; sourceCommit = ('a' * 40); version = '0.3.0'
        profile = 'release'; sourceDirty = $false; developmentBuild = $false; errors = 0; outputBytes = 1; releaseProblems = @() }
    foreach ($k in $receiptOverrides.Keys) { $receipt[$k] = $receiptOverrides[$k] }
    $receipt | ConvertTo-Json | Set-Content (Join-Path $b 'build-receipt.json')
    return $b
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Test-Packaged([string]$name, [string]$build, [string[]]$mustContain) {
    $json = & pwsh -NoProfile -File $Script -BuildDirectory $build -OutputRoot (Join-Path $root "packages-$name") 2>&1
    if ($LASTEXITCODE -ne 0) { return [ordered]@{ packaged = $false; error = ($json -join ' ') } }
    $summary = ($json -join "`n") | ConvertFrom-Json
    $run = Split-Path $summary.package
    $z = [IO.Compression.ZipFile]::OpenRead($summary.package); $entries = @($z.Entries.FullName); $z.Dispose()
    $manifest = Get-Content -Raw (Join-Path $run 'Let-me-sleep-0.3.0-Windows/BUILD.json') | ConvertFrom-Json
    $sha = (Get-Content (Join-Path $run 'Let-me-sleep-0.3.0-Windows.zip.sha256.txt')).Trim()
    $checks = [ordered]@{
        packaged = $true
        zipName = [IO.Path]::GetFileName($summary.package) -eq 'Let-me-sleep-0.3.0-Windows.zip'
        shaLine = $sha -eq ((Get-FileHash $summary.package -Algorithm SHA256).Hash.ToLowerInvariant() + '  Let-me-sleep-0.3.0-Windows.zip')
        seriesMarker = (Test-Path (Join-Path $run 'Let-me-sleep-series-2.json')) -and ((Get-Item (Join-Path $run 'Let-me-sleep-series-2.json')).Length -le 1024)
        manifest = $manifest.version -eq '0.3.0' -and $manifest.releaseSeries -eq 1 -and $manifest.sourceCommit -eq ('a' * 40) -and $manifest.profile -eq 'release'
        noPdb = -not ($entries | Where-Object { $_ -like '*.pdb' })
        noBurst = -not ($entries | Where-Object { $_ -like '*DoNotShip*' -or $_ -like '*ButDontShip*' })
        noReceipt = -not ($entries | Where-Object { $_ -like '*build-receipt.json' })
        hasGame = ($entries -contains 'Let-me-sleep-0.3.0-Windows/Let-me-sleep.exe') -and ($entries -contains 'Let-me-sleep-0.3.0-Windows/BUILD.json') -and ($entries -contains 'Let-me-sleep-0.3.0-Windows/Let-me-sleep_Data/StreamingAssets/online.local.json')
        keepsReleaseAssemblies = @($mustContain | Where-Object { $entries -notcontains "Let-me-sleep-0.3.0-Windows/Let-me-sleep_Data/Managed/$_" }).Count -eq 0
        hashesMatch = @($manifest.files.PSObject.Properties).Count -eq ($entries.Count - 1)
    }
    # The walkthrough's package checks (hash every extracted file against BUILD.json, boot.config, debug content).
    $verify = & pwsh -NoProfile -File $Walkthrough -PackageDirectory $run -OutputRoot (Join-Path $root "walkthrough-$name") -VerifyOnly 2>&1
    $walk = Get-Content -Raw (Join-Path $root "walkthrough-$name/walkthrough.json") | ConvertFrom-Json
    $checks.walkthroughVerifyOnly = $LASTEXITCODE -eq 0 -and $walk.packageVerified -eq $true -and $walk.pass -eq $false -and $walk.mode -eq 'verify-only'
    $script:lastPackage = $run
    return $checks
}
# The walkthrough must refuse a package whose ZIP does not match its .sha256.txt or whose files do not match BUILD.json.
function Test-WalkthroughRefuses([string]$name, [scriptblock]$tamper) {
    $copy = Join-Path $root "tampered-$name"
    Copy-Item -LiteralPath $script:lastPackage -Destination $copy -Recurse
    & $tamper $copy
    $o = & pwsh -NoProfile -File $Walkthrough -PackageDirectory $copy -OutputRoot (Join-Path $root "walkthrough-tampered-$name") -VerifyOnly 2>&1
    $walk = Get-Content -Raw (Join-Path $root "walkthrough-tampered-$name/walkthrough.json") | ConvertFrom-Json
    return ($LASTEXITCODE -ne 0) -and $walk.packageVerified -eq $false
}
$results = [ordered]@{}
$results.valid = Test-Packaged 'valid' (New-Build 'valid') @('Unity.Pipeline.Attributes.dll', 'Unity.Timeline.dll')
$results.walkthroughRefusesShaMismatch = Test-WalkthroughRefuses 'sha' {
    param($dir) Set-Content -LiteralPath (Join-Path $dir 'Let-me-sleep-0.3.0-Windows.zip.sha256.txt') ('0' * 64 + '  Let-me-sleep-0.3.0-Windows.zip') }
$results.walkthroughRefusesManifestMismatch = Test-WalkthroughRefuses 'manifest' {
    param($dir)
    $zipPath = Join-Path $dir 'Let-me-sleep-0.3.0-Windows.zip'
    $unpacked = Join-Path $dir 'unpacked'
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $unpacked)
    Set-Content -LiteralPath (Join-Path $unpacked 'Let-me-sleep-0.3.0-Windows/Let-me-sleep_Data/globalgamemanagers') 'changed'
    Remove-Item -LiteralPath $zipPath
    [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $unpacked 'Let-me-sleep-0.3.0-Windows'), $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
    ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() + '  Let-me-sleep-0.3.0-Windows.zip') |
        Set-Content -LiteralPath "$zipPath.sha256.txt" -Encoding ascii
}
if ($ReleaseAssembliesReport) {
    $report = Get-Content -Raw -LiteralPath $ReleaseAssembliesReport | ConvertFrom-Json
    if (@($report.releaseManaged).Count -eq 0) { throw 'The release assemblies report lists no managed assemblies.' }
    $results.predictedReleaseSet = Test-Packaged 'predicted' (New-Build 'predicted' -Managed @($report.releaseManaged)) @($report.releaseManaged)
    $results.predictedReleaseSet.sourceCommit = $report.sourceCommit
    $results.predictedReleaseSet.count = @($report.releaseManaged).Count
}
function Test-Refused([string]$name, [string]$build) {
    $o = & pwsh -NoProfile -File $Script -BuildDirectory $build -OutputRoot (Join-Path $root "refused-$name") 2>&1
    return ($LASTEXITCODE -ne 0) -and -not (Test-Path (Join-Path $root "refused-$name"))
}
$results.refusesDevelopment = Test-Refused 'dev' (New-Build 'dev' @{ profile = 'development'; developmentBuild = $true })
$results.refusesDirty = Test-Refused 'dirty' (New-Build 'dirty' @{ sourceDirty = $true })
$results.refusesOtherVersion = Test-Refused 'ver' (New-Build 'ver' @{ version = '0.2.0' })
$results.refusesReceiptProblems = Test-Refused 'problems' (New-Build 'problems' @{ releaseProblems = @('boot.config: player-connection-mode') })
$results.refusesPlayerConnection = Test-Refused 'pc' (New-Build 'pc' @{} @('player-connection-mode=Listen', 'player-connection-ip=10.0.0.1'))
$results.refusesNativeDebuggerWait = Test-Refused 'dbg' (New-Build 'dbg' @{} @('wait-for-native-debugger=1'))
$results.refusesLocalPath = Test-Refused 'path' (New-Build 'path' -LocalPath)
foreach ($dll in 'Unity.Pipeline.dll', 'Unity.Pipeline.IlInterpreter.dll', 'UnityPipeline.Microsoft.CodeAnalysis.dll', 'UnityPipeline.System.Collections.Immutable.dll') {
    $results["refuses $dll"] = Test-Refused $dll (New-Build $dll -Managed ($releaseManaged + $dll))
}
$overlay = New-Build 'overlay'; Set-Content (Join-Path $overlay 'Let-me-sleep_Data/GfxPluginNativeRender-x64.dll') 'x'
$results.refusesOverlayHelper = Test-Refused 'overlay' $overlay
$results | ConvertTo-Json -Depth 4 | Tee-Object (Join-Path $root 'selftest.json')
$flat = foreach ($key in $results.Keys) {
    $value = $results[$key]
    if ($value -is [System.Collections.IDictionary]) { foreach ($k in $value.Keys) { if ($value[$k] -is [bool]) { $value[$k] } } } else { $value }
}
if (@($flat) -contains $false) { throw "package-v030 self-test FAILED ($root)" } else { "package-v030 self-test PASSED ($root)" }
