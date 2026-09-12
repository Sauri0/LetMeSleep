# Fixed authoring data only. No Unity or Blender execution, no generated scene.
$ErrorActionPreference = 'Stop'
function Zone($id,$floor,$kind,$minX,$minZ,$maxX,$maxZ) {
    $y=if($floor -eq 'upper'){3.0}else{0.0}
    [ordered]@{id=$id;floor=$floor;kind=$kind;min=@($minX,$y,$minZ);max=@($maxX,($y+2.8),$maxZ)}
}
function Portal($id,$from,$to,$x,$y,$z,$width,$normal,$door=$false) {
    $initial=if($door){100}else{0}
    [ordered]@{id=$id;from=$from;to=$to;center=@($x,($y+1.1),$z);width=$width;height=2.2;normal=$normal;door=$door;initial_degrees=$initial}
}
$zones=@(
    (Zone 'GroundHall' 'ground' 'corridor' 5.16 .18 6.96 11.22),
    (Zone 'GroundWestLanding' 'ground' 'landing' .18 4.76 4.98 7.38),
    (Zone 'GroundEastLanding' 'ground' 'landing' 7.14 4.76 12.62 6.56),
    (Zone 'Living' 'ground' 'room' .18 .18 4.98 4.58),
    (Zone 'Dining' 'ground' 'room' 7.14 .18 12.62 4.58),
    (Zone 'Kitchen' 'ground' 'room' 7.14 6.74 12.62 11.22),
    (Zone 'UpperHall' 'upper' 'corridor' 5.16 .18 6.96 11.22),
    (Zone 'UpperWestLanding' 'upper' 'landing' .18 4.76 4.98 7.38),
    (Zone 'UpperEastLanding' 'upper' 'landing' 7.14 4.76 12.62 6.56),
    (Zone 'BedroomA' 'upper' 'room' .18 .18 4.98 4.58),
    (Zone 'BedroomB' 'upper' 'room' 7.14 .18 12.62 4.58),
    (Zone 'Bathroom' 'upper' 'room' 7.14 6.74 9.50 11.22),
    (Zone 'Utility' 'upper' 'room' 9.68 6.74 12.62 11.22),
    (Zone 'Patio' 'ground' 'patio' .18 11.4 12.62 19.22)
)
$portals=@(
    (Portal 'Entry' 'Outside' 'GroundHall' 6.06 0 .09 1.1 @(0,0,1) $true),
    (Portal 'HallWest0' 'GroundHall' 'GroundWestLanding' 5.07 0 6.05 1.8 @(-1,0,0)),
    (Portal 'HallEast0' 'GroundHall' 'GroundEastLanding' 7.05 0 5.66 1.8 @(1,0,0)),
    (Portal 'LivingDoor' 'GroundWestLanding' 'Living' 3.86 0 4.67 1.1 @(0,0,-1) $true),
    (Portal 'DiningDoor' 'GroundEastLanding' 'Dining' 8.6 0 4.67 1.1 @(0,0,-1) $true),
    (Portal 'KitchenDoor' 'GroundEastLanding' 'Kitchen' 8.6 0 6.65 1.1 @(0,0,1) $true),
    (Portal 'HallWest1' 'UpperHall' 'UpperWestLanding' 5.07 3 6.05 1.8 @(-1,0,0)),
    (Portal 'HallEast1' 'UpperHall' 'UpperEastLanding' 7.05 3 5.66 1.8 @(1,0,0)),
    (Portal 'BedroomADoor' 'UpperWestLanding' 'BedroomA' 3.86 3 4.67 1.1 @(0,0,-1) $true),
    (Portal 'BedroomBDoor' 'UpperEastLanding' 'BedroomB' 8.6 3 4.67 1.1 @(0,0,-1) $true),
    (Portal 'BathroomDoor' 'UpperEastLanding' 'Bathroom' 8.25 3 6.65 1.1 @(0,0,1) $true),
    (Portal 'UtilityDoor' 'UpperEastLanding' 'Utility' 11.1 3 6.65 1.1 @(0,0,1) $true),
    (Portal 'PatioDoor' 'GroundHall' 'Patio' 6.06 0 11.31 1.1 @(0,0,1) $true)
)
$spawns=@(); $index=0
foreach($z in @(-1.5,-.5,.5,1.5)){foreach($x in @(-2.4,-.8,.8,2.4)){
    $length=[Math]::Sqrt($x*$x+$z*$z)
    $spawns+=@{id=('LobbySpawn_{0:00}' -f $index);position=@($x,0,$z);forward=@((-$x/$length),0,(-$z/$length))}
    $index++
}}
$plan=[ordered]@{
    schema_version=1
    status='fixed layout proposal in authoring data; geometry, traversal and native rendering pending'
    map_id='house-patio-v1'
    axes='Unity +Y up/+Z forward; all house coordinates metres'
    house_exterior=@{min=@(0,0,0);max=@(12.8,6.0,11.4);wall_thickness=.18;floor_finished_y=@(0,3);clear_height=2.8}
    plan_revision_note='Expands earlier 12x10 proposal to12.8x11.4 to fit exact4.8x4.4 sample,1.8m halls and1.6m U-stair without shrinking clearances.'
    floor_volumes=@(
        @{id='HouseFloor0';min=@(.18,0,.18);max=@(12.62,2.8,11.22)},
        @{id='HouseFloor1';min=@(.18,3,.18);max=@(12.62,5.8,11.22)}
    )
    zones=$zones
    portals=$portals
    stair=[ordered]@{
        id='MainStair';from='GroundWestLanding';to='UpperWestLanding'
        interior_box=@{min=@(1.48,0,7.38);max=@(4.98,5.8,11.22)}
        lower_flight=@{clear_x=@(3.38,4.98);direction=@(0,0,1);start_z=7.38;end_z=9.62;start_y=0;end_y=1.5}
        upper_flight=@{clear_x=@(1.48,3.08);direction=@(0,0,-1);start_z=9.62;end_z=7.38;start_y=1.5;end_y=3}
        risers_per_flight=9;total_risers=18;riser_height=(3.0/18);treads_per_flight=8;tread_depth=.28;clear_width=1.6
        mid_landing=@{min=@(1.48,1.5,9.62);max=@(4.98,1.5,11.22);clear_depth=1.6}
        lower_approach=@{min=@(3.38,0,5.58);max=@(4.98,0,7.38);clear_depth=1.8}
        upper_exit=@{min=@(1.48,3,5.58);max=@(3.08,3,7.38);clear_depth=1.8}
        slab_opening=@{min=@(1.48,2.8,7.38);max=@(4.98,3.0,11.22)}
        minimum_headroom=2.2
        guardrails='Outside clear flight widths; divider in0.30m gap. Guard upper floor front edge above lower flight, keep upper exit free. Understair is not a human route.'
    }
    sample_reuse=@{zone='BedroomA';asset='room_sample';position=@(4.98,3,4.58);yaw_degrees=180;note='Exact interior fit; door faces north landing. Reuse furniture/door after sample native receipt. Full-house shell owns boundaries; never duplicate sample floor/walls/ceiling over house shell.'}
    patio=@{bounds_xz=@(0,11.4,12.8,19.4);main_path=@{center_x=6.06;z=@(11.4,18.2);width=1.8};door_turn_clearance=1.8;bench_reserve=@{min=@(9,0,15.5);max=@(11,1.2,16.1)};tree_candidates=@(@(2,0,16.5),@(10.8,0,18.3));note='Candidate props only; no new assets generated.'}
    sealed_service_void=@{min=@(.18,0,7.56);max=@(1.30,5.8,11.22);note='Not a room or required route; final shell must close it, no accidental mosquito escape.'}
    lobby=@{scene_id='private-lobby-v1';separate_scene=$true;bounds=@{min=@(-7,0,-6);max=@(7,3.2,6)};source_shell_scale=@(1.4,1,1.5);dressing_note='Existing lobby shell expands to14x12; existing kit fills outer apron; center6x4 plus1.8m circulation ring stays clear;16 spawns unchanged.';central_clear=@{min=@(-3,0,-2);max=@(3,3.2,2)};perimeter_min_width=1.8;spawn_clearance_radius=.4;spawns=$spawns;capacity_status='16 geometric candidates only; capacity/performance/network not certified'}
    boundary_ownership='One shell owner per wall/slab junction. Portals cut real openings; no overlapping complete room shells. No physics through closed window.'
    validation_scope='Numeric extents, non-overlapping same-floor zones, graph connectivity and lobby spawn clearances only; no Unity traversal or art evidence.'
}

