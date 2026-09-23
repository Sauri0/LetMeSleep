"""v0.3.0 scenes/decor round 2: the art director's corrections that live in the atmosphere pipeline (menu/sala profile,
Casa fireplace, camp pond, Puerto cottages, Isla cabin lamp, spawn facings). Rebuilds docs/v030/maps/atmosphere-v030.json
from r4_config.build() plus the deltas declared here, so every value stays reviewable and the result is reproducible:
    python docs/v030/maps/r5_config.py
HiggsfieldAtmosphereCorrection then applies the JSON inside Unity (V030SceneTools step "atmosphere").

Director corrections -> where they live:
  1/3 menu + sala           AlfaGlobalVolume: bloom threshold 1.3, scatter 0.55 (eye whites keep their black pupils), softer blue shadow
                            tint and neutral exposure so the sala measures its UI-06 colours (lights: scenes-v030.json).
  5 Puerto cottages         the three book shelves crossing the back windows are hidden (visual only); the night-window
                            shader now decides the room side by the wall plane (a viewer in the doorway sees the night).
  6 camp pond               the pale #6680B4 foam polygons of the jetty pond (they read as ice) are hidden; the creek foam
                            drops to a dark #2A4A8A.
  7 Isla cabin              the bedside lamp shade glows #FFB347 (its light is a decor light, decor_config.py).
  8 Casa living             the fireplace light drops until the floor hotspot stays <= #C07830.
  9 spawns                  human spawns facing an object closer than 1 m turn to the nearest clear view
                            (HiggsfieldDecorBuilder.SpawnClearanceReport, decor included).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r4_config  # noqa: E402
from r3_config import GEN, by_id, set_material, set_rule, dump  # noqa: E402

CONFIG = os.path.join(HERE, 'atmosphere-v030.json')


def spawn(role, index, yaw, note):
    return {'role': role, 'index': index, 'yaw': yaw, 'note': note}


def set_spawns(map_config, updates):
    """Replace or add spawn facings by (role, index), keeping the others."""
    facings = {(f['role'], f['index']): f for f in map_config.get('spawnFacings', [])}
    for f in updates:
        facings[(f['role'], f['index'])] = f
    map_config['spawnFacings'] = [facings[k] for k in sorted(facings, key=lambda k: (k[0] != 'human', k[1]))]


def renderer(match, **values):
    rule = {'match': 'Environment/' + match}
    rule.update(values)
    return rule


def build():
    c = r4_config.build()

    # ---------------- Menu and sala (AlfaGlobalVolume) ----------------
    for profile in c['profiles']:
        if profile['path'].endswith('AlfaGlobalVolume.asset'):
            profile['bloom']['threshold'] = 1.3
            profile['bloom']['scatter'] = 0.55
            profile['color']['postExposure'] = 0.0
            profile['color']['saturation'] = 0
            profile['color']['contrast'] = 5
            profile['shadows'] = [0.96, 0.98, 1.06, -0.02]
            profile['vignette']['intensity'] = 0.14  # the left wall under the logo reads navy, not black (director #2)

    # ---------------- Casa ----------------
    casa = by_id(c, 'hf-casa-del-patio-v1')
    casa['note'] += ' scenes r2 (director #8): fuego de la chimenea mas bajo (charco del piso <= #C07830).'
    # The practical sat 0.65 m over the hearth floor: inverse-square made a #EC8913 hotspot. Raised to ~1.2 m and softer
    # (r2-full2 measured the hearth floor at #C87420 with 0.8; 0.6 keeps it under #C07830).
    set_rule(casa['lightRules'], 'Environment/CASA_Fireplace_Practical_Anchor', color=[1.0, 0.58, 0.26], intensity=0.6, range=4.5,
             flicker=0.3, offset=[0, 0.6, 0.9], flame={'height': 1.2, 'offset': [-0.07, -0.95, -0.43]})

    # ---------------- Camp ----------------
    camp = by_id(c, 'hf-campamento-pinar-v2')
    camp['note'] += ' scenes r2 (director #6): sin espuma palida en la charca del muelle (parecia hielo).'
    camp['renderers'] = [r for r in camp['renderers'] if r['match'] != 'Environment/Foam_Lake_*']
    camp['renderers'].append(renderer('Foam_Lake_*', hide=True, expect=4,
                                      note='Pale foam polygons of the jetty pond read as ice (director #6).'))
    set_material(camp, 7, 'CAMP_Mat_Foam', baseColor='#2A4A8A', emission=[0.01, 0.02, 0.05])

    # ---------------- Puerto ----------------
    port = by_id(c, 'hf-puerto-del-faro-v1')
    port['note'] += ' scenes r2 (director #5): sin estantes de libros cruzando las ventanas traseras de las cabanas.'
    port['renderers'].append(renderer('Cottage_0*_Shelf_Books', hide=True, expect=3,
                                      note='The book shelf crossed the back window of each cottage (director #5).'))

    # ---------------- Isla ----------------
    isla = by_id(c, 'hf-isla-del-laguito-v2')
    isla['note'] += ' scenes r2 (director #7): velador de la cabana encendido.'
    isla.setdefault('renderers', [])
    isla['renderers'].append(renderer('Cabin_Root/Interior_Bedside_Lamp', swapFrom='Color_010',
                                      swapTo=GEN + 'Higgsfield_LampShadeGlowDay.mat', ignoreLocalLights=True, expect=1,
                                      note='Bedside lamp shade switched on #FFB347 (director #7); its light is a decor light.'))

    # ---------------- Spawn facings (director #9) ----------------
    for map_id, updates in SPAWN_FACINGS.items():
        set_spawns(by_id(c, map_id), updates)
    return c


# Filled from HiggsfieldDecorBuilder.SpawnClearanceReport (Validation/V030/Scenes/.../spawn-clearance.json): the nearest
# yaw whose first view is free >= 2.5 m over +-16 degrees and >= 1.2 m over +-34 degrees, decor included.
# Values from runs/r2-build3 (spawn-clearance.json): "near" = nearest clear yaw, "open" = most open view within 90 degrees.
SPAWN_FACINGS = {
    'hf-campamento-pinar-v2': [
        spawn('human', 1, 146, 'director #9: a tent corner and a box within 1 m at 126; 146 keeps the fire in view (near, 14.1 m ahead)'),
        spawn('human', 2, 300, 'nearest clear view toward the fire (8.8 m ahead)'),
        spawn('human', 3, 62, 'director #9: a barrel and the shelter post at the frame edge; most open view (9.8 m ahead, 4.1 m frame)'),
    ],
    'hf-puerto-del-faro-v1': [
        spawn('human', 2, 102, 'director #9: cottage wall at the left edge; most open view (5.7 m frame)'),
        spawn('human', 3, 276, 'director #9: a pine trunk ahead and a cottage wall at the right; most open view (10.7 m ahead)'),
        spawn('human', 4, 76, 'director #9: the lighthouse door at ~1 m; most open view (8.0 m)'),
    ],
    'hf-casa-del-patio-v1': [
        spawn('human', 1, 90, 'hall: a wall within 1 m at the frame edge along +Z; toward the kitchen (2.7 m ahead, 2.4 m frame)'),
        spawn('human', 3, 108, 'upstairs landing: 0.9 m at the frame edge at 180; most open view (2.8 m ahead)'),
    ],
    'hf-isla-del-laguito-v2': [
        spawn('human', 4, 14, 'lookout: 2.5 m ahead along +Z; 14 degrees clears the view (20 m ahead)'),
    ],
}


if __name__ == '__main__':
    config = build()
    text = dump(config) + '\n'
    if '--check' in sys.argv:
        print('ok', len(text.splitlines()), 'lines')
    else:
        open(CONFIG, 'w', encoding='utf-8', newline='\n').write(text)
        print('wrote', CONFIG)
