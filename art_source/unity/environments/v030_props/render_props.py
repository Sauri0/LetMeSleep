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
  vignette_menu.png (+ .json)  night bedroom (UI-06) built from Bed.menu_layout: bed, nightstand, lamp +
                               clock on their anchors, render-only sleeper head proxy, moonlight + lamp;
                               the json holds composition thirds, wall/halo colours, digit contrast and
                               the navy-wall fraction (mask pass vignette_menu_mask.png)
  --vignette-only / --search-menu-camera   only the vignette (the search ignores menu_layout.camera)
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
from mathutils import Matrix, Vector
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
    """Render-only '03:27' on the AlarmClock 'screen' anchor, sized like the Unity TMP text described in
    the manifest (clock_text: digit height, colour, HDR intensity)."""
    spec = clock.get('clock_text', {'text': '03:27', 'color_srgb': '#FF3B30', 'hdr_intensity': 2.5,
                                    'digit_height_m': 0.046, 'max_width_m': 0.15})
    a = clock['anchors']['screen']
    n = Vector(a['normal_blender']).normalized()
    up = Vector((0, 0, 1)) - n * n.z
    up.normalize()
    right = up.cross(n)
    rot = Matrix((right, up, n)).transposed().to_4x4()
    rz = Matrix.Rotation(math.radians(yaw), 4, 'Z')
    cu = bpy.data.curves.new('ClockText', 'FONT')
    cu.body = spec['text']
    cu.size = 1.0
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.extrude = 0.0005
    ob = bpy.data.objects.new('ClockText', cu)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.update()
    w1, h1 = ob.dimensions.x, ob.dimensions.y          # digit height of the built-in font at size 1
    cu.size = spec['digit_height_m'] / h1
    sx = min(1.0, spec['max_width_m'] / (w1 * cu.size))
    pos = loc + rz @ (Vector(a['blender_m']) + n * spec.get('offset_along_normal_m', 0.001))
    ob.matrix_world = Matrix.Translation(pos) @ rz @ rot @ Matrix.Diagonal((sx, 1.0, 1.0, 1.0))
    ob.data.materials.append(mat_simple('ClockDigits', spec['color_srgb'], 0.5, emission=spec['hdr_intensity']))
    bpy.context.view_layer.update()
    return ob


MENU_NEED = ['Bed', 'Nightstand', 'TableLamp', 'AlarmClock', 'Window']
MENU_DEFAULTS = {
    'resolution': [1600, 900],                       # 16:9 like the game menu (UI-06 panel 1)
    'wall_gap_m': 0.01,                              # back wall behind the headboard posts
    'nightstand_gap_m': 0.16,                        # bed side -> nightstand top edge
    'wainscot_height_m': 1.55,                       # the lamp halo stays on wood (no lavender on the navy wall)
    'colors_srgb': {'wall': '#2A3560', 'wainscot': '#8B5A2B', 'wainscot_trim': '#A86F3A', 'floor': '#5A3A24',
                    'world': '#101830'},
    'window': {'x_m': -1.6, 'sill_z_m': 0.98},              # off-frame on the left: motivates the moonlight
    # moonlight from the front-left, above: lights the quilt, the pillow and the sleeper's face
    'moon': {'color_srgb': '#6A7BD0', 'strength': 2.0, 'direction': [0.45, 0.45, -0.77]},
    'lamp': {'color_rgb_linear': [1.0, 0.8, 0.45], 'watts': 27.0, 'radius_m': 0.06},
    # warm key on the sleeper only (Unity: Point light whose renderingLayerMask holds just the character layer),
    # placed between the lamp and the head so the face reads warm as in UI-06 without over-lighting the wall
    'sleeper_fill': {'color_rgb_linear': [1.0, 0.72, 0.42], 'watts': 9.0, 'offset_from_head_m': [0.3, -0.35, 0.25]},
    'world_strength': 0.35,
    'glare_strength': 0.3,
}


def _ld(layout, key):
    return layout.get(key, MENU_DEFAULTS[key])


