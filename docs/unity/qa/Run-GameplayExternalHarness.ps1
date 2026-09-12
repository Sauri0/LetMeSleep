param(
    [string]$NUnitFrameworkPath = 'N:\LetMeSleep\Repository\unity\Library\PackageCache\com.unity.ext.nunit@d8c07649098d\net40\unity-custom\nunit.framework.dll',
    [string]$GameplaySourceRoot = ''
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$temporaryRoot = Join-Path $workspaceRoot '.codex-tmp-gameplay-tests'
if ([string]::IsNullOrWhiteSpace($GameplaySourceRoot)) {
    $GameplaySourceRoot = Join-Path $workspaceRoot 'unity\Assets\LetMeSleep\Gameplay'
}
$GameplaySourceRoot = [System.IO.Path]::GetFullPath($GameplaySourceRoot)

if (-not (Test-Path -LiteralPath $NUnitFrameworkPath -PathType Leaf)) {
    throw "NUnit framework not found: $NUnitFrameworkPath"
}
if (-not (Test-Path -LiteralPath $GameplaySourceRoot -PathType Container)) {
    throw "Gameplay source directory not found: $GameplaySourceRoot"
}
if (Test-Path -LiteralPath $temporaryRoot) {
    throw "Temporary harness path already exists: $temporaryRoot"
}
if (-not $temporaryRoot.StartsWith($workspaceRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary harness path is outside the workspace."
}

$escapedNUnitPath = [System.Security.SecurityElement]::Escape($NUnitFrameworkPath)
$escapedGameplaySourceRoot = [System.Security.SecurityElement]::Escape($GameplaySourceRoot)
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
      <HintPath>$escapedNUnitPath</HintPath>
    </Reference>
    <Compile Include="Program.cs" />
    <Compile Include="..\unity\Assets\LetMeSleep\Core\RoomSession.cs" Link="RoomSession.cs" />
    <Compile Include="$escapedGameplaySourceRoot\*.cs" Link="Gameplay\%(Filename)%(Extension)" />
    <Compile Include="..\unity\Assets\LetMeSleep\Tests\EditMode\RoomSession*.cs" Link="Tests\%(Filename)%(Extension)" />
    <Compile Include="..\unity\Assets\LetMeSleep\Tests\EditMode\Gameplay*.cs" Link="Tests\%(Filename)%(Extension)" />
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
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var type in assembly.GetTypes().Where(type => type.Namespace == "LetMeSleep.Tests.EditMode" && !type.IsAbstract))
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), false).Cast<TestCaseAttribute>().ToArray();
            bool plain = method.GetCustomAttributes(typeof(TestAttribute), false).Length > 0;
            if (plain) Run(type, method, Array.Empty<object>(), ref count, ref failed);
            foreach (var item in cases) Run(type, method, ConvertArguments(method, item.Arguments), ref count, ref failed);
        }
        Console.WriteLine($"GAMEPLAY_EXTERNAL_HARNESS tests={count} failures={failed} unity_runner=false");
        return failed == 0 ? 0 : 1;
    }

    private static object[] ConvertArguments(MethodInfo method, object[] values)
    {
        var parameters = method.GetParameters();
        var converted = new object[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            var target = parameters[index].ParameterType;
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
            Console.WriteLine($"FAIL {type.Name}.{method.Name}: {error.InnerException?.GetType().Name}: {error.InnerException?.Message}");
        }
        catch (Exception error)
        {
            failed++;
            Console.WriteLine($"FAIL {type.Name}.{method.Name}: {error.GetType().Name}: {error.Message}");
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
        [System.IO.Directory]::Delete($temporaryRoot, $true)
    }
}

exit $exitCode
