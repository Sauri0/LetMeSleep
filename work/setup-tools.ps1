$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$toolsDir = Join-Path $PSScriptRoot 'tools'
[System.IO.Directory]::CreateDirectory($toolsDir) | Out-Null
$baseUri = 'https://github.com/godotengine/godot-builds/releases/download/4.5.2-stable/'
$checksumsPath = Join-Path $toolsDir 'SHA512-SUMS.txt'
Invoke-WebRequest -Uri ($baseUri + 'SHA512-SUMS.txt') -OutFile $checksumsPath
$checksums = [System.IO.File]::ReadAllLines($checksumsPath)
$downloads = @{
    'Godot_v4.5.2-stable_win64.exe.zip' = 'godot-editor.zip'
    'Godot_v4.5.2-stable_export_templates.tpz' = 'godot-templates.tpz'
}
foreach ($name in $downloads.Keys) {
    $destination = Join-Path $toolsDir $downloads[$name]
    if (-not (Test-Path -LiteralPath $destination)) {
        Write-Output "Descargando dependencia oficial: $name"
        Invoke-WebRequest -Uri ($baseUri + $name) -OutFile $destination
    }
    $line = @($checksums | Where-Object { $_.EndsWith('  ' + $name) })
    if ($line.Count -ne 1) { throw "Checksum oficial ausente para $name" }
    $expected = $line[0].Split(' ')[0]
    $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA512).Hash
    if ($actual -ne $expected) { throw "Checksum incorrecto: $destination. No se ejecutara." }
}
Expand-Archive -LiteralPath (Join-Path $toolsDir 'godot-editor.zip') -DestinationPath (Join-Path $toolsDir 'godot-4.5.2') -Force
$templatesDir = Join-Path $toolsDir 'templates'
[System.IO.Directory]::CreateDirectory($templatesDir) | Out-Null
$archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $toolsDir 'godot-templates.tpz'))
try {
    foreach ($entry in $archive.Entries) {
        if ($entry.Name -match '^windows_(debug|release)_x86_64.exe$|^version.txt$') {
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $templatesDir $entry.Name), $true)
        }
    }
} finally { $archive.Dispose() }
Write-Output 'Godot 4.5.2 y plantillas Windows listos dentro del proyecto.'
