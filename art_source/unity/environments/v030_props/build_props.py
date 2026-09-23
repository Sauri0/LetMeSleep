"""Let me sleep v0.3.0 - build the faceted low-poly prop library (Blender 5.2 LTS).

  N:/Blender/blender.exe --background --factory-startup --python build_props.py -- [--only A,B --out DIR]

Without --only everything is written beside this script:
  Prop_<Name>.fbx   one FBX per prop (single mesh, flat normals, no colliders/lights/cameras)
  v030_props.blend  editable source; props laid out on a grid (object location is layout only,
                    each mesh keeps its own base-centred pivot)
  manifest.json     dimensions, triangles, materials (sRGB + linear), emission, validation
The geometry itself is authored in props_catalog.py (helpers in props_lib.py).
Renders/validation images are produced by render_props.py and compose_sheet.py.
"""
import argparse
import hashlib
import json
import math
import re
import sys
import time
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from props_lib import lin  # noqa: E402
from props_catalog import REGISTRY  # noqa: E402

TRI_MIN, TRI_MAX = 50, 1500
NAME_RE = re.compile(r'^Prop_[A-Z][A-Za-z0-9]+_[A-Z][A-Za-z0-9]*(_Emissive)?$')
EXPORT = dict(use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
              apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
              mesh_smooth_type='FACE', use_tspace=False, use_triangles=False, add_leaf_bones=False,
              bake_anim=False, path_mode='AUTO', embed_textures=False, use_custom_props=False)
CATEGORY_ORDER = ['item', 'bedroom', 'interior', 'kitchen', 'exterior', 'camp', 'water', 'nature']


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def r4(v):
    return round(float(v), 4)


def make_material(prop, part):
    spec = prop.mats[part]
    name = prop.material_name(part)
    m = bpy.data.materials.new(name)
    c = lin(spec['hex'])
    m.diffuse_color = (*c, 1.0)
    m.roughness = spec['roughness']
    m.metallic = spec['metallic']
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*c, 1.0)
    bsdf.inputs['Roughness'].default_value = spec['roughness']
    bsdf.inputs['Metallic'].default_value = spec['metallic']
    if spec['emission'] > 0:
        bsdf.inputs['Emission Color'].default_value = (*c, 1.0)
        bsdf.inputs['Emission Strength'].default_value = spec['emission']
    m.use_backface_culling = True
    return m


def polygon_area(me, poly):
    vs = [me.vertices[i].co for i in poly.vertices]
    return (vs[1] - vs[0]).cross(vs[2] - vs[0]).length / 2


def validate(ob, prop):
    me = ob.data
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    tris = len(me.polygons)
    used = set(p.material_index for p in me.polygons)
    used_verts = set(i for p in me.polygons for i in p.vertices)
    names = [m.name for m in me.materials]
    emissive_ok = all((prop.mats[part]['emission'] > 0) == prop.material_name(part).endswith('_Emissive')
                      for part in prop.order)
    checks = {
        'triangles_in_budget': TRI_MIN <= tris <= TRI_MAX,
        'all_triangles': all(len(p.vertices) == 3 for p in me.polygons),
        'pivot_base_centered': abs(min(zs)) < 1e-5 and abs((min(xs) + max(xs)) / 2) < 1e-5 and abs((min(ys) + max(ys)) / 2) < 1e-5,
        'flat_shaded': not any(p.use_smooth for p in me.polygons),
        'material_names': all(NAME_RE.match(n) for n in names) and len(set(names)) == len(names),
        'emissive_suffix_consistent': emissive_ok,
        'every_material_used': used == set(range(len(names))),
        'no_loose_vertices': len(used_verts) == len(me.vertices),
        'no_degenerate_triangles': sum(1 for p in me.polygons if polygon_area(me, p) < 1e-10) == 0,
        'identity_transform': ob.location.length == 0 and tuple(ob.rotation_euler) == (0, 0, 0) and tuple(ob.scale) == (1, 1, 1),
    }
    dims = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    return checks, dims, tris, len(me.vertices)


