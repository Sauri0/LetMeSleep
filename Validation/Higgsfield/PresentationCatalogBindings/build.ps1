param(
    [string]$UnityManaged = 'N:/Unity/Editors/6000.3.24f1/Editor/Data/Managed/UnityEngine',
    [string]$ScriptAssemblies = 'N:/LetMeSleep/Repository/unity/Library/ScriptAssemblies',
    [string]$OutputDirectory = 'N:/LetMeSleep/Validation/Higgsfield/PresentationCatalogBindings-20260913'
)
$ErrorActionPreference = 'Stop'
$fixtureOutput = [IO.Path]::GetFullPath($OutputDirectory).Replace('\','/')
if (-not $fixtureOutput.StartsWith('N:/LetMeSleep/Validation/Higgsfield/', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'External fixture adapter requires output under N:/LetMeSleep/Validation/Higgsfield/.'
}
$null = New-Item -ItemType Directory -Force -Path $fixtureOutput
$fixtureXml = New-Object System.Xml.XmlDocument
$fixtureProject = $fixtureXml.CreateElement('Project'); $fixtureProject.SetAttribute('Sdk','Microsoft.NET.Sdk'); $null = $fixtureXml.AppendChild($fixtureProject)
$fixtureProperties = $fixtureXml.CreateElement('PropertyGroup'); $null = $fixtureProject.AppendChild($fixtureProperties)
foreach ($setting in @{TargetFramework='netstandard2.1'; LangVersion='9'; EnableDefaultCompileItems='false'; AssemblyName='PresentationCatalogBindingsFixture'; UseSharedCompilation='false'}.GetEnumerator()) {
    $element = $fixtureXml.CreateElement($setting.Key); $element.InnerText = $setting.Value; $null = $fixtureProperties.AppendChild($element)
}
$fixtureItems = $fixtureXml.CreateElement('ItemGroup'); $null = $fixtureProject.AppendChild($fixtureItems)
$fixtureReferences = @(Get-ChildItem -LiteralPath $UnityManaged -Filter '*.dll')
foreach ($assemblyName in @('LetMeSleep.Bootstrap','LetMeSleep.Presentation','LetMeSleep.Content.Environment','LetMeSleep.Core','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime')) {
    $fixtureReferences += Get-Item -LiteralPath (Join-Path $ScriptAssemblies ($assemblyName + '.dll'))
}
foreach ($reference in $fixtureReferences) {
    $element = $fixtureXml.CreateElement('Reference'); $element.SetAttribute('Include',$reference.FullName)
    $privateElement = $fixtureXml.CreateElement('Private'); $privateElement.InnerText = 'false'; $null = $element.AppendChild($privateElement)
    $null = $fixtureItems.AppendChild($element)
}
$fixtureSource = $fixtureXml.CreateElement('Compile'); $fixtureSource.SetAttribute('Include',(Join-Path $PSScriptRoot 'HiggsfieldMapChecks.cs')); $null = $fixtureItems.AppendChild($fixtureSource)
$fixtureProjectPath = Join-Path $fixtureOutput 'PresentationCatalogBindingsFixture.csproj'; $fixtureXml.Save($fixtureProjectPath)
dotnet build $fixtureProjectPath --nologo -v:q --output (Join-Path $fixtureOutput 'bin')
if ($LASTEXITCODE -ne 0) { throw 'Offline fixture compilation failed.' }
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $fixtureOutput 'bin/PresentationCatalogBindingsFixture.dll')
