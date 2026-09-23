"""v0.3.0 maps round 3, second pass: the art director's ten corrections on the r3 captures (maps-r3 review, "CORRECCIONES
DEL DIRECTOR"). Rebuilds docs/v030/maps/atmosphere-v030.json from r3_config.build() (round-2 config + r3 deltas) plus
the deltas declared here, so every value stays reviewable and the result is reproducible:
    python docs/v030/maps/r4_config.py
HiggsfieldAtmosphereCorrection then applies the JSON inside Unity (see r3_config.py). Menu and lobby are untouched here.

Director corrections -> where they live:
  1 camp first view            spawnFacings (catalog, applied by AlfaApplication), CanvasOlive #5A6B2E, shelter lantern
  2 lighthouse beam / lens      Beam shader fade/core, beam rule #FFD27A alpha 0.15 narrower, lens emission, halo core
  3 cobbles and stairs          PatternLit cobbles (flattened bevel normals), lighter joints, warm pools near lanterns,
                                wooden stairway (treads #6A4A30 / risers #3A2618 by face orientation)
  4 water reflections           Glint shader (continuous broken strokes, alpha blend), #FFB347 alpha 0.7, 3.5 m, one per
                                lamp by the water (camp lanterns 05-07 + jetty, Puerto piers)
  5 dock/bridge wood            planks #8B5A2B, rails dark brown, lantern posts wood; planks receive the lantern pool
  6 Casa fireplace              hidden curtain/rod behind the chimney breast, faceted stone blocks, 1.5x flame, ember rims,
                                warmer and dimmer living light
  7 Puerto cottages             warm plaster walls (InteriorLit, cream inside), interior-fill shadow normal bias 1.0,
                                emissive lantern glass (table lamps and every Puerto lantern)
  8 character measurement       harness ID masks + measure-spec-r4.json (validation side)
  9 functional                  spawn yaws (Puerto h2, camp h1, Isla h3 ...), runtime head visibility (CharacterView)
 10 yacht porthole              round porthole (PatternLit), brass #B08A3A ring, sea #2F8FD0 / sky #8CC8F0
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r3_config  # noqa: E402
from r3_config import GEN, by_id, set_material, set_rule, authored, lit, srgb, dump  # noqa: E402

CONFIG = os.path.join(HERE, 'atmosphere-v030.json')
AMBER = srgb('#FFB347')
BEAM_WARM = srgb('#FFD27A')


def pattern(note, base, **values):
    """PatternLit material (LetMeSleep/Higgsfield/PatternLit); colors sRGB, vectors raw."""
    material = {'shader': 'LetMeSleep/Higgsfield/PatternLit', 'note': note,
                'colors': {'_BaseColor': srgb(base) + [1.0]}, 'floats': {'_Smoothness': 0, '_Metallic': 0}}
    for key, value in values.items():
        if key == 'colors':
            material['colors'].update(value)
        elif key == 'floats':
            material['floats'].update(value)
        else:
            material[key] = value
    return material


def spawn(role, index, yaw, note):
    return {'role': role, 'index': index, 'yaw': yaw, 'note': note}


def renderer(match, **values):
    rule = {'match': 'Environment/' + match}
    rule.update(values)
    return rule


def build():
    c = r3_config.build()

    # ---------------- Kit shaders ----------------
    for item in c['authoredMaterials']:
        if item['path'] == GEN + 'Higgsfield_Beam.mat':
            item['floats'] = {'_BeamIntensity': 1.0, '_EdgeSoftness': 1.2, '_LengthFalloff': 1.5,
                              '_FadeEnd': 0.6, '_CoreBoost': 5.0, '_CoreLength': 0.3}
            item['note'] = ('Lighthouse shaft #FFD27A, additive, alpha 0.15 from the rule, gone at 60 % of its length, warm core '
                            'near the lens (director #2).')
        if item['path'] == GEN + 'Higgsfield_Glint.mat':
            item['floats'] = {'_GlintIntensity': 1.6, '_BandFrequency': 7.0, '_BandSpeed': 0.6, '_StrokeFill': 0.62}
            item['note'] = ('Lamp reflection on night water: one continuous column of broken horizontal #FFB347 strokes, alpha '
                            'blended (0.7 at the lamp to 0 at 3.5 m, director #4).')
        if item['path'] == GEN + 'Higgsfield_LighthouseLens.mat':
            item['vectors']['_EmissionColor'] = [2.0, 0.5, 0.18, 1]  # + halo centre + bloom, Neutral tonemap: ~#FFD27A face
            item['note'] = 'Puerto lighthouse lens: #FFD27A core after tonemapping (director #2).'
        if item['path'] == GEN + 'Higgsfield_PlazaJoint.mat':
            item['colors']['_BaseColor'] = srgb('#5C5E66') + [1.0]
            item['note'] = 'Puerto plaza joints, lighter so they read #353A48 at night instead of #252F53 (director #3).'
        if item['path'] == GEN + 'Higgsfield_FireStone.mat':
            item.clear()
            item.update(pattern('Casa fireplace: faceted grey stone blocks #7A7470 (cool albedo under the warm room light), director #6.',
                                '#9AAAB8', floats={'_Pattern': 1, '_MortarWidth': 0.014, '_BlockVariation': 0.24, '_BlockTilt': 0.6},
                                colors={'_MortarColor': srgb('#76726E') + [1.0]},
                                vectors={'_BlockSize': [0.34, 0.22, 0, 0]}))
            item['path'] = GEN + 'Higgsfield_FireStone.mat'
        if item['path'] == GEN + 'Higgsfield_FireLog.mat':
            item.clear()
            item.update(pattern('Casa hearth logs #5A3420 with #FF7A2A embers only on their edges (director #6).', '#5A3420',
                                floats={'_Pattern': 2, '_EmberPower': 3.0},
                                vectors={'_EmberColor': [2.2, 0.43, 0.05, 1]}))  # ~2.2 x #FF7A2A (linear HDR)
            item['path'] = GEN + 'Higgsfield_FireLog.mat'

    authored(c, GEN + 'Higgsfield_CampPostWood.mat', **pattern(
        'Camp lantern posts: wood #5A3A24 at night, not black metal; warm lift so a backlit post still reads as wood '
        '(director #5).', '#5A3A24', floats={'_Pattern': 0}, vectors={'_AmbientLift': [0.45, 0.4, 0.35, 0]}))
    authored(c, GEN + 'Higgsfield_CampRailWood.mat', **pattern(
        'Camp bridge and jetty rails, posts and stringers: dark brown #3A2618 at night instead of black #1A1E2A '
        '(warm lift, the moon backlights them), director #5.', '#5A3A24',
        floats={'_Pattern': 0}, vectors={'_AmbientLift': [0.55, 0.5, 0.45, 0]}))
    authored(c, GEN + 'Higgsfield_CobbleStone.mat', **pattern(
        'Puerto plaza cobbles: bevel normals flattened so the p95 stays <= #5A6070 (director #3).', '#5E5F60',
        floats={'_Pattern': 0, '_NormalFlatten': 0.8}))
    authored(c, GEN + 'Higgsfield_CobbleStoneLight.mat', **pattern(
        'Puerto plaza lighter cobbles, same flattened bevels (director #3).', '#666560', floats={'_Pattern': 0, '_NormalFlatten': 0.8}))
    authored(c, GEN + 'Higgsfield_StairWood.mat', **pattern(
        'Lighthouse stairway in wood: treads #6A4A30 and risers #3A2618 by face orientation, warm lift of the path lanterns '
        '(director #3).', '#7E5A38',
        floats={'_Pattern': 0, '_SplitByNormal': 1}, colors={'_SideColor': srgb('#86583A') + [1.0]},
        vectors={'_AmbientLift': [0.7, 0.7, 0.7, 0]}))
    authored(c, GEN + 'Higgsfield_PuertoLanternGlass.mat', **pattern(
        'Puerto lanterns (table, wall, path, pier): lit glass band #FFB347 inside the iron frame (director #7).', '#4D5759',
        floats={'_Pattern': 4}, vectors={'_EmissionColor': [2.4, 0.65, 0.42, 1], '_GlassBand': [0.2, 0.74, 0.8, 0]}))
    authored(c, GEN + 'Higgsfield_PortholeRound.mat', **pattern(
        'Yacht porthole: round window, brass ring #B08A3A, clean horizon of sea #2F8FD0 under sky #8CC8F0 (director #10).', '#EADFC7',
        floats={'_Pattern': 3, '_RingWidth': 0.18, '_PortholeScale': 0.92, '_Horizon': 0.02, '_ViewIntensity': 1.0},
        # Inputs slightly desaturated: the yacht grade (+saturation, +contrast) brings them back to #8CC8F0 / #2F8FD0 / #B08A3A.
        colors={'_SideColor': srgb('#EADFC7') + [1.0], '_RimColor': srgb('#BC9A55') + [1.0], '_SkyColor': srgb('#9CD2F4') + [1.0],
                '_SkyTopColor': srgb('#80BEEC') + [1.0], '_SeaColor': srgb('#4A98D4') + [1.0], '_SeaDeepColor': srgb('#3F88C4') + [1.0]}))
    authored(c, GEN + 'Higgsfield_PortholeWall.mat', **lit('#EADFC7', 'Old rectangular porthole frame painted as the cabin wall (the round ring replaces it).'))
    authored(c, GEN + 'Higgsfield_PuertoWall.mat', shader='LetMeSleep/Higgsfield/InteriorLit',
             note='Puerto cottage/workshop walls: warm plaster outside (hue 20-35 at night, ~#5A4638), cream #E8DCC5 inside with a '
                  '#8B5A2B baseboard (director #7).',
             colors={'_BaseColor': srgb('#B09360') + [1.0], '_InteriorColor': srgb('#E8DCC5') + [1.0],
                     '_CeilingColor': srgb('#E8DCC5') + [1.0], '_TrimColor': srgb('#8B5A2B') + [1.0]},
             vectors={'_ExteriorFill': [0.41, 0.385, 0.2, 0]},
             floats={'_TrimHeight': 0.16, '_InteriorFaceMargin': 0.35, '_Smoothness': 0, '_Metallic': 0})
    authored(c, GEN + 'Higgsfield_PuertoGable.mat', shader='LetMeSleep/Higgsfield/InteriorLit',
             note='Puerto gables: lighter warm plaster outside, cream inside (director #7).',
             colors={'_BaseColor': srgb('#B89A70') + [1.0], '_InteriorColor': srgb('#E8DCC5') + [1.0],
                     '_CeilingColor': srgb('#E8DCC5') + [1.0], '_TrimColor': srgb('#8B5A2B') + [1.0]},
             vectors={'_ExteriorFill': [0.41, 0.385, 0.2, 0]},
             floats={'_TrimHeight': 0, '_InteriorFaceMargin': 0.35, '_Smoothness': 0, '_Metallic': 0})
    reflection = {'length': 3.5, 'width': 0.55, 'alpha': 0.7, 'color': AMBER}

    # ---------------- Camp ----------------
    camp = by_id(c, 'hf-campamento-pinar-v2')
    camp['note'] += (' r4 (director): primera vista a fogata/carpa/farol, carpa oliva, farol propio del refugio, madera de muelle y '
                     'puente, postes de madera, reflejos continuos ambar en cada farol junto al agua.')
    camp['spawnFacings'] = [
        spawn('human', 0, 152, 'fire, olive tents and lantern post (ENV-04 02), not the empty meadow'),
        spawn('human', 1, 126, 'toward the fire; +Z faced a tent wall at 1.9 m'),
        spawn('human', 2, 290, 'toward the fire'),
        spawn('human', 3, 100, 'toward the fire past the shelter'),
        spawn('human', 4, 242, 'toward the fire'),
        spawn('mosquito', 0, 280, 'fire at the left, tents and lanterns at the right'),
    ]
    for index, name, color in [(1, 'CAMP_Mat_CanvasOlive', '#5A6B2E'), (26, 'CAMP_Mat_Wood', '#6A4630'),
                               (27, 'CAMP_Mat_WoodDark', '#5E4028'), (28, 'CAMP_Mat_WoodLight', '#86603A')]:
        set_material(camp, index, name, baseColor=color)
    set_material(camp, 7, 'CAMP_Mat_Foam', baseColor='#7488B0', emission=[0.05, 0.08, 0.16])
    rules = camp['lightRules']
    for n in ('05', '06', '07'):
        set_rule(rules, 'Environment/LGT_Lantern_' + n, reflection=dict(reflection, waterY=-0.05))
    for item in rules:
        if item['match'] == 'Environment/LGT_Jetty_Lantern':
            item['reflection'] = dict(reflection, waterY=-0.05)
    # Small lantern of its own, hung toward the path side so it is seen past the centre post (director #1).
    set_rule(rules, 'Environment/LGT_CAMP_Shelter', color=[1.0, 0.62, 0.3], intensity=3.0, range=6.5, shadows='Soft', shadowTier=0,
             shadowNormalBias=1.0, offset=[0.8, -0.45, 0.8], lantern={'size': 0.22},
             halo={'size': 0.6, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'offset': [0.8, -0.45, 0.8]})
    # Fire bounce on the fire-facing side of each tent: the tent lights move 1.9 m toward the fire and 1 m up (director #1).
    fire = (0.7, 0.2)
    for n, (tx, tz) in {'01': (-13.154, 12.976), '02': (-11.126, 15.333), '03': (-14.436, 16.359),
                        '04': (10.619, -7.188), '05': (13.19, -5.265), '06': (12.863, -9.386)}.items():
        dx, dz = fire[0] - tx, fire[1] - tz
        length = (dx * dx + dz * dz) ** 0.5
        set_rule(rules, 'Environment/LGT_CAMP_Tent_' + n, color=[1.0, 0.62, 0.32], intensity=1.3, range=4.5, shadows='None',
                 offset=[round(dx / length * 1.9, 3), 1.0, round(dz / length * 1.9, 3)])
    # Warm bounce of the shelter lantern on its own beams, posts and benches (beams read #5A3A24).
    camp['interiorVolumes'] = [{'center': [-24.5, 1.5, 5.0], 'size': [5.6, 3.0, 4.9], 'note': 'Shelter (director #1).'}]
    camp['interiorAmbient'] = {'color': [0.55, 0.38, 0.22], 'intensity': 1.8, 'keep': 0.5, 'minSize': 0.3}
    # Director #8: camp fill a touch cooler so the shirt keeps its cream (not fire-orange) and the pants their blue.
    camp['characterFill'] = {'color': [0.92, 0.95, 1.0], 'intensity': 1.0}
    # Posts were moon-only (r2) and read black against the sky: now wood lit by their own lantern (layer 4).
    camp['renderers'] = [r for r in camp['renderers']
                         if r['match'] not in ('Environment/CAMP_Lantern_*_Post', 'Environment/CAMP_Jetty_Lantern_Post')]
    camp['renderers'] += [
        renderer('CAMP_Lantern_*_Post', swapFrom='Color_027', swapTo=GEN + 'Higgsfield_CampPostWood.mat', expect=11,
                 addLightLayers=[4], note='Wooden lantern posts #5A3A24 lit by their lantern (director #5).'),
        renderer('CAMP_Jetty_Lantern_Post', swapFrom='Color_027', swapTo=GEN + 'Higgsfield_CampPostWood.mat', expect=1,
                 addLightLayers=[4]),
        renderer('CAMP_Jetty_Plank_*', addLightLayers=[4], note='Planks receive their lantern (~#8A5A36 under it).'),
        renderer('CAMP_Jetty_Shore_*', addLightLayers=[4]),
        renderer('CAMP_Bridge_*_Plank_*', addLightLayers=[4]),
        renderer('CAMP_Bridge_*_Rail_*', swapFrom='Color_026', swapTo=GEN + 'Higgsfield_CampRailWood.mat',
                 note='Dark brown rails (director #5).'),
        renderer('CAMP_Bridge_*_RailPost*', swapFrom='Color_027', swapTo=GEN + 'Higgsfield_CampRailWood.mat'),
        renderer('CAMP_Bridge_*_Stringer*', swapFrom='Color_027', swapTo=GEN + 'Higgsfield_CampRailWood.mat'),
        renderer('CAMP_Jetty_Rail_*', swapFrom='Color_026', swapTo=GEN + 'Higgsfield_CampRailWood.mat'),
        renderer('CAMP_Jetty_Post_*', swapFrom='Color_027', swapTo=GEN + 'Higgsfield_CampRailWood.mat'),
        renderer('CAMP_Jetty_EndPost_*', swapFrom='Color_027', swapTo=GEN + 'Higgsfield_CampRailWood.mat'),
    ]

    # ---------------- Puerto ----------------
    port = by_id(c, 'hf-puerto-del-faro-v1')
    port['note'] += (' r4 (director): haz calido angosto, foco #FFD27A, empedrado sin bisel brillante con juntas #353A48 y charcos '
                     'calidos, escalera de madera, reflejos continuos, muelle de madera, cabanas de revoque calido, vidrio de '
                     'faroles encendido.')
    port['spawnFacings'] = [spawn('human', 2, 100, 'toward the village and harbor path; +Z faced the cottage door at 1.9 m')]
    port['characterFill'] = {'color': [0.95, 0.97, 1.0], 'intensity': 1.8}  # director #8: mosquito body >= #8A1E24
    for index, name, color in [(10, 'PDF05_Honey', '#A0703C'), (14, 'PDF05_Oak', '#8B5A2B'), (35, 'PDF05_Wood', '#6E4A2E')]:
        set_material(port, index, name, baseColor=color)
    rules = port['lightRules']
    set_rule(rules, 'Environment/Anchor_LGT_Pueblo_LighthouseLens', color=BEAM_WARM, intensity=7.0, range=18, shadows='None',
             # Halo more orange than the target: Neutral tonemapping compresses red first, so lens + core read #FFD27A.
             halo={'size': 6.8, 'color': srgb('#FFB060'), 'alpha': 0.4, 'intensity': 1.2, 'offset': [0, -0.35, 0],
                   'depthTolerance': 3.6, 'core': 0.0},
             beam={'length': 26, 'radius': 2.0, 'speed': 28, 'tilt': 3, 'count': 2, 'alpha': 0.14, 'color': BEAM_WARM,
                   'offset': [0, -0.35, 0]})
    pool = {'radius': 4.0, 'color': srgb('#7A5234'), 'alpha': 0.4}
    for item in rules:
        if item['match'] == 'Environment/Anchor_LGT_Pier_*':
            item['reflection'] = dict(reflection, waterY=0.12, width=0.6)
        if item['match'] in ('Environment/Anchor_LGT_PathLantern_*',):
            item['pool'] = pool
    set_rule(rules, 'Environment/Anchor_LGT_Cottage_01_ExteriorLantern', pool=pool)
    set_rule(rules, 'Environment/Anchor_LGT_Cottage_02_ExteriorLantern', pool=pool)
    set_rule(rules, 'Environment/Anchor_LGT_Cottage_03_ExteriorLantern', pool=pool)
    for n in ('01', '02', '03'):
        set_rule(rules, 'Environment/Anchor_LGT_Cottage_%s_InteriorFill' % n, color=[1.0, 0.72, 0.42], intensity=1.5, range=6.0,
                 shadows='Soft', shadowTier=0, shadowNormalBias=1.0, lightLayers=[0], halo={'size': 0})
    set_rule(rules, 'Environment/Anchor_LGT_Workshop_Interior', color=[1.0, 0.72, 0.42], intensity=1.8, range=6.0, shadows='Soft',
             shadowTier=0, shadowNormalBias=1.0, lightLayers=[0], halo={'size': 0})
    lanterns = ['Cottage_*_BedsideLamp', 'Cottage_*_ExteriorLantern', 'Workshop_BenchLamp', 'Workshop_ExteriorLantern',
                'Pier_*_EndLantern'] + ['PathLantern_%d' % i for i in range(1, 7)]
    port['renderers'] += [renderer(m, swapFrom='Color_011', swapTo=GEN + 'Higgsfield_PuertoLanternGlass.mat',
                                   note='Lit lantern glass #FFB347 (director #7).') for m in lanterns]
    port['renderers'] += [
        renderer('Plaza_GroupedCobbles', expect=1, swaps=[
            {'from': 'Color_002', 'to': GEN + 'Higgsfield_CobbleStone.mat'},
            {'from': 'Color_003', 'to': GEN + 'Higgsfield_CobbleStoneLight.mat'},
            {'from': 'Color_034', 'to': GEN + 'Higgsfield_CobbleStoneLight.mat'}],
            note='Low-contrast cobbles without bright bevels (director #3).'),
        renderer('Lighthouse_Approach_StoneStairway', expect=1, swaps=[
            {'from': 'Color_032', 'to': GEN + 'Higgsfield_StairWood.mat'},
            {'from': 'Color_034', 'to': GEN + 'Higgsfield_StairWood.mat'}],
            note='Wooden stairway instead of black and white stripes (director #3).'),
        renderer('Cottage_*_SealedWall', swapFrom='Color_024', swapTo=GEN + 'Higgsfield_PuertoWall.mat', expect=12,
                 note='Warm plaster instead of mauve #604C57 (director #7).'),
        renderer('BoatWorkshop_*_SealedWall', swapFrom='Color_024', swapTo=GEN + 'Higgsfield_PuertoWall.mat', expect=4),
        renderer('Cottage_*_Gable', swapFrom='Color_025', swapTo=GEN + 'Higgsfield_PuertoGable.mat', expect=6),
        renderer('BoatWorkshop_*_Gable', swapFrom='Color_025', swapTo=GEN + 'Higgsfield_PuertoGable.mat', expect=2),
    ]

    # ---------------- Casa ----------------
    casa = by_id(c, 'hf-casa-del-patio-v1')
    casa['note'] += (' r4 (director): chimenea de bloques de piedra facetados, llama 1.5x con brasas en el borde de los troncos, '
                     'sin cortina detras del revestimiento, living mas calido.')
    casa['spawnFacings'] = [spawn('human', 2, 202.5, 'along the hall; +Z faced a wall at 2.7 m'),
                            spawn('human', 3, 180, 'upstairs landing; +Z faced a wall at 0.9 m')]
    casa['renderers'] += [
        renderer('CASA_Curtain_Pleat_0*', hide=True, expect=6,
                 note='Curtain of the front window the chimney breast covers; it poked out on both sides (director #6).'),
        renderer('CASA_Curtain_Rod_0', hide=True, expect=1),
        renderer('CASA_Opening_00_Jamb*', hide=True, expect=2,
                 note='Jambs of that window stuck out beside the breast as a loose board (director #6).'),
        renderer('CASA_Opening_00_Lintel', hide=True, expect=1, note='Its lintel stuck out above the breast.'),
    ]
    casa['interiorAmbient'] = dict(casa['interiorAmbient'], color=[0.55, 0.35, 0.12])
    casa['characterFill'] = {'color': [0.95, 0.97, 1.0], 'intensity': 1.8}  # director #8: mosquito body >= #8A1E24
    for item in c['authoredMaterials']:
        if item['path'] == GEN + 'Higgsfield_CasaShell.mat':
            item['colors']['_CeilingColor'] = srgb('#DCC4A0') + [1.0]  # warm ceiling: hotspot p95 <= #A85A20
    rules = casa['lightRules']
    set_rule(rules, 'Environment/CASA_Fireplace_Practical_Anchor', color=[1.0, 0.58, 0.26], intensity=1.1, range=5.0, flicker=0.3,
             offset=[0, 0.05, 0.75], flame={'height': 1.2, 'offset': [-0.07, -0.4, -0.28]})
    set_rule(rules, 'Environment/CASA_Interior_Living_Anchor', color=[1.0, 0.6, 0.2], intensity=0.45, shadows='Soft')

    # ---------------- Yate ----------------
    yate = by_id(c, 'hf-yate-a-la-deriva-v3')
    yate['note'] += ' r4 (director): ojos de buey redondos con aro de laton, mar y cielo con horizonte limpio.'
    yate['renderers'] = [r for r in yate['renderers'] if r['match'] != 'Environment/YATE_Porthole_*']
    yate['renderers'].append(renderer('YATE_Porthole_*', expect=8, swaps=[
        {'from': 'Color_005', 'to': GEN + 'Higgsfield_PortholeRound.mat'},
        {'from': 'Color_000', 'to': GEN + 'Higgsfield_PortholeWall.mat'}],
        note='Round porthole with brass ring instead of the rectangle with white squares (director #10).'))

    # ---------------- Isla ----------------
    isla = by_id(c, 'hf-isla-del-laguito-v2')
    isla['spawnFacings'] = [spawn('human', 3, 180, 'toward the island center; +Z looked into low pine branches')]
    return c


if __name__ == '__main__':
    config = build()
    text = dump(config) + '\n'
    if '--check' in sys.argv:
        print('ok', len(text.splitlines()), 'lines')
    else:
        open(CONFIG, 'w', encoding='utf-8', newline='\n').write(text)
        print('wrote', CONFIG)