# Check data relationships before serializing; these are not gameplay tests.
foreach($zone in $zones){for($axis=0;$axis -lt 3;$axis++){if($zone.max[$axis] -le $zone.min[$axis]){throw ('Invalid bounds '+$zone.id)}}}
for($i=0;$i -lt $zones.Count;$i++){for($j=$i+1;$j -lt $zones.Count;$j++){
    $a=$zones[$i];$b=$zones[$j]
    if($a.floor -ne $b.floor){continue}
    $overlapX=[Math]::Min($a.max[0],$b.max[0])-[Math]::Max($a.min[0],$b.min[0])
    $overlapZ=[Math]::Min($a.max[2],$b.max[2])-[Math]::Max($a.min[2],$b.min[2])
    if($overlapX -gt 1e-8 -and $overlapZ -gt 1e-8){throw ('Overlapping zones '+$a.id+' / '+$b.id)}
}}
$known=@('Outside')+@($zones | ForEach-Object {$_.id})
$edges=@($portals)+@(@{from=$plan.stair.from;to=$plan.stair.to})
foreach($edge in $edges){if($edge.from -notin $known -or $edge.to -notin $known){throw 'Unknown graph endpoint'}}
$reached=[System.Collections.Generic.HashSet[string]]::new(); [void]$reached.Add('Outside')
do{$before=$reached.Count;foreach($edge in $edges){if($reached.Contains($edge.from)){[void]$reached.Add($edge.to)};if($reached.Contains($edge.to)){[void]$reached.Add($edge.from)}}}while($reached.Count -ne $before)
if($reached.Count -ne $known.Count){throw 'Disconnected room or patio'}
foreach($spawn in $spawns){if([Math]::Abs($spawn.position[0])+.4 -gt 3 -or [Math]::Abs($spawn.position[2])+.4 -gt 2){throw 'Spawn outside central reserve'}}
for($i=0;$i -lt $spawns.Count;$i++){for($j=$i+1;$j -lt $spawns.Count;$j++){
    $dx=$spawns[$i].position[0]-$spawns[$j].position[0];$dz=$spawns[$i].position[2]-$spawns[$j].position[2]
    if([Math]::Sqrt($dx*$dx+$dz*$dz) -lt .8){throw 'Lobby spawn reserves overlap'}
}}
if([Math]::Abs($plan.stair.total_risers*$plan.stair.riser_height-3) -gt 1e-8){throw 'Stair floor height mismatch'}
$plan | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'house_layout_plan.json') -Encoding utf8
Write-Output ('Fixed-plan data: {0} zones reachable, {1} portals, 18 risers, 16 separated lobby candidates. No scenes generated.' -f $zones.Count,$portals.Count)
