"""Let me sleep v0.3.0 - validation renders + FBX round trip for the prop library.

  N:/Blender/blender.exe --background --factory-startup --python render_props.py -- --out <dir> [--src <dir>] [--only A,B]

Reads manifest.json from --src (default: this folder), IMPORTS each Prop_<Name>.fbx (so the
renders show what Unity receives), re-applies the manifest materials (flat colour + emission,
back-face culling on, like URP/Lit) and writes into --out:
  individual/Prop_<Name>.png   3/4 view, slate ground/background, warm key + cool fill + rim
  lineup_<group>.png (+ .json) real-scale rows next to a 1.72 m human proxy (labels are drawn
                               by compose_sheet.py from the json)
  fbx_roundtrip.json           per FBX: object count/types, triangles, dimensions, pivot and
                               material names compared with the manifest
  vignette_menu.png            night bedroom (UI-06): bed, nightstand, lamp + clock on their anchors,
                               window, rug and plant, lit only by moonlight and the lamp
Nothing is written into the source folder.
"""
import argparse
import json
import math
import re
import sys
import time
from pathlib import Path

import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from props_lib import lin  # noqa: E402

SLATE = '#3B4863'
LINEUPS = {
    'estructuras': ['DockLampPost', 'Signpost', 'Mailbox', 'FenceSegment', 'Bookshelf', 'PicnicTable', 'FoldingChair'],
    'campamento_agua': ['Rowboat', 'LogBench', 'Campfire', 'Firewood', 'Reeds', 'LilyPads'],
    'naturaleza': ['Bush', 'RockCluster', 'TreeStump', 'Fern', 'FlowersWhite', 'FlowersYellow', 'Mushrooms'],
    'interior': ['Crate', 'Chest', 'Barrel', 'PottedPlant', 'HangingPlant', 'PaintingLandscape', 'HangingPans',
                 'RugRedStriped', 'RugBlue'],
    'objetos': ['HandLantern', 'Mug', 'Toolbox', 'Medkit', 'RolledMap', 'SoccerBall'],
    'dormitorio': ['Bed', 'Nightstand', 'TableLamp', 'AlarmClock', 'Window'],
}


def mat_simple(name, hexcol, rough=0.85, emission=0.0):
    m = bpy.data.materials.new(name)
    c = lin(hexcol)
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*c, 1)
    b.inputs['Roughness'].default_value = rough
    if emission:
        b.inputs['Emission Color'].default_value = (*c, 1)
        b.inputs['Emission Strength'].default_value = emission
    m.use_backface_culling = True
    return m