def srgb_hex(c):
    return '#%02X%02X%02X' % tuple(int(round(max(0.0, min(1.0, x)) * 255)) for x in c[:3])


def rel_lum(c):
    def ch(x):
        return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    r, g, b = (ch(x) for x in c[:3])
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def build_menu_room(src, by_name, layout):
    """Night bedroom in bed-local coordinates (bed pivot at the origin, headboard toward +Y)."""
    sc, cam, ground = setup_scene(768)
    res = _ld(layout, 'resolution')
    sc.render.resolution_x, sc.render.resolution_y = res
    cols = _ld(layout, 'colors_srgb')
    world_bg = sc.world.node_tree.nodes['Background']
    world_bg.inputs['Color'].default_value = (*lin(cols['world']), 1)
    world_bg.inputs['Strength'].default_value = _ld(layout, 'world_strength')
    for name in ('Fill', 'Rim'):
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    moon = bpy.data.objects['Key']
    moon.name = 'Moon'
    mcfg = _ld(layout, 'moon')
    moon.data.color = lin(mcfg['color_srgb'])
    moon.data.energy = mcfg['strength']
    moon.data.angle = math.radians(3)
    moon.rotation_euler = (-Vector(mcfg['direction'])).normalized().to_track_quat('Z', 'Y').to_euler()
    ground.data.materials[0] = mat_simple('Floor', cols['floor'], 0.9)
    if sc.compositing_node_group:
        for nd_ in sc.compositing_node_group.nodes:
            if nd_.bl_idname == 'CompositorNodeGlare':
                nd_.inputs['Strength'].default_value = _ld(layout, 'glare_strength')
    bed, ns, lamp, clock, win = (by_name[n] for n in MENU_NEED)
    bd, nd = bed['dimensions_m'], ns['dimensions_m']
    y_wall = bd['y'] / 2 + _ld(layout, 'wall_gap_m')
    hw = _ld(layout, 'wainscot_height_m')
    room = {}

    def plane(name, color, x0, x1, z0, z1, y):
        bpy.ops.mesh.primitive_plane_add(size=1, location=((x0 + x1) / 2, y, (z0 + z1) / 2), rotation=(math.radians(90), 0, 0))
        o = bpy.context.object
        o.name = name
        o.scale = (x1 - x0, z1 - z0, 1)
        o.data.materials.append(mat_simple(name, color, 0.95))
        return o
    room['wall'] = plane('WallNavy', cols['wall'], -4, 4, hw, 3.2, y_wall)
    room['wainscot'] = plane('Wainscot', cols['wainscot'], -4, 4, 0.0, hw, y_wall - 0.002)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, y_wall - 0.02, hw))
    trim = bpy.context.object
    trim.name = 'WainscotTrim'
    trim.scale = (8, 0.04, 0.05)
    trim.data.materials.append(mat_simple('WainscotTrim', cols['wainscot_trim'], 0.8))
    placed = {'Bed': place(src, bed, (0, 0, 0))}
    ns_loc = Vector((bd['x'] / 2 + _ld(layout, 'nightstand_gap_m') + nd['x'] / 2, y_wall - nd['y'] / 2 - 0.01, 0))
    placed['Nightstand'] = place(src, ns, ns_loc)
    lamp_loc = ns_loc + anchor(ns, 'lamp')
    placed['TableLamp'] = place(src, lamp, lamp_loc)
    clock_loc = ns_loc + anchor(ns, 'clock')
    wcfg = _ld(layout, 'window')
    placed['Window'] = place(src, win, (wcfg['x_m'], y_wall - win['dimensions_m']['y'] / 2 - 0.004, wcfg['sill_z_m']))
    head = anchor(bed, 'sleeper_head')
    lcfg = _ld(layout, 'lamp')
    ld = bpy.data.lights.new('LampLight', 'POINT')
    ld.color = lcfg['color_rgb_linear']
    ld.energy = lcfg['watts']
    ld.shadow_soft_size = lcfg['radius_m']
    ld.use_shadow = False
    lo = bpy.data.objects.new('LampLight', ld)
    lo.location = lamp_loc + anchor(lamp, 'light')
    sc.collection.objects.link(lo)
    # Unity: the lamp mesh sits on its own Rendering Layer that this light excludes. Blender: light linking.
    excl = bpy.data.collections.new('LampSelfExclude')
    for o in placed['TableLamp']:
        excl.objects.link(o)
    lo.light_linking.receiver_collection = excl
    for item in excl.collection_objects:
        item.light_linking.link_state = 'EXCLUDE'
    return dict(sc=sc, cam=cam, placed=placed, room=room, y_wall=y_wall, ns_loc=ns_loc, lamp_loc=lamp_loc,
                clock_loc=clock_loc, light=lo, head=head, bed=bed, clock=clock)


