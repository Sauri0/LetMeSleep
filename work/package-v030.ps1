# Packages a v0.3.0 RELEASE build (WindowsAlfaBuild.BuildV030) for the public GitHub release.
# Output: <OutputRoot>/<UTC yyyyMMdd-HHmmss>-<guid8>/
#   Let-me-sleep-0.3.0-Windows/            unpacked copy with BUILD.json (what the ZIP contains)
#   Let-me-sleep-0.3.0-Windows.zip         the release asset
#   Let-me-sleep-0.3.0-Windows.zip.sha256.txt
#   Let-me-sleep-series-2.json             series marker for the retired 1.2.0 launcher (release series 1)
#   package.json                           internal summary (never uploaded)
# Never packaged: build-receipt.json (internal), *_BurstDebugInformation_DoNotShip (local paths),
# *_BackUpThisFolder_ButDontShipItWithYourGame and every *.pdb. Development builds are refused.
param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [string]$OutputRoot = 'N:/LetMeSleep/Artifacts/v0.3.0/packages'
)
$ErrorActionPreference = 'Stop'
$version = '0.3.0'
$source = (Resolve-Path -LiteralPath $BuildDirectory).Path
$receiptPath = Join-Path $source 'build-receipt.json'
if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw 'build-receipt.json is missing: package only the output of BuildV030.' }
$receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
if ($receipt.result -ne 'Succeeded' -or $receipt.errors -ne 0 -or $receipt.version -ne $version -or $receipt.sourceDirty -ne $false) {
    throw "A successful, clean v$version build receipt is required."
}
if ($receipt.sourceCommit -notmatch '^[a-f0-9]{40}$') { throw 'Missing source commit.' }
# BuildV030 writes profile=release; BuildV030Diagnostics (Development, smoke probe) must never be published.
if ($receipt.profile -ne 'release' -or $receipt.developmentBuild -ne $false) { throw 'Only a release-profile build (BuildV030) can be packaged.' }
if (@($receipt.releaseProblems).Where({ $_ }).Count -ne 0) { throw 'The build receipt lists release problems: ' + ($receipt.releaseProblems -join '; ') }

$required = @('Let-me-sleep.exe', 'UnityPlayer.dll', 'GUIA-DE-PRUEBA.md', 'Let-me-sleep_Data/globalgamemanagers',
    'Let-me-sleep_Data/boot.config', 'Let-me-sleep_Data/StreamingAssets/online.local.json')
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $relative) -PathType Leaf)) { throw "Incomplete build: $relative" }
}

$excludedDirectory = '(_BurstDebugInformation_DoNotShip|_BackUpThisFolder_ButDontShipItWithYourGame)$'
function Test-Excluded([string]$relative) {
    $parts = $relative.Replace('\', '/').Split('/')
    if ($parts.Count -eq 1 -and $parts[0] -eq 'build-receipt.json') { return $true }
    if ($parts[-1] -like '*.pdb') { return $true }
    foreach ($part in $parts) { if ($part -match $excludedDirectory) { return $true } }
    return $false
}
# Same rule as WindowsAlfaBuild.IsDevelopmentOnlyAssembly: com.unity.pipeline's runtime (Unity.Pipeline.dll,
# Unity.Pipeline.IlInterpreter.dll, the UnityPipeline.* Roslyn plugins) is Development-only by define constraint.
# Unity.Pipeline.Attributes.dll is unconstrained on purpose (inert attributes, so release builds compile) and every
# non-Development Mono player carries it: it is allowed.
function Test-DevelopmentOnlyAssembly([string]$name) {
    if ($name -notlike '*.dll' -or $name -eq 'Unity.Pipeline.Attributes.dll') { return $false }
    return ($name -like 'Unity.Pipeline*' -or $name -like 'UnityPipeline.*')
}
# Content that must not reach players even inside kept files (content-editor-build-1/2, launcher-5, architecture-1).
function Get-ReleaseProblems([string]$root) {
    $problems = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        if (Test-Excluded $relative) { return }
        if ($_.Name -like 'GfxPluginNativeRender*') { $problems.Add("native overlay helper $relative") }
        if (Test-DevelopmentOnlyAssembly $_.Name) { $problems.Add("development-only assembly $relative") }
        if ($relative -eq 'Let-me-sleep_Data/boot.config') {
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                if ($line -match '^player-connection' -or ($line -match '^wait-for-native-debugger=' -and $line.Trim() -ne 'wait-for-native-debugger=0')) {
                    $problems.Add("boot.config: $(($line -split '=')[0])")
                }
            }
        }
        if ($_.Extension -in '.json', '.txt', '.config', '.ini', '.xml', '.md', '.cfg' -and $_.Length -lt 4MB) {
            if (Select-String -LiteralPath $_.FullName -Pattern 'N:[\\/]+LetMeSleep' -Quiet) { $problems.Add("build-machine path in $relative") }
        }
    }
    return $problems
}
$problems = Get-ReleaseProblems $source
if ($problems.Count -ne 0) { throw ('The build carries content that must not ship: ' + ($problems -join '; ')) }