def setup_scene(res=768):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.eevee.taa_render_samples = 48
    for attr, val in (('use_shadows', True), ('use_fast_gi', True), ('use_raytracing', False)):
        if hasattr(sc.eevee, attr):
            setattr(sc.eevee, attr, val)
    sc.render.resolution_x = res
    sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    world = bpy.data.worlds.new('LMS_Slate')
    sc.world = world
    bg = world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value = (*lin('#46557A'), 1)
    bg.inputs['Strength'].default_value = 0.9

    def sun(name, color, energy, rot, angle):
        d = bpy.data.lights.new(name, 'SUN')
        d.color = color
        d.energy = energy
        d.angle = math.radians(angle)
        d.use_shadow = True
        o = bpy.data.objects.new(name, d)
        o.rotation_euler = [math.radians(a) for a in rot]
        sc.collection.objects.link(o)
        return o
    sun('Key', (1.0, 0.84, 0.64), 3.4, (52, 0, -38), 6)
    sun('Fill', (0.62, 0.72, 1.0), 0.7, (60, 0, 70), 20)
    sun('Rim', (1.0, 0.9, 0.78), 1.6, (60, 0, 170), 10)
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0))
    ground = bpy.context.object
    ground.name = 'Ground'
    ground.data.materials.append(mat_simple('Ground', SLATE, 1.0))
    cam_data = bpy.data.cameras.new('Cam')
    cam_data.lens = 50
    cam = bpy.data.objects.new('Cam', cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    setup_glare(sc)
    return sc, cam, ground


def setup_glare(sc):
    try:
        tree = bpy.data.node_groups.new('LMS_Comp', 'CompositorNodeTree')
        tree.interface.new_socket('Image', in_out='OUTPUT', socket_type='NodeSocketColor')
        rl = tree.nodes.new('CompositorNodeRLayers')
        gl = tree.nodes.new('CompositorNodeGlare')
        out = tree.nodes.new('NodeGroupOutput')
        for v in ('Fog Glow', 'FOG_GLOW', 'Bloom', 'BLOOM'):
            try:
                gl.inputs['Type'].default_value = v
                break
            except Exception:
                continue
        gl.inputs['Threshold'].default_value = 1.0
        gl.inputs['Strength'].default_value = 0.55
        if 'Size' in gl.inputs:
            try:
                gl.inputs['Size'].default_value = 0.6
            except Exception:
                pass
        tree.links.new(rl.outputs['Image'], gl.inputs['Image'])
        tree.links.new(gl.outputs['Image'], out.inputs[0])
        sc.compositing_node_group = tree
        sc.render.use_compositing = True
        print('LMS_GLARE on type=%s' % gl.inputs['Type'].default_value)
        return True
    except Exception as e:  # compositor API differs between versions: renders stay valid without glow
        print('LMS_GLARE_UNAVAILABLE', e)
        return False


def import_fbx(path):
    before_o = set(bpy.data.objects)
    before_m = set(bpy.data.materials)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objs = [o for o in bpy.data.objects if o not in before_o]
    mats = [m for m in bpy.data.materials if m not in before_m]
    return objs, mats


def world_corners(objs):
    pts = []
    for o in objs:
        if o.type == 'MESH':
            pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    return pts


def apply_manifest_materials(objs, entry):
    specs = {m['name']: m for m in entry['materials']}
    names = []
    for o in objs:
        for slot in getattr(o, 'material_slots', []):
            m = slot.material
            if m is None:
                names.append(None)
                continue
            base = re.sub(r'\.\d{3}$', '', m.name)
            names.append(base)
            spec = specs.get(base)
            if not spec:
                continue
            b = m.node_tree.nodes.get('Principled BSDF')
            c = lin(spec['color_srgb'])
            # Preview only: emissive parts get a dark base and a clamped strength so the
            # Standard view transform keeps their hue (the manifest strength is for Unity HDR).
            k = 0.0 if spec['emissive'] else 1.0
            b.inputs['Base Color'].default_value = (c[0] * k, c[1] * k, c[2] * k, 1)
            b.inputs['Roughness'].default_value = spec['roughness']
            b.inputs['Metallic'].default_value = spec['metallic']
            b.inputs['Emission Color'].default_value = (*c, 1)
            b.inputs['Emission Strength'].default_value = min(spec['emission_strength'], 1.25)
            m.use_backface_culling = True
    return names


def roundtrip(objs, entry):
    meshes = [o for o in objs if o.type == 'MESH']
    pts = world_corners(objs)
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    dims = mx - mn
    tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
    want = entry['dimensions_m']
    dim_err = max(abs(dims.x - want['x']), abs(dims.y - want['y']), abs(dims.z - want['z']))
    names = [re.sub(r'\.\d{3}$', '', s.material.name) for o in meshes for s in o.material_slots if s.material]
    res = {
        'objects': len(objs),
        'types': sorted(set(o.type for o in objs)),
        'mesh_objects': len(meshes),
        'triangles': tris,
        'triangles_manifest': entry['triangles'],
        'dimensions_m': [round(dims.x, 4), round(dims.y, 4), round(dims.z, 4)],
        'max_dimension_error_m': round(dim_err, 6),
        'min_z': round(mn.z, 6),
        'centre_xy': [round((mn.x + mx.x) / 2, 6), round((mn.y + mx.y) / 2, 6)],
        'materials': names,
        'flat_shaded': all(not p.use_smooth for o in meshes for p in o.data.polygons),
    }
    res['passed'] = (len(objs) == 1 and len(meshes) == 1 and tris == entry['triangles'] and dim_err < 1e-3 and
                     abs(mn.z) < 1e-3 and max(abs(c) for c in res['centre_xy']) < 1e-3 and
                     names == [m['name'] for m in entry['materials']])
    return res


def cam_basis(yaw, elev):
    d = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(elev)),
                -math.cos(math.radians(yaw)) * math.cos(math.radians(elev)), math.sin(math.radians(elev))))
    fwd = -d
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd).normalized()
    return d, fwd, right, up


