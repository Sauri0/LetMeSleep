# MANDATORY manual walkthrough of the v0.3.0 RELEASE player, exactly as friends get it (the packaged ZIP), before
# publishing. The release player compiles the smoke probe out, so smoke-v030.ps1 only covers the diagnostics build;
# this script is the functional evidence for the binary that is uploaded. An operator plays; the script:
#   1. checks the ZIP against its .sha256.txt, extracts it to <OutputRoot>/game, re-hashes every file against BUILD.json
#      and rechecks boot.config (no player-connection*, no native-debugger wait), .pdb and *_DoNotShip content;
#   2. launches Let-me-sleep.exe with -logFile <OutputRoot>/player.log;
#   3. walks the operator through: menu (0.3.0, no "Development Build"), training as HUMAN, training as MOSQUITO,
#      creating an EOS room (code visible), leaving it, quitting from the menu; at each step it captures the game
#      window (only when the game is the foreground window) and records the operator's yes/no and note;
#   4. scans player.log for exceptions and LMS_*FAILED/MISSING/INVALID/UNSUPPORTED markers;
#   5. writes walkthrough.json; pass=true only when every package check, capture and confirmation holds, the game
#      exits with 0 and the log is clean. A pass is not a visual review: look at every PNG.
# -VerifyOnly runs step 1 only (used by package-v030-selftest.ps1 and as a dry run); it never passes the gate.
param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [switch]$VerifyOnly,
    [ValidateRange(2, 30)][int]$CaptureDelaySeconds = 5
)
$ErrorActionPreference = 'Stop'
$version = '0.3.0'
$packageName = "Let-me-sleep-$version-Windows"
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$zip = Join-Path $package "$packageName.zip"
$shaFile = "$zip.sha256.txt"
foreach ($required in $zip, $shaFile) { if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing $required (pass a package-v030.ps1 output directory)." } }
$root = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $root) { throw "Evidence directory already exists: $root" }
New-Item -ItemType Directory -Path $root | Out-Null
$result = [ordered]@{ schema = 'lms-release-walkthrough-1'; mode = $(if ($VerifyOnly) { 'verify-only' } else { 'manual' })
    version = $version; startedUtc = [DateTime]::UtcNow.ToString('o'); zip = $zip }
function Save-Result { $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'walkthrough.json') -Encoding utf8NoBOM }

# 1. Package checks on what is uploaded, not on the build folder.
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$result.zipSha256 = $zipHash
$result.shaFileMatches = ((Get-Content -Raw -LiteralPath $shaFile).Trim() -eq "$zipHash  $packageName.zip")
$gameRoot = Join-Path $root 'game'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($zip, $gameRoot)
$game = Join-Path $gameRoot $packageName
$manifest = Get-Content -Raw -LiteralPath (Join-Path $game 'BUILD.json') | ConvertFrom-Json
$result.sourceCommit = $manifest.sourceCommit
$result.manifestValid = $manifest.version -eq $version -and $manifest.profile -eq 'release' -and $manifest.releaseSeries -eq 1 -and
    $manifest.executable -eq 'Let-me-sleep.exe' -and $manifest.sourceCommit -match '^[a-f0-9]{40}$'
$expected = @{}; foreach ($p in $manifest.files.PSObject.Properties) { $expected[$p.Name] = $p.Value }
$mismatched = [System.Collections.Generic.List[string]]::new(); $extra = [System.Collections.Generic.List[string]]::new()
$debugContent = [System.Collections.Generic.List[string]]::new()
$seen = @{}
Get-ChildItem -LiteralPath $game -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($game, $_.FullName).Replace('\', '/')
    if ($relative -eq 'BUILD.json') { return }
    if ($relative -like '*.pdb' -or $relative -match '_DoNotShip|_ButDontShipItWithYourGame' -or $relative -eq 'build-receipt.json') { $debugContent.Add($relative) }
    if (-not $expected.ContainsKey($relative)) { $extra.Add($relative); return }
    $seen[$relative] = $true
    if ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected[$relative]) { $mismatched.Add($relative) }
}
$missing = @($expected.Keys | Where-Object { -not $seen.ContainsKey($_) })
$result.extractedFilesMatchManifest = $mismatched.Count -eq 0 -and $extra.Count -eq 0 -and $missing.Count -eq 0
$result.fileProblems = [ordered]@{ mismatched = @($mismatched); extra = @($extra); missing = $missing }
$bootConfig = Join-Path $game 'Let-me-sleep_Data/boot.config'
$bootProblems = @(if (Test-Path -LiteralPath $bootConfig) {
    Get-Content -LiteralPath $bootConfig | Where-Object { $_ -match '^player-connection' -or ($_ -match '^wait-for-native-debugger=' -and $_.Trim() -ne 'wait-for-native-debugger=0') } |
        ForEach-Object { ($_ -split '=')[0] }
} else { 'boot.config missing' })
$result.bootConfigClean = $bootProblems.Count -eq 0
$result.bootConfigProblems = $bootProblems
$result.noDebugContent = $debugContent.Count -eq 0
$result.packageVerified = $result.shaFileMatches -and $result.manifestValid -and $result.extractedFilesMatchManifest -and $result.bootConfigClean -and $result.noDebugContent
if ($VerifyOnly -or -not $result.packageVerified) {
    $result.pass = $false; $result.endedUtc = [DateTime]::UtcNow.ToString('o'); Save-Result
    if (-not $result.packageVerified) { throw "Package verification failed: see $(Join-Path $root 'walkthrough.json')." }
    "PACKAGE VERIFIED (verify-only: the manual walkthrough still has to run) $root"
    return
}

