param(
    [string]$Central='N:/LetMeSleep/Repository',
    [string]$UnityEditorData='N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$OutputRoot='N:/LetMeSleep/Validation/AuthorityStrike-20260913'
)
$ErrorActionPreference='Stop'
$probeRoot=$PSScriptRoot
$stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$output=Join-Path $OutputRoot ('compiled-'+$stamp)
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath (Join-Path $probeRoot 'AuthorityStrikeProbe.cs') -Destination $output
Copy-Item -LiteralPath (Join-Path $probeRoot 'AuthorityStrikeJson.cs') -Destination $output
$references=@(Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll')
$names=@('Core','Gameplay','Gameplay.Unity','Content.Characters','Presentation','Presentation.Gameplay')
$references+=@($names | ForEach-Object {Get-Item -LiteralPath (Join-Path $Central ('unity/Library/ScriptAssemblies/LetMeSleep.'+$_+'.dll'))})
function Xml([string]$value){[Security.SecurityElement]::Escape($value.Replace('\','/'))}
$refXml=($references | ForEach-Object {'<Reference Include="'+(Xml $_.FullName)+'"><Private>false</Private></Reference>'}) -join "`n"
$assembly='AuthorityStrikeProbe_'+$stamp.Replace('-','_')
@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><AssemblyName>$assembly</AssemblyName></PropertyGroup><ItemGroup><Compile Include="AuthorityStrikeProbe.cs"/><Compile Include="AuthorityStrikeJson.cs"/>$refXml</ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $output 'Probe.csproj')
& dotnet build (Join-Path $output 'Probe.csproj') --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
$compileExit=$LASTEXITCODE
$dll=Join-Path $output ('bin/Debug/netstandard2.1/'+$assembly+'.dll')
[ordered]@{
    utc=[DateTime]::UtcNow.ToString('o');centralHead=(& git -C $Central rev-parse HEAD);compileExit=$compileExit
    nativeExecuted=$false;scope='Offline compilation only; real imported assets and loaded IK still require Director eval_file.'
    script=Join-Path $output 'AuthorityStrikeProbe.cs';scriptSha256=(Get-FileHash -LiteralPath (Join-Path $output 'AuthorityStrikeProbe.cs')).Hash
    writerSha256=(Get-FileHash -LiteralPath (Join-Path $output 'AuthorityStrikeJson.cs')).Hash
    assembly=$dll
    dependencies=@($references | ForEach-Object {[ordered]@{file=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    sourceFiles=@(@('unity/Assets/LetMeSleep/Presentation/Gameplay/ActorVisualBinding.cs','unity/Assets/LetMeSleep/Gameplay.Unity/GameplayActorProxy.cs') | ForEach-Object {$p=Join-Path $Central $_;[ordered]@{file=$p;sha256=(Get-FileHash -LiteralPath $p).Hash}})
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'offline-receipt.json')
if($compileExit -ne 0){throw "Offline compile failed: $output"}
$dllLiteral=$dll.Replace('\','/').Replace('"','""')
$receiptLiteral=(Join-Path $OutputRoot 'native').Replace('\','/').Replace('"','""')
@"
var assembly = System.Reflection.Assembly.LoadFrom(@"$dllLiteral");
var type = assembly.GetType("AuthorityStrikeProbe");
return type.GetMethod("Run").Invoke(null, new object[] { @"$receiptLiteral" });
"@ | Set-Content -LiteralPath (Join-Path $output 'run-in-director-slot.cs')
Write-Output "Probe compiled: $output"

