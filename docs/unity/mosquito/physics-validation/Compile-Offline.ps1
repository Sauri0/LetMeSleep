param(
    [string]$UnityData = 'N:/Unity/Editors/6000.3.24f1/Editor/Data',
    [string]$CentralUnity = 'N:/LetMeSleep/Repository/unity',
    [string]$OutputDirectory = 'N:/LetMeSleep/Validation/MosquitoPhysics-20260912/compile'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ([IO.Path]::GetPathRoot($output) -ne 'N:\') { throw 'Build output must be on N:.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$dotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$references = @('/reference:' + (Join-Path $UnityData 'NetStandard/ref/2.1.0/netstandard.dll'))
$references += Get-ChildItem -LiteralPath (Join-Path $UnityData 'Managed/UnityEngine') -Filter 'UnityEngine*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $repo 'unity/Assets/LetMeSleep/Presentation/Physics') -Filter '*.cs' | ForEach-Object FullName)
$module = Join-Path $output 'MosquitoRagdoll.Offline.dll'
$moduleLog = @(& $dotnet $compiler /nologo /target:library /nostdlib+ /langversion:9.0 /warnaserror+ ('/out:' + $module) @references @sources 2>&1)
$moduleCode = $LASTEXITCODE
$moduleLog | Set-Content -LiteralPath (Join-Path $output 'module.log')
if ($moduleCode -ne 0) { throw ($moduleLog -join "`n") }
$testReferences = @($references)
$testReferences += Get-ChildItem -LiteralPath (Join-Path $UnityData 'Managed/UnityEngine') -Filter 'UnityEditor*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$testReferences += '/reference:' + (Join-Path $UnityData 'NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll')
$testReferences += '/reference:' + (Join-Path $CentralUnity 'Library/ScriptAssemblies/UnityEngine.TestRunner.dll')
$testReferences += '/reference:' + (Join-Path $CentralUnity 'Library/ScriptAssemblies/UnityEditor.TestRunner.dll')
$nunit = @(Get-ChildItem -Path (Join-Path $CentralUnity 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll'))
if ($nunit.Count -ne 1) { throw 'Expected one installed Unity NUnit reference.' }
$testReferences += '/reference:' + $nunit[0].FullName
$testReferences += '/reference:' + $module
$testLog = @(& $dotnet $compiler /nologo /target:library /nostdlib+ /langversion:9.0 /warnaserror+ /define:UNITY_EDITOR ('/out:' + (Join-Path $output 'MosquitoRagdoll.Proof.Offline.dll')) @testReferences (Join-Path $PSScriptRoot 'MosquitoRagdollPlayModeProof.cs') 2>&1)
$testCode = $LASTEXITCODE
$testLog | Set-Content -LiteralPath (Join-Path $output 'proof.log')
if ($testCode -ne 0) { throw ($testLog -join "`n") }
$result = [ordered]@{
    kind = 'Roslyn compile only; no Unity editor or physics execution'
    utc = [DateTime]::UtcNow.ToString('o')
    unity_data = $UnityData
    git_head = (& git -C $repo rev-parse HEAD)
    module_exit_code = $moduleCode
    proof_exit_code = $testCode
    source_sha256 = @($sources + (Join-Path $PSScriptRoot 'MosquitoRagdollPlayModeProof.cs') | ForEach-Object {
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_
        [ordered]@{ path = $_; sha256 = $hash.Hash.ToLowerInvariant() }
    })
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'result.json')
$result | ConvertTo-Json -Depth 5
