param([string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$verification = Join-Path $PSScriptRoot '.verification'
New-Item -ItemType Directory -Path $verification -Force | Out-Null
$source = Join-Path $repository 'unity/Assets/LetMeSleep/Content/Editor/Environment/EnvironmentSampleBuilder.cs'
$references = @(Get-ChildItem "$UnityEditorData/NetStandard/ref/2.1.0" -Filter '*.dll') + @(Get-ChildItem "$UnityEditorData/Managed/UnityEngine" -Filter 'Unity*.dll')
function Compile-Subset($name,$files,$dependencies) {
    $rsp = @('-nologo','-target:library','-langversion:latest','-nostdlib+',('-out:"'+(Join-Path $verification ($name+'.dll'))+'"'))
    $rsp += @($references | Where-Object { $_.Name -notin @('UnityEngine.dll','UnityEditor.dll') } | ForEach-Object { '-r:"'+$_.FullName+'"' })
    $rsp += @($dependencies | ForEach-Object { '-r:"'+(Join-Path $verification ($_+'.dll'))+'"' })
    $rsp += @($files | ForEach-Object { '"'+$_+'"' })
    $rspPath=Join-Path $verification ($name+'.rsp');$rsp|Set-Content -LiteralPath $rspPath -Encoding utf8
    & "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" ('@'+$rspPath)
    if($LASTEXITCODE -ne 0){throw ('Offline compilation failed: '+$name)}
}
Compile-Subset 'LetMeSleep.Core' @((Join-Path $repository 'unity/Assets/LetMeSleep/Core/RoomSession.cs')) @()
Compile-Subset 'LetMeSleep.Gameplay' @(Get-ChildItem (Join-Path $repository 'unity/Assets/LetMeSleep/Gameplay') -Filter '*.cs' | ForEach-Object FullName) @('LetMeSleep.Core')
# Exact delivered bridge types only; this does not retest the whole Gameplay runtime.
Compile-Subset 'LetMeSleep.Gameplay.Unity' @((Join-Path $repository 'unity/Assets/LetMeSleep/Gameplay.Unity/GameplayDoor.cs'),(Join-Path $repository 'unity/Assets/LetMeSleep/Gameplay.Unity/GameplaySurface.cs')) @('LetMeSleep.Gameplay')
Compile-Subset 'LetMeSleep.Content.Environment' @((Join-Path $repository 'unity/Assets/LetMeSleep/Content/Environment/EnvironmentMapDefinition.cs')) @()
$builderFiles=@(Get-ChildItem (Join-Path $repository 'unity/Assets/LetMeSleep/Content/Editor/Environment') -Filter '*.cs' | ForEach-Object FullName)
Compile-Subset 'EnvironmentBuilder' $builderFiles @('LetMeSleep.Core','LetMeSleep.Gameplay','LetMeSleep.Gameplay.Unity','LetMeSleep.Content.Environment')
$receipt = [ordered]@{
    scope = 'C# compiler against installed Unity managed API only; no Editor, import, rendering or Play mode executed'
    unity_api_version = '6000.3.24f1'
    compiler = 'Unity bundled DotNetSdkRoslyn/csc.dll'
    exit_code = 0
    source_sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    builder_sources = @($builderFiles | ForEach-Object { @{name=[System.IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()} })
    checks = @('C# compilation','Unity API names and signatures','Delivered GameplayDoor/GameplaySurface bridge and map data types')
    pending = @('Native Editor import and builder execution','Prefab and scene audit','Visual and gameplay review')
}
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repository 'docs/unity/environment/UNITY-BUILDER-OFFLINE-CHECK.json') -Encoding utf8
Write-Output 'Unity builder offline compilation: PASS (no Editor started).'
