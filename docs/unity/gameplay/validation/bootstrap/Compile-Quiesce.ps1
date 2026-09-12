param(
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$UiAssembly = '',
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/BootstrapQuiesce-20260912'
)
$ErrorActionPreference = 'Stop'
if (!$UiAssembly) { $UiAssembly = Join-Path $Central 'unity/Library/ScriptAssemblies/LetMeSleep.UI.dll' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$output = Join-Path $OutputRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
$sources = Join-Path $output 'sources'
New-Item -ItemType Directory -Path $sources | Out-Null
$centralSources = Get-ChildItem -LiteralPath (Join-Path $Central 'unity/Assets/LetMeSleep/Bootstrap') -Filter '*.cs' |
    Where-Object { $_.Name -ne 'AlfaApplication.cs' }
$main = Join-Path $workspace 'unity/Assets/LetMeSleep/Bootstrap/AlfaApplication.cs'
foreach ($file in $centralSources) { Copy-Item -LiteralPath $file.FullName -Destination $sources }
Copy-Item -LiteralPath $main -Destination $sources
$references = @(
    Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll'
    Get-ChildItem -LiteralPath (Join-Path $Central 'unity/Library/ScriptAssemblies') -Filter 'LetMeSleep*.dll' |
        Where-Object { $_.Name -notmatch 'Bootstrap|\.Tests\.|\.Editor\.' -and $_.Name -ne 'LetMeSleep.UI.dll' }
    Get-Item -LiteralPath $UiAssembly
    foreach ($name in @('Unity.InputSystem.dll', 'Unity.TextMeshPro.dll', 'UnityEngine.UI.dll', 'com.Epic.OnlineServices.dll')) {
        Get-Item -LiteralPath (Join-Path $Central "unity/Library/ScriptAssemblies/$name")
    }
)
function Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\', '/')) }
foreach ($variant in @('Editor', 'Player')) {
    $build = Join-Path $output $variant
    New-Item -ItemType Directory -Path $build | Out-Null
    $constants = if ($variant -eq 'Editor') { 'UNITY_EDITOR' } else { '' }
    $variantReferences = $references | Where-Object { $variant -eq 'Editor' -or $_.Name -notlike 'UnityEditor*' }
    $refXml = ($variantReferences | ForEach-Object { '<Reference Include="' + (Xml $_.FullName) + '"><Private>false</Private></Reference>' }) -join "`n"
    $project = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><DefineConstants>$constants</DefineConstants><AssemblyName>BootstrapQuiesce.$variant</AssemblyName></PropertyGroup><ItemGroup><Compile Include="../sources/*.cs"/>$refXml</ItemGroup></Project>
"@
    $projectFile = Join-Path $build 'Compile.csproj'
    Set-Content -LiteralPath $projectFile -Value $project
    & dotnet build $projectFile --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $build 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw "$variant compilation failed: $output" }
}
[ordered]@{
    utc = [DateTime]::UtcNow.ToString('o'); scope = 'Offline compilation of Editor and Player preprocessor paths only; no Unity, audio, PID exit or native lifecycle assertion.'
    sourceMain = $main; centralHead = (& git -C $Central rev-parse HEAD)
    sources = @(Get-ChildItem -LiteralPath $sources -Filter '*.cs' | ForEach-Object { [ordered]@{ name = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    inputAssemblies = @($references | ForEach-Object { [ordered]@{ path = $_.FullName; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    editorCompileExitCode = 0; playerCompileExitCode = 0; nativeExecuted = $false
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
Write-Output "Evidence: $output"