# 2-3. Play the extracted release exe with the operator.
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class LmsWalkthroughWindow {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
'@
[LmsWalkthroughWindow]::SetProcessDPIAware() | Out-Null
function Save-GameCapture([int]$processId, [string]$path) {
    $window = [LmsWalkthroughWindow]::GetForegroundWindow()
    [uint32]$owner = 0; [LmsWalkthroughWindow]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
    if ($owner -ne $processId) { return $false }
    $rect = New-Object LmsWalkthroughWindow+RECT
    [LmsWalkthroughWindow]::GetWindowRect($window, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    if ($width -lt 320 -or $height -lt 240) { return $false }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size) } finally { $graphics.Dispose() }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    return $true
}
$steps = @(
    [ordered]@{ id = '01-menu'; instruction = 'Menú principal: tiene que decir 0.3.0 y NO tiene que aparecer "Development Build" en ninguna esquina.' }
    [ordered]@{ id = '02-training-human'; instruction = 'ENTRENAMIENTO -> elegí mapa y modo -> INICIAR en la tarjeta HUMANO. Movete (WASD), corré (Shift), saltá y golpeá (clic). Dejá el humano jugando en pantalla.' }
    [ordered]@{ id = '03-training-mosquito'; instruction = 'Esc -> SALIR DEL ENTRENAMIENTO -> SALIR. ENTRENAMIENTO -> INICIAR en la tarjeta MOSQUITO. Volá (W), subí (Espacio), bajá (Ctrl) y posate (F). Dejá el mosquito jugando en pantalla.' }
    [ordered]@{ id = '04-room-created'; instruction = 'Esc -> SALIR DEL ENTRENAMIENTO -> SALIR. JUGAR -> CREAR SALA, escribí un nombre y creá la sala. Esperá a ver la sala con su código (EOS).' }
    [ordered]@{ id = '05-room-left'; instruction = 'Esc -> SALIR DE LA SALA -> SALIR. Tenés que volver al menú sin avisos de error.' }
)
$exe = Join-Path $game 'Let-me-sleep.exe'
$log = Join-Path $root 'player.log'
Write-Host "Evidencia: $root"
Write-Host 'Si aparece SmartScreen: Más información -> Ejecutar de todas formas (es parte de la prueba).'
$process = Start-Process -FilePath $exe -ArgumentList "-logFile `"$log`"" -WorkingDirectory $game -PassThru
$null = $process.Handle # keeps ExitCode readable after the player exits
$result.steps = @()
foreach ($step in $steps) {
    Write-Host ''
    Write-Host "[$($step.id)] $($step.instruction)"
    $png = Join-Path $root ($step.id + '.png')
    $captured = $false
    while (-not $captured -and -not $process.HasExited) {
        $null = Read-Host "Cuando la pantalla muestre eso, apretá Enter acá y volvé al juego: la captura sale en $CaptureDelaySeconds s"
        Start-Sleep -Seconds $CaptureDelaySeconds
        $captured = Save-GameCapture $process.Id $png
        if (-not $captured) { Write-Host 'El juego no estaba al frente: volvé a intentarlo.' }
    }
    $bytes = if (Test-Path -LiteralPath $png) { (Get-Item -LiteralPath $png).Length } else { 0 }
    $answer = Read-Host '¿Se vio y funcionó exactamente lo pedido? (s/n)'
    $note = Read-Host 'Nota (opcional; sin datos personales)'
    # A black frame of the game window compresses to a few tens of KB; real frames are several hundred KB.
    $result.steps += [ordered]@{ id = $step.id; instruction = $step.instruction; png = $(if ($captured) { [IO.Path]::GetFileName($png) } else { $null })
        pngBytes = $bytes; pngPlausible = $bytes -ge 100KB; operatorConfirmed = $answer -match '^\s*(s|si|sí|y|yes)\s*$'; note = $note }
    Save-Result
}
Write-Host ''
Write-Host '[06-quit] En el menú principal: SALIR -> SALIR para cerrar el juego.'
if (-not $process.WaitForExit(300000)) { $process.Kill(); $result.processExit = 'timeout (killed after 5 min)' } else { $result.processExit = $process.ExitCode }
$quit = Read-Host '¿El juego se cerró con SALIR, sin cuelgues ni ventanas de error? (s/n)'
$result.quitConfirmed = $quit -match '^\s*(s|si|sí|y|yes)\s*$'

# 4. Log scan.
$logText = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log } else { @() }
$result.logPresent = $logText.Count -gt 0
$exceptions = @($logText | Where-Object { $_ -match '^\s*[A-Za-z_][\w.]*Exception\b' })
$markers = @($logText | Where-Object { $_ -match 'LMS_[A-Z_]*(FAILED|MISSING|INVALID|UNSUPPORTED)' })
$result.logExceptions = $exceptions.Count
$result.logFailureMarkers = $markers.Count
$result.logFindings = @($exceptions + $markers | Select-Object -First 20)

# 5. Verdict.
$result.endedUtc = [DateTime]::UtcNow.ToString('o')
$stepsOk = @($result.steps).Count -eq $steps.Count -and @($result.steps | Where-Object { -not $_.png -or -not $_.pngPlausible -or -not $_.operatorConfirmed }).Count -eq 0
$result.pass = [bool]($result.packageVerified -and $stepsOk -and $result.processExit -eq 0 -and $result.quitConfirmed -and $result.logPresent -and
    $result.logExceptions -eq 0 -and $result.logFailureMarkers -eq 0)
$result.visualReviewedByAgent = $false; $result.twoIdentityVerified = $false; $result.wanVerified = $false
Save-Result
if (-not $result.pass) { throw "WALKTHROUGH FAILED: see $(Join-Path $root 'walkthrough.json'), the PNGs and player.log." }
"WALKTHROUGH PASSED $root. Look at every PNG before publishing: a pass does not prove the look."