def material_entries(prop, ob):
    out = []
    for slot, part in enumerate(prop.order):
        spec = prop.mats[part]
        out.append({
            'slot': slot,
            'name': prop.material_name(part),
            'part': part,
            'color_srgb': spec['hex'].upper(),
            'base_color_linear': [r4(c) for c in lin(spec['hex'])],
            'emissive': spec['emission'] > 0,
            'emission_strength': spec['emission'],
            'roughness': spec['roughness'],
            'metallic': spec['metallic'],
        })
    return out


def to_unity(v):
    """Blender (x, y, z) -> Unity local for the FBX settings above (-Z forward, Y up)
    plus Unity's right- to left-handed import: (-x, z, -y). Front (-Y) becomes +Z."""
    return [r4(-v[0]), r4(v[2]), r4(-v[1])]


def anchor_entries(prop):
    out = {}
    off = prop.pivot_offset
    for name, a in prop.anchors.items():
        c = [a['center'][i] - off[i] for i in range(3)]
        n = a['normal']
        e = {
            'blender_m': [r4(x) for x in c],
            'unity_local_m': to_unity(c),
            'normal_blender': [r4(x) for x in n],
            'normal_unity': to_unity(n),
        }
        if a.get('size'):
            e['size_m'] = [r4(x) for x in a['size']]
        if a.get('note'):
            e['note'] = a['note']
        out[name] = e
    return out


def derive_menu_layout(entries):
    """Bed.menu_layout -> positions of the whole UI-06 set (nightstand, lamp, clock, lights, camera) in bed-local
    Blender metres plus Unity-local copies, so the Unity menu scene can reproduce vignette_menu.png."""
    by = {e['name']: e for e in entries}
    if not all(n in by for n in ('Bed', 'Nightstand', 'TableLamp', 'AlarmClock')) or 'menu_layout' not in by['Bed']:
        return
    lay = by['Bed']['menu_layout']
    bd, nd = by['Bed']['dimensions_m'], by['Nightstand']['dimensions_m']

    def anc(name, a):
        return by[name]['anchors'][a]['blender_m']

    def add(a, b):
        return [a[i] + b[i] for i in range(3)]
    y_wall = bd['y'] / 2 + lay['wall_gap_m']
    ns = [bd['x'] / 2 + lay['nightstand_gap_m'] + nd['x'] / 2, y_wall - nd['y'] / 2 - 0.01, 0.0]
    lamp = add(ns, anc('Nightstand', 'lamp'))
    clock = add(ns, anc('Nightstand', 'clock'))
    light = add(lamp, anc('TableLamp', 'light'))
    head = anc('Bed', 'sleeper_head')
    cam = lay['camera']
    to_cam = [cam['location'][i] - clock[i] for i in range(3)]
    yaw = math.degrees(math.atan2(to_cam[0], -to_cam[1]))
    fill = add(add(head, [0.0, -0.035, 0.115]), lay['sleeper_fill']['offset_from_head_m'])
    res = lay['resolution']
    vfov = math.degrees(2 * math.atan(cam['sensor_width_mm'] / 2 * res[1] / res[0] / cam['lens_mm']))
    hfov = math.degrees(2 * math.atan(cam['sensor_width_mm'] / 2 / cam['lens_mm']))
    pts = {'back_wall_plane_y': y_wall, 'nightstand': ns, 'table_lamp': lamp, 'alarm_clock': clock,
           'lamp_light': light, 'sleeper_head': head, 'sleeper_fill_light': fill,
           'camera': cam['location'], 'camera_look_at': cam['look_at']}
    lay['derived_blender_m'] = {k: (r4(v) if isinstance(v, float) else [r4(x) for x in v]) for k, v in pts.items()}
    lay['derived_blender_m']['alarm_clock_yaw_deg'] = r4(yaw)
    lay['unity'] = {
        'note': 'Local de la cama en Unity (bed pivot = origen, cabecera hacia -Z de Unity porque el frente del FBX '
                'mira a +Z): posición = (-x, z, -y) de Blender. Rotación del reloj alrededor de Y = -yaw de Blender. '
                'Pared del fondo: plano z = -back_wall_z. Verificar una vez en Unity contra el FBX importado.',
        'nightstand': to_unity(ns), 'table_lamp': to_unity(lamp), 'alarm_clock': to_unity(clock),
        'alarm_clock_yaw_deg': r4(-yaw), 'lamp_light': to_unity(light), 'sleeper_head': to_unity(head),
        'sleeper_fill_light': to_unity(fill), 'back_wall_z': r4(-y_wall),
        'camera_position': to_unity(cam['location']), 'camera_look_at': to_unity(cam['look_at']),
        'camera_vertical_fov_deg': r4(vfov), 'camera_horizontal_fov_deg': r4(hfov), 'aspect': '%d:%d' % (16, 9),
        'moon_direction': to_unity(lay['moon']['direction']),
        'lights': {
            'lamp': 'Point #FFB347-ish (lineal %s), sin sombras; renderingLayerMask sin la capa del velador.'
                    % lay['lamp']['color_rgb_linear'],
            'moon': 'Directional %s baja (strength Blender %.1f), desde el frente-izquierda y arriba.'
                    % (lay['moon']['color_srgb'], lay['moon']['strength']),
            'sleeper_fill': 'Point cálido solo para la capa del personaje (Rendering Layers), sin sombras.',
        },
    }


