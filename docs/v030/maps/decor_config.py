"""v0.3.0 map decoration config (MAPA-SISTEMAS maps, extension point Decor; ENV-03/ENV-04/ENV-05).

    python docs/v030/maps/decor_config.py            -> docs/v030/maps/decor-v030.json

Consumed by unity/Assets/Editor/ProjectBootstrap/HiggsfieldDecorBuilder.cs (step "decor" of V030SceneTools), which places
the v0.3.0 prop library (art_source/unity/environments/v030_props) on every map, searching a small spiral around each
authored spot and keeping only placements that rest on an allowed surface (downward raycast), stay out of routes,
portals, stairs, spawns and objectives, stay inside PlayBounds and do not intersect solid map geometry or other decor.
Coordinates are map-local metres (x, z) or (x, y, z) with y as a floor hint for interiors; yaw in degrees (+Z = front
of the prop); faceAt [x, z] turns the prop's front toward a point. Scatters are seeded (reproducible).

Density follows the sketches: flowers, bushes, ferns and rocks along paths; lanterns, crates and barrels around
buildings; rugs, pans, shelves, pots and paintings inside houses and cabins; docks with a post lantern, rowboat,
barrels, lily pads and reeds (Isla, Puerto); Camp with firewood, log benches and a signpost; the yacht with folding
chairs and lanterns. Warm decor lights only on night maps, capped per map.
"""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
WARM = '#FFB347'


def item(id, prop, at, group='Decor', **kw):
    d = {'id': id, 'prop': prop, 'at': list(at), 'group': group}
    d.update(kw)
    return d


def lantern_light(intensity=1.5, range_=4.0, flicker=0.08):
    return {'color': WARM, 'intensity': intensity, 'range': range_, 'anchor': 'light', 'flicker': flicker}


def halo(size=0.7, alpha=0.35):
    return {'color': WARM, 'size': size, 'alpha': alpha, 'anchor': 'light'}


PATH_EDGE = {'FlowersWhite': 3, 'FlowersYellow': 2, 'Fern': 2, 'Mushrooms': 1.2, 'RockCluster': 1, 'Bush': 1.2}
FOREST = {'Fern': 3, 'Mushrooms': 2, 'RockCluster': 1.2, 'TreeStump': 1, 'Bush': 1.5}
MEADOW = {'FlowersWhite': 3, 'FlowersYellow': 3, 'Bush': 1.5, 'Fern': 1}


