$ErrorActionPreference = 'Stop'
$contract = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'room_contract.json') -Raw | ConvertFrom-Json
$validation = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'validation.json') -Raw | ConvertFrom-Json
$rendererNames = @($validation.checks | Where-Object { $_.name.StartsWith('positive finite bounds ') } | ForEach-Object { $_.name.Substring(23) })
if ($rendererNames.Count -ne $validation.meshes) { throw 'Renderer list does not match geometry receipt' }
$renderers = @($rendererNames | ForEach-Object {
    $isMoving = $_ -match '^Door_' -and $_ -notmatch '^Door_(Jamb|Frame)'
    $isGlass = $_ -eq 'Window_Pane'
    $surface = if ($_ -match '(Handle|Hinge|Knob)') { 'Metal' } elseif ($_ -match '(Mattress|Quilt|Fold|Pillow)') { 'Carpet' } elseif ($isGlass) { 'Stone' } else { 'Wood' }
    [ordered]@{
        node = $_
        mobility = $(if ($isMoving) { 'dynamic' } else { 'static' })
        contribute_gi = -not $isMoving
        receive_gi = $(if ($isMoving) { 'LightProbes' } else { 'Lightmaps' })
        cast_shadows = -not $isGlass
        receive_shadows = $true
        light_probe_usage = $(if ($isMoving) { 'BlendProbes' } else { 'Off' })
        reflection_probe_usage = 'BlendProbes'
        static_occluder = $_ -eq 'Architecture_Shell'
        static_occludee = -not $isMoving
        surface = $(if ($_ -eq 'Architecture_Shell') { $null } else { $surface })
        material_slot_surfaces = $(if ($_ -eq 'Architecture_Shell') { @('Wood', 'Stone', 'Stone') } else { @() })
        lightmap_uv2_status = $(if ($isMoving) { 'not_required_dynamic' } else { 'pending_unity_import_generation_and_overlap_validation' })
    }
})
$colliders = @($contract.box_colliders | ForEach-Object {
    [ordered]@{
        parent_node = $_.node
        child_name = 'Collider_' + $_.source
        type = 'BoxCollider'
        center = $_.center
        size = $_.size
        attach_to_visual_mesh = $false
        collision_role = $(if ($_.node -eq 'Door_01_Hinge') { 'WorldDynamic' } else { 'WorldStatic' })
        camera_query_included = $true
    }
})
$anchors = @(
    @{ name='LightAnchor_Room_Ceiling'; position=@(2.4,2.62,2.2); role='fixture_candidate' },
    @{ name='LightAnchor_Door_Inside'; position=@(1.12,1.4,.35); role='probe_candidate' },
    @{ name='LightAnchor_Door_Outside'; position=@(1.12,1.4,-.35); role='probe_candidate' },
    @{ name='LightAnchor_Room_Low_SW'; position=@(.4,.25,.4); role='probe_candidate' },
    @{ name='LightAnchor_Room_Low_SE'; position=@(4.4,.25,.4); role='probe_candidate' },
    @{ name='LightAnchor_Room_Low_Center'; position=@(2.2,.25,2.4); role='probe_candidate' },
    @{ name='LightAnchor_Room_High_SW'; position=@(.4,2.5,.4); role='probe_candidate' },
    @{ name='LightAnchor_Room_High_SE'; position=@(4.4,2.5,.4); role='probe_candidate' },
    @{ name='LightAnchor_Room_High_NW'; position=@(.4,2.5,4.0); role='probe_candidate' },
    @{ name='LightAnchor_Room_High_NE'; position=@(4.4,2.5,4.0); role='probe_candidate' },
    @{ name='ReflectionVolume_Room'; position=@(2.4,1.4,2.2); size=@(4.8,2.8,4.4); role='box_projection_bounds' },
    @{ name='AudioZone_Room'; position=@(2.4,1.4,2.2); size=@(4.8,2.8,4.4); role='room' },
    @{ name='CameraCollision'; position=@(0,0,0); role='semantic_anchor_only_not_collider' }
)
$manifest = [ordered]@{
    schema_version = 1
    presentation_contract_commit = '190fad6'
    geometry_commit = '75356a094f1c626316dea4aaa33c86eb344fc524'
    asset = $contract.asset
    units = 'metres'
    coordinates = 'Unity room-local; +Y up, +Z forward, +X right'
    root_scale = @(1,1,1)
    pivot = @(0,0,0)
    source = 'room_contract.json + validation.json'
    implementation_status = 'integration_recipe_only; source FBX unchanged'
    renderers = $renderers
    materials = [ordered]@{
        policy = 'Shared URP material assets by source material name; never per-instance clones'
        source_shader_status = 'Blender Principled placeholders; Unity URP remap required'
        glass = 'Glass_Blue_Opaque stays opaque for sample, does not cast shadows'
        surface_enum = @('Wood','Tile','Carpet','Grass','Stone','Metal')
        fallback_notes = 'Plaster/ceiling/glass use Stone acoustics provisionally; bed textiles use Carpet. Tile/Grass absent in room sample.'
    }
    lighting = [ordered]@{
        texels_per_metre = 16
        static_uv2 = 'Missing in current source. Generate Secondary UV on Unity model import, then check overlap/padding at bake resolution.'
        uv2_padding_target_texels = 4
        uv2_status = 'pending; not certified by Blender geometry tests'
        probe_anchors = 'Candidate 3D positions only. Worker 2 expands tetrahedral probe grid and validates against colliders.'
    }
    anchor_parent = 'PresentationAnchors under room root'
    anchors = $anchors
    anchor_status = 'Definitions only; Director creates Empty transforms through Unity API. Not present in FBX.'
    forbidden_source_components = @('Light','ReflectionProbe','LightProbeGroup','AudioSource','Volume')
    collider_children = $colliders
    camera_collision = [ordered]@{
        anchor = 'CameraCollision'
        policy = 'Camera query must include static room collision and moving leaf at its actual angle; never leave an invisible closed-door blocker.'
        layer_note = 'One Unity layer per GameObject. Director combines WorldStatic/WorldDynamic in camera masks OR creates synchronized CameraCollision query proxies; do not assign two layers to one collider.'
        proxy_duplicate_physics_forbidden = $true
    }
    interior_volumes = @(
        @{id='SampleFloor_00';kind='floor';center=@(2.4,1.4,2.2);size=@(4.8,2.8,4.4);scope='sample_only_not_full_house'},
        @{id='SampleRoom_Bedroom';kind='room';parent='SampleFloor_00';center=@(2.4,1.4,2.2);size=@(4.8,2.8,4.4)}
    )
    portals = @(
        @{id='Portal_Door_01';kind='door';center=@(1.12,1.10,0);width=1.10;height=2.20;outward_normal=@(0,0,-1);state_node='Door_01_Hinge';traversable='when physically open'},
        @{id='Portal_Window_01';kind='window';center=@(3.6,1.70,4.49);width=1.10;height=1.0;outward_normal=@(0,0,1);traversable=$false;acoustic_state='sealed';note='Aperture inside frame; no traversal or open acoustic portal through glass'}
    )
    acoustic_zones = @(@{id='AudioZone_Room';kind='room';bounds='SampleRoom_Bedroom'})
    future_map_zone_kinds = @('room','corridor','stair','patio','lobby')
    lod = [ordered]@{
        architecture = 'No culling or simplification that opens shell'
        large_props = @(@{node='Furniture_Bed';required_component='LODGroup';levels=@(@{index=0;screen_relative_transition_height=0.0;renderers='all descendant MeshRenderers'});fade_mode='None';status='Unity integration pending; only LOD0 supplied, no optimized LOD claim'})
        repeated_trees_fences = 'Absent from sample. Supply real mesh LOD variants and shared material/mesh references in later assigned batch.'
    }
    missing_evidence = @('Unity URP material remap','UV2 overlap and texel margin','Unity anchor instantiation','Collider and camera layers','Light/probe bake and leakage','Real LOD variants outside sample','Full house/floor/patio/lobby volumes')
}
$output = Join-Path $PSScriptRoot 'presentation_manifest.json'
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $output -Encoding utf8
$readback = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
if ($readback.renderers.Count -ne 55 -or $readback.anchors.Count -ne 13 -or $readback.portals.Count -ne 2) { throw 'Presentation manifest counts changed unexpectedly' }
if (@($readback.renderers | Where-Object { -not $_.node }).Count) { throw 'Empty renderer name' }
if (@($readback.collider_children | Where-Object { $_.parent_node -eq 'Door_01_Hinge' }).Count -ne 1) { throw 'Moving leaf collider missing or duplicated' }
Write-Output ('Presentation manifest validated: {0} renderers, {1} collider children, {2} anchors, {3} portals.' -f $readback.renderers.Count,$readback.collider_children.Count,$readback.anchors.Count,$readback.portals.Count)