def export_fbx(ob, path):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    keep = ob.location.copy()
    ob.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=str(path), **EXPORT)
    ob.location = keep


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default='')
    ap.add_argument('--out', default='')
    args = ap.parse_args(argv)
    only = [s.strip() for s in args.only.split(',') if s.strip()]
    if only and not args.out:
        raise SystemExit('--only requires --out (partial builds never overwrite the committed library)')
    out = Path(args.out) if args.out else HERE
    out.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    root = bpy.data.collections.new('LMS_v030_Props')
    scene.collection.children.link(root)
    cats = {}

    entries = []
    t0 = time.time()
    built = []
    for fn in REGISTRY:
        prop = fn()
        if only and prop.name not in only:
            continue
        if prop.category not in cats:
            cats[prop.category] = bpy.data.collections.new('Props_' + prop.category)
            root.children.link(cats[prop.category])
        ob = prop.build(cats[prop.category], make_material)
        checks, dims, tris, nverts = validate(ob, prop)
        fbx = out / ('Prop_%s.fbx' % prop.name)
        export_fbx(ob, fbx)
        entry = {
            'name': prop.name,
            'asset': 'Prop_' + prop.name,
            'fbx': fbx.name,
            'fbx_sha256': sha256(fbx),
            'display_name_es': prop.es,
            'category': prop.category,
            'mount': prop.mount,
            'dimensions_m': {'x': r4(dims[0]), 'y': r4(dims[1]), 'z': r4(dims[2])},
            'unity_size_m': {'x': r4(dims[0]), 'y': r4(dims[2]), 'z': r4(dims[1])},
            'triangles': tris,
            'vertices': nverts,
            'materials': material_entries(prop, ob),
            'notes': prop.notes,
            'preview': prop.preview,
            'validation': checks,
            'passed': all(checks.values()),
        }
        if prop.anchors:
            entry['anchors'] = anchor_entries(prop)
        if prop.extra:
            entry.update(prop.extra)
        entries.append(entry)
        built.append((prop, ob, dims))
        print('LMS_PROP %-18s tris=%4d mats=%2d dims=%.3f x %.3f x %.3f %s' % (
            prop.name, tris, len(prop.order), dims[0], dims[1], dims[2], 'OK' if entry['passed'] else 'FAIL ' + str(
                [k for k, v in checks.items() if not v])))

    derive_menu_layout(entries)

    # layout on a grid by category (location only; meshes keep their pivots)
    built.sort(key=lambda b: (CATEGORY_ORDER.index(b[0].category) if b[0].category in CATEGORY_ORDER else 99))
    y = 0.0
    for cat in CATEGORY_ORDER:
        row = [b for b in built if b[0].category == cat]
        if not row:
            continue
        x = 0.0
        depth = 0.0
        for prop, ob, dims in row:
            ob.location = (x + dims[0] / 2, y, 0.0)
            x += dims[0] + 0.6
            depth = max(depth, dims[1])
        y -= depth + 1.0

    manifest = {
        'schema': 'lms.props.v030/1',
        'project': 'Let me sleep',
        'generator': 'art_source/unity/environments/v030_props/build_props.py',
        'sources': {name: sha256(HERE / name) for name in ('build_props.py', 'props_catalog.py', 'props_lib.py')},
        'blender': bpy.app.version_string,
        'units': 'metres (1 Blender unit = 1 m)',
        'axes_source': 'Blender Z up, front of each prop toward -Y',
        'axes_unity': 'Mismo convenio que los personajes (build_characters.py, FBX -Z forward / Y up): '
                      'posición Unity local = (-x, z, -y) de Blender; el frente (-Y en Blender) mira a +Z en Unity. '
                      'Los anchors traen ambas versiones; verificar una vez contra el FBX importado.',
        'pivot': 'base centre: XY bounding-box centre, lowest vertex at Z = 0',
        'reference_human_height_m': 1.72,
        'triangle_budget': [TRI_MIN, TRI_MAX],
        'fbx_export': {k: (sorted(v) if isinstance(v, set) else v) for k, v in EXPORT.items() if k != 'use_selection'},
        'unity_import_recommendation': {
            'globalScale': 1.0, 'useFileScale': True, 'bakeAxisConversion': True,
            'addCollider': False, 'importCameras': False, 'importLights': False, 'importBlendShapes': False,
            'importAnimation': False, 'animationType': 'None', 'materialImportMode': 'None',
            'materials_note': 'Crear URP/Lit de color plano por slot desde este manifiesto (color_srgb como _BaseColor; '
                              'los *_Emissive con _EmissionColor = color_srgb * emission_strength y keyword _EMISSION).',
            'decor_note': 'Solo decorativos: sin Collider/Rigidbody bajo MapRoot (ver docs/v030/MAPA-SISTEMAS.md, maps).',
        },
        'rules': ['Let me sleep (nunca Bite & Build)', 'sin armas', 'sin texto en carteles', 'low-poly facetado, color plano',
                  'sin colliders', 'un FBX por prop', 'chaflán de 1 segmento (1-2 cm) en cantos de madera y cajas'],
        'unity_verification_pending': [
            'Conversión de anchors (-x, z, -y) contra un FBX importado (p. ej. Nightstand.lamp y AlarmClock.screen).',
            'Emisión URP (_EMISSION + _EmissionColor = color_srgb x emission_strength) de los 6 props emisivos: '
            'HandLantern, DockLampPost, TableLamp, AlarmClock, Window, Campfire.',
            'Rendering Layer propio del velador (TableLamp) excluido de su luz.',
        ],
        'color_note': 'color_srgb es la autoridad de paleta; base_color_linear es el valor que usa Blender.',
        'props': entries,
        'totals': {
            'props': len(entries),
            'triangles': sum(e['triangles'] for e in entries),
            'materials': sum(len(e['materials']) for e in entries),
            'emissive_props': [e['name'] for e in entries if any(m['emissive'] for m in e['materials'])],
        },
        'passed': all(e['passed'] for e in entries),
    }
    name = 'manifest.json' if not only else 'manifest_partial.json'
    (out / name).write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')

    txt = bpy.data.texts.new('LMS_README')
    txt.write(__doc__ + '\nProps: %d. Generated with Blender %s.\n' % (len(entries), bpy.app.version_string))
    blend = out / ('v030_props.blend' if not only else 'v030_props_partial.blend')
    bpy.context.preferences.filepaths.save_version = 0      # no .blend1 backups beside the committed source
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    print('LMS_PROPS_DONE props=%d tris=%d passed=%s seconds=%.1f' % (
        len(entries), manifest['totals']['triangles'], manifest['passed'], time.time() - t0))
    if not manifest['passed']:
        sys.exit(2)


if __name__ == '__main__':
    main()