def camp():
    fire = [0.0, 0.0]
    items = [
        # Fire plaza (ENV-04 "Campsite"): stacked firewood and an extra log bench facing the fire.
        item('fire-firewood-a', 'Firewood', (4.6, -3.9), 'Decor_Camp', faceAt=fire, search=1.4),
        item('fire-firewood-b', 'Firewood', (-4.9, 3.6), 'Decor_Camp', faceAt=fire, search=1.4),
        item('fire-logbench', 'LogBench', (-4.4, -2.9), 'Decor_Camp', faceAt=fire, search=1.4),
        item('plaza-signpost', 'Signpost', (-7.8, -4.2), 'Decor_Camp', faceAt=fire, search=1.6),
        item('fire-logbench-b', 'LogBench', (4.8, 3.6), 'Decor_Camp', faceAt=fire, search=1.6),
        item('shelter-firewood', 'Firewood', (-19.6, 2.4), 'Decor_Camp', yaw=20, search=1.4),
        item('tents-b-firewood', 'Firewood', (16.2, -4.2), 'Decor_Camp', yaw=-30, search=1.4),
        # Shelter and washroom (buildings): crates, barrels, a lit hand lantern on a crate.
        item('shelter-crate-a', 'Crate', (-26.8, 7.9), 'Decor_Buildings', yaw=12, search=1.2),
        item('shelter-crate-b', 'Crate', (-21.1, 7.2), 'Decor_Buildings', yaw=-8, search=1.2),
        item('shelter-lantern', 'HandLantern', (-21.0, 3.4), 'Decor_Buildings', mount='on', on='shelter-crate-a', search=0.2,
             halo=halo(0.6)),
        item('shelter-barrel', 'Barrel', (-27.8, 3.0), 'Decor_Buildings', search=1.2),
        item('washroom-barrel', 'Barrel', (-25.8, 18.2), 'Decor_Buildings', search=1.2),
        item('washroom-crate', 'Crate', (-30.2, 21.8), 'Decor_Buildings', yaw=20, search=1.2),
        # Tent clusters: folding chairs toward the fire, crates, lit lanterns by the tents.
        item('tents-a-chair-b', 'FoldingChair', (-8.3, 10.6), 'Decor_Camp', faceAt=fire, search=1.2),
        item('tents-a-crate', 'Crate', (-15.8, 11.2), 'Decor_Buildings', yaw=30, search=1.2),
        item('tents-a-lantern', 'HandLantern', (-12.8, 9.2), 'Decor_Camp', search=1.5, light=lantern_light(1.4, 3.6), halo=halo(0.6)),
        item('tents-b-chair', 'FoldingChair', (9.0, -3.3), 'Decor_Camp', faceAt=fire, search=1.2),
        item('tents-b-crate', 'Crate', (15.6, -8.6), 'Decor_Buildings', yaw=-15, search=1.2),
        item('tents-b-lantern', 'HandLantern', (10.4, -8.9), 'Decor_Camp', search=1.2, light=lantern_light(1.4, 3.6), halo=halo(0.6)),
        # Jetty and pond (ENV-04 "Lake & Dock"): lamp post at the shore end, moored rowboat, barrels, lily pads, reeds.
        item('jetty-lamppost', 'DockLampPost', (36.4, -16.4), 'Decor_Dock', yaw=180, search=2.0,
             light=lantern_light(2.0, 5.0, 0.06), halo=halo(0.8)),
        item('jetty-rowboat', 'Rowboat', (37.4, -22.5), 'Decor_Dock', mount='water', yaw=90, waterOffset=-0.14, search=1.2),
        item('jetty-barrel-a', 'Barrel', (38.2, -15.6), 'Decor_Dock', search=2.0),
        item('jetty-barrel-b', 'Barrel', (37.9, -14.6), 'Decor_Dock', search=1.0),
    ]
    scatters = [
        {'id': 'camp-path-edge', 'group': 'Decor_PathEdges', 'props': PATH_EDGE, 'min': [-40, -32], 'max': [30, 28], 'count': 85,
         'near': 'path', 'nearMin': 0.9, 'nearMax': 2.0, 'minSpacing': 1.7, 'seed': 3},
        {'id': 'camp-forest', 'group': 'Decor_Forest', 'props': FOREST, 'min': [-45, -38], 'max': [46, 36], 'count': 26,
         'minSpacing': 3.2, 'seed': 5},
        {'id': 'camp-lily', 'group': 'Decor_Dock', 'props': {'LilyPads': 1}, 'min': [28, -33], 'max': [44, -22], 'count': 7,
         'mount': 'water', 'waterOffset': 0.02, 'minSpacing': 2.0, 'scale': [0.8, 1.3], 'seed': 7},
        {'id': 'camp-reeds', 'group': 'Decor_Dock', 'props': {'Reeds': 1}, 'min': [22, -37], 'max': [47, -13], 'count': 9,
         'near': 'water', 'nearMin': 0.6, 'nearMax': 1.6, 'minSpacing': 1.8, 'seed': 11},
    ]
    return {'mapId': 'hf-campamento-pinar-v2',
            'floorPattern': r'^(CAMP_Terrain_(Grass|Paths|Plaza)|CAMP_Jetty_(Plank|Shore)|CAMP_Hill_|CAMP_Washroom_Floor)',
            'pathPattern': r'^CAMP_Terrain_(Paths|Plaza)$', 'waterPattern': r'^Water_(Lake|Creek)$', 'waterY': -0.12,
            'excludePattern': r'^(CAMP_Tent|CAMP_Shelter_Roof|CAMP_Pine)', 'maxLights': 4, 'items': items, 'scatters': scatters}


