param(
    [ValidateSet('PatchedSources','Integrated')][string]$Mode = 'PatchedSources',
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/StrikePose-20260912'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$output = Join-Path $OutputRoot ($Mode + '-' + $stamp)
$sources = Join-Path $output 'sources'
New-Item -ItemType Directory -Path $sources | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'StrikePoseChecks.cs') -Destination $sources
if ($Mode -eq 'PatchedSources') {
    Copy-Item -LiteralPath (Join-Path $workspace 'unity/Assets/LetMeSleep/Presentation/Gameplay/ActorVisualBinding.cs') -Destination $sources
}
$names = @('Core','Gameplay','Gameplay.Unity','Content.Characters','Presentation')
if ($Mode -eq 'Integrated') { $names += 'Presentation.Gameplay' }
$references = @(Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll')
$references += @($names | ForEach-Object { Get-Item -LiteralPath (Join-Path $Central ('unity/Library/ScriptAssemblies/LetMeSleep.' + $_ + '.dll')) })
function Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\','/')) }
$refXml = ($references | ForEach-Object { '<Reference Include="' + (Xml $_.FullName) + '"><Private>false</Private></Reference>' }) -join "`n"
$assembly = 'StrikePoseChecks_' + $Mode + '_' + $stamp.Replace('-','_')
$project = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><AssemblyName>$assembly</AssemblyName></PropertyGroup><ItemGroup><Compile Include="sources/*.cs"/>$refXml</ItemGroup></Project>
"@
$projectPath = Join-Path $output 'Checks.csproj'
Set-Content -LiteralPath $projectPath -Value $project
& dotnet build $projectPath --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $output" }
$dll = Join-Path $output ('bin/Debug/netstandard2.1/' + $assembly + '.dll')
[ordered]@{
    mode=$Mode; sourceHead=(& git -C $workspace rev-parse HEAD); centralHead=(& git -C $Central rev-parse HEAD)
    scope='Offline compile only. PatchedSources is NOT a runtime plugin; use Integrated after Director imports the fix.'
    nativeExecuted=$false; assembly=$dll
    sources=@(Get-ChildItem -LiteralPath $sources -Filter '*.cs' | ForEach-Object { [ordered]@{file=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    dependencies=@($references | ForEach-Object { [ordered]@{file=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
if ($Mode -eq 'Integrated') {
    $literal = $dll.Replace('\','/').Replace('"','""')
    @"
var assembly = System.Reflection.Assembly.LoadFrom(@"$literal");
return assembly.GetType("StrikePoseChecks").GetMethod("RunAll").Invoke(null, null);
"@ | Set-Content -LiteralPath (Join-Path $output 'run-in-director-slot.cs')
}
Write-Output "Evidence: $output"
