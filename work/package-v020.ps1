param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [string]$OutputRoot = 'N:/LetMeSleep/Artifacts/v0.2.0/packages'
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $BuildDirectory).Path
$receiptPath = Join-Path $source 'build-receipt.json'
$receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
if ($receipt.result -ne 'Succeeded' -or $receipt.errors -ne 0 -or $receipt.version -ne '0.2.0' -or $receipt.sourceDirty) {
    throw 'A successful, clean v0.2.0 build receipt is required.'
}
if ($receipt.sourceCommit -notmatch '^[a-f0-9]{40}$') { throw 'Missing source commit.' }
if (-not (Test-Path -LiteralPath (Join-Path $source 'Let-me-sleep.exe') -PathType Leaf)) { throw 'Game executable missing.' }
if (-not (Test-Path -LiteralPath (Join-Path $source 'UnityPlayer.dll') -PathType Leaf)) { throw 'Unity runtime missing.' }

# Every invocation gets a new directory. Existing builds and packages stay intact.
$run = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$packageName = 'Let-me-sleep-0.2.0-Windows'
$package = Join-Path $run $packageName
[IO.Directory]::CreateDirectory($package) | Out-Null
Get-ChildItem -LiteralPath $source -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $package -Recurse }

$files = [ordered]@{}
Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($package, $_.FullName).Replace('\','/')
    if ($relative -eq 'BUILD.json') { throw 'Build directory already contains a package manifest.' }
    $files[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest = [ordered]@{
    version = '0.2.0'; releaseSeries = 1; executable = 'Let-me-sleep.exe'
    sourceCommit = $receipt.sourceCommit; unity = $receipt.unity; files = $files
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'BUILD.json') -Encoding utf8NoBOM

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $run ($packageName + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($package, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
if ((Get-Item -LiteralPath $zip).Length -gt 2147483648) { throw 'Package exceeds launcher download limit.' }
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($hash + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256.txt') -Encoding ascii
@{ releaseSeries = 1; firstVersion = '0.2.0'; launcherMinimum = '1.2.0' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'Let-me-sleep-series-2.json') -Encoding utf8NoBOM
[ordered]@{ package=$zip; sha256=$hash; sourceCommit=$receipt.sourceCommit; fileCount=$files.Count; published=$false } | ConvertTo-Json