def isla():
    items = [
        # Lake dock (ENV-04 "Lake & Dock"): post lantern at the end, rowboat alongside, barrels at the bank.
        item('lakedock-lamppost', 'DockLampPost', (0.95, -8.8), 'Decor_Dock', yaw=180, search=0.6),
        item('lakedock-rowboat', 'Rowboat', (2.9, -9.6), 'Decor_Dock', mount='water', yaw=8, waterOffset=-0.14, search=1.5),
        item('lakedock-barrel-a', 'Barrel', (1.9, -15.0), 'Decor_Dock', search=1.4),
        item('lakedock-barrel-b', 'Barrel', (-1.9, -15.2), 'Decor_Dock', search=1.4),
        # Arrival dock and beach.
        item('arrival-lamppost', 'DockLampPost', (1.2, -45.4), 'Decor_Dock', yaw=180, search=0.8),
        item('arrival-crate-a', 'Crate', (2.4, -34.2), 'Decor_Dock', yaw=15, search=1.4),
        item('arrival-crate-b', 'Crate', (2.5, -34.2), 'Decor_Dock', mount='on', on='arrival-crate-a', yaw=40, search=0.2),
        item('arrival-barrel', 'Barrel', (-2.6, -34.6), 'Decor_Dock', search=1.4),
        item('arrival-signpost', 'Signpost', (3.2, -21.2), 'Decor_PathEdges', yaw=200, search=1.6),
        # Cabin (buildings): barrel, crates, potted plants, mailbox by the access path.
        item('cabin-mailbox', 'Mailbox', (21.4, -4.2), 'Decor_Buildings', yaw=90, search=1.6),
        item('cabin-crate', 'Crate', (34.9, 2.5), 'Decor_Buildings', yaw=20, search=1.4),
        item('cabin-pot-a', 'PottedPlant', (21.8, -0.2), 'Decor_Buildings', search=1.0),
        item('cabin-toolbox', 'Toolbox', (34.6, 5.4), 'Decor_Buildings', yaw=70, search=1.4),
        # Cabin interior (floor y 3.3): painting, potted plant, hanging pans by the kitchenette, a lantern on the shelf wall.
        # The cabin is turned 45 degrees: east axis (0.8, -0.6), back axis (0.6, 0.8) around (29.0, 4.0); 8 x 6 m inside.
        item('cabin-painting', 'PaintingLandscape', (28.79, 3.3, 7.72), 'Decor_Interior', mount='wall', height=1.6, search=0.6),
        item('cabin-pans', 'HangingPans', (30.7, 3.3, -0.15), 'Decor_Interior', mount='wall', height=1.6, search=0.6),
        item('cabin-plant-in', 'PottedPlant', (33.34, 3.3, 3.87), 'Decor_Interior', search=0.5),
        item('cabin-chest', 'Chest', (25.56, 3.3, 5.08), 'Decor_Interior', yaw=127, search=0.5),
        # Picnic area: a ball on the grass, a folding chair, a lantern and mugs already on the table.
        item('picnic-ball', 'SoccerBall', (19.6, -20.6), 'Decor_Picnic', search=1.5),
        item('picnic-chair', 'FoldingChair', (26.8, -24.9), 'Decor_Picnic', faceAt=[23, -23], search=1.2),
    ]
    scatters = [
        {'id': 'isla-path-edge', 'group': 'Decor_PathEdges', 'props': PATH_EDGE, 'min': [-46, -34], 'max': [46, 36], 'count': 95,
         'near': 'path', 'nearMin': 0.9, 'nearMax': 2.2, 'minSpacing': 1.8, 'seed': 13},
        {'id': 'isla-meadow', 'group': 'Decor_Meadow', 'props': MEADOW, 'min': [-40, -30], 'max': [40, 32], 'count': 25,
         'minSpacing': 3.5, 'seed': 17},
        {'id': 'isla-lily', 'group': 'Decor_Dock', 'props': {'LilyPads': 1}, 'min': [-12, -6], 'max': [12, 14], 'count': 8,
         'mount': 'water', 'waterOffset': 0.02, 'minSpacing': 2.0, 'scale': [0.8, 1.3], 'seed': 19},
        # No reed scatter on the Isla: the map already authors Lake_Reed_Clusters_NONSOLID around the lake.
    ]
    return {'mapId': 'hf-isla-del-laguito-v2',
            'floorPattern': r'^(Terrain_Island|Terrain_Banks_Submerged|Path_|Dock_|Cabin_Floor|Lookout_Terrace|Beach_)',
            'pathPattern': r'^Path_', 'waterPattern': r'^Water_Lake$', 'waterY': 1.0,
            'excludePattern': r'^(Coast_Boulder|Pine_Tree|Shrub|Grove_Rock|Cabin_Roof|Lake_Shore_Rock)', 'maxLights': 0,
            'items': items, 'scatters': scatters}