def sleeper_proxy(ctx, layout):
    """Render-only stand-in for the sleeping human on the sleeper_head anchor: skin head, red nightcap #C8322E
    over the crown flopping toward the lamp, white pompom, lit by a warm sleeper-only fill (light linking =
    Unity Rendering Layers). Never exported; the real character goes on the same anchor in Unity."""
    sc, cam, head = ctx['sc'], ctx['cam'], ctx['head']
    col = bpy.data.collections.new('SleeperProxy')
    sc.collection.children.link(col)

    def add(op, color, rough=0.85, **kw):
        op(**kw)
        o = bpy.context.object
        o.data.materials.append(mat_simple('Sleeper_' + color, color, rough))
        for c_ in list(o.users_collection):
            c_.objects.unlink(o)
        col.objects.link(o)
        return o
    c = head + Vector((0, -0.035, 0.115))
    R = 0.12
    add(bpy.ops.mesh.primitive_ico_sphere_add, '#C98B5A', subdivisions=2, radius=R, location=c, scale=(1.0, 1.05, 0.95))
    base = c + Vector((0.02, 0.03, 0.075))                              # cap over the crown, as in UI-06
    d = Vector((0.8, 0.25, 0.3)).normalized()
    L = 0.3
    add(bpy.ops.mesh.primitive_cone_add, '#C8322E', vertices=8, radius1=0.13, radius2=0.02, depth=L,
        location=base + d * (L / 2), rotation=d.to_track_quat('Z', 'Y').to_euler())
    add(bpy.ops.mesh.primitive_ico_sphere_add, '#F4F4F0', subdivisions=1, radius=0.045,
        location=base + d * (L + 0.015) + Vector((0, 0, -0.04)))
    fcfg = _ld(layout, 'sleeper_fill')
    fd = bpy.data.lights.new('SleeperFill', 'POINT')
    fd.color = fcfg['color_rgb_linear']
    fd.energy = fcfg['watts']
    fd.shadow_soft_size = 0.1
    fd.use_shadow = False
    fo = bpy.data.objects.new('SleeperFill', fd)
    fo.location = c + Vector(fcfg['offset_from_head_m'])
    sc.collection.objects.link(fo)
    fo.light_linking.receiver_collection = col
    for item in col.collection_objects:
        item.light_linking.link_state = 'INCLUDE'
    ctx['sleeper_fill'] = fo
    return list(col.objects)


def aim_camera(cam, loc, target, lens):
    cam.data.type = 'PERSP'
    cam.data.lens = lens
    cam.data.sensor_fit = 'HORIZONTAL'
    cam.data.sensor_width = 36.0
    cam.location = Vector(loc)
    d = (Vector(target) - Vector(loc)).normalized()
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_start = 0.02
    cam.data.clip_end = 60


def group_points(objs):
    return [c for c in world_corners([o for o in objs if o.type == 'MESH'])]


def project_box(sc, cam, pts):
    uv = [world_to_camera_view(sc, cam, p) for p in pts]
    return dict(u_min=min(p.x for p in uv), u_max=max(p.x for p in uv), v_min=min(p.y for p in uv),
                v_max=max(p.y for p in uv), behind=any(p.z <= 0 for p in uv))


