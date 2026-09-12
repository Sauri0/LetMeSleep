param(
    [string]$NUnitFrameworkPath = 'N:\LetMeSleep\Repository\unity\Library\PackageCache\com.unity.ext.nunit@d8c07649098d\net40\unity-custom\nunit.framework.dll',
    [string]$SourceWorkspaceRoot = ''
)

$ErrorActionPreference = 'Stop'
$testWorkspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
if ([string]::IsNullOrWhiteSpace($SourceWorkspaceRoot)) {
    $SourceWorkspaceRoot = $testWorkspaceRoot
}
$SourceWorkspaceRoot = [System.IO.Path]::GetFullPath($SourceWorkspaceRoot)
$temporaryRoot = Join-Path $testWorkspaceRoot '.codex-tmp-protocol-tool-tests'

$required = @(
    $NUnitFrameworkPath,
    (Join-Path $SourceWorkspaceRoot 'unity\Assets\LetMeSleep\Core\RoomSession.cs'),
    (Join-Path $SourceWorkspaceRoot 'unity\Assets\LetMeSleep\Gameplay'),
    (Join-Path $SourceWorkspaceRoot 'unity\Assets\LetMeSleep\Online\GameplayWireCodec.cs'),
    (Join-Path $testWorkspaceRoot 'unity\Assets\LetMeSleep\Tests\EditMode\RoomSessionTestSupport.cs'),
    (Join-Path $testWorkspaceRoot 'unity\Assets\LetMeSleep\Tests\EditMode\AlphaProtocolToolOwnershipTests.cs')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required harness input not found: $path"
    }
}
if (Test-Path -LiteralPath $temporaryRoot) {
    throw "Temporary harness path already exists: $temporaryRoot"
}
if (-not $temporaryRoot.StartsWith($testWorkspaceRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Temporary harness path is outside the test workspace.'
}

$escapedNUnit = [System.Security.SecurityElement]::Escape($NUnitFrameworkPath)
$escapedSource = [System.Security.SecurityElement]::Escape($SourceWorkspaceRoot)
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
    <Compile Include="$escapedSource\unity\Assets\LetMeSleep\Core\RoomSession.cs" Link="Core\RoomSession.cs" />
    <Compile Include="$escapedSource\unity\Assets\LetMeSleep\Gameplay\*.cs" Link="Gameplay\%(Filename)%(Extension)" />
    <Compile Include="$escapedSource\unity\Assets\LetMeSleep\Online\GameplayWireCodec.cs" Link="Online\GameplayWireCodec.cs" />
    <Compile Include="..\unity\Assets\LetMeSleep\Tests\EditMode\RoomSessionTestSupport.cs" Link="Tests\RoomSessionTestSupport.cs" />
    <Compile Include="..\unity\Assets\LetMeSleep\Tests\EditMode\AlphaProtocolToolOwnershipTests.cs" Link="Tests\AlphaProtocolToolOwnershipTests.cs" />
  </ItemGroup>
</Project>
"@

$program = @'
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class Program
{
    private static int Main()
    {
        int count = 0, failed = 0;
        Type type = typeof(LetMeSleep.Tests.EditMode.AlphaProtocolToolOwnershipTests);
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            TestCaseAttribute[] cases = method.GetCustomAttributes(typeof(TestCaseAttribute), false).Cast<TestCaseAttribute>().ToArray();
            if (method.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                Run(type, method, Array.Empty<object>(), ref count, ref failed);
            foreach (TestCaseAttribute item in cases)
                Run(type, method, ConvertArguments(method, item.Arguments), ref count, ref failed);
        }
        Console.WriteLine($"PROTOCOL_TOOL_EXTERNAL_HARNESS tests={count} failures={failed} unity_runner=false");
        return failed == 0 ? 0 : 1;
    }

    private static object[] ConvertArguments(MethodInfo method, object[] values)
    {
        ParameterInfo[] parameters = method.GetParameters();
        var converted = new object[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            Type target = parameters[index].ParameterType;
            converted[index] = target.IsEnum
                ? Enum.ToObject(target, values[index])
                : Convert.ChangeType(values[index], target, CultureInfo.InvariantCulture);
        }
        return converted;
    }

    private static void Run(Type type, MethodInfo method, object[] arguments, ref int count, ref int failed)
    {
        count++;
        try
        {
            method.Invoke(Activator.CreateInstance(type), arguments);
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
}
'@

$exitCode = 1
try {
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot 'Harness.csproj'), $project)
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot 'Program.cs'), $program)
    & dotnet run --project (Join-Path $temporaryRoot 'Harness.csproj') --configuration Release
    $exitCode = $LASTEXITCODE
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