def casa():
    items = [
        # Front garden: mailbox by the gate path, lit lantern on the porch step, crates/barrel/toolbox at the shed.
        item('front-mailbox', 'Mailbox', (1.6, -17.6), 'Decor_Garden', yaw=-90, search=1.2),
        item('shed-crate-a', 'Crate', (-12.9, 11.4), 'Decor_Buildings', yaw=10, search=1.2),
        item('shed-crate-b', 'Crate', (-12.9, 11.4), 'Decor_Buildings', mount='on', on='shed-crate-a', yaw=35, search=0.2),
        item('shed-barrel', 'Barrel', (-17.4, 11.0), 'Decor_Buildings', search=1.2),
        item('shed-toolbox', 'Toolbox', (-15.0, 0.2, 12.6), 'Decor_Buildings', yaw=15, search=0.8),
        item('shed-lantern', 'HandLantern', (-15.9, 10.0), 'Decor_Buildings', search=1.2, light=lantern_light(1.3, 3.4), halo=halo(0.55)),
        item('garden-toolbox', 'Toolbox', (-11.5, 7.4), 'Decor_Garden', yaw=-20, search=1.2),
        item('rear-chair', 'FoldingChair', (-3.3, 6.5), 'Decor_Garden', yaw=180, search=1.0),
        item('picnic-mug', 'Mug', (-5.3, 13.1), 'Decor_Garden', mount='floor', surfacePattern=r'^CASA_Picnic_Table_Top', search=0.3),
        item('woodpile-crate', 'Crate', (8.9, 8.9), 'Decor_Buildings', yaw=5, search=1.2),
        # Ground floor (ENV-03): striped rug in the hall, pans on the kitchen wall, plant and painting in the living room.
        item('hall-rug', 'RugRedStriped', (0.4, 0.3, -3.2), 'Decor_Interior', yaw=90, scale=[0.8], search=0.6),
        item('kitchen-pans', 'HangingPans', (5.2, 0.3, -1.0), 'Decor_Interior', mount='wall', dir=[1, 0], height=1.75, search=0.8),
        item('kitchen-plant', 'PottedPlant', (3.0, 0.3, -4.2), 'Decor_Interior', search=0.8),
        item('living-plant', 'PottedPlant', (-2.1, 0.3, 4.2), 'Decor_Interior', search=0.8),
        item('living-painting', 'PaintingLandscape', (-6.2, 0.3, -0.9), 'Decor_Interior', mount='wall', dir=[-1, 0], height=1.45, search=0.5),
        # Upper floor bedrooms: chest at the foot of the bed, painting, potted plant.
        item('bed1-chest', 'Chest', (-2.7, 3.2, 2.5), 'Decor_Interior', yaw=-90, search=0.8),
        item('bed1-painting', 'PaintingLandscape', (-6.1, 3.2, 0.6), 'Decor_Interior', mount='wall', dir=[-1, 0], height=1.5, search=1.0),
        item('bed1-plant', 'PottedPlant', (-6.2, 3.2, -1.6), 'Decor_Interior', search=0.8),
        item('bed2-chest', 'Chest', (2.9, 3.2, -3.2), 'Decor_Interior', yaw=90, search=0.8),
        item('bed2-painting', 'PaintingLandscape', (6.2, 3.2, -1.6), 'Decor_Interior', mount='wall', dir=[1, 0], height=1.5, search=1.0),
        item('bed2-plant', 'PottedPlant', (6.3, 3.2, -1.2), 'Decor_Interior', search=0.8),
        item('uphall-rug', 'RugBlue', (-0.5, 3.2, 0.8), 'Decor_Interior', yaw=90, scale=[0.6], search=0.6),
        item('living-hanging-plant', 'HangingPlant', (-2.2, 0.3, -4.2), 'Decor_Interior', mount='ceiling', search=0.8),
        item('kitchen-crate', 'Crate', (6.2, 0.3, 1.3), 'Decor_Interior', yaw=10, scale=[0.8], search=0.8),
        item('porch-lantern-l', 'HandLantern', (-3.2, 0.2, -5.6), 'Decor_Garden', search=0.6, halo=halo(0.5, 0.3)),
        item('porch-lantern-r', 'HandLantern', (2.4, 0.2, -5.6), 'Decor_Garden', search=0.6, halo=halo(0.5, 0.3)),
        item('garden-bush-a', 'Bush', (-8.5, -9.5), 'Decor_Garden', search=1.5),
        item('garden-bush-b', 'Bush', (8.5, -9.5), 'Decor_Garden', search=1.5),
    ]
    scatters = [
        {'id': 'casa-path-edge', 'group': 'Decor_PathEdges', 'props': {'FlowersWhite': 3, 'FlowersYellow': 3, 'Bush': 1, 'Fern': 1, 'RockCluster': 0.6},
         'min': [-20, -19], 'max': [20, 18], 'count': 45, 'near': 'path', 'nearMin': 0.7, 'nearMax': 1.6, 'minSpacing': 1.5, 'seed': 29},
        {'id': 'casa-garden', 'group': 'Decor_Garden', 'props': MEADOW, 'min': [-20, -19], 'max': [20, 18], 'count': 20,
         'minSpacing': 3.0, 'seed': 31},
    ]
    return {'mapId': 'hf-casa-del-patio-v1',
            'floorPattern': r'^(CASA_Terrain_Dry_Property|CASA_Path_|CASA_Floorboard_|CASA_Floor_|CASA_Porch_(Front|Rear)_(Board|Support)|CASA_Shed_Floor)',
            'pathPattern': r'^CASA_Path_', 'excludePattern': r'^(CASA_Roof|CASA_Pine|CASA_Shrub|CASA_GardenBed|CASA_Vegetable)',
            'maxLights': 3, 'items': items, 'scatters': scatters}