def menu_metrics_geometry(ctx):
    sc, cam = ctx['sc'], ctx['cam']
    bpy.context.view_layer.update()
    hp = world_to_camera_view(sc, cam, ctx['head'])
    grp = group_points(ctx['placed']['Nightstand'] + ctx['placed']['TableLamp'] + ctx['placed']['AlarmClock'])
    box = project_box(sc, cam, grp)
    return {'sleeper_head_uv': [round(hp.x, 4), round(hp.y, 4)],
            'nightstand_group_uv_box': {k: (round(v, 4) if isinstance(v, float) else v) for k, v in box.items()},
            'nightstand_group_height_fraction': round(min(box['v_max'], 1.0) - max(box['v_min'], 0.0), 4),
            'nightstand_group_u_centre': round((box['u_min'] + box['u_max']) / 2, 4)}


def search_menu_camera(ctx, headboard_y):
    """Grid search over camera placements for the UI-06 composition rules: camera at pillow height ~1.2 m
    from the headboard, sleeper head in the central third, nightstand + lamp inside the right third with its
    visible part >= 35 % of the frame height (the nightstand body may be cropped by the bottom edge, as in
    UI-06; lamp and clock stay in frame). Violations are penalised so the best compromise is reported."""
    sc, cam = ctx['sc'], ctx['cam']
    head = ctx['head']
    grp = group_points(ctx['placed']['Nightstand'] + ctx['placed']['TableLamp'])
    clock_top = ctx['clock_loc'] + Vector((0, 0, 0.12))
    gc = sum(grp, Vector()) / len(grp)
    hb = Vector((0.0, headboard_y, head.z))
    best = None
    tested = 0
    for dist in (1.15, 1.2, 1.25):
        for az in range(0, 71, 5):
            for cz in (0.86, 0.92, 0.98):
                loc = hb + Vector((-dist * math.sin(math.radians(az)), -dist * math.cos(math.radians(az)), 0))
                loc.z = cz
                for t in (-0.2, -0.15, -0.1, -0.05, 0.0, 0.05, 0.1, 0.2):
                    for tz in (0.55, 0.62, 0.7, 0.78, 0.86):
                        target = head.lerp(gc, t)
                        target.z = tz
                        for lens in (18, 20, 22, 24, 26, 28, 30, 32):
                            aim_camera(cam, loc, target, lens)
                            bpy.context.view_layer.update()
                            tested += 1
                            hp = world_to_camera_view(sc, cam, head)
                            box = project_box(sc, cam, grp)
                            ck = world_to_camera_view(sc, cam, clock_top)
                            if box['behind'] or hp.z <= 0:
                                continue
                            vis = min(box['v_max'], 1.0) - max(box['v_min'], 0.0)
                            uc = (box['u_min'] + box['u_max']) / 2
                            pen = ((max(0, 0.37 - hp.x) + max(0, hp.x - 0.63)) * 10
                                   + (max(0, 0.3 - hp.y) + max(0, hp.y - 0.72)) * 5
                                   + max(0, 0.675 - box['u_min']) * 10 + max(0, box['u_max'] - 0.985) * 10
                                   + max(0, box['v_max'] - 0.97) * 10 + max(0, 0.37 - vis) * 10
                                   + max(0, 0.06 - ck.y) * 10)
                            score = (-pen + min(vis, 0.55) - 0.3 * abs(hp.x - 0.5) - abs(uc - 0.82)
                                     - abs(dist - 1.2) - 0.5 * abs(hp.y - 0.5) - 0.01 * abs(lens - 28))
                            if best is None or score > best[0]:
                                best = (score, dict(location=[round(x, 4) for x in loc], look_at=[round(x, 4) for x in target],
                                                    lens_mm=lens, headboard_distance_m=dist, azimuth_deg=az,
                                                    head_uv=[round(hp.x, 4), round(hp.y, 4)],
                                                    group_uv_box=[round(box[k], 4) for k in ('u_min', 'u_max', 'v_min', 'v_max')],
                                                    group_visible_height_fraction=round(vis, 4), group_u_centre=round(uc, 4),
                                                    penalty=round(pen, 4)))
    print('LMS_MENU_CAMERA_TESTED', tested)
    return best


