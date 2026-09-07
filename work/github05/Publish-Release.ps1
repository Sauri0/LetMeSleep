[CmdletBinding()]
param(
    [string]$Repository = 'Sauri0/LetMeSleep',
    [string]$Manifest = '',
    [string]$Notes = '',
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
if (-not $Manifest) { $Manifest = Join-Path $PSScriptRoot '../../outputs/MANIFIESTO-0.4.0.json' }
if (-not $Notes) { $Notes = Join-Path $PSScriptRoot 'release-notes-0.4.0.md' }
if (-not (Test-Path -LiteralPath $Notes -PathType Leaf)) { throw 'Faltan las notas de la Release.' }
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$artifactDirectory = Split-Path $manifestPath -Parent
$record = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$tag = 'v' + [string]$record.version
$commit = [string]$record.source_and_docs_commit
if ($Repository -ne 'Sauri0/LetMeSleep' -or $tag -notmatch '^v\d+\.\d+\.\d+$' -or $commit -notmatch '^[a-fA-F0-9]{40}$') {
    throw 'Repositorio, version o commit inesperado.'
}
$packages = @($record.artifacts | Where-Object { $_.file -match '^Let-me-sleep-[0-9.]+-(Windows|fuentes)\.zip$' })
if ($packages.Count -ne 2) { throw 'El manifiesto debe identificar dos paquetes ZIP.' }
$assets = @()
foreach ($package in $packages) {
    $path = Join-Path $artifactDirectory ([string]$package.file)
    $actual = Get-FileHash -LiteralPath $path -Algorithm SHA256
    if ($actual.Hash -ne $package.sha256 -or (Get-Item -LiteralPath $path).Length -ne $package.bytes) {
        throw ('Paquete distinto del manifiesto: ' + $package.file)
    }
    $sidecar = $path + '.sha256.txt'
    $expectedLine = [string]$package.sha256 + '  ' + [string]$package.file
    if ((Get-Content -LiteralPath $sidecar -Raw).Trim() -ne $expectedLine) { throw 'SHA256 adjunto inconsistente.' }
    $assets += $path
    $assets += $sidecar
}
$assets += $manifestPath
$assetHashes = @{}
foreach ($path in $assets) { $assetHashes[[IO.Path]::GetFileName($path)] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
$repoJson = & gh repo view $Repository --json visibility,viewerPermission,isArchived
if ($LASTEXITCODE -ne 0) { throw 'No se pudo comprobar el acceso a GitHub.' }
$repo = $repoJson | ConvertFrom-Json
if ($repo.isArchived -or $repo.viewerPermission -notin @('ADMIN','MAINTAIN','WRITE')) { throw 'Falta acceso de escritura al repositorio activo.' }
$remoteUrl = 'https://github.com/' + $Repository + '.git'
$remoteRefs = @(& git ls-remote $remoteUrl ('refs/tags/' + $tag) ('refs/tags/' + $tag + '^{}'))
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar la etiqueta remota.' }
$peeled = @($remoteRefs | Where-Object { $_.EndsWith('^{}') })
$tagLine = if ($peeled.Count) { $peeled[0] } elseif ($remoteRefs.Count) { $remoteRefs[0] } else { '' }
$tagCommit = if ($tagLine) { ($tagLine -split '\s+')[0] } else { '' }
if ($tagCommit -and $tagCommit -ne $commit) { throw 'La etiqueta remota apunta a otro commit; no se reemplazara.' }
Write-Output ('Preflight: {0}, {1}, {2}, cinco assets verificados; etiqueta remota presente={3}.' -f $Repository, $tag, $repo.visibility, [bool]$tagCommit)
if (-not $Publish) { Write-Output 'Comprobacion completa. No hubo escrituras en GitHub ni cambios de Git local.'; return }
if (-not $tagCommit) { throw 'Root debe subir antes la etiqueta exacta. El script no crea ni mueve ramas o tags.' }
$existingJson = & gh release list --repo $Repository --limit 100 --json tagName
if ($LASTEXITCODE -ne 0) { throw 'No se pudo verificar si la Release ya existe.' }
if (@($existingJson | ConvertFrom-Json | Where-Object { $_.tagName -eq $tag }).Count) { throw 'La Release ya existe. Revisar su estado sin reemplazar assets ni borrar la publicacion.' }
& gh release create $tag @assets --repo $Repository --verify-tag --draft --title ('Let me sleep ' + $record.version + ' - Windows') --notes-file $Notes
if ($LASTEXITCODE -ne 0) { throw 'Creacion/carga incompleta. Revisar el borrador; no se reintentara con clobber.' }
# Verify bytes through authenticated downloads before making the draft available.
$downloadDirectory = Join-Path $PSScriptRoot ('verified-download-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($downloadDirectory) | Out-Null
foreach ($name in $assetHashes.Keys) {
    & gh release download $tag --repo $Repository --pattern $name --dir $downloadDirectory
    if ($LASTEXITCODE -ne 0) { throw ('No se pudo verificar el asset: ' + $name) }
    if ((Get-FileHash -LiteralPath (Join-Path $downloadDirectory $name) -Algorithm SHA256).Hash -ne $assetHashes[$name]) {
        throw ('El asset remoto difiere; se conserva borrador: ' + $name)
    }
}
& gh release edit $tag --repo $Repository --draft=false --latest
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar el borrador verificado.' }
& gh release view $tag --repo $Repository --json url,isDraft,tagName,assets
if ($LASTEXITCODE -ne 0) { throw 'Publicacion enviada, comprobacion final no disponible; inspeccionar antes de reintentar.' }
