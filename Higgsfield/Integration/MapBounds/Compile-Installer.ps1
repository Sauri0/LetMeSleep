param(
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$EditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data'
)
$ErrorActionPreference='Stop'
$stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$output=Join-Path 'N:/LetMeSleep/Validation/Higgsfield/CompleteScope/MapBounds' ('compile-'+$stamp)
New-Item -ItemType Directory -Path $output | Out-Null
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../unity/Assets/Editor/ProjectBootstrap/HiggsfieldMapBoundaryInstaller.cs'))
Copy-Item -LiteralPath $source -Destination (Join-Path $output 'HiggsfieldMapBoundaryInstaller.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CaptureBoundaryDependencies.cs') -Destination $output
$refs=@(Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed/UnityEngine') -Filter '*.dll')
$refs+=Get-Item -LiteralPath (Join-Path $EditorData 'Managed/Newtonsoft.Json.dll')
foreach($name in @('Core','Gameplay','Gameplay.Unity','Content.Environment')){
    $refs+=Get-Item -LiteralPath (Join-Path $Central ('unity/Library/ScriptAssemblies/LetMeSleep.'+$name+'.dll'))
}
$xml=($refs | ForEach-Object {'<Reference Include="'+[Security.SecurityElement]::Escape($_.FullName)+'"><Private>false</Private></Reference>'}) -join "`n"
$project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><UseSharedCompilation>false</UseSharedCompilation><AssemblyName>MapBounds_'+$stamp.Replace('-','_')+'</AssemblyName></PropertyGroup><ItemGroup>'+$xml+'</ItemGroup></Project>'
$projectPath=Join-Path $output 'MapBounds.csproj'
Set-Content -LiteralPath $projectPath -Value $project
& dotnet build $projectPath --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Offline installer compilation failed.'}
[ordered]@{
    status='PASS_OFFLINE_COMPILE_ONLY';nativeExecuted=$false;source=$source
    sourceSha256=(Get-FileHash -LiteralPath $source).Hash
    dependencies=@($refs | ForEach-Object {[ordered]@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'compile-receipt.json')
Write-Output $output