def sample_patch(px, W, H, u, v, r=4):
    import numpy as np
    x, y = int(u * W), int(v * H)
    if not (r <= x < W - r and r <= y < H - r):
        return None
    patch = px[y - r:y + r + 1, x - r:x + r + 1, :3].reshape(-1, 3)
    return [float(c) for c in np.median(patch, axis=0).tolist()]


def menu_color_metrics(ctx, png):
    """Wall colour 0.5 m from the lamp light, halo, digit contrast (all measured on the rendered PNG)."""
    import numpy as np
    sc, cam = ctx['sc'], ctx['cam']
    img = bpy.data.images.load(str(png))
    W, H = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
    dg = bpy.context.evaluated_depsgraph_get()
    light = ctx['light'].location.copy()
    y_wall = ctx['y_wall'] - 0.003
    dperp = abs(light.y - y_wall)

    def wall_ring(dist):
        out = []
        rr = math.sqrt(max(0.0, dist * dist - dperp * dperp))
        for k in range(24):
            a = 2 * math.pi * k / 24
            q = Vector((light.x + rr * math.cos(a), y_wall, light.z + rr * math.sin(a)))
            if q.z <= 0.02:
                continue
            d = q - cam.location
            hit, loc, *_ = sc.ray_cast(dg, cam.location, d.normalized(), distance=d.length + 0.01)
            if not hit or (loc - q).length > 0.02:
                continue
            uv = world_to_camera_view(sc, cam, q)
            if not (0 < uv.x < 1 and 0 < uv.y < 1):
                continue
            col = sample_patch(px, W, H, uv.x, uv.y)
            if col:
                out.append({'point_m': [round(x, 3) for x in q], 'uv': [round(uv.x, 4), round(uv.y, 4)],
                            'srgb': srgb_hex(col), 'wood': q.z < _ld(ctx.get('layout', {}), 'wainscot_height_m')})
        return out

    def mean_hex(samples):
        if not samples:
            return None
        cs = [[int(s['srgb'][i:i + 2], 16) for i in (1, 3, 5)] for s in samples]
        return '#%02X%02X%02X' % tuple(int(round(sum(c[i] for c in cs) / len(cs))) for i in range(3))
    wall05 = wall_ring(0.5)
    halo = wall_ring(0.3)
    # lamp base (jar shoulder): must not burn out now that the lamp mesh is excluded from its own light
    jar = ctx['lamp_loc'] + Vector((0, 0, 0.13))
    uvj = world_to_camera_view(sc, cam, jar)
    dj = jar - cam.location
    hitj, locj, *_ = sc.ray_cast(dg, cam.location, dj.normalized(), distance=dj.length + 0.05)
    jar_srgb = None
    if hitj and (locj - jar).length < 0.06:
        cj = sample_patch(px, W, H, uvj.x, uvj.y, r=2)
        jar_srgb = srgb_hex(cj) if cj else None
    target = [0x8B, 0x5A, 0x2B]
    m05 = mean_hex(wall05)
    diff = None if not m05 else [int(m05[i:i + 2], 16) - target[k] for k, i in enumerate((1, 3, 5))]
    # digit contrast inside the projected screen quad
    a = ctx['clock']['anchors']['screen']
    n = Vector(a['normal_blender']).normalized()
    up = (Vector((0, 0, 1)) - n * n.z).normalized()
    right = up.cross(n)
    rz = Matrix.Rotation(math.radians(ctx['clock_yaw']), 4, 'Z')
    c0 = ctx['clock_loc'] + rz @ Vector(a['blender_m'])
    sw, sh = a['size_m']
    corners = [c0 + rz @ (right * sx * sw / 2 + up * sy * sh / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    uvs = [world_to_camera_view(sc, cam, q) for q in corners]
    xs = [int(q.x * W) for q in uvs]
    ys = [int(q.y * H) for q in uvs]
    box = px[max(0, min(ys)):min(H, max(ys) + 1), max(0, min(xs)):min(W, max(xs) + 1), :3].reshape(-1, 3)
    contrast = None
    if len(box) > 20:
        lums = np.array([rel_lum(c) for c in box])
        order = np.argsort(lums)
        lo = box[order[:max(1, len(order) * 3 // 10)]].mean(axis=0)
        hi = box[order[-max(1, len(order) // 10):]].mean(axis=0)
        hi, lo = [float(x) for x in hi.tolist()], [float(x) for x in lo.tolist()]
        contrast = {'screen_px_box': [min(xs), min(ys), max(xs), max(ys)], 'pixels': int(len(box)),
                    'digit_srgb': srgb_hex(hi), 'background_srgb': srgb_hex(lo),
                    'ratio': round(float((rel_lum(hi) + 0.05) / (rel_lum(lo) + 0.05)), 2)}
    bpy.data.images.remove(img)
    return {'wall_0p5m_samples': wall05, 'wall_0p5m_mean_srgb': m05, 'wall_0p5m_target_srgb': '#8B5A2B',
            'wall_0p5m_diff_rgb': diff, 'halo_0p3m_samples': halo, 'halo_0p3m_mean_srgb': mean_hex(halo),
            'lamp_base_srgb': jar_srgb,
            'clock_contrast': contrast}


def navy_fraction(ctx, out_png):
    """Mask pass: navy wall white, everything else black (same camera), fraction of the frame."""
    import numpy as np
    sc = ctx['sc']
    white = bpy.data.materials.new('MaskWhite')
    black = bpy.data.materials.new('MaskBlack')
    for m, c in ((white, (1, 1, 1, 1)), (black, (0, 0, 0, 1))):
        b = m.node_tree.nodes.get('Principled BSDF')
        b.inputs['Base Color'].default_value = (0, 0, 0, 1)
        b.inputs['Emission Color'].default_value = c
        b.inputs['Emission Strength'].default_value = 1.0 if c[0] else 0.0
        b.inputs['Roughness'].default_value = 1.0
    for o in sc.objects:
        if o.type in ('MESH', 'FONT'):
            for slot in o.material_slots:
                slot.link = 'OBJECT'
                slot.material = white if o.name == 'WallNavy' else black
    for o in sc.objects:
        if o.type == 'LIGHT':
            o.hide_render = True
    sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.0
    sc.render.use_compositing = False
    sc.eevee.taa_render_samples = 1
    render(sc, out_png)
    img = bpy.data.images.load(str(out_png))
    W, H = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
    frac = float((px[:, :, 0] > 0.5).mean())
    bpy.data.images.remove(img)
    return round(frac, 4)


def menu_vignette(src, out, by_name, search=False):
    """UI-06 bedroom at night assembled from the library, following Bed.menu_layout (camera, nightstand,
    lights). Writes vignette_menu.png + vignette_menu.json (composition, colour and contrast metrics)."""
    if any(n not in by_name for n in MENU_NEED):
        return
    bed = by_name['Bed']
    layout = dict(bed.get('menu_layout') or {})
    ctx = build_menu_room(src, by_name, layout)
    ctx['layout'] = layout
    sc, cam = ctx['sc'], ctx['cam']
    headboard_y = bed['dimensions_m']['y'] / 2 - 0.05
    if search or 'camera' not in layout:
        best = search_menu_camera(ctx, headboard_y)
        print('LMS_MENU_CAMERA_SEARCH', json.dumps(best[1] if best else None))
        (out / 'vignette_menu_camera_search.json').write_text(json.dumps(best[1] if best else None, indent=1), encoding='utf-8')
        if not best:
            return
        camcfg = best[1]
    else:
        camcfg = layout['camera']
    aim_camera(cam, camcfg['location'], camcfg['look_at'], camcfg['lens_mm'])
    # clock turned to face the camera (Unity: same yaw around the nightstand anchor)
    to_cam = cam.location - ctx['clock_loc']
    clock_yaw = layout.get('clock_yaw_deg', round(math.degrees(math.atan2(to_cam.x, -to_cam.y)), 1))
    ctx['clock_yaw'] = clock_yaw
    ctx['placed']['AlarmClock'] = place(src, by_name['AlarmClock'], ctx['clock_loc'], yaw=clock_yaw)
    clock_text(by_name['AlarmClock'], ctx['clock_loc'], clock_yaw)
    sleeper_proxy(ctx, layout)
    bpy.context.view_layer.update()
    png = out / 'vignette_menu.png'
    render(sc, png)
    metrics = {'layout_source': 'Bed.menu_layout' if 'camera' in layout else 'search', 'camera': camcfg,
               'clock_yaw_deg': clock_yaw, 'resolution': list(_ld(layout, 'resolution')),
               'horizontal_fov_deg': round(math.degrees(2 * math.atan(18.0 / cam.data.lens)), 3),
               'vertical_fov_deg': round(math.degrees(2 * math.atan(18.0 * sc.render.resolution_y / sc.render.resolution_x
                                                                   / cam.data.lens)), 3),
               'nightstand_location_m': [round(x, 4) for x in ctx['ns_loc']],
               'lamp_light_location_m': [round(x, 4) for x in ctx['light'].location],
               'wall_y_m': round(ctx['y_wall'], 4), 'sleeper_proxy': 'render-only head + nightcap on sleeper_head',
               'sleeper_fill_location_m': [round(x, 4) for x in ctx['sleeper_fill'].location]}
    metrics.update(menu_metrics_geometry(ctx))
    metrics.update(menu_color_metrics(ctx, png))
    metrics['navy_wall_fraction'] = navy_fraction(ctx, out / 'vignette_menu_mask.png')
    metrics['criteria'] = {
        'sleeper_head_central_third': 1 / 3 <= metrics['sleeper_head_uv'][0] <= 2 / 3,
        'nightstand_group_right_third': metrics['nightstand_group_uv_box']['u_min'] >= 2 / 3,
        'nightstand_group_height_ge_35pct': metrics['nightstand_group_height_fraction'] >= 0.35,
        'navy_wall_le_35pct': metrics['navy_wall_fraction'] <= 0.35,
        'clock_contrast_ge_4': bool(metrics['clock_contrast'] and metrics['clock_contrast']['ratio'] >= 4.0),
    }
    (out / 'vignette_menu.json').write_text(json.dumps(metrics, indent=1, ensure_ascii=False), encoding='utf-8')
    print('LMS_VIGNETTE menu', json.dumps(metrics['criteria']), 'wall05', metrics['wall_0p5m_mean_srgb'],
          'halo', metrics['halo_0p3m_mean_srgb'], 'navy', metrics['navy_wall_fraction'],
          'contrast', metrics['clock_contrast'] and metrics['clock_contrast']['ratio'])


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
    ap.add_argument('--vignette-only', action='store_true', help='only the UI-06 menu vignette (+ metrics json)')
    ap.add_argument('--search-menu-camera', action='store_true', help='grid-search the vignette camera (ignores menu_layout.camera)')
    args = ap.parse_args(argv)
    src, out = Path(args.src), Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((src / args.manifest).read_text(encoding='utf-8'))
    only = [s for s in args.only.split(',') if s]
    entries = [e for e in manifest['props'] if not only or e['name'] in only]
    t0 = time.time()
    if args.vignette_only or args.search_menu_camera:
        menu_vignette(src, out, {e['name']: e for e in manifest['props']}, search=args.search_menu_camera)
        print('LMS_RENDER_DONE vignette seconds=%.1f' % (time.time() - t0))
        return

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
            focus_h = maxh
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