def frame_camera(cam, corners, yaw=35.0, elev=24.0, fill=0.8, aspect=1.0):
    """aspect = height / width of the render (the lens angle applies to the wider side)."""
    d, fwd, right, up = cam_basis(yaw, elev)
    tanh = math.tan(cam.data.angle / 2) * fill
    tanv = tanh * aspect
    center = sum(corners, Vector()) / len(corners)
    for _ in range(3):
        dist = 0.0
        for c in corners:
            rel = c - center
            z = rel.dot(fwd)
            dist = max(dist, abs(rel.dot(right)) / tanh - z, abs(rel.dot(up)) / tanv - z)
        xs = [(c - center).dot(right) / (dist + (c - center).dot(fwd)) for c in corners]
        ys = [(c - center).dot(up) / (dist + (c - center).dot(fwd)) for c in corners]
        center = center + right * ((min(xs) + max(xs)) / 2 * dist) + up * ((min(ys) + max(ys)) / 2 * dist)
    cam.location = center + d * dist
    cam.rotation_euler = fwd.to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_start = max(0.001, dist * 0.01)
    cam.data.clip_end = dist * 20 + 50


def remove(objs, mats):
    for o in objs:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and getattr(data, 'users', 1) == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
    for m in mats:
        if m.users == 0:
            bpy.data.materials.remove(m)


def human_proxy(x, y=0.0):
    """1.72 m human reference in the default outfit: cream tee, blue pyjama trousers,
    slippers and red nightcap (render-only proxy, never exported)."""
    col = bpy.data.collections.new('HumanProxy')
    bpy.context.scene.collection.children.link(col)
    m_pj = mat_simple('Proxy_Pajama', '#2D4F9A')
    m_sh = mat_simple('Proxy_Shirt', '#E8DCC5')
    m_sk = mat_simple('Proxy_Skin', '#C98B5A')
    m_sl = mat_simple('Proxy_Slipper', '#6F86B8')
    m_wh = mat_simple('Proxy_White', '#F4F4F0')
    m_cap = mat_simple('Proxy_Nightcap', '#C8322E')

    def add(op, mat, **kw):
        op(**kw)
        o = bpy.context.object
        o.data.materials.append(mat)
        for c in list(o.users_collection):
            c.objects.unlink(o)
        col.objects.link(o)
        return o
    for s in (-1, 1):
        add(bpy.ops.mesh.primitive_cube_add, m_sl, size=1, location=(x + s * 0.1, y - 0.03, 0.035), scale=(0.11, 0.27, 0.07))
        add(bpy.ops.mesh.primitive_cylinder_add, m_pj, vertices=8, radius=0.078, depth=0.76, location=(x + s * 0.1, y, 0.45))
        add(bpy.ops.mesh.primitive_cylinder_add, m_pj, vertices=8, radius=0.055, depth=0.58,
            location=(x + s * 0.27, y, 1.08), rotation=(0, s * math.radians(-8), 0))
        add(bpy.ops.mesh.primitive_ico_sphere_add, m_sk, subdivisions=1, radius=0.06, location=(x + s * 0.3, y, 0.76))
    add(bpy.ops.mesh.primitive_cone_add, m_sh, vertices=8, radius1=0.2, radius2=0.17, depth=0.62, location=(x, y, 1.12))
    add(bpy.ops.mesh.primitive_cylinder_add, m_sk, vertices=8, radius=0.05, depth=0.08, location=(x, y, 1.46))
    add(bpy.ops.mesh.primitive_ico_sphere_add, m_sk, subdivisions=2, radius=0.13, location=(x, y, 1.59))
    add(bpy.ops.mesh.primitive_cone_add, m_cap, vertices=8, radius1=0.125, radius2=0.0, depth=0.26,
        location=(x + 0.03, y, 1.79), rotation=(0, math.radians(18), 0))
    add(bpy.ops.mesh.primitive_ico_sphere_add, m_wh, subdivisions=1, radius=0.035, location=(x + 0.075, y, 1.91))
    return col


def backdrop(corners):
    """Slate wall behind wall-mounted props (their back faces +Y)."""
    ymax = max(p.y for p in corners)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, ymax + 0.003, 0), rotation=(math.radians(90), 0, 0))
    w = bpy.context.object
    w.name = 'Wall'
    w.data.materials.append(mat_simple('Wall', '#56648A', 1.0))
    return w


def place(src, e, loc=(0, 0, 0), yaw=0.0):
    objs, mats = import_fbx(src / e['fbx'])
    apply_manifest_materials(objs, e)
    for o in objs:
        o.rotation_euler.z += math.radians(yaw)
        o.location = Vector(loc)
    return objs


def anchor(e, name):
    return Vector(e['anchors'][name]['blender_m'])


