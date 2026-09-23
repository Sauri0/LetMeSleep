# Smoke test of the v0.3.0 DIAGNOSTICS player (WindowsAlfaBuild.BuildV030Diagnostics).
# The release player (BuildV030) compiles the probe out, so the smoke runs on a Development build of the
# same commit; pass -ReleaseBuildDirectory to prove both receipts name the same source commit.
# Each case: menu -> training as human and as mosquito on <map>/<mode> -> menu -> create/leave an EOS room,
# written by AlfaApplication.Probe to player-probe.json plus menu.png, human.png and mosquito.png.
# Runs with a visible window and the default graphics API: hidden or -force-d3d11 runs produced black PNGs.
param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [string]$ReleaseBuildDirectory,
    [switch]$Matrix
)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$receipt = Get-Content -Raw -LiteralPath (Join-Path $build 'build-receipt.json') | ConvertFrom-Json
if ($receipt.result -ne 'Succeeded' -or $receipt.version -ne '0.3.0' -or $receipt.profile -ne 'development' -or $receipt.sourceDirty -ne $false) {
    throw 'Smoke needs a clean, successful v0.3.0 diagnostics build (BuildV030Diagnostics).'
}
if ($ReleaseBuildDirectory) {
    $release = Get-Content -Raw -LiteralPath (Join-Path $ReleaseBuildDirectory 'build-receipt.json') | ConvertFrom-Json
    if ($release.profile -ne 'release' -or $release.sourceCommit -ne $receipt.sourceCommit) {
        throw "The diagnostics build ($($receipt.sourceCommit)) is not the release build's commit ($($release.sourceCommit))."
    }
}
$exe = Join-Path $build 'Let-me-sleep.exe'
$maps = [ordered]@{ casa = 'hf-casa-del-patio-v1'; camp = 'hf-campamento-pinar-v2'; port = 'hf-puerto-del-faro-v1'
    isla = 'hf-isla-del-laguito-v2'; yate = 'hf-yate-a-la-deriva-v3' }
$cases = @(if ($Matrix) { foreach ($m in $maps.Keys) { foreach ($mode in 'blood', 'survival', 'tasks') { , @($m, $mode) } } } else { , @('casa', 'blood') })
$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) { throw "Evidence directory already exists: $root" }
New-Item -ItemType Directory -Path $root | Out-Null
$results = @()
foreach ($case in $cases) {
    $name = $case[0] + '-' + $case[1]; $mapId = $maps[$case[0]]; $mode = $case[1]
    $casePath = Join-Path $root $name; $dataPath = Join-Path $casePath 'data'
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
    $arguments = @('--lms-probe-output', $casePath, '--lms-probe-mode', $mode, '--lms-probe-map', $mapId,
        '--lms-validation-data', $dataPath, '-logFile', (Join-Path $casePath 'player.log'))
    Write-Output "START $name"
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
    if (-not $process.WaitForExit(180000)) { $process.Kill(); throw "Player timed out: $name" }
    $probePath = Join-Path $casePath 'player-probe.json'
    if (-not (Test-Path -LiteralPath $probePath)) { throw "Missing probe receipt: $name; exit=$($process.ExitCode)" }
    $probe = Get-Content -Raw -LiteralPath $probePath | ConvertFrom-Json
    $functional = $process.ExitCode -eq 0 -and $probe.mapId -eq $mapId -and $probe.modeId -eq $mode -and
        $probe.humanRuntime -and $probe.mosquitoRuntime -and $probe.humanStationary -and $probe.returnedToMenu -and
        $probe.onlineRoomCreated -and $probe.onlineRoomLeft -and $probe.humanGameplayVisible -and
        $probe.mosquitoGameplayVisible -and $probe.runtimeErrorCount -eq 0 -and [string]::IsNullOrEmpty($probe.failure)
    # A black 1920x1080 frame compresses to ~27 KB; real frames were several hundred KB.
    $pngs = [ordered]@{}
    foreach ($png in 'menu.png', 'human.png', 'mosquito.png') {
        $file = Join-Path $casePath $png
        $pngs[$png] = if (Test-Path -LiteralPath $file) { (Get-Item -LiteralPath $file).Length } else { 0 }
    }
    $imagesPlausible = @($pngs.Values | Where-Object { $_ -lt 100KB }).Count -eq 0
    $result = [ordered]@{ case = $name; mapId = $mapId; mode = $mode; process_exit = $process.ExitCode
        functional_pass = [bool]$functional; png_bytes = $pngs; png_size_plausible = $imagesPlausible
        probe_sha256 = (Get-FileHash -LiteralPath $probePath -Algorithm SHA256).Hash.ToLowerInvariant()
        sourceCommit = $receipt.sourceCommit; visual_verified = $false; two_identity_gameplay_verified = $false; wan_verified = $false }
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $casePath 'execution.json') -Encoding utf8NoBOM
    $results += [pscustomobject]$result
    Write-Output "RESULT $name functional_pass=$functional png_size_plausible=$imagesPlausible exit=$($process.ExitCode)"
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'smoke.json') -Encoding utf8NoBOM
$failed = @($results | Where-Object { -not $_.functional_pass -or -not $_.png_size_plausible })
if ($failed.Count -ne 0) { throw ('Smoke failed: ' + (($failed | ForEach-Object case) -join ', ') + '. Inspect player.log and the PNGs.') }
"SMOKE PASSED $($results.Count) case(s). Look at every PNG before accepting: a passing receipt does not prove the look."
