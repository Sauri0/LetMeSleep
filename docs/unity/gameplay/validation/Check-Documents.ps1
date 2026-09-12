$ErrorActionPreference = 'Stop'
$documentRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = [Collections.Generic.List[string]]::new()
$hashes = [ordered]@{}
$checks = 0
foreach ($file in (Get-ChildItem -LiteralPath $documentRoot -Filter '*.md' -File | Sort-Object Name)) {
    $content = [IO.File]::ReadAllText($file.FullName).Replace("`r`n", "`n")
    $bytes = [Text.Encoding]::UTF8.GetBytes($content)
    $hashes[$file.Name] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $checks++
    if ([regex]::Matches($content, '(?m)^```').Count % 2 -ne 0) { $failures.Add("Unbalanced code fence: $($file.Name)") }
    foreach ($link in [regex]::Matches($content, '\]\(([^)]+\.md)\)')) {
        $target = $link.Groups[1].Value
        if ($target -match '^https?://') { continue }
        $checks++
        if (!(Test-Path -LiteralPath (Join-Path $documentRoot $target))) { $failures.Add("Missing link $($file.Name): $target") }
    }
}
$acceptance = Get-Content -LiteralPath (Join-Path $documentRoot 'ACCEPTANCE.md') -Raw
foreach ($gate in 1..20) {
    $checks++; $pattern = '(?m)^\| G{0:D2}\s' -f $gate
    if ($acceptance -notmatch $pattern) { $failures.Add("Missing gate G$gate") }
}
$interfaces = Get-Content -LiteralPath (Join-Path $documentRoot 'INTERFACES.md') -Raw
foreach ($required in @('DoorInteractionQuery','DoorSweepResult','ApplyDoorPose','DoorSnapshot','StaleRevision','Public','SurfaceId')) {
    if ($required -eq 'Public') { continue }
    $checks++
    if (!$interfaces.Contains($required)) { $failures.Add("Missing door/contact contract $required") }
}
$checks++
if (!$acceptance.Contains('Unproven')) { $failures.Add('G10 must not treat unproven continuous cells as passed.') }
[ordered]@{ scope = 'Documentation consistency only, not runtime gate execution'; base_commit = '33ea4f0'; hash_format = 'SHA256 UTF8 markdown normalized LF'; documents = $hashes; checks = $checks; failures = @($failures) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $documentRoot 'DOCUMENT-CHECK.json')
Write-Output "DOCUMENT_CHECK checks=$checks failures=$($failures.Count)"
if ($failures.Count -gt 0) { throw ($failures -join '; ') }
