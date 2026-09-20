param([string]$Variant = 'Player')
$ErrorActionPreference = 'Stop'
$repository = 'N:/LetMeSleep/Repository'
$base = "N:/LetMeSleep/Validation/V020/ModesUI/$Variant"
New-Item -ItemType Directory -Force $base | Out-Null
$modules = @('Core','Gameplay','UI','Content.Environment','Gameplay.Unity','Online','Presentation.Gameplay','Bootstrap')
$folders = @{'Presentation.Gameplay'='Presentation/Gameplay'; 'Content.Environment'='Content/Environment'}
$constants = if ($Variant -eq 'Editor') {'UNITY_EDITOR'} else {'DEVELOPMENT_BUILD'}
foreach ($module in $modules) {
  $directory = Join-Path $base $module
  New-Item -ItemType Directory -Force $directory | Out-Null
  $folder = if ($folders.ContainsKey($module)) {$folders[$module]} else {$module}
  $refs = @(Get-ChildItem N:/Unity/Editors/6000.3.24f1/Editor/Data/Managed/UnityEngine -Filter '*.dll' | Where-Object { $Variant -eq 'Editor' -or $_.Name -notlike 'UnityEditor*' })
  $refs += @(Get-ChildItem "$repository/unity/Library/ScriptAssemblies" -Filter '*.dll' | Where-Object { $_.Name -notmatch 'Tests|Editor' -and $_.BaseName -notin @($modules | ForEach-Object {'LetMeSleep.'+$_}) })
  $refXml = ($refs | ForEach-Object { '<Reference Include="'+$_.FullName.Replace('\','/')+'"><Private>false</Private></Reference>' }) -join "`n"
  $dependencies = switch ($module) { 'Core' {@()} 'Gameplay' {@('Core')} 'UI' {@('Core')} 'Content.Environment' {@()} 'Gameplay.Unity' {@('Core','Gameplay','Content.Environment')} 'Online' {@('Core','Gameplay')} 'Presentation.Gameplay' {@('Core','Gameplay','Gameplay.Unity')} 'Bootstrap' {@('Core','Gameplay','UI','Content.Environment','Gameplay.Unity','Online','Presentation.Gameplay')} }
  $projectRefs = ($dependencies | ForEach-Object {'<ProjectReference Include="../'+$_+'/Compile.csproj"/>'}) -join "`n"
  @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation><DefineConstants>$constants</DefineConstants><AssemblyName>LetMeSleep.$module</AssemblyName></PropertyGroup><ItemGroup><Compile Include="$repository/unity/Assets/LetMeSleep/$folder/**/*.cs" Exclude="$repository/unity/Assets/LetMeSleep/$folder/**/Editor/**/*.cs"/>$projectRefs $refXml</ItemGroup></Project>
"@ | Set-Content (Join-Path $directory 'Compile.csproj')
}
dotnet build "$base/Bootstrap/Compile.csproj" --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object "$base/compile.log"
exit $LASTEXITCODE
