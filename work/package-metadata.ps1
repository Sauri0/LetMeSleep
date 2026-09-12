# Shared by build and package; importing this file performs no export or writes.
. (Join-Path $PSScriptRoot 'online-package.ps1')
function Get-PackageProtocol {
    param([string]$ProjectRoot)
    $source = [System.IO.File]::ReadAllText((Join-Path $ProjectRoot 'game/scripts/online_invitation.gd'))
    $protocol = [regex]::Match($source,'(?m)^const PROTOCOL := ([0-9]+)')
    $prefix = [regex]::Match($source,'(?m)^const PREFIX := "([A-Z0-9]+-)"')
    if (-not $protocol.Success -or -not $prefix.Success) { throw 'Invitation protocol metadata unavailable.' }
    $project = [System.IO.File]::ReadAllText((Join-Path $ProjectRoot 'game/project.godot'))
    $network = [System.IO.File]::ReadAllText((Join-Path $ProjectRoot 'game/scripts/network.gd'))
    $projectVersion = [regex]::Match($project,'(?m)^config/version="([0-9]+\.[0-9]+\.[0-9]+(?:-(?:alfa|beta|omega|delta|gamma))?)"')
    $networkVersion = [regex]::Match($network,'(?m)^const VERSION := "([0-9]+\.[0-9]+\.[0-9]+(?:-(?:alfa|beta|omega|delta|gamma))?)"')
    if (-not $projectVersion.Success -or -not $networkVersion.Success -or $projectVersion.Groups[1].Value -ne $networkVersion.Groups[1].Value) { throw 'Project and network versions differ.' }
    @{protocol=[int]$protocol.Groups[1].Value; invitation=$prefix.Groups[1].Value; version=$projectVersion.Groups[1].Value}
}

function Write-PackageMetadata {
    param([string]$ProjectRoot, [string]$OutputDirectory, [string]$Version, [bool]$HeadlessTests, [bool]$NativeTests)
    $protocol = Get-PackageProtocol -ProjectRoot $ProjectRoot
    if ($Version -ne $protocol.version) { throw 'Requested build version differs from source.' }
    $commit = (& git -C $ProjectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[a-f0-9]{40}$') { throw 'Cannot identify source commit.' }
    & git -C $ProjectRoot diff --quiet HEAD -- game native/voice native/online art_source
    $sourceDirty = $LASTEXITCODE -ne 0
    $untrackedSource = @(& git -C $ProjectRoot ls-files --others --exclude-standard -- game native/voice native/online art_source)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect untracked build source.' }
    $sourceDirty = $sourceDirty -or $untrackedSource.Count -gt 0
    $exe = Join-Path $OutputDirectory 'Let-me-sleep.exe'
    $dlls = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File -Filter 'lms_opus.windows.x86_64.dll')
    if ($dlls.Count -eq 0) { throw 'Cannot describe a voice build without its Opus DLL.' }
    $expectedDll = (Get-FileHash -LiteralPath (Join-Path $ProjectRoot 'game/addons/lms_opus/bin/lms_opus.windows.x86_64.dll') -Algorithm SHA256).Hash
    foreach ($dll in $dlls) {
        if ((Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash -ne $expectedDll) { throw 'Opus DLL differs from build source.' }
    }
    $files = [ordered]@{}
    $required = @($exe) + @($dlls.FullName)
    $launcher = Join-Path $OutputDirectory 'Let-me-sleep-Launcher.exe'
    if (Test-Path -LiteralPath $launcher) { $required += $launcher }
    $required += @(Get-OnlinePackageFiles -ProjectRoot $ProjectRoot -OutputDirectory $OutputDirectory)
    foreach ($name in @('LEEME.md','LEEME.html','PRUEBAS.md')) { $required += Join-Path $OutputDirectory $name }
    $notices = @(Get-ChildItem -LiteralPath (Join-Path $OutputDirectory 'Licencias-voz') -File)
    $required += @($notices.FullName)
    foreach ($file in $required) {
        $relative = [System.IO.Path]::GetRelativePath($OutputDirectory,$file).Replace('\','/')
        $files[$relative] = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    }
    $metadata = [ordered]@{
        schema=1; version=$Version; protocol=$protocol.protocol; invitation=$protocol.invitation
        source_commit=$commit; source_dirty=$sourceDirty; built_utc=[DateTime]::UtcNow.ToString('o')
        checks=[ordered]@{headless=$HeadlessTests; native=$NativeTests}
        executable='Let-me-sleep.exe'; files=$files
        limits=@('Build checks are not a WAN, microphone hardware, performance or visual certification.','See PRUEBAS.md for the tested scope of this candidate.')
    }
    $text = @(
        "Let me sleep $Version — Windows x86_64"
        "Godot 4.5.2 / Compatibility OpenGL / protocolo $($protocol.protocol) / invitación $($protocol.invitation)"
        'Ejecutable: Let-me-sleep.exe'
        "SHA256: $($files['Let-me-sleep.exe'])"
        "Commit de código: $commit"
        "Fuente con cambios sin commit: $sourceDirty"
        "Comprobaciones de build: headless=$HeadlessTests; nativas=$NativeTests"
        'Manifiesto de archivos: BUILD.json. Alcance y límites: PRUEBAS.md.'
        'Online EOS por invitación LMS1-. El anfitrión ejecuta la sala dentro del juego. Alcance probado: PRUEBAS.md.'
    ) -join [Environment]::NewLine
    [System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'BUILD.txt'),$text+[Environment]::NewLine)
    $files['BUILD.txt'] = (Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'BUILD.txt') -Algorithm SHA256).Hash
    [System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'BUILD.json'),($metadata | ConvertTo-Json -Depth 8)+[Environment]::NewLine)
}