def clock_text(clock, loc, yaw):
    """Render-only '03:27' on the AlarmClock 'screen' anchor (shows Unity where the TMP text goes)."""
    a = clock['anchors']['screen']
    n = Vector(a['normal_blender']).normalized()
    up = Vector((0, 0, 1)) - n * n.z
    up.normalize()
    right = up.cross(n)
    from mathutils import Matrix
    rot = Matrix((right, up, n)).transposed().to_4x4()
    rz = Matrix.Rotation(math.radians(yaw), 4, 'Z')
    cu = bpy.data.curves.new('ClockText', 'FONT')
    cu.body = '03:27'
    cu.size = 0.042
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.extrude = 0.0005
    ob = bpy.data.objects.new('ClockText', cu)
    ob.matrix_world = Matrix.Translation(loc + rz @ (Vector(a['blender_m']) + n * 0.001)) @ rz @ rot
    ob.data.materials.append(mat_simple('ClockDigits', '#FF3B30', 0.5, emission=1.25))
    bpy.context.scene.collection.objects.link(ob)
    return ob


def menu_vignette(src, out, by_name):
    """UI-06 bedroom at night assembled from the library (anchors place lamp and clock)."""
    need = ['Bed', 'Nightstand', 'TableLamp', 'AlarmClock', 'Window', 'RugRedStriped', 'PottedPlant']
    if any(n not in by_name for n in need):
        return
    sc, cam, ground = setup_scene(768)
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = (*lin('#1B2442'), 1)
    sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.45
    for name, col, energy in (('Key', (0.55, 0.66, 1.0), 0.55), ('Fill', (0.45, 0.55, 1.0), 0.2), ('Rim', (0.6, 0.7, 1.0), 0.35)):
        o = bpy.data.objects[name]
        o.data.color = col
        o.data.energy = energy
    ground.data.materials[0] = mat_simple('Floor', '#5A3A24', 0.9)
    bed, ns, lamp, clock, win = (by_name[n] for n in need[:5])
    bd = bed['dimensions_m']
    y_wall = 1.6
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, y_wall, 1.4), rotation=(math.radians(90), 0, 0))
    wall = bpy.context.object
    wall.scale = (8, 2.8, 1)
    wall.data.materials.append(mat_simple('WallBack', '#2A3560', 0.95))
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, y_wall - 0.004, 0.45), rotation=(math.radians(90), 0, 0))
    wains = bpy.context.object
    wains.scale = (8, 0.9, 1)
    wains.data.materials.append(mat_simple('Wainscot', '#8B5A2B', 0.9))
    y_bed = y_wall - bd['y'] / 2 - 0.01
    placed = []
    placed += place(src, bed, (0, y_bed, 0))
    nd = ns['dimensions_m']
    ns_loc = Vector((bd['x'] / 2 + 0.08 + nd['x'] / 2, y_wall - nd['y'] / 2 - 0.03, 0))
    placed += place(src, ns, ns_loc)
    lamp_loc = ns_loc + anchor(ns, 'lamp')
    placed += place(src, lamp, lamp_loc)
    clock_loc = ns_loc + anchor(ns, 'clock')
    placed += place(src, clock, clock_loc, yaw=-24)
    clock_text(clock, clock_loc, -24)
    wd = win['dimensions_m']
    placed += place(src, win, (-0.55, y_wall - wd['y'] / 2 - 0.004, 1.2))
    rug = by_name['RugRedStriped']
    placed += place(src, rug, (0.1, y_bed - bd['y'] / 2 - 0.55, 0), yaw=4)
    plant = by_name['PottedPlant']
    placed += place(src, plant, (-bd['x'] / 2 - 0.55, y_wall - 0.3, 0))
    ld = bpy.data.lights.new('LampLight', 'POINT')
    ld.color = (1.0, 0.7, 0.38)
    ld.energy = 170.0
    ld.shadow_soft_size = 0.08
    ld.use_shadow = False      # the shade is a closed emissive mesh; Unity uses the same light without shadows
    lo = bpy.data.objects.new('LampLight', ld)
    lo.location = lamp_loc + anchor(lamp, 'light')
    sc.collection.objects.link(lo)
    bpy.context.view_layer.update()
    # frame the head of the bed, the nightstand group and the window (UI-06 composition)
    focus = [o for o in placed if o.type == 'MESH' and not o.name.startswith(('Prop_RugRedStriped', 'Prop_PottedPlant'))]
    corners = [c for c in world_corners(focus) if c.y > y_bed - 0.6]
    cam.data.lens = 35
    frame_camera(cam, corners, yaw=-30.0, elev=15.0, fill=0.9, aspect=sc.render.resolution_y / sc.render.resolution_x)
    render(sc, out / 'vignette_menu.png')
    print('LMS_VIGNETTE menu')