def puerto():
    items = [
        # Piers (ENV-04 harbor): post lantern on pier 2, barrels and crates at the roots, reeds and lily pads by the shore.
        item('pier2-lamppost', 'DockLampPost', (15.2, -27.9), 'Decor_Dock', yaw=-90, search=0.8, light=lantern_light(2.0, 5.0, 0.06), halo=halo(0.8)),
        item('pier1-barrel-a', 'Barrel', (-15.0, -20.4), 'Decor_Dock', search=0.8),
        item('pier1-barrel-b', 'Barrel', (-15.1, -21.5), 'Decor_Dock', search=0.8),
        item('pier2-crate', 'Crate', (12.9, -20.6), 'Decor_Dock', yaw=10, search=0.8),
        # Cottages and workshop: crates/barrels by the doors, potted plants, mailboxes, lit lanterns.
        item('c1-plant', 'PottedPlant', (-12.2, -5.9), 'Decor_Buildings', search=1.0),
        item('c2-mailbox', 'Mailbox', (-10.3, 8.6), 'Decor_Buildings', yaw=180, search=1.2),
        item('c2-plant', 'PottedPlant', (-11.6, 9.3), 'Decor_Buildings', search=1.0),
        item('c2-lantern', 'HandLantern', (-13.8, 9.2), 'Decor_Buildings', search=1.0, light=lantern_light(1.3, 3.4), halo=halo(0.55)),
        item('c3-mailbox', 'Mailbox', (4.3, 11.6), 'Decor_Buildings', yaw=180, search=1.2),
        item('c3-plant', 'PottedPlant', (0.3, 12.3), 'Decor_Buildings', search=1.0),
        item('workshop-barrel', 'Barrel', (11.0, -10.7), 'Decor_Buildings', search=1.0),
        item('workshop-toolbox', 'Toolbox', (18.4, 3.35, -6.5), 'Decor_Interior', search=1.0),
        item('plaza-signpost', 'Signpost', (8.6, 9.6), 'Decor_PathEdges', faceAt=[29, 26], search=1.6),
        # Cottage interiors (floor y 3.36): rugs, paintings, plants, pans, shelf.
        item('c1-rug', 'RugBlue', (-13.2, 3.36, -2.4), 'Decor_Interior', yaw=90, search=0.6),
        item('c1-pans', 'HangingPans', (-12.0, 3.36, -1.0), 'Decor_Interior', mount='wall', dir=[1, 0], height=1.6, search=0.8),
        item('c1-plant-in', 'PottedPlant', (-11.6, 3.36, -0.2), 'Decor_Interior', search=0.8),
        item('c2-rug', 'RugRedStriped', (-11.0, 3.36, 12.8), 'Decor_Interior', yaw=90, scale=[0.8], search=0.6),
        item('c2-painting', 'PaintingLandscape', (-10.4, 3.36, 14.0), 'Decor_Interior', mount='wall', dir=[0, 1], height=1.45, search=0.8),
        item('c2-shelf', 'Bookshelf', (-9.3, 3.36, 11.5), 'Decor_Interior', yaw=-90, search=0.8),
        item('c3-rug', 'RugBlue', (3.0, 3.36, 15.9), 'Decor_Interior', yaw=90, search=0.6),
        item('c3-painting', 'PaintingLandscape', (3.6, 3.36, 17.0), 'Decor_Interior', mount='wall', dir=[0, 1], height=1.45, search=0.8),
        item('c3-plant-in', 'PottedPlant', (4.7, 3.36, 13.8), 'Decor_Interior', search=0.8),
    ]
    scatters = [
        {'id': 'puerto-path-edge', 'group': 'Decor_PathEdges', 'props': PATH_EDGE, 'min': [-28, -16], 'max': [36, 30], 'count': 80,
         'near': 'path', 'nearMin': 0.8, 'nearMax': 2.0, 'minSpacing': 1.8, 'seed': 37},
        {'id': 'puerto-meadow', 'group': 'Decor_Meadow', 'props': {'FlowersWhite': 2, 'FlowersYellow': 2, 'Bush': 2, 'Fern': 1, 'RockCluster': 1},
         'min': [-40, -25], 'max': [42, 38], 'count': 25, 'minSpacing': 3.5, 'seed': 41},
        {'id': 'puerto-lily', 'group': 'Decor_Dock', 'props': {'LilyPads': 1}, 'min': [-9, -19], 'max': [9, -15], 'count': 4,
         'mount': 'water', 'waterOffset': 0.05, 'minSpacing': 2.0, 'seed': 43},
        {'id': 'puerto-reeds', 'group': 'Decor_Dock', 'props': {'Reeds': 1}, 'min': [-24, -24], 'max': [24, -12], 'count': 8,
         'near': 'water', 'nearMin': 0.6, 'nearMax': 1.8, 'minSpacing': 1.8, 'seed': 47},
    ]
    return {'mapId': 'hf-puerto-del-faro-v1',
            'floorPattern': r'^(Terrain_Playable|Path_|Plaza_|Cottage_\d+_Floorboards|BoatWorkshop_Floorboards|Pier_\d_GroupedDeckPlanks|Workshop_QuaysidePorch_Support|Footbridge_CamberedContinuousDeck)',
            'pathPattern': r'^(Path_|Plaza_)', 'waterPattern': r'^Water_Ocean_Pueblo$', 'waterY': 0.0,
            'excludePattern': r'^(Rock_|Pine_|BackdropPine|Terrain_North)', 'maxLights': 3, 'items': items, 'scatters': scatters}