function Test-PackageMetadata {
    param([string]$ProjectRoot, [string]$OutputDirectory, [string]$Version, [switch]$AllowTargetedHeadless)
    $metadata = Get-Content -LiteralPath (Join-Path $OutputDirectory 'BUILD.json') -Raw | ConvertFrom-Json
    $protocol = Get-PackageProtocol -ProjectRoot $ProjectRoot
    if ($Version -ne $protocol.version -or $metadata.schema -ne 1 -or $metadata.version -ne $Version -or $metadata.protocol -ne $protocol.protocol -or $metadata.invitation -ne $protocol.invitation) { throw 'Package version/protocol metadata differs from requested source.' }
    if ($metadata.source_dirty -ne $false) { throw 'Package requires committed source.' }
    if ($metadata.checks.headless -ne $true -or $metadata.checks.native -ne $true) {
        if (-not $AllowTargetedHeadless -or $metadata.verification.mode -ne 'targeted_headless' -or $metadata.verification.report -ne 'VERIFICATION.json') { throw 'Package requires both full suites or explicit targeted-headless verification.' }
        $proof=Get-Content (Join-Path $OutputDirectory 'VERIFICATION.json') -Raw | ConvertFrom-Json
        if ($proof.source_commit -ne $metadata.source_commit -or $proof.native_run -ne $false -or $proof.tests.Count -lt 8) { throw 'Targeted verification identity or coverage missing.' }
        if (@($proof.tests | Where-Object { -not $_.passed -or $_.exit_code -ne 0 -or $_.stderr_bytes -ne 0 -or $_.arguments -notcontains '--headless' }).Count) { throw 'Targeted verification has a failed or non-headless test.' }
        $exportProof=@($proof.tests | Where-Object { -not $_.source -and $_.exe_sha256 -eq $metadata.files.'Let-me-sleep.exe' })
        foreach ($requiredCheck in @('online093-export-ui','online093-export-live-host')) {
            if (-not @($exportProof | Where-Object name -eq $requiredCheck).Count) { throw ('Missing exported check: '+$requiredCheck) }
        }
        if (-not $metadata.files.'VERIFICATION.json') { throw 'Verification report must have a package checksum.' }
    }
    if ($metadata.source_commit -notmatch '^[a-f0-9]{40}$' -or $metadata.executable -ne 'Let-me-sleep.exe') { throw 'Invalid source or executable identity.' }
    $root = [System.IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')+[System.IO.Path]::DirectorySeparatorChar
    $names = @($metadata.files.PSObject.Properties.Name)
    foreach ($required in @('Let-me-sleep.exe','BUILD.txt','LEEME.md','LEEME.html','PRUEBAS.md','Licencias-voz/Opus-COPYING.txt','Licencias-voz/godot-cpp-LICENSE.md')) {
        if ($names -cnotcontains $required) { throw ('Package manifest lacks '+$required) }
    }
    $dllNames = @($names | Where-Object { [System.IO.Path]::GetFileName($_) -eq 'lms_opus.windows.x86_64.dll' })
    if ($dllNames.Count -eq 0) { throw 'Package manifest lacks Opus DLL.' }
    $actualDlls = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File -Filter 'lms_opus.windows.x86_64.dll')
    if ($actualDlls.Count -ne $dllNames.Count) { throw 'Unaccounted Opus DLL in package.' }
    $expectedDll = (Get-FileHash -LiteralPath (Join-Path $ProjectRoot 'game/addons/lms_opus/bin/lms_opus.windows.x86_64.dll') -Algorithm SHA256).Hash
    foreach ($dll in $actualDlls) {
        if ((Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash -ne $expectedDll) { throw 'Packaged Opus DLL differs from build source.' }
    }
    foreach ($notice in Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'game/addons/lms_opus/licenses') -File) {
        $noticeName = 'Licencias-voz/'+$notice.Name
        if ($names -cnotcontains $noticeName -or (Get-FileHash -LiteralPath (Join-Path $OutputDirectory $noticeName) -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $notice.FullName -Algorithm SHA256).Hash) { throw ('Packaged voice notice differs from source: '+$notice.Name) }
    }
    foreach ($onlineFile in @(Get-OnlinePackageFiles -ProjectRoot $ProjectRoot -OutputDirectory $OutputDirectory)) {
        $onlineName = [IO.Path]::GetRelativePath($OutputDirectory,$onlineFile).Replace('\','/')
        if ($names -cnotcontains $onlineName) { throw ('Package manifest lacks online runtime file: '+$onlineName) }
    }
    foreach ($entry in $metadata.files.PSObject.Properties) {
        $path = [System.IO.Path]::GetFullPath((Join-Path $OutputDirectory $entry.Name))
        if (-not $path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes package directory.' }
        if ($entry.Value -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value) { throw ('Package file differs from build: '+$entry.Name) }
    }
    foreach ($name in @('LEEME.md','LEEME.html')) {
        $guide = [System.IO.File]::ReadAllText((Join-Path $OutputDirectory $name))
        if (-not $guide.Contains($protocol.invitation) -or [regex]::IsMatch($guide,'DD[0-4]-')) { throw ('Stale invitation instructions: '+$name) }
    }
}
