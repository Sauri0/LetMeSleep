param([string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskVerification = Join-Path $PSScriptRoot '.verification'
New-Item -ItemType Directory -Path $taskVerification -Force | Out-Null
$taskRefs = @(Get-ChildItem "$UnityEditorData/NetStandard/ref/2.1.0" -Filter '*.dll') + @(Get-ChildItem "$UnityEditorData/Managed/UnityEngine" -Filter 'Unity*.dll')
$taskRefs = @($taskRefs | Where-Object { $_.Name -notin @('UnityEngine.dll','UnityEditor.dll') } | ForEach-Object { '-r:"'+$_.FullName+'"' })
function Compile-Higgsfield([string]$Name, [string[]]$Sources, [string[]]$ExtraRefs = @(), [string]$Target = 'library') {
    $taskRsp = @('-nologo', "-target:$Target", '-langversion:latest', '-nostdlib+', ('-out:"'+(Join-Path $taskVerification "$Name.dll")+'"')) + $taskRefs
    $taskRsp += @($ExtraRefs | ForEach-Object { '-r:"'+(Join-Path $taskVerification $_)+'"' })
    $taskRsp += @($Sources | ForEach-Object { '"'+(Join-Path $taskRepository $_)+'"' })
    $taskRspPath = Join-Path $taskVerification "$Name.rsp"
    $taskRsp | Set-Content -LiteralPath $taskRspPath -Encoding utf8
    & "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" ('@'+$taskRspPath)
    if ($LASTEXITCODE -ne 0) { throw "$Name offline compile failed." }
}
$taskRuntime = @('unity/Assets/LetMeSleep/Content/Environment/EnvironmentMapDefinition.cs', 'unity/Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/HiggsfieldLowPolyWater.cs')
$taskSurface = @('unity/Assets/LetMeSleep/Gameplay.Unity/GameplaySurface.cs')
$taskContract = 'unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldImportContract.cs'
$taskEditor = @($taskContract, 'unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldEnvironmentImporter.cs')
Compile-Higgsfield 'Higgsfield.Runtime' $taskRuntime
Compile-Higgsfield 'Higgsfield.Surface' $taskSurface
Compile-Higgsfield 'Higgsfield.Editor' $taskEditor @('Higgsfield.Runtime.dll', 'Higgsfield.Surface.dll')
Compile-Higgsfield 'ContractChecks' @($taskContract, 'Higgsfield/Integration/ContractChecks.cs') @() 'exe'
$taskVersion = (Get-ChildItem "$UnityEditorData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name
@{ runtimeOptions = @{ tfm='net6.0'; framework=@{ name='Microsoft.NETCore.App'; version=$taskVersion } } } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskVerification 'ContractChecks.runtimeconfig.json') -Encoding utf8
$taskTestOutput = & "$UnityEditorData/NetCoreRuntime/dotnet.exe" (Join-Path $taskVerification 'ContractChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Offline contract checks failed.' }
$taskTestOutput | Write-Output
[ordered]@{
    status = 'PASS_OFFLINE_COMPILE_AND_CONTRACT_CHECKS_ONLY'
    unityApiVersion = '6000.3.24f1'
    checks = $taskTestOutput
    assemblySeparation = 'Runtime and surface compiled separately; Editor references both. Contract checks execute pure System code only.'
    files = @(@($taskRuntime + $taskSurface + $taskEditor + 'Higgsfield/Integration/ContractChecks.cs') | ForEach-Object { @{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $taskRepository $_) -Algorithm SHA256).Hash.ToLowerInvariant() } })
    pending = @('Final FBX and actual hierarchy/material names', 'Native import and saved asset roundtrip', 'Spawn clearance and 16-player capacity', 'Axis/scale and route checks', 'Water animation and performance', 'Visual review', 'RoomRules/UI/Online registration')
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'offline-validation.json') -Encoding utf8
