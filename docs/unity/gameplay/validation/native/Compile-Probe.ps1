param(
    [string]$UnityEditorData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$Central = 'N:/LetMeSleep/Repository',
    [string]$OutputRoot = 'N:/LetMeSleep/Validation/SurfaceVisual-20260912'
)
$ErrorActionPreference = 'Stop'
$buildId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$output = Join-Path $OutputRoot ('probe-' + $buildId)
New-Item -ItemType Directory -Path $output | Out-Null
$assemblyName = 'SurfaceVisualProbe_' + $buildId.Replace('-', '_')
$source = Join-Path $PSScriptRoot 'SurfaceVisualProbe.cs'
Copy-Item -LiteralPath $source -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $output
$references = @(
    Get-ChildItem -LiteralPath (Join-Path $UnityEditorData 'Managed/UnityEngine') -Filter '*.dll'
    Get-Item -LiteralPath (Join-Path $UnityEditorData 'Managed/Newtonsoft.Json.dll')
    Get-ChildItem -LiteralPath (Join-Path $Central 'unity/Library/ScriptAssemblies') -Filter 'LetMeSleep*.dll' |
        Where-Object { $_.Name -notmatch '\.Tests\.|\.Editor\.' }
    Get-Item -LiteralPath (Join-Path $Central 'unity/Library/ScriptAssemblies/Unity.InputSystem.dll')
)
function Xml([string]$value) { [Security.SecurityElement]::Escape($value.Replace('\', '/')) }
$refXml = ($references | ForEach-Object { '<Reference Include="' + (Xml $_.FullName) + '"><Private>false</Private></Reference>' }) -join "`n"
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>$assemblyName</AssemblyName><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup>
  <ItemGroup><Compile Include="SurfaceVisualProbe.cs"/>$refXml</ItemGroup>
</Project>
"@
$projectFile = Join-Path $output 'Probe.csproj'
Set-Content -LiteralPath $projectFile -Value $project
& dotnet build $projectFile --nologo -v minimal --ignore-failed-sources 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Probe compilation failed: $output" }
$assembly = (Join-Path $output "bin/Debug/netstandard2.1/$assemblyName.dll").Replace('\', '/')
$load = @"
var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
foreach (var existing in assemblies) {
    if (!existing.GetName().Name.StartsWith("SurfaceVisualProbe_")) continue;
    var probe = existing.GetType("SurfaceVisualProbe");
    if (probe != null && (bool)probe.GetField("active", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null))
        return "Refusing load while a probe is active. Run its Stop.cs first.";
}
var loaded = System.Reflection.Assembly.LoadFrom("$assembly");
return loaded.FullName;
"@
Set-Content -LiteralPath (Join-Path $output 'Load.cs') -Value $load
$typeExpression = 'System.Reflection.Assembly.LoadFrom("' + $assembly + '").GetType("SurfaceVisualProbe")'
foreach ($surface in @('floor', 'wall', 'ceiling')) {
    foreach ($mode in @('Manual', 'Continuous')) {
        $manual = if ($mode -eq 'Manual') { 'true' } else { 'false' }
        $body = 'return ' + $typeExpression + '.GetMethod("Start").Invoke(null, new object[] { "' + $surface + '", ' + $manual + ' });'
        Set-Content -LiteralPath (Join-Path $output "Start-$surface-$mode.cs") -Value $body
    }
}
foreach ($method in @('Status', 'Next', 'Stop')) {
    Set-Content -LiteralPath (Join-Path $output "$method.cs") -Value ('return ' + $typeExpression + '.GetMethod("' + $method + '").Invoke(null, null);')
}
$inputs = $references | ForEach-Object { [ordered]@{ path = $_.FullName; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash; lastWriteUtc = $_.LastWriteTimeUtc } }
[ordered]@{
    scope = 'External compile only. No Unity process, rendered evidence, native acceptance, FPS or WAN.'
    utc = [DateTime]::UtcNow.ToString('o'); assembly = $assembly; unityEditorData = $UnityEditorData
    centralHead = (& git -C $Central rev-parse HEAD); centralStatus = @(& git -C $Central status --short)
    sourceSha256 = (Get-FileHash -LiteralPath $source).Hash; assemblySha256 = (Get-FileHash -LiteralPath $assembly).Hash
    inputAssemblies = @($inputs); compileExitCode = 0; nativeExecuted = $false; nativeVisualAccepted = $false
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'compile-receipt.json')
Write-Output "Probe package: $output"
