param(
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/SurfaceAdapter-20260912'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$output = Join-Path $OutputRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $output | Out-Null
function Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\','/')) }
$engine = @(Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll')
$assemblies = Join-Path $Central 'unity/Library/ScriptAssemblies'
$dependencies = @($engine) + @(Get-Item -LiteralPath (Join-Path $assemblies 'LetMeSleep.Core.dll'),(Join-Path $assemblies 'LetMeSleep.Content.Characters.dll'),(Join-Path $assemblies 'LetMeSleep.Presentation.dll'),(Join-Path $assemblies 'Unity.InputSystem.dll'))
foreach ($module in @('Gameplay','Gameplay.Unity','Actor')) {
    $folder = Join-Path $output $module; $sources = Join-Path $folder 'sources'
    New-Item -ItemType Directory -Path $sources | Out-Null
    if ($module -eq 'Actor') {
        Copy-Item -LiteralPath (Join-Path $workspace 'unity/Assets/LetMeSleep/Presentation/Gameplay/ActorVisualBinding.cs') -Destination $sources
    } else {
        Get-ChildItem -LiteralPath (Join-Path $workspace ('unity/Assets/LetMeSleep/' + $module)) -Filter '*.cs' | Copy-Item -Destination $sources
    }
    $refs = @('<Reference Include="' + (Xml (Join-Path $assemblies 'LetMeSleep.Core.dll')) + '"/>')
    $projects = ''
    if ($module -ne 'Gameplay') {
        $refs += @($engine | ForEach-Object { '<Reference Include="' + (Xml $_.FullName) + '"/>' })
        $projects = '<ProjectReference Include="../Gameplay/Compile.csproj"/>'
    }
    if ($module -eq 'Gameplay.Unity') { $refs += '<Reference Include="' + (Xml (Join-Path $assemblies 'Unity.InputSystem.dll')) + '"/>' }
    if ($module -eq 'Actor') {
        $projects += '<ProjectReference Include="../Gameplay.Unity/Compile.csproj"/>'
        foreach ($name in @('Content.Characters','Presentation')) { $refs += '<Reference Include="' + (Xml (Join-Path $assemblies ('LetMeSleep.' + $name + '.dll'))) + '"/>' }
    }
    $name = if ($module -eq 'Actor') { 'SurfaceActorCompile' } else { 'LetMeSleep.' + $module }
    $refXml = $refs -join "`n"
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><AssemblyName>$name</AssemblyName></PropertyGroup><ItemGroup><Compile Include="sources/*.cs"/>$projects$refXml</ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $folder 'Compile.csproj')
}
& dotnet build (Join-Path $output 'Actor/Compile.csproj') --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $output" }
[ordered]@{
    sourceHead=(& git -C $workspace rev-parse HEAD); centralHead=(& git -C $Central rev-parse HEAD)
    scope='Offline domain/Unity adapter/ActorVisualBinding compile only. Do not load these replacement assemblies into Unity.'
    nativeExecuted=$false
    sources=@(Get-ChildItem -LiteralPath $output -Recurse -Filter '*.cs' | Where-Object { $_.Directory.Name -eq 'sources' } | ForEach-Object { [ordered]@{file=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    dependencies=@($dependencies | ForEach-Object { [ordered]@{file=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
Write-Output "Evidence: $output"