def render(sc, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    sc.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', required=True)
    ap.add_argument('--src', default=str(HERE))
    ap.add_argument('--only', default='')
    ap.add_argument('--manifest', default='manifest.json')
    ap.add_argument('--no-lineups', action='store_true')
    args = ap.parse_args(argv)
    src, out = Path(args.src), Path(args.out)
    manifest = json.loads((src / args.manifest).read_text(encoding='utf-8'))
    only = [s for s in args.only.split(',') if s]
    entries = [e for e in manifest['props'] if not only or e['name'] in only]
    t0 = time.time()

    sc, cam, ground = setup_scene(768)
    results = {}
    for e in entries:
        objs, mats = import_fbx(src / e['fbx'])
        results[e['name']] = roundtrip(objs, e)
        apply_manifest_materials(objs, e)
        pv = e.get('preview') or {}
        corners = world_corners(objs)
        wall = backdrop(corners) if e.get('mount') == 'wall' else None
        frame_camera(cam, corners, yaw=pv.get('yaw', 35.0), elev=pv.get('elev', 24.0))
        render(sc, out / 'individual' / ('Prop_%s.png' % e['name']))
        remove(objs, mats)
        if wall:
            bpy.data.objects.remove(wall, do_unlink=True)
        print('LMS_RENDER %-18s roundtrip=%s' % (e['name'], 'OK' if results[e['name']]['passed'] else results[e['name']]))

    if not args.no_lineups and not only:
        by_name = {e['name']: e for e in manifest['props']}
        for group, names in LINEUPS.items():
            names = [n for n in names if n in by_name]      # partial manifests (build --only) render what exists
            if not names:
                continue
            sc, cam, ground = setup_scene(768)
            human_proxy(0.0)
            x = 0.55
            placed = []
            for n in names:
                e = by_name[n]
                objs, mats = import_fbx(src / e['fbx'])
                apply_manifest_materials(objs, e)
                w = e['dimensions_m']['x']
                x += 0.35 + w / 2
                for o in objs:
                    o.location.x += x
                placed.append((n, objs, x, e))
                x += w / 2
            width = x + 0.5
            maxh = max([1.95] + [e['dimensions_m']['z'] for _, _, _, e in placed])
            cam.data.type = 'ORTHO'
            yaw, elev = 12.0, 14.0
            d, fwd, right, up = cam_basis(yaw, elev)
            focus_h = maxh if group != 'objetos' else 1.0
            aspect = max(1.6, (width + 0.6) / (focus_h + 0.8))
            sc.render.resolution_x = 2400
            sc.render.resolution_y = int(2400 / aspect)
            cam.data.ortho_scale = (width + 0.6)
            center = Vector((width / 2 - 0.3, 0.0, focus_h / 2 + 0.05))
            cam.location = center + d * 40
            cam.rotation_euler = fwd.to_track_quat('-Z', 'Y').to_euler()
            cam.data.clip_start = 1.0
            cam.data.clip_end = 200
            bpy.context.view_layer.update()
            labels = []
            for n, objs, xx, e in placed:
                p = world_to_camera_view(sc, cam, Vector((xx, -e['dimensions_m']['y'] / 2, 0.0)))
                labels.append({'name': n, 'es': e['display_name_es'], 'height_m': e['dimensions_m']['z'],
                               'u': round(p.x, 4), 'v': round(p.y, 4)})
            hp = world_to_camera_view(sc, cam, Vector((0.0, 0.0, 0.0)))
            labels.insert(0, {'name': 'HumanProxy', 'es': 'Humano 1,72 m', 'height_m': 1.72, 'u': round(hp.x, 4), 'v': round(hp.y, 4)})
            render(sc, out / ('lineup_%s.png' % group))
            (out / ('lineup_%s.json' % group)).write_text(json.dumps(
                {'group': group, 'resolution': [sc.render.resolution_x, sc.render.resolution_y], 'labels': labels},
                indent=1, ensure_ascii=False), encoding='utf-8')
            print('LMS_LINEUP', group, len(placed))

    if not args.no_lineups and not only:
        menu_vignette(src, out, {e['name']: e for e in manifest['props']})

    summary = {'blender': bpy.app.version_string, 'source': str(src), 'props': results,
               'passed': all(r['passed'] for r in results.values()), 'seconds': round(time.time() - t0, 1)}
    (out / 'fbx_roundtrip.json').write_text(json.dumps(summary, indent=1, ensure_ascii=False), encoding='utf-8')
    print('LMS_RENDER_DONE props=%d roundtrip_passed=%s seconds=%.1f' % (len(results), summary['passed'], time.time() - t0))


if __name__ == '__main__':
    main()
