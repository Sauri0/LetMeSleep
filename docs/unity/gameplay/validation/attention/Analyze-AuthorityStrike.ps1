param(
    [Parameter(Mandatory=$true)][string]$Receipt,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a new output directory to preserve evidence.'}
$report=Get-Content -Raw -LiteralPath $Receipt | ConvertFrom-Json
if($report.cases.Count -ne 54 -or $report.failedCases -ne 0){throw 'Native measurement incomplete.'}
$rows=foreach($case in $report.cases){foreach($sample in $case.samples){if($sample.swept){
    [ordered]@{posture=$case.posture;tool=$case.tool;pitch=$case.pitch;hand=$sample.hand;target=$case.targetMode;tick=$sample.tick;elapsed=$sample.elapsed;phase=$sample.phase;
        hit=$sample.hit;end=$sample.endResidual;pointSegment=$sample.pointToSweep;segmentSegment=$sample.visualSegmentToSweep;
        proxyClamp=$sample.proxyTargetClamped;proxyReachExcess=$sample.reachExcess;contactReachExcess=$sample.contactReachExcess;avoidableEnd=$sample.avoidableEndResidual;
        upperDelta=$sample.upperLengthDelta;lowerDelta=$sample.lowerLengthDelta;pelvisDelta=$sample.pelvisIKDelta;legsDelta=$sample.legsIKDelta;gripDistance=$sample.gripDistance;gripAngle=$sample.gripAngle} | ForEach-Object {[pscustomobject]$_}
}}}
$groups=foreach($group in ($rows | Group-Object posture,tool,target)){
    $samples=$group.Group
    [ordered]@{group=$group.Name;sweeps=$samples.Count;maxEnd=($samples.end|Measure-Object -Maximum).Maximum;meanEnd=($samples.end|Measure-Object -Average).Average;
        maxPointSegment=($samples.pointSegment|Measure-Object -Maximum).Maximum;maxSegmentSegment=($samples.segmentSegment|Measure-Object -Maximum).Maximum;
        proxyClamps=@($samples|Where-Object proxyClamp).Count;maxContactReach=($samples.contactReachExcess|Measure-Object -Maximum).Maximum;
        maxAvoidableEnd=($samples.avoidableEnd|Measure-Object -Maximum).Maximum}
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$rows | Export-Csv -NoTypeInformation -LiteralPath (Join-Path $OutputDirectory 'sweeps.csv')
[ordered]@{receipt=$Receipt;sha256=(Get-FileHash -LiteralPath $Receipt).Hash;cases=$report.cases.Count;samples=@($report.cases.samples).Count;sweeps=$rows.Count;
    maxUpperDelta=($rows.upperDelta|Measure-Object -Maximum).Maximum;maxLowerDelta=($rows.lowerDelta|Measure-Object -Maximum).Maximum;
    maxPelvisDelta=($rows.pelvisDelta|Measure-Object -Maximum).Maximum;maxLegsDelta=($rows.legsDelta|Measure-Object -Maximum).Maximum;
    maxGripDistance=($rows.gripDistance|Measure-Object -Maximum).Maximum;maxGripAngle=($rows.gripAngle|Measure-Object -Maximum).Maximum;
    groups=@($groups);scope='Numeric analysis of Director native receipt, not visual approval.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json')
Get-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json')
