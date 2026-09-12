param([string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$taskVerification = Join-Path $PSScriptRoot '.verification'
New-Item -ItemType Directory -Path $taskVerification -Force | Out-Null
$taskSource = Join-Path $taskRepository 'unity/Assets/LetMeSleep/Content/Editor/Environment/AlfaQualityExterior.cs'
$taskReferences = @(Get-ChildItem "$UnityEditorData/NetStandard/ref/2.1.0" -Filter '*.dll') + @(Get-ChildItem "$UnityEditorData/Managed/UnityEngine" -Filter 'Unity*.dll')
$taskRsp = @('-nologo','-target:library','-langversion:latest','-nostdlib+',('-out:"'+(Join-Path $taskVerification 'ExteriorImporter.dll')+'"'))
$taskRsp += @($taskReferences | Where-Object { $_.Name -notin @('UnityEngine.dll','UnityEditor.dll') } | ForEach-Object { '-r:"'+$_.FullName+'"' })
$taskDependencies = @('unity/Assets/LetMeSleep/Content/Environment/EnvironmentMapDefinition.cs',
    'unity/Assets/LetMeSleep/Gameplay.Unity/GameplaySurface.cs',
    'unity/Assets/LetMeSleep/Content/Editor/Environment/EnvironmentSampleBuilder.cs')
$taskRsp += @($taskDependencies | ForEach-Object { '"'+(Join-Path $taskRepository $_)+'"' })
$taskRsp += '"'+$taskSource+'"'
$taskRspPath = Join-Path $taskVerification 'compile.rsp'
$taskRsp | Set-Content -LiteralPath $taskRspPath -Encoding utf8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" ('@'+$taskRspPath)
if ($LASTEXITCODE -ne 0) { throw 'Exterior importer offline compile failed.' }
[ordered]@{
    status = 'PASS_OFFLINE_COMPILE_ONLY'
    scope = 'New importer plus actual source of map data, surface and shared folder helper against installed Unity API; no editor/import/render.'
    unityApiVersion = '6000.3.24f1'
    sourceSha256 = (Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash.ToLowerInvariant()
    dependencies = @($taskDependencies | ForEach-Object { @{ name=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $taskRepository $_) -Algorithm SHA256).Hash.ToLowerInvariant() } })
    pending = @('Native import','Generated assets','Visual review','Gameplay traversal')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'offline_compile_receipt.json') -Encoding utf8
Write-Output 'Exterior importer offline compile PASS; no Unity process started.'
