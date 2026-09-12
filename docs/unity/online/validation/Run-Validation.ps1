param(
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$ScriptAssemblies = 'N:/LetMeSleep/Repository/unity/Library/ScriptAssemblies',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
if (!$OutputDirectory) { $OutputDirectory = 'N:/LetMeSleep/Validation/Online-' + [Guid]::NewGuid().ToString('N') }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory to preserve previous evidence.' }

$dependencies = [ordered]@{
    Core = Join-Path $ScriptAssemblies 'LetMeSleep.Core.dll'
    Gameplay = Join-Path $ScriptAssemblies 'LetMeSleep.Gameplay.dll'
    Epic = Join-Path $ScriptAssemblies 'com.Epic.OnlineServices.dll'
    PlayEveryWare = Join-Path $ScriptAssemblies 'com.playeveryware.eos.core.dll'
    UnityCore = Join-Path $UnityEditorData 'Managed/UnityEngine/UnityEngine.CoreModule.dll'
    UnityJson = Join-Path $UnityEditorData 'Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll'
}
foreach ($dependency in $dependencies.Values) {
    if (!(Test-Path -LiteralPath $dependency)) { throw "Missing local dependency: $dependency" }
}

New-Item -ItemType Directory -Path $output | Out-Null
function Escape-Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\', '/')) }
$rootXml = Escape-Xml $workspace
$coreXml = Escape-Xml $dependencies.Core
$gameplayXml = Escape-Xml $dependencies.Gameplay
$epicXml = Escape-Xml $dependencies.Epic
$playEveryWareXml = Escape-Xml $dependencies.PlayEveryWare
$unityCoreXml = Escape-Xml $dependencies.UnityCore
$unityJsonXml = Escape-Xml $dependencies.UnityJson

Set-Content -LiteralPath (Join-Path $output 'Directory.Build.props') -Value '<Project><PropertyGroup><BaseIntermediateOutputPath>obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath></PropertyGroup></Project>'
$online = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><DefineConstants>DEVELOPMENT_BUILD</DefineConstants><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="$rootXml/unity/Assets/LetMeSleep/Online/*.cs"/><Reference Include="$coreXml"/><Reference Include="$gameplayXml"/><Reference Include="$epicXml"/><Reference Include="$playEveryWareXml"/><Reference Include="$unityCoreXml"/><Reference Include="$unityJsonXml"/></ItemGroup></Project>
"@
$checks = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="$rootXml/unity/Assets/LetMeSleep/Online/LobbyJoinPolicy.cs"/><Compile Include="$rootXml/docs/unity/online/validation/LobbyJoinPolicyChecks.cs"/><Reference Include="$coreXml"/></ItemGroup></Project>
"@
Set-Content -LiteralPath (Join-Path $output 'Online.csproj') -Value $online
Set-Content -LiteralPath (Join-Path $output 'Checks.csproj') -Value $checks

& dotnet build (Join-Path $output 'Online.csproj') --nologo -v minimal 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Online compilation failed.' }
& dotnet run --project (Join-Path $output 'Checks.csproj') --nologo 2>&1 | Tee-Object -FilePath (Join-Path $output 'checks.log')
if ($LASTEXITCODE -ne 0) { throw 'Online policy checks failed.' }

$hashes = [ordered]@{}
foreach ($sourceDirectory in @('unity/Assets/LetMeSleep/Online', 'docs/unity/online/validation')) {
    Get-ChildItem -LiteralPath (Join-Path $workspace $sourceDirectory) -File | Sort-Object Name | ForEach-Object {
        $hashes["$sourceDirectory/$($_.Name)"] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
[ordered]@{
    scope = 'External C# compilation and pure policy assertions only; EOS native SDK was not loaded; no Unity Test Runner, transport, local pair or WAN'
    utc = [DateTime]::UtcNow.ToString('o')
    sourceHashes = $hashes
    compileExitCode = 0
    checksExitCode = 0
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'receipt.json')
Write-Output "Evidence: $output"
