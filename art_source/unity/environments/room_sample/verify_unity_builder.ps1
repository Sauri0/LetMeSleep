param([string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$verification = Join-Path $PSScriptRoot '.verification'
New-Item -ItemType Directory -Path $verification -Force | Out-Null
$source = Join-Path $repository 'unity/Assets/LetMeSleep/Content/Editor/Environment/EnvironmentSampleBuilder.cs'
$references = @(Get-ChildItem "$UnityEditorData/NetStandard/ref/2.1.0" -Filter '*.dll') + @(Get-ChildItem "$UnityEditorData/Managed/UnityEngine" -Filter 'Unity*.dll')
$rsp = @('-nologo','-target:library','-langversion:latest','-nostdlib+',('-out:"'+(Join-Path $verification 'EnvironmentBuilder.dll')+'"'))
$rsp += @($references | Where-Object { $_.Name -notin @('UnityEngine.dll','UnityEditor.dll') } | ForEach-Object { '-r:"'+$_.FullName+'"' })
$rsp += '"'+$source+'"'
$rspPath = Join-Path $verification 'compile.rsp'
$rsp | Set-Content -LiteralPath $rspPath -Encoding utf8
& "$UnityEditorData/NetCoreRuntime/dotnet.exe" "$UnityEditorData/DotNetSdkRoslyn/csc.dll" ('@'+$rspPath)
if($LASTEXITCODE -ne 0) { throw 'Offline Unity API compilation failed' }
$receipt = [ordered]@{
    scope = 'C# compiler against installed Unity managed API only; no Editor, import, rendering or Play mode executed'
    unity_api_version = '6000.3.24f1'
    compiler = 'Unity bundled DotNetSdkRoslyn/csc.dll'
    exit_code = 0
    source_sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    checks = @('C# compilation','Unity API names and signatures')
    pending = @('Native Editor import and builder execution','Prefab and scene audit','Visual and gameplay review')
}
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repository 'docs/unity/environment/UNITY-BUILDER-OFFLINE-CHECK.json') -Encoding utf8
Write-Output 'Unity builder offline compilation: PASS (no Editor started).'
