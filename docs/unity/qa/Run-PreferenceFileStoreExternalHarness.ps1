param(
    [string]$NUnitFrameworkPath = 'N:\LetMeSleep\Repository\unity\Library\PackageCache\com.unity.ext.nunit@d8c07649098d\net40\unity-custom\nunit.framework.dll',
    [string]$SourceWorkspaceRoot = ''
)

$ErrorActionPreference = 'Stop'
$testWorkspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$integratedSource = Join-Path $testWorkspaceRoot 'unity\Assets\LetMeSleep\Bootstrap\PreferenceFileStore.cs'
if ([string]::IsNullOrWhiteSpace($SourceWorkspaceRoot)) {
    $SourceWorkspaceRoot = if (Test-Path -LiteralPath $integratedSource -PathType Leaf) {
        $testWorkspaceRoot
    } else {
        'N:\LetMeSleep\Worktrees\director-persistence'
    }
}
$SourceWorkspaceRoot = [System.IO.Path]::GetFullPath($SourceWorkspaceRoot)
$sourcePath = Join-Path $SourceWorkspaceRoot 'unity\Assets\LetMeSleep\Bootstrap\PreferenceFileStore.cs'
$testPath = Join-Path $testWorkspaceRoot 'unity\Assets\LetMeSleep\Tests\StorageEditMode\PreferenceFileStoreTests.cs'
$temporaryRoot = Join-Path $testWorkspaceRoot '.codex-tmp-preference-store-tests'

foreach ($path in @($NUnitFrameworkPath, $sourcePath, $testPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required harness input not found: $path" }
}
if (Test-Path -LiteralPath $temporaryRoot) { throw "Temporary harness path already exists: $temporaryRoot" }
if (-not $temporaryRoot.StartsWith($testWorkspaceRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Temporary harness path is outside the test workspace.'
}

$escapedNUnit = [System.Security.SecurityElement]::Escape($NUnitFrameworkPath)
$escapedTests = [System.Security.SecurityElement]::Escape($testPath)
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="nunit.framework">
      <HintPath>$escapedNUnit</HintPath>
    </Reference>
    <Compile Include="Program.cs" />
    <Compile Include="PreferenceFileStore.cs" />
    <Compile Include="$escapedTests" Link="Tests\PreferenceFileStoreTests.cs" />
  </ItemGroup>
</Project>
"@

$program = @'
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class Program
{
    private static int Main()
    {
        int count = 0, failed = 0;
        Type type = typeof(LetMeSleep.Tests.Storage.EditMode.PreferenceFileStoreTests);
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(item => item.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
            .OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            count++;
            try
            {
                method.Invoke(Activator.CreateInstance(type), Array.Empty<object>());
            }
            catch (TargetInvocationException error)
            {
                failed++;
                Console.WriteLine($"FAIL {method.Name}: {error.InnerException?.GetType().Name}: {error.InnerException?.Message}");
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine($"FAIL {method.Name}: {error.GetType().Name}: {error.Message}");
            }
        }
        Console.WriteLine($"PREFERENCE_STORE_RUN tests={count} failures={failed} unity_runner=false");
        return failed == 0 ? 0 : 1;
    }
}
'@

function Replace-ExactlyOnce([string]$Text, [string]$Old, [string]$New, [string]$Label) {
    $first = $Text.IndexOf($Old, [System.StringComparison]::Ordinal)
    if ($first -lt 0 -or $Text.IndexOf($Old, $first + $Old.Length, [System.StringComparison]::Ordinal) -ge 0) {
        throw "Mutation anchor is not unique: $Label"
    }
    return $Text.Substring(0, $first) + $New + $Text.Substring($first + $Old.Length)
}

function Invoke-Run([string]$Label, [string]$Source, [bool]$ExpectPass) {
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot 'PreferenceFileStore.cs'), $Source)
    $output = (& dotnet run --project (Join-Path $temporaryRoot 'Harness.csproj') --configuration Release 2>&1 | Out-String)
    $exitCode = $LASTEXITCODE
    $summary = ($output -split "`r?`n" | Where-Object { $_ -match '^PREFERENCE_STORE_RUN ' } | Select-Object -Last 1)
    if ($ExpectPass -and $exitCode -ne 0) { Write-Output $output; throw "Baseline failed: $Label" }
    if (-not $ExpectPass -and $exitCode -eq 0) { throw "Tests did not reject mutant: $Label" }
    Write-Output "PREFERENCE_STORE_CASE label=$Label expected_pass=$ExpectPass exit=$exitCode summary=$summary"
}

$exitCode = 1
try {
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot 'Harness.csproj'), $project)
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot 'Program.cs'), $program)
    $source = [System.IO.File]::ReadAllText($sourcePath).Replace("`r`n", "`n")

    Invoke-Run 'baseline' $source $true

    $noRecovery = Replace-ExactlyOnce $source `
        'if (kind == PreferenceDocumentKind.Current) { RecoveredFromBackup = true; return backup; }' `
        'if (kind == PreferenceDocumentKind.Current) { return null; }' `
        'backup recovery'
    Invoke-Run 'no-backup-recovery' $noRecovery $false

    $archiveAnchor = 'string backup = kind == PreferenceDocumentKind.Current ? path + ".backup"' + "`n" +
        '                        : path + ".rejected-" + Guid.NewGuid().ToString("N");'
    $overwriteBackup = Replace-ExactlyOnce $source `
        $archiveAnchor `
        'string backup = path + ".backup";' `
        'malformed archive'
    Invoke-Run 'overwrite-good-backup' $overwriteBackup $false

    $primaryVersionAnchor = '            if (kind == PreferenceDocumentKind.UnsupportedVersion) WriteBlocked = true;' + "`n" +
        '            if (WriteBlocked)'
    $allowDowngrade = Replace-ExactlyOnce $source `
        $primaryVersionAnchor `
        '            if (WriteBlocked)' `
        'unsupported version guard'
    Invoke-Run 'allow-future-version-overwrite' $allowDowngrade $false

    $backupRecheckAnchor = @'
            if (kind != PreferenceDocumentKind.Current)
            {
                Read(path + ".backup", out var backupKind);
                if (backupKind == PreferenceDocumentKind.UnsupportedVersion) WriteBlocked = true;
            }
'@
    $noBackupRecheck = Replace-ExactlyOnce $source $backupRecheckAnchor '' 'unsupported backup recheck'
    Invoke-Run 'allow-future-backup-overwrite' $noBackupRecheck $false

    Write-Output 'PREFERENCE_STORE_EXTERNAL_HARNESS baseline_tests=8 baseline_failures=0 mutants=4 rejected=4 unity_runner=false'
    $exitCode = 0
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        $resolvedTemporaryRoot = (Resolve-Path -LiteralPath $temporaryRoot).Path
        if (-not $resolvedTemporaryRoot.StartsWith($testWorkspaceRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a temporary path outside the test workspace.'
        }
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}

exit $exitCode
