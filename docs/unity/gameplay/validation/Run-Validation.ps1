param(
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$InputSystemAssembly = 'N:/LetMeSleep/Repository/unity/Library/ScriptAssemblies/Unity.InputSystem.dll',
    [string]$NUnitAssembly = 'N:/LetMeSleep/Repository/unity/Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll',
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
if (!$OutputDirectory) { $OutputDirectory = 'N:/LetMeSleep/Validation/Gameplay-' + [Guid]::NewGuid().ToString('N') }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory to preserve previous evidence.' }
foreach ($dependency in @($UnityEditorData, $InputSystemAssembly, $NUnitAssembly)) {
    if (!(Test-Path -LiteralPath $dependency)) { throw "Missing local dependency: $dependency" }
}
New-Item -ItemType Directory -Path $output | Out-Null
function Escape-Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\', '/')) }
$rootXml = Escape-Xml $workspace
$engineXml = Escape-Xml $UnityEditorData
$inputXml = Escape-Xml $InputSystemAssembly
$nunitXml = Escape-Xml $NUnitAssembly
Set-Content -LiteralPath (Join-Path $output 'Directory.Build.props') -Value '<Project><PropertyGroup><BaseIntermediateOutputPath>obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath></PropertyGroup></Project>'
$domain = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="$rootXml/unity/Assets/LetMeSleep/Core/*.cs"/><Compile Include="$rootXml/unity/Assets/LetMeSleep/Gameplay/*.cs"/><Compile Include="$rootXml/unity/Assets/LetMeSleep/Online/GameplayWireCodec.cs"/></ItemGroup></Project>
"@
$adapter = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><ProjectReference Include="Domain.csproj"/><Compile Include="$rootXml/unity/Assets/LetMeSleep/Gameplay.Unity/*.cs"/><Reference Include="$engineXml/Managed/UnityEngine/*.dll"/><Reference Include="$inputXml"/></ItemGroup></Project>
"@
$checks = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><ProjectReference Include="Domain.csproj"/><Compile Include="$rootXml/docs/unity/gameplay/validation/*.cs"/><Compile Include="$rootXml/unity/Assets/LetMeSleep/Tests/EditMode/RoomSession*.cs"/><Compile Include="$rootXml/unity/Assets/LetMeSleep/Tests/EditMode/Gameplay*.cs"/><Reference Include="$nunitXml"/></ItemGroup></Project>
"@
Set-Content -LiteralPath (Join-Path $output 'Domain.csproj') -Value $domain
Set-Content -LiteralPath (Join-Path $output 'Adapter.csproj') -Value $adapter
Set-Content -LiteralPath (Join-Path $output 'Checks.csproj') -Value $checks
& dotnet build (Join-Path $output 'Adapter.csproj') --nologo -v minimal 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Adapter compilation failed.' }
& dotnet run --project (Join-Path $output 'Checks.csproj') --nologo 2>&1 | Tee-Object -FilePath (Join-Path $output 'checks.log')
if ($LASTEXITCODE -ne 0) { throw 'CPU checks failed.' }
$hashes = [ordered]@{}
$codecRelative = 'unity/Assets/LetMeSleep/Online/GameplayWireCodec.cs'
$hashes[$codecRelative] = (Get-FileHash -LiteralPath (Join-Path $workspace $codecRelative) -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($sourceDirectory in @('unity/Assets/LetMeSleep/Core', 'unity/Assets/LetMeSleep/Gameplay', 'unity/Assets/LetMeSleep/Gameplay.Unity', 'docs/unity/gameplay/validation')) {
    Get-ChildItem -LiteralPath (Join-Path $workspace $sourceDirectory) -File | Sort-Object Name | ForEach-Object {
        $hashes["$sourceDirectory/$($_.Name)"] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
[ordered]@{ scope = 'External C# compilation and CPU assertions only; no Unity Test Runner, rendered scene, FPS or WAN'; utc = [DateTime]::UtcNow.ToString('o'); unityEditorData = $UnityEditorData; sourceHashes = $hashes; compileExitCode = 0; checksExitCode = 0 } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
Write-Output "Evidence: $output"
