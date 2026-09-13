param(
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/Higgsfield/MapChecks',
    [string]$Config = (Join-Path $PSScriptRoot 'isla-review-01.checks.json')
)
$ErrorActionPreference = 'Stop'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$output = Join-Path $OutputRoot $stamp
$sources = Join-Path $output 'sources'
New-Item -ItemType Directory -Path $sources | Out-Null
foreach ($file in @('HiggsfieldMapChecks.cs','HiggsfieldMapJson.cs','HiggsfieldIslaPreparation.cs','SouthArrivalColliderCandidate.cs','CompleteSouthArrivalSupport.cs','HiggsfieldCasaPreparation.cs','ApplyCasaSemanticNavigation.cs','HumanMotorContactChecks.cs','HiggsfieldCampPreparation.cs','CampTechnicalCandidate.cs','ApplyCampSemanticNavigation.cs','HiggsfieldRemainingMapsPreparation.cs','YateStorageCandidate.cs','ApplyRemainingSemanticNavigation.cs','YateBulkheadCandidate.cs','PuertoStairCandidate.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $sources
}
Copy-Item -LiteralPath $Config -Destination (Join-Path $output 'checks.json')
$checkConfig = Get-Content -Raw -LiteralPath (Join-Path $output 'checks.json') | ConvertFrom-Json
if ($checkConfig.navigationOverridePath) {
    $navCopy = Join-Path $output 'navigation-candidate.json'
    Copy-Item -LiteralPath $checkConfig.navigationOverridePath -Destination $navCopy
    $checkConfig.navigationOverridePath = $navCopy.Replace('\','/')
    $checkConfig | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $output 'checks.json')
}
$references = @(Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll')
$references += Get-Item -LiteralPath (Join-Path $UnityEditorData 'Managed/Newtonsoft.Json.dll')
# Unity 6000 places the modular UnityEditor.CoreModule in Managed/UnityEngine.
# Do not also reference the monolithic Managed/UnityEditor.dll (duplicate types).
# Only integrated central assemblies. Never rebuild or consume gameplay worktree WIP.
$references += @(@('Core','Gameplay','Gameplay.Unity','Content.Environment') | ForEach-Object {
    Get-Item -LiteralPath (Join-Path $Central ('unity/Library/ScriptAssemblies/LetMeSleep.' + $_ + '.dll'))
})
function Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\','/')) }
$refXml = ($references | ForEach-Object { '<Reference Include="' + (Xml $_.FullName) + '"><Private>false</Private></Reference>' }) -join "`n"
$assembly = 'HiggsfieldMapChecks_' + $stamp.Replace('-','_')
$project = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><AssemblyName>$assembly</AssemblyName></PropertyGroup><ItemGroup><Compile Include="sources/*.cs"/>$refXml</ItemGroup></Project>
"@
$projectPath = Join-Path $output 'Checks.csproj'
Set-Content -LiteralPath $projectPath -Value $project
& dotnet build $projectPath --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $output" }
$dll = Join-Path $output ('bin/Debug/netstandard2.1/' + $assembly + '.dll')
[ordered]@{
    centralHead=(& git -C $Central rev-parse HEAD)
    scope='Offline compilation against integrated central assemblies only. Native execution pending coordinator slot.'
    nativeExecuted=$false; assembly=$dll
    configSha256=(Get-FileHash -LiteralPath (Join-Path $output 'checks.json')).Hash
    sources=@(Get-ChildItem -LiteralPath $sources -Filter '*.cs' | ForEach-Object { [ordered]@{file=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    dependencies=@($references | ForEach-Object { [ordered]@{file=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
$dllLiteral = $dll.Replace('\','/').Replace('"','""')
$configLiteral = (Join-Path $output 'checks.json').Replace('\','/').Replace('"','""')
$resultsLiteral = (Join-Path $output 'native-results').Replace('\','/').Replace('"','""')
@"
var assembly = System.Reflection.Assembly.LoadFrom(@"$dllLiteral");
return assembly.GetType("HiggsfieldMapChecks").GetMethod("Run").Invoke(null, new object[] { @"$configLiteral", @"$resultsLiteral" });
"@ | Set-Content -LiteralPath (Join-Path $output 'run-in-coordinator-slot.cs')
Write-Output "Evidence: $output"