# Every invocation gets a new directory. Existing builds and packages stay intact.
$run = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
if (Test-Path -LiteralPath $run) { throw "Output already exists: $run" }
$packageName = "Let-me-sleep-$version-Windows"
$package = Join-Path $run $packageName
[IO.Directory]::CreateDirectory($package) | Out-Null
$skipped = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $source -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($source, $_.FullName).Replace('\', '/')
    if (Test-Excluded $relative) { $skipped.Add($relative); return }
    $target = Join-Path $package $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $target
}

$files = [ordered]@{}
Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($package, $_.FullName).Replace('\', '/')
    if ($relative -eq 'BUILD.json') { throw 'Build directory already contains a package manifest.' }
    if (Test-Excluded $relative) { throw "Excluded content reached the package: $relative" }
    $files[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest = [ordered]@{
    version = $version; releaseSeries = 1; executable = 'Let-me-sleep.exe'; profile = 'release'
    sourceCommit = $receipt.sourceCommit; unity = $receipt.unity; files = $files
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'BUILD.json') -Encoding utf8NoBOM

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $run ($packageName + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($package, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
if ((Get-Item -LiteralPath $zip).Length -gt 2147483648) { throw 'Package exceeds launcher download limit.' }

# Re-read the ZIP itself: exactly the manifest's files plus BUILD.json, all under one top folder, nothing excluded.
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object { $_.FullName -notmatch '/$' } | ForEach-Object { $_.FullName.Replace('\', '/') })
} finally { $archive.Dispose() }
$prefix = "$packageName/"
foreach ($entry in $entries) {
    if (-not $entry.StartsWith($prefix, [StringComparison]::Ordinal)) { throw "ZIP entry outside $prefix : $entry" }
    if (Test-Excluded $entry.Substring($prefix.Length)) { throw "Excluded content inside the ZIP: $entry" }
}
if ($entries.Count -ne $files.Count + 1 -or -not ($entries -contains ($prefix + 'BUILD.json'))) { throw 'ZIP contents do not match BUILD.json.' }

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($hash + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256.txt') -Encoding ascii
# Marker the launcher 1.2.0 still reads: without it the release falls into series 0 and 0.2.0 stays preferred.
@{ releaseSeries = 1; firstVersion = '0.2.0'; launcherMinimum = '1.2.0' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'Let-me-sleep-series-2.json') -Encoding utf8NoBOM
$summary = [ordered]@{
    package = $zip; sha256 = $hash; bytes = (Get-Item -LiteralPath $zip).Length; version = $version; profile = 'release'
    sourceCommit = $receipt.sourceCommit; fileCount = $files.Count; excluded = $skipped; published = $false
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'package.json') -Encoding utf8NoBOM
$summary | ConvertTo-Json -Depth 4
