param(
    [Parameter(Mandatory=$true)][string]$Python,
    [string]$Godot = '',
    [switch]$PrepareDependencies
)
$ErrorActionPreference = 'Stop'
$bootstrapRoot = $PSScriptRoot
Set-Location -LiteralPath $bootstrapRoot
New-Item -ItemType Directory -Force -Path './logs' | Out-Null
if ($PrepareDependencies) {
    & $Python './bootstrap_downloads.py'
    if ($LASTEXITCODE -ne 0) { throw 'Dependency download or digest verification failed' }
    if (-not (Test-Path -LiteralPath './deps/godot-cpp/.git')) {
        & git clone --depth 1 --branch godot-4.5-stable https://github.com/godotengine/godot-cpp.git './deps/godot-cpp'
        if ($LASTEXITCODE -ne 0) { throw 'godot-cpp clone failed' }
    }
}
$bindingCommit = & git -C './deps/godot-cpp' rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $bindingCommit -ne 'e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77') { throw 'Unexpected godot-cpp revision' }
$bindingChanges = & git -C './deps/godot-cpp' status --porcelain
if ($bindingChanges) { throw 'godot-cpp source must match the unmodified pinned checkout' }
$cmake = Join-Path $bootstrapRoot '_tools/cmake-4.4.3-windows-x86_64/bin/cmake.exe'
$compiler = Join-Path $bootstrapRoot '_tools/llvm-mingw-20260826-ucrt-x86_64/bin'
$ninja = Join-Path $bootstrapRoot '_tools/ninja-1.13.2/ninja.exe'
$configureArgs = @('-S', 'extension', '-B', 'b', '-G', 'Ninja', '-DCMAKE_BUILD_TYPE=Release', "-DCMAKE_C_COMPILER=$compiler/clang.exe", "-DCMAKE_CXX_COMPILER=$compiler/clang++.exe", "-DCMAKE_MAKE_PROGRAM=$ninja", '-DCMAKE_OBJECT_PATH_MAX=240', "-DPython3_EXECUTABLE=$Python")
& $cmake @configureArgs *> './logs/configure-repro.log'
if ($LASTEXITCODE -ne 0) { Get-Content './logs/configure-repro.log' -Tail 35; throw 'Configure failed' }
& $cmake --build b --parallel 2 *> './logs/build-repro.log'
if ($LASTEXITCODE -ne 0) { Get-Content './logs/build-repro.log' -Tail 35; throw 'Build failed' }
if ($Godot) {
    & $Godot --headless --audio-driver Dummy --path './extension/smoke' --editor --import --quit --frame-delay 800 *> './logs/import.log'
    if ($LASTEXITCODE -ne 0) { throw 'Isolated smoke import failed; inspect logs/import.log' }
    & $Godot --headless --audio-driver Dummy --path './extension/smoke' *> './logs/smoke.log'
    if ($LASTEXITCODE -ne 0) { Get-Content './logs/smoke.log'; throw 'Synthetic codec smoke failed' }
    if (-not (Select-String -Path './logs/smoke.log' -Pattern 'LMS_OPUS_SMOKE checks=\d+ failures=0')) { throw 'Smoke completion marker missing' }
}
Get-FileHash './extension/smoke/bin/lms_opus.windows.x86_64.dll' -Algorithm SHA256
