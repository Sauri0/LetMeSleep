param(
    [Parameter(Mandatory=$true)][string]$Recipe,
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskVerification = Join-Path $PSScriptRoot '.verification'
New-Item -ItemType Directory -Path $taskVerification -Force | Out-Null
$taskRuntime = Get-ChildItem "$UnityEditorData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$taskOutput = Join-Path $taskVerification 'RecipeFileChecks.dll'
$taskRsp = @('-nologo', '-target:exe', '-langversion:latest', '-nostdlib+', ('-out:"'+$taskOutput+'"'))
$taskRsp += @(Get-ChildItem $taskRuntime.FullName -Filter 'System.*.dll' |
    Where-Object { $_.Name -notlike '*.Native.dll' } | ForEach-Object { '-r:"'+$_.FullName+'"' })
$taskRsp += '"'+(Join-Path $taskRepository 'unity/Assets/LetMeSleep/Content/Editor/Environment/Higgsfield/HiggsfieldImportContract.cs')+'"'
$taskRsp += '"'+(Join-Path $PSScriptRoot 'RecipeFileChecks.cs')+'"'
$taskRspPath = Join-Path $taskVerification 'RecipeFileChecks.rsp'
$taskRsp | Set-Content -LiteralPath $taskRspPath -Encoding utf8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" ('@'+$taskRspPath)
if ($LASTEXITCODE -ne 0) { throw 'Recipe contract checker compile failed.' }
@{ runtimeOptions = @{ tfm='net6.0'; framework=@{ name='Microsoft.NETCore.App'; version=$taskRuntime.Name } } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskVerification 'RecipeFileChecks.runtimeconfig.json') -Encoding utf8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" $taskOutput (Resolve-Path -LiteralPath $Recipe).Path
if ($LASTEXITCODE -ne 0) { throw 'Recipe contract check failed.' }
