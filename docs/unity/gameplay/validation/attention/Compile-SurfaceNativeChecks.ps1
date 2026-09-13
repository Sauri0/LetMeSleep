param(
    [string]$Modules = '',
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/SurfaceNativeChecks-20260912'
)
$ErrorActionPreference = 'Stop'
$mode = if ($Modules) { 'PatchedModules' } else { 'Integrated' }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$output = Join-Path $OutputRoot ($mode + '-' + $stamp)
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SurfaceTraversalNativeChecks.cs') -Destination $output
$assemblies = Join-Path $Central 'unity/Library/ScriptAssemblies'
$refs = @(Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll')
$refs += Get-Item -LiteralPath (Join-Path $assemblies 'LetMeSleep.Core.dll')
foreach ($name in @('Gameplay','Gameplay.Unity')) {
    $path = if ($Modules) { Join-Path $Modules ($name + '/bin/Debug/netstandard2.1/LetMeSleep.' + $name + '.dll') } else { Join-Path $assemblies ('LetMeSleep.' + $name + '.dll') }
    $refs += Get-Item -LiteralPath $path
}
function Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\','/')) }
$refXml = ($refs | ForEach-Object { '<Reference Include="' + (Xml $_.FullName) + '"><Private>false</Private></Reference>' }) -join "`n"
$assembly = 'SurfaceTraversalChecks_' + $mode + '_' + $stamp.Replace('-','_')
@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><AssemblyName>$assembly</AssemblyName></PropertyGroup><ItemGroup><Compile Include="SurfaceTraversalNativeChecks.cs"/>$refXml</ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Compile.csproj')
& dotnet build (Join-Path $output 'Compile.csproj') --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $output" }
$dll = Join-Path $output ('bin/Debug/netstandard2.1/' + $assembly + '.dll')
[ordered]@{ mode=$mode; nativeExecuted=$false; assembly=$dll; sourceHash=(Get-FileHash -LiteralPath (Join-Path $output 'SurfaceTraversalNativeChecks.cs')).Hash; dependencies=@($refs | ForEach-Object { [ordered]@{file=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} }) } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
if (!$Modules) {
    $literal = $dll.Replace('\','/').Replace('"','""')
    @"
var assembly = System.Reflection.Assembly.LoadFrom(@"$literal");
return assembly.GetType("SurfaceTraversalNativeChecks").GetMethod("RunAll").Invoke(null, null);
"@ | Set-Content -LiteralPath (Join-Path $output 'run-in-director-slot.cs')
}
Write-Output "Evidence: $output"
