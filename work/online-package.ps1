# Build/package helpers. Importing this file never initializes EOS or logs credentials.
function Initialize-OnlineBuildConfiguration {
    param([string]$ProjectRoot)
    $source = Join-Path $ProjectRoot 'work/eos-config.local.cfg'
    $target = Join-Path $ProjectRoot 'game/eos.local.cfg'
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $target }
    if (-not (Test-Path -LiteralPath $target)) { throw 'Online release configuration missing: work/eos-config.local.cfg' }
    $text = [IO.File]::ReadAllText($target)
    if ($text -notmatch '(?m)^\[eos\]\s*$') { throw 'Online configuration section missing.' }
    foreach ($field in @('product_id','sandbox_id','deployment_id','client_id','client_secret')) {
        if (-not [regex]::IsMatch($text,'(?m)^'+$field+'\s*=\s*"[^"\r\n]+"\s*$')) { throw ('Online configuration field missing: '+$field) }
    }
    & git -C $ProjectRoot check-ignore --quiet game/eos.local.cfg
    if ($LASTEXITCODE -ne 0) { throw 'The local online configuration must remain excluded from Git.' }
    $preset = [IO.File]::ReadAllText((Join-Path $ProjectRoot 'game/export_presets.cfg'))
    if ($preset -notmatch '(?m)^include_filter="eos\.local\.cfg"\s*$') { throw 'Export must include the local online configuration.' }
}

function Get-OnlinePackageFiles {
    param([string]$ProjectRoot, [string]$OutputDirectory)
    $addon = Join-Path $ProjectRoot 'game/addons/epic-online-services-godot'
    $result = [Collections.Generic.List[string]]::new()
    foreach ($relative in @('bin/windows/libeosg.windows.template_release.x86_64.dll','bin/windows/EOSSDK-Win64-Shipping.dll','bin/windows/x64/xaudio2_9redist.dll')) {
        $source = Join-Path $addon $relative
        $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $copies = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File -Filter ([IO.Path]::GetFileName($relative)))
        if ($copies.Count -eq 0) { throw ('Missing online runtime DLL: '+[IO.Path]::GetFileName($relative)) }
        foreach ($copy in $copies) {
            if ((Get-FileHash -LiteralPath $copy.FullName -Algorithm SHA256).Hash -ne $expected) { throw ('Online DLL differs from build source: '+$copy.Name) }
            $result.Add($copy.FullName)
        }
    }
    foreach ($notice in Get-ChildItem -LiteralPath (Join-Path $addon 'licenses') -File) {
        $copy = Join-Path (Join-Path $OutputDirectory 'Licencias-online') $notice.Name
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $notice.FullName -Algorithm SHA256).Hash) { throw ('Online notice differs from source: '+$notice.Name) }
        $result.Add($copy)
    }
    return $result.ToArray()
}