def yate():
    items = [
        # Aft deck: folding chairs by the rail looking at the sea, a crate and lanterns.
        item('aft-chair-a', 'FoldingChair', (2.9, 3.4, -11.0), 'Decor_Deck', faceAt=[6, -13], search=0.8),
        item('aft-chair-b', 'FoldingChair', (-2.9, 3.4, -9.6), 'Decor_Deck', faceAt=[-6, -12], search=0.8),
        item('aft-lantern', 'HandLantern', (-3.1, 3.4, -12.6), 'Decor_Deck', search=0.8, halo=halo(0.5, 0.3)),
        item('aft-barrel', 'Barrel', (-3.2, 3.4, -11.5), 'Decor_Deck', search=0.8),
        item('aft-crate', 'Crate', (3.2, 3.4, -9.9), 'Decor_Deck', yaw=15, scale=[0.8], search=0.8),
        # Foredeck lounge: chairs toward the bow, lantern on the low table.
        item('fore-chair-a', 'FoldingChair', (-2.3, 3.4, 8.2), 'Decor_Deck', faceAt=[-1, 14], search=0.8),
        item('fore-chair-b', 'FoldingChair', (2.3, 3.4, 8.2), 'Decor_Deck', faceAt=[1, 14], search=0.8),
        item('fore-lantern', 'HandLantern', (-2.6, 3.4, 11.8), 'Decor_Deck', search=0.8, halo=halo(0.5, 0.3)),
        # Flybridge: map on the drinks table, a chair.
        item('fly-chair', 'FoldingChair', (-0.8, 6.1, -5.3), 'Decor_Deck', faceAt=[0, -12], search=0.8),
        # Lower cabins: rugs and plants.
        item('salon-rug', 'RugRedStriped', (0.5, 3.4, 1.8), 'Decor_Interior', yaw=90, scale=[0.7], search=0.8),
        item('lower-rug', 'RugBlue', (0.0, 0.8, -1.5), 'Decor_Interior', yaw=90, scale=[0.7], search=1.0),
        item('salon-plant', 'PottedPlant', (-2.9, 3.4, 0.6), 'Decor_Interior', search=0.8),
    ]
    return {'mapId': 'hf-yate-a-la-deriva-v3',
            'floorPattern': r'^(YATE_Teak_|YATE_LowerCabinFloor|YATE_MainDeck_Continuous)',
            'pathPattern': r'^$', 'maxLights': 0, 'items': items, 'scatters': []}


def main():
    config = {
        'schema': 'lms.decor.v030/1',
        'generator': 'docs/v030/maps/decor_config.py',
        'outputRoot': 'Assets/LetMeSleep/Content/Environment/HiggsfieldDecor',
        'kit': 'Assets/LetMeSleep/Presentation/Generated/HiggsfieldAtmosphereKit.asset',
        'maps': [isla(), casa(), camp(), yate(), puerto()],
    }
    path = HERE / 'decor-v030.json'
    path.write_text(json.dumps(config, indent=1, ensure_ascii=False) + '\n', encoding='utf-8')
    print('wrote', path, sum(len(m['items']) for m in config['maps']), 'items',
          sum(s['count'] for m in config['maps'] for s in m['scatters']), 'scatter slots')


if __name__ == '__main__':
    main()
