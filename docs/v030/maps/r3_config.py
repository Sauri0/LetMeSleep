"""v0.3.0 maps round 3 (maps-r3.md): rebuilds docs/v030/maps/atmosphere-v030.json from the round-2 config
committed in git (base revision below) plus the round-3 deltas declared here, so every value stays reviewable
and the result is reproducible:  python docs/v030/maps/r3_config.py
HiggsfieldAtmosphereCorrection then applies the JSON inside Unity:
  Unity.exe -batchmode -quit -projectPath unity -executeMethod LetMeSleep.Editor.HiggsfieldAtmosphereCorrection.ApplyFromCommandLine
    -higgsfieldAtmosphereConfig <abs path to atmosphere-v030.json>"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
CONFIG = os.path.join(HERE, 'atmosphere-v030.json')
BASE_REVISION = '046ac0a3'  # round-2 config as merged into claude/v0.3.0
GEN = 'Assets/LetMeSleep/Presentation/Generated/Materials/'


def load_base():
    text = subprocess.check_output(['git', '-C', REPO, 'show', BASE_REVISION + ':docs/v030/maps/atmosphere-v030.json'])
    return json.loads(text.decode('utf-8'))


def dump(value, indent=0):
    pad = '  ' * indent
    if isinstance(value, dict):
        if not value:
            return '{}'
        items = [pad + '  ' + json.dumps(k) + ': ' + dump(v, indent + 1) for k, v in value.items()]
        return '{\n' + ',\n'.join(items) + '\n' + pad + '}'
    if isinstance(value, list):
        if all(not isinstance(v, (dict, list)) for v in value):
            return '[' + ', '.join(json.dumps(v) for v in value) + ']'
        items = [pad + '  ' + dump(v, indent + 1) for v in value]
        return '[\n' + ',\n'.join(items) + '\n' + pad + ']'
    return json.dumps(value, ensure_ascii=False)


def srgb(hex_color):
    h = hex_color.lstrip('#')
    return [round(int(h[i:i + 2], 16) / 255.0, 4) for i in (0, 2, 4)]


def by_id(config, map_id):
    return next(m for m in config['maps'] if m['mapId'] == map_id)


def set_material(map_config, index, name, **values):
    edits = [e for e in map_config['materials'] if e['index'] != index]
    edit = {'index': index, 'name': name}
    edit.update(values)
    edits.append(edit)
    map_config['materials'] = sorted(edits, key=lambda e: e['index'])


def set_rule(rules, match, **values):
    """Replace (or append) the light rule for an exact match string, keeping rule order stable."""
    for i, rule in enumerate(rules):
        if rule['match'] == match:
            new = {'match': match}
            new.update(values)
            rules[i] = new
            return
    new = {'match': match}
    new.update(values)
    rules.append(new)


def authored(config, path, **values):
    items = [m for m in config['authoredMaterials'] if m['path'] != path]
    material = {'path': path}
    material.update(values)
    items.append(material)
    config['authoredMaterials'] = items


def lit(color_hex, note, smoothness=0.0, emission=None):
    material = {'shader': 'Universal Render Pipeline/Lit', 'note': note,
                'colors': {'_BaseColor': srgb(color_hex) + [1.0]},
                'floats': {'_Smoothness': smoothness, '_Metallic': 0}}
    if emission:
        material['vectors'] = {'_EmissionColor': emission + [1]}
        material['keywords'] = ['_EMISSION']
    return material


def build():
    c = load_base()
    rim = {'color': srgb('#FF5A4A'), 'intensity': 2.2, 'spread': 1.0}
    fill = {'color': [1.0, 0.93, 0.84], 'intensity': 0.85}

    # ---------------- Kit and authored materials ----------------
    c['kit']['beam'] = GEN + 'Higgsfield_Beam.mat'
    c['kit']['glint'] = GEN + 'Higgsfield_Glint.mat'
    c['kit']['pool'] = GEN + 'Higgsfield_Pool.mat'
    authored(c, GEN + 'Higgsfield_Pool.mat', shader='LetMeSleep/Higgsfield/Pool',
             note='Warm ground pool decal under camp post lanterns (#6A4A2A, ~3 m).',
             floats={'_PoolFalloff': 1.3}, renderQueue=2950)
    c['ssao'] = {'renderer': 'Assets/Settings/PC_Renderer.asset', 'method': 1, 'samples': 0, 'blur': 0}
    authored(c, GEN + 'Higgsfield_Beam.mat', shader='LetMeSleep/Higgsfield/Beam',
             note='Lighthouse light shaft (additive, alpha ~0.15 from the light rule).',
             floats={'_BeamIntensity': 1.0, '_EdgeSoftness': 1.4, '_LengthFalloff': 1.3}, renderQueue=3010)
    authored(c, GEN + 'Higgsfield_Glint.mat', shader='LetMeSleep/Higgsfield/Glint',
             note='Warm #FFB347 reflection streak of a lamp on night water (additive).',
             floats={'_GlintIntensity': 1.6, '_BandFrequency': 2.4, '_BandSpeed': 0.6}, renderQueue=3005)
    authored(c, GEN + 'Higgsfield_LanternGlass.mat', shader='Universal Render Pipeline/Lit',
             note='Camp post lantern glass: translucent amber #FFB347 over the lantern flame (moon-lit only).',
             colors={'_BaseColor': [1.0, 0.55, 0.18, 0.55]},
             vectors={'_EmissionColor': [2.6, 0.45, 0.12, 1]},
             floats={'_Surface': 1, '_Blend': 0, '_SrcBlend': 5, '_DstBlend': 10, '_SrcBlendAlpha': 1,
                     '_DstBlendAlpha': 10, '_ZWrite': 0, '_Smoothness': 0.35, '_Metallic': 0},
             keywords=['_SURFACE_TYPE_TRANSPARENT', '_EMISSION'], tags={'RenderType': 'Transparent'}, renderQueue=3010)
    authored(c, GEN + 'Higgsfield_FireSoot.mat', **lit('#3A2A24', 'Casa firebox back: soot #2A1E1A, moon-lit only.'))
    authored(c, GEN + 'Higgsfield_FireStone.mat', **lit('#7A7470', 'Casa fireplace stone surround #7A7470.'))
    authored(c, GEN + 'Higgsfield_FireLog.mat', **lit('#5A3420', 'Casa hearth logs #5A3420 (moon-lit only, embers from the flame).'))
    authored(c, GEN + 'Higgsfield_PlazaJoint.mat', **lit('#3A4052', 'Puerto plaza joints under the cobbles (#2A2E3A at night).'))
    authored(c, GEN + 'Higgsfield_CasaShell.mat', shader='LetMeSleep/Higgsfield/InteriorLit',
             note='Casa plaster shell: warm wood-tan facade outside, cream #E8DCC5 walls with a #8B5A2B baseboard inside.',
             colors={'_BaseColor': srgb('#C8935C') + [1.0], '_InteriorColor': srgb('#E8DCC5') + [1.0],
                     '_CeilingColor': srgb('#E8DCC5') + [1.0], '_TrimColor': srgb('#8B5A2B') + [1.0]},
             floats={'_TrimHeight': 0.16, '_InteriorFaceMargin': 0.35, '_Smoothness': 0, '_Metallic': 0})
    authored(c, GEN + 'Higgsfield_PuertoRoof.mat', shader='LetMeSleep/Higgsfield/InteriorLit',
             note='Puerto roof panels: blue roof outside, lit wood #5A3A24 ceiling inside the cottages and workshop.',
             colors={'_BaseColor': [0.304, 0.449, 0.556, 1.0], '_InteriorColor': srgb('#C8B8A8') + [1.0],
                     '_CeilingColor': srgb('#C8B8A8') + [1.0], '_TrimColor': srgb('#8B5A2B') + [1.0]},
             floats={'_TrimHeight': 0, '_InteriorFaceMargin': 0.4, '_Smoothness': 0, '_Metallic': 0})
    authored(c, GEN + 'Higgsfield_PuertoRoofRed.mat', shader='LetMeSleep/Higgsfield/InteriorLit',
             note='Puerto red roof panels: red roof outside, lit wood ceiling inside.',
             colors={'_BaseColor': [0.634, 0.323, 0.304, 1.0], '_InteriorColor': srgb('#C8B8A8') + [1.0],
                     '_CeilingColor': srgb('#C8B8A8') + [1.0], '_TrimColor': srgb('#8B5A2B') + [1.0]},
             floats={'_TrimHeight': 0, '_InteriorFaceMargin': 0.4, '_Smoothness': 0, '_Metallic': 0})
    authored(c, GEN + 'Higgsfield_LampShadeGlowDay.mat', **lit('#5A3418', 'Yacht cabin lamp shade #FFB347 under bright day ambient.',
             emission=[3.6, 0.42, 0.1]))
    authored(c, GEN + 'Higgsfield_PortholeView.mat', shader='LetMeSleep/Higgsfield/NightWindow',
             note='Yacht portholes: day sky over a blue sea seen through the glass.',
             colors={'_SkyHorizon': [0.66, 0.85, 0.98], '_SkyZenith': [0.22, 0.52, 0.9], '_SeaColor': [0.09, 0.4, 0.7],
                     '_SeaHorizonColor': [0.34, 0.66, 0.88], '_HillColor': [0.3, 0.5, 0.6], '_MoonColor': [1.0, 1.0, 1.0]},
             vectors={'_MoonDirection': [0.3, 0.6, 0.7, 0], '_StarColor': [1, 1, 1, 1]},
             floats={'_ForceNight': 1, '_ViewMode': 1, '_HorizonLift': 0.14, '_StarDensity': 0, '_MoonIntensity': 0, '_MoonGlow': 0, '_Muntins': 0})
    window = {'_OutsideColor': [1.0, 0.55, 0.28], '_OutsideBottom': [0.36, 0.1, 0.05], '_CurtainColor': [0.62, 0.26, 0.14]}
    window_floats = {'_ForceNight': 0, '_OutsideIntensity': 2.0, '_OutsideGradient': 1.0, '_RoomDepth': 2.4, '_RoomShade': 0.4,
                     '_LampGlow': 0.25, '_CurtainOpacity': 0.35, '_CurtainWidth': 0.32, '_StarDensity': 0.55, '_MoonSize': 1.6,
                     '_MoonIntensity': 1.1, '_MoonPhase': 0.5, '_MoonGlow': 0.25, '_InteriorMargin': 0.45}
    for item in c['authoredMaterials']:
        if item['path'] in (GEN + 'Higgsfield_NightWindow_Casa.mat', GEN + 'Higgsfield_NightWindow_Puerto.mat'):
            item['colors'].update(window)
            item['floats'].update(window_floats)
            item['note'] = ('Night windows: lit room behind the glass (gradient #FFB347 top to #C8602A bottom, 35% curtains, '
                            'parallax) outside; night sky inside.')
        if item['path'] == GEN + 'Higgsfield_LighthouseLens.mat':
            item['vectors']['_EmissionColor'] = [4.5, 2.2, 1.2, 1]
            item['note'] = 'Puerto lighthouse lens #FFD27A (HDR ~4, moon-lit only so the lens light does not wash it white).'

    # Bloom: the game renders through URP's HDR intermediate, where the round-2 bloom (0.8, scatter 0.7) spread a
    # haze from fires and windows over whole rooms ("bloom leve solo en fuentes de luz", style guide section 4).
    for profile in c['profiles']:
        if profile['path'].endswith(('hf-casa-del-patio-v1-Volume.asset', 'hf-campamento-pinar-v2-Volume.asset',
                                     'hf-puerto-del-faro-v1-Volume.asset')):
            profile['bloom'].update({'threshold': 1.1, 'intensity': 0.45, 'scatter': 0.55})
        if profile['path'].endswith(('hf-isla-del-laguito-v2-Volume.asset', 'hf-yate-a-la-deriva-v3-Volume.asset')):
            profile['bloom'].update({'threshold': 1.1, 'intensity': 0.25, 'scatter': 0.6})

    # ---------------- Camp ----------------
    camp = by_id(c, 'hf-campamento-pinar-v2')
    camp['note'] = ('Bosque azul oscuro con fogon central (A27 A, ENV-04 Campsite): r3 suelos de tierra calida #5E4634 y pasto '
                    '#2F5A3A, faroles de poste con cristal ambar, llama y charco (luz solo al suelo), agua nocturna.')
    for index, name, color in [(4, 'CAMP_Mat_Dirt', '#7C5A24'), (5, 'CAMP_Mat_DirtDark', '#82603A'),
                               (8, 'CAMP_Mat_Grass', '#48733B'), (9, 'CAMP_Mat_GrassDark', '#3E6634'),
                               (12, 'CAMP_Mat_Lake', '#28436E'), (19, 'CAMP_Mat_Sand', '#876038'),
                               (23, 'CAMP_Mat_WaterDeep', '#1F3964'), (24, 'CAMP_Mat_WaterMid', '#25416E'),
                               (25, 'CAMP_Mat_WaterTurq', '#2C4B78'), (35, 'CAMP_Prod_ForestSoil_0', '#44703A'),
                               (36, 'CAMP_Prod_ForestSoil_1', '#3F6936'), (37, 'CAMP_Prod_ForestSoil_2', '#396131')]:
        set_material(camp, index, name, baseColor=color)
    set_material(camp, 7, 'CAMP_Mat_Foam', baseColor='#A8BCE0', emission=[0.16, 0.24, 0.42])
    rules = camp['lightRules']
    set_rule(rules, 'Environment/LGT_Fire', color=[1.0, 0.6, 0.3], intensity=3.4, range=8, shadows='Soft', shadowTier=1,
             flicker=0.32, offset=[0, 0.35, 0], flame={'height': 1.35, 'offset': [0, -1.1, 0]})
    set_rule(rules, 'Environment/LGT_Fire_Outer', color=[1.0, 0.55, 0.25], intensity=3.2, range=11, shadows='None', flicker=0.22)
    lantern = dict(color=[1.0, 0.56, 0.22], intensity=2.8, range=4.0, shadows='None', offset=[0, 0, 0], lightLayers=[4, 5],
                   halo={'size': 0.75, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'depthTolerance': 0.6},
                   flame={'height': 0.16, 'offset': [0, -0.1, 0]},
                   pool={'radius': 3.0, 'color': srgb('#7A4A24'), 'alpha': 0.9})
    set_rule(rules, 'Environment/LGT_Lantern_*', **lantern)
    jetty = dict(lantern)
    del jetty['pool']  # Over the lake: the glint is its pool.
    jetty['offset'] = [0, 0.05, 0]
    jetty['reflection'] = {'length': 6.0, 'width': 0.55, 'waterY': -0.03, 'alpha': 0.6, 'color': [1.0, 0.55, 0.15]}
    set_rule(rules, 'Environment/LGT_Jetty_Lantern', **jetty)
    set_rule(rules, 'Environment/LGT_CAMP_Shelter', color=[1.0, 0.66, 0.34], intensity=0.8, range=5, shadows='Soft', shadowTier=0,
             offset=[0, -0.35, 0])
    renderers = list(camp['renderers'])
    renderers += [
        {'match': 'Environment/CAMP_Lantern_*_Arm', 'hide': True, 'expect': 11,
         'note': 'White arm sticking out of the lantern on the far side of the post (collider kept).'},
        {'match': 'Environment/CAMP_Lantern_*_Cap', 'ignoreLocalLights': True, 'expect': 11},
        {'match': 'Environment/CAMP_Prod_Lantern_*', 'ignoreLocalLights': True,
         'note': 'Cage, cap, loop and glass stay dark iron/amber: the light sits inside the glass.'},
        {'match': 'Environment/CAMP_Prod_Lantern_*_WarmGlass', 'swapFrom': 'Color_013', 'swapTo': GEN + 'Higgsfield_LanternGlass.mat',
         'expect': 12, 'note': 'Translucent amber glass #FFB347 with the flame visible.'},
    ]
    for match in ['CAMP_Terrain_Grass', 'CAMP_Terrain_Paths', 'CAMP_Terrain_Plaza', 'CAMP_Terrain_Rim', 'CAMP_Hill_*',
                  'CAMP_Rock_*', 'CAMP_Prod_BankRock_*', 'CAMP_Stump_*', 'CAMP_Tent_*_Groundcloth']:
        renderers.append({'match': 'Environment/' + match, 'addLightLayers': [4],
                          'note': 'Receives the lantern pools (lantern lights skip pines, rails and props).'})
    camp['renderers'] = renderers
    camp['characterRim'] = rim
    camp['characterFill'] = fill

    # ---------------- Puerto ----------------
    port = by_id(c, 'hf-puerto-del-faro-v1')
    port['note'] = ('Noche azul marino (cielo #13265D a #1A2A6A, niebla #2A3570) con luces calidas; r3 pasto #33563A, empedrado '
                    'de bajo contraste, faro #FFD27A con halo y haz, agua nocturna #16305A con reflejos, techos de madera adentro.')
    for index, name, color in [(2, 'PDF05_Cobble', '#666458'), (3, 'PDF05_CobbleLight', '#6C6A5E'),
                                                              (8, 'PDF05_Ground', '#4C703F'), (9, 'PDF05_GroundLight', '#547842'),
                               (13, 'PDF05_Moss', '#3C6236'), (15, 'PDF05_Ocean', '#1A2640'), (16, 'PDF05_OceanBlue', '#1C2A46'),
                               (17, 'PDF05_OceanLight', '#263C60'), (18, 'PDF05_OceanTeal', '#1E2E4A'),
                               (19, 'PDF05_Path', '#8A6234'), (34, 'PDF05_StoneLight', '#8C8A82')]:
        set_material(port, index, name, baseColor=color)
    set_material(port, 5, 'PDF05_Foam', baseColor='#A8BCE0', emission=[0.1, 0.15, 0.28])
    # Grass tufts (spiky clumps) read black at night: a faint emission floor keeps them dark green #1E3A26.
    set_material(port, 6, 'PDF05_Grass', baseColor='#3E7A48', emission=[0.014, 0.045, 0.02])
    set_material(port, 7, 'PDF05_GrassLight', baseColor='#468450', emission=[0.014, 0.045, 0.02])
    rules = port['lightRules']
    set_rule(rules, 'Environment/Anchor_LGT_Pueblo_LighthouseLens', color=[1.0, 0.8235, 0.4784], intensity=7.0, range=18,
             shadows='None',
             halo={'size': 7.5, 'color': [1.0, 0.72, 0.3], 'alpha': 0.45, 'intensity': 1.3, 'offset': [0, -0.35, 0],
                   'depthTolerance': 3.6},
             beam={'length': 30, 'radius': 3.8, 'speed': 28, 'tilt': 3, 'count': 2, 'alpha': 0.12,
                   'color': [1.0, 0.8235, 0.4784], 'offset': [0, -0.35, 0]})
    set_rule(rules, 'Environment/Anchor_LGT_Pier_*', color=[1.0, 0.62, 0.28], intensity=4.5, range=8, shadows='None',
             halo={'size': 0.6, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'offset': [0, 0.05, 0]},
             reflection={'length': 7.0, 'width': 0.6, 'waterY': 0.12, 'alpha': 0.6, 'color': [1.0, 0.55, 0.15]})
    set_rule(rules, 'Environment/Anchor_LGT_Cottage_*', color=[1.0, 0.62, 0.28], intensity=4.0, range=7.5, shadows='None',
             lightLayers=[3, 5], halo={'size': 0.6, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'offset': [0, 0.05, 0]})
    set_rule(rules, 'Environment/Anchor_LGT_Workshop_*', color=[1.0, 0.62, 0.28], intensity=4.0, range=7.5, shadows='None',
             lightLayers=[3, 5], halo={'size': 0.6, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'offset': [0, 0.05, 0]})
    for n in ('01', '02', '03'):
        set_rule(rules, 'Environment/Anchor_LGT_Cottage_%s_InteriorFill' % n, color=[1.0, 0.72, 0.42], intensity=1.5, range=6.0,
                 shadows='Soft', shadowTier=0, lightLayers=[0], halo={'size': 0})
        set_rule(rules, 'Environment/Anchor_LGT_Cottage_%s_BedsideLamp' % n, color=[1.0, 0.62, 0.3], intensity=1.5, range=4.5,
                 lightLayers=[0])
    set_rule(rules, 'Environment/Anchor_LGT_Workshop_Interior', color=[1.0, 0.72, 0.42], intensity=1.8, range=6.0, shadows='Soft',
             shadowTier=0, lightLayers=[0], halo={'size': 0})
    set_rule(rules, 'Environment/Anchor_LGT_Workshop_BenchLamp', color=[1.0, 0.62, 0.3], intensity=1.8, range=4.5, lightLayers=[0])
    port['exteriorLightsSkipInteriors'] = True
    renderers = list(port['renderers'])
    renderers += [
        {'match': 'Environment/Lighthouse_LanternLens', 'ignoreLocalLights': True, 'expect': 1,
         'note': 'Lens shows its own #FFD27A emission; the lens light no longer washes it white.'},
        {'match': 'Environment/Lighthouse_LanternMullions', 'ignoreLocalLights': True, 'expect': 1},
        {'match': 'Environment/Plaza_ContinuousPaving', 'swapFrom': 'Color_019', 'swapTo': GEN + 'Higgsfield_PlazaJoint.mat',
         'expect': 1, 'note': 'Dark joints between the cobbles, paths keep the warm earth.'},
        {'match': 'Environment/Cottage_01_RoofPanel_*', 'swapFrom': 'Color_027', 'swapTo': GEN + 'Higgsfield_PuertoRoof.mat',
         'expect': 2, 'note': 'Wood ceiling inside, blue roof outside.'},
        {'match': 'Environment/Cottage_03_RoofPanel_*', 'swapFrom': 'Color_027', 'swapTo': GEN + 'Higgsfield_PuertoRoof.mat',
         'expect': 2},
        {'match': 'Environment/Cottage_02_RoofPanel_*', 'swapFrom': 'Color_029', 'swapTo': GEN + 'Higgsfield_PuertoRoofRed.mat',
         'expect': 2, 'note': 'Same for the red-roofed cottage.'},
        {'match': 'Environment/BoatWorkshop_RoofPanel_*', 'swapFrom': 'Color_027', 'swapTo': GEN + 'Higgsfield_PuertoRoof.mat'},
    ]
    port['renderers'] = renderers
    port['interiorAmbient'] = {'color': [0.55, 0.4, 0.24], 'intensity': 0.7, 'keep': 0.3, 'minSize': 0.5}
    port['characterRim'] = rim
    port['characterFill'] = fill

    # ---------------- Casa ----------------
    casa = by_id(c, 'hf-casa-del-patio-v1')
    casa['note'] = ('Noche azul marino (A23 C, ENV-03): r3 paredes interiores crema con zocalo de madera, rebote calido interior, '
                    'chimenea de piedra con hogar de hollin y troncos oscuros, ventanas con cuarto iluminado y cortinas.')
    rules = casa['lightRules']
    for match in ['Environment/CASA_Porch_Lamp_*', 'Environment/CASA_Rear_Lamp_*', 'Environment/CASA_Shed_Lamp_Light_Anchor']:
        set_rule(rules, match, color=[1.0, 0.62, 0.28], intensity=3.2, range=8.0, shadows='None', lightLayers=[3, 5],
                 halo={'size': 0.6, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'offset': [0, -0.01, 0.21]})
    set_rule(rules, 'Environment/CASA_Interior_*', color=[1.0, 0.66, 0.28], intensity=0.55, range=4.5, shadows='None',
             offset=[0, -0.45, 0])
    set_rule(rules, 'Environment/CASA_Fireplace_Practical_Anchor', color=[1.0, 0.58, 0.26], intensity=1.1, range=5.0, flicker=0.3,
             offset=[0, 0.05, 0.75], flame={'height': 0.8, 'offset': [-0.07, -0.38, -0.28]})
    renderers = list(casa['renderers'])
    renderers += [
        {'match': 'Environment/CASA_Fireplace_Back', 'swapFrom': 'Color_012', 'swapTo': GEN + 'Higgsfield_FireSoot.mat',
         'ignoreLocalLights': True, 'expect': 1, 'note': 'Sooty firebox #2A1E1A.'},
        {'match': 'Environment/CASA_Fireplace_Pier*', 'swapFrom': 'Color_025', 'swapTo': GEN + 'Higgsfield_FireStone.mat',
         'expect': 2, 'note': 'Stone surround #7A7470.'},
        {'match': 'Environment/CASA_Fireplace_Hearth', 'swapFrom': 'Color_024', 'swapTo': GEN + 'Higgsfield_FireStone.mat',
         'expect': 1},
        {'match': 'Environment/CASA_Fireplace_ChimneyBreast', 'swapFrom': 'Color_005', 'swapTo': GEN + 'Higgsfield_FireStone.mat',
         'expect': 1, 'note': 'Chimney breast in the same stone so it reads as part of the fireplace.'},
        {'match': 'Environment/CASA_Hearth_Log*', 'swapFrom': 'Color_028', 'swapTo': GEN + 'Higgsfield_FireLog.mat',
         'ignoreLocalLights': True, 'expect': 2, 'note': 'Dark logs #5A3420 instead of glowing cheese.'},
    ]
    casa['renderers'] = renderers
    swaps = [s for s in casa['materialSwaps'] if s['from'] != 'Color_018']
    swaps.append({'from': 'Color_018', 'to': GEN + 'Higgsfield_CasaShell.mat',
                  'note': 'CASA_Plaster walls and gables: cream inside with baseboard, warm facade outside.'})
    casa['materialSwaps'] = swaps
    casa['interiorVolumes'] = [
        {'center': [0, 1.622, 0], 'size': [13.48, 2.696, 9.52], 'note': 'House ground floor (floor top to ceiling).'},
        {'center': [0, 4.512, 0], 'size': [13.48, 2.676, 9.52], 'note': 'House upper floor.'},
    ]
    set_rule(rules, 'Environment/CASA_Bedside_*', color=[1.0, 0.64, 0.32], intensity=1.4, range=5.0,
             halo={'size': 0.6, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.35, 'offset': [0, 0.04, 0]})
    casa['interiorAmbient'] = {'color': [0.55, 0.4, 0.24], 'intensity': 1.0, 'keep': 0.3, 'minSize': 0.5}
    casa['exteriorLightsSkipInteriors'] = True
    casa['characterRim'] = rim
    casa['characterFill'] = fill

    # ---------------- Isla ----------------
    isla = by_id(c, 'hf-isla-del-laguito-v2')
    isla['note'] = ('Dia de vacaciones saturado (A25 A, ENV-04 Lake & Dock): r3 sendero de tierra #B9854A distinto de la arena, '
                    'laguito #2F8FD0 con brillos, cumulos de base plana y acantilados con bruma al fondo.')
    set_material(isla, 17, 'ISLA_Path', baseColor='#976234')
    set_material(isla, 27, 'ISLA_Sand', baseColor='#C6B47C')
    set_material(isla, 28, 'ISLA_SandLight', baseColor='#CFBD86')
    for index, name, color in [(7, 'ISLA_Lake', '#2472AC'), (8, 'ISLA_LakeDeep', '#1E64A0'), (9, 'ISLA_LakeLight', '#5FA8D0')]:
        set_material(isla, index, name, baseColor=color)
    sky = isla['sky']
    sky['horizon'] = [0.66, 0.86, 1.0]
    sky['zenith'] = [0.06, 0.36, 0.88]
    sky['exponent'] = 0.5
    sky['clouds'] = {'coverage': 0.6, 'scale': 1.0, 'speed': 0.004, 'opacity': 0.97, 'color': [1, 1, 1],
                     'shade': [0.8, 0.86, 0.96], 'style': 'cumulus', 'width': 20, 'baseMin': 3, 'baseMax': 12}
    sky['ridge'] = [0.36, 0.47, 0.58]
    sky['ridgeAlpha'] = 0.85
    sky['ridgeHeight'] = 3.2
    isla['fog'] = {'enabled': True, 'mode': 'Linear', 'start': 45, 'end': 220, 'density': 0.01}

    # ---------------- Yate ----------------
    yate = by_id(c, 'hf-yate-a-la-deriva-v3')
    yate['note'] = ('Dia vacacional saturado (A28 A, ENV-04): r3 cocina sin quemar (techo <= #E8DCC8), ojos de buey con mar y '
                    'cielo, lampara del camarote encendida #FFB347.')
    ysky = yate['sky']
    ysky['clouds'] = {'coverage': 0.6, 'scale': 1.0, 'speed': 0.004, 'opacity': 0.97, 'color': [1, 1, 1],
                      'shade': [0.8, 0.86, 0.96], 'style': 'cumulus', 'width': 20, 'baseMin': 3, 'baseMax': 12}
    rules = yate['lightRules']
    set_rule(rules, 'Environment/YATE_Anchor_Practical_SalonAft', intensity=1.4, shadows='Soft', shadowTier=1)
    set_rule(rules, 'Environment/YATE_Anchor_Practical_Galley', intensity=1.3, shadows='Soft', shadowTier=1, offset=[0, -0.35, 0])
    for side, x in (('Port', -0.45), ('Starboard', 0.45)):
        set_rule(rules, 'Environment/YATE_Anchor_Practical_Cabin' + side, color=[1.0, 0.64, 0.32], intensity=1.2, range=4.5,
                 shadows='None', offset=[x, -1.1, 1.2],
                 halo={'size': 0.35, 'color': [1.0, 0.702, 0.2784], 'alpha': 0.3, 'offset': [x, -1.4, 1.2]})
    renderers = list(yate['renderers'])
    renderers += [
        {'match': 'Environment/YATE_Porthole_*', 'swapFrom': 'Color_005', 'swapTo': GEN + 'Higgsfield_PortholeView.mat',
         'expect': 8, 'note': 'Sea and sky through the portholes instead of a flat gray pane.'},
        {'match': 'Environment/YATE_Cabin_*_NightstandLamp', 'swapFrom': 'Color_006', 'swapTo': GEN + 'Higgsfield_LampShadeGlowDay.mat',
         'ignoreLocalLights': True, 'expect': 2, 'note': 'Cabin lamps switched on (#FFB347 shade, the lamp light sits inside it).'},
    ]
    yate['renderers'] = renderers

    # Sky fill: until r3 the UI customization key (directional, 1.5, Euler 30/150/0, meant for the preview layer) lit
    # every map because Forward+ ignores light culling masks. The binding now suppresses it and owns the same fill on
    # purpose; in Casa and Puerto it only lights open-air renderers and characters (not the shells of rooms), so rooms stop burning.
    for m in c['maps']:
        m['skyFill'] = {'color': [1.0, 1.0, 1.0], 'intensity': 1.5, 'eulerDegrees': [30, 150, 0]}
    casa['skyFill']['lightLayers'] = [2, 5]
    port['skyFill']['lightLayers'] = [2, 5]

    # Night skies: warm cream moon disc (#FFF3C8 in UI-06).
    for night in (camp, port, casa):
        night['sky']['disc'] = [1.88, 1.52, 0.93]  # displays ~#FFF3C8 (SetColor converts sRGB to linear)
    return c


if __name__ == '__main__':
    config = build()
    text = dump(config) + '\n'
    if '--check' in sys.argv:
        print('ok', len(text.splitlines()), 'lines')
    else:
        open(CONFIG, 'w', encoding='utf-8', newline='\n').write(text)
        print('wrote', CONFIG)
