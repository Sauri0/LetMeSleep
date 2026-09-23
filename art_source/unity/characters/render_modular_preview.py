"""Render a modular customization combination from modular/<species>/LMS_<Species>_modular_preview.blend.

Usage:
  blender --background --factory-startup --python render_modular_preview.py -- <Human|Mosquito> <out_dir> <prefix>
          [slot=option ...] [--views front,threequarter,side,back] [--closeup head|feet|body|wings|face]
          [--stance] [--perch-v]  (mosquito idle wing aim; --perch-v: the perched V of claude/v030-anim)

Only the host renderers that stay authored in Unity are shown (human: head with its hair sideburns
and neck, and the hands; mosquito: none) plus the selected parts; a part renderer whose
'hidden_when_slot_selected' slot has a non-none selection is hidden, exactly like the Unity
assembler. Missing slots use the defaults from parts.json. EEVEE, slate background, never saves.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
args = sys.argv[sys.argv.index('--') + 1:]
species, out_dir, prefix = args[0], Path(args[1]), args[2]
rest = args[3:]
views = ['front', 'threequarter', 'side', 'back']
closeup = None
stance = False
perch_v = False
selection = {}
i = 0
while i < len(rest):
    if rest[i] == '--views':
        views = rest[i + 1].split(','); i += 2; continue
    if rest[i] == '--closeup':
        closeup = rest[i + 1]; i += 2; continue
    if rest[i] == '--stance':
        stance = True; i += 1; continue
    if rest[i] == '--perch-v':
        # the perched V of claude/v030-anim (author_mosquito_motion WING_V_*), to preview the blades after
        # that branch is merged: it rotates the geometry's stance frame onto this one
        stance = perch_v = True; i += 1; continue
    slot, option = rest[i].split('=')
    selection[slot] = option
    i += 1
out_dir.mkdir(parents=True, exist_ok=True)
folder = HERE / 'modular' / species.lower()
manifest = json.loads((folder / 'parts.json').read_text(encoding='utf8'))
bpy.ops.wm.open_mainfile(filepath=str(folder / f'LMS_{species}_modular_preview.blend'))
DEFAULTS = {'Human': {'human.base': 'base', 'human.hair': 'corto', 'human.headwear': 'gorro-dormir', 'human.glasses': 'none',
                      'human.top': 'remera', 'human.bottom': 'pijama', 'human.footwear': 'pantuflas', 'human.back': 'none'},
            'Mosquito': {'mosquito.base': 'base', 'mosquito.wings': 'clasicas', 'mosquito.proboscis': 'estandar',
                         'mosquito.markings': 'none', 'mosquito.accessory': 'none'}}[species]
chosen = dict(DEFAULTS)
chosen.update(selection)
HOST_KEEP = ('HeadAuthoredPlanes', 'MouthCavity', 'EyeWhite.', 'Pupil.', 'Brow.', 'HeadNose', 'Ear.', 'HeadHair', 'Neck',
             'HeadLid', 'HandSkin.') if species == 'Human' else ()
hidden_by = {}
shown_by = {}
for part in manifest['parts']:
    for r in part['renderers']:
        if r['hidden_when_slot_selected']:
            hidden_by[(part['slot'], part['option'], r['name'])] = r['hidden_when_slot_selected']
        if r.get('shown_when_slot_selected'):
            shown_by[(part['slot'], part['option'], r['name'])] = r['shown_when_slot_selected']
scene = bpy.context.scene
for obj in scene.objects:
    if obj.type != 'MESH':
        continue
    slot = obj.get('lms_slot')
    if slot:
        on = chosen.get(slot) == obj.get('lms_option')
        blocker = hidden_by.get((slot, obj.get('lms_option'), obj.get('lms_renderer', obj.name)))
        if on and blocker and chosen.get(blocker, 'none') != 'none':
            on = False
        needs = shown_by.get((slot, obj.get('lms_option'), obj.get('lms_renderer', obj.name)))
        if on and needs and chosen.get(needs, 'none') == 'none':
            on = False
    else:
        on = obj.name.startswith(HOST_KEEP) if HOST_KEEP else False
        # the host's authored hair is alpha-clipped in Unity while any headwear is worn
        if on and obj.name.startswith('HeadHair') and chosen.get('human.headwear', 'none') != 'none':
            on = False
    obj.hide_render = not on
    obj.hide_viewport = not on

scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items} else 'BLENDER_EEVEE'
scene.render.resolution_x = 700
scene.render.resolution_y = 860 if closeup is None else 700
scene.view_settings.view_transform = 'Standard'
scene.world = scene.world or bpy.data.worlds.new('World')
scene.world.use_nodes = True
bg = scene.world.node_tree.nodes['Background']
bg.inputs[0].default_value = (0.105, 0.135, 0.20, 1)
bg.inputs[1].default_value = 1.0
for obj in [o for o in scene.objects if o.type in {'LIGHT', 'CAMERA'}]:
    bpy.data.objects.remove(obj, do_unlink=True)
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
for b in rig.pose.bones:
    b.rotation_mode = 'XYZ'
    b.rotation_euler = (0, 0, 0)
if species == 'Human':
    # relaxed arms like render_review.py
    rig.pose.bones['UpperArm.L'].rotation_euler.z = -1.2
    rig.pose.bones['UpperArm.R'].rotation_euler.z = 1.2
bpy.context.view_layer.update()
if species == 'Mosquito' and stance:
    # author_mosquito_motion.stance(): Rz(sign * .24) @ Ry(-sign * .12) about the axilla (armature space)
    from mathutils import Matrix
    V_SPAN, V_NORMAL = (.52, .67, .52), (.60, -.77, .22)

    def frame(span, hint):
        span = Vector(span).normalized()
        normal = (Vector(hint) - span * Vector(hint).dot(span)).normalized()
        return Matrix((span, normal, span.cross(normal))).transposed()

    import sys as _sys
    _sys.path.insert(0, str(HERE))
    from author_mosquito_geometry import WING_STANCE_SPAN, WING_STANCE_NORMAL
    opening = (frame(V_SPAN, V_NORMAL) @ frame(WING_STANCE_SPAN, WING_STANCE_NORMAL).transposed()).to_4x4() if perch_v else Matrix.Identity(4)
    mirror = Matrix.Diagonal((-1, 1, 1, 1))
    for side, sign in (('L', 1), ('R', -1)):
        rest_m = rig.data.bones['Wing.' + side].matrix_local.copy()
        origin = rest_m.translation.copy()
        spread = opening if sign > 0 else mirror @ opening @ mirror
        rotation = spread @ Matrix.Rotation(sign * .24, 4, 'Z') @ Matrix.Rotation(-sign * .12, 4, 'Y')
        rig.pose.bones['Wing.' + side].matrix = Matrix.Translation(origin) @ rotation @ Matrix.Translation(-origin) @ rest_m
        bpy.context.view_layer.update()
deps = bpy.context.evaluated_depsgraph_get()
pts = []
for o in scene.objects:
    if o.type == 'MESH' and not o.hide_render:
        ev = o.evaluated_get(deps)
        pts += [ev.matrix_world @ v.co for v in ev.data.vertices]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
center = (lo + hi) / 2
size = max(hi - lo)
if closeup:
    boxes = {'head': ((-.3, -.35, 1.25), (.3, .3, 1.85)), 'feet': ((-.35, -.3, 0), (.35, .15, .3)),
             'body': ((-.45, -.35, .6), (.45, .35, 1.35)), 'face': ((-.2, -.3, 1.4), (.2, .1, 1.7)),
             'wings': ((-.35, -.1, 0), (.35, .3, .35)), 'mfront': ((-.15, -.22, -.05), (.15, .05, .22))}[closeup]
    lo, hi = Vector(boxes[0]), Vector(boxes[1])
    center = (lo + hi) / 2
    size = max(hi - lo)


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


for name, direction, energy in (('Key', (-0.6, -1.0, 1.1), 4.0), ('Fill', (1.0, -0.4, 0.4), 1.6), ('Rim', (0.3, 1.0, 0.9), 2.5)):
    data = bpy.data.lights.new(name, 'SUN')
    data.energy = energy
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = center + Vector(direction) * size * 3
    aim(light, center)
cam_data = bpy.data.cameras.new('SheetCamera')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = size * 1.2
camera = bpy.data.objects.new('SheetCamera', cam_data)
scene.collection.objects.link(camera)
scene.camera = camera
angles = {'front': 0, 'threequarter': 35, 'side': 90, 'back': 180, 'threequarterback': 145, 'left': -90,
          'right45': 45, 'left45': -45}
for view in views:
    a = math.radians(angles[view])
    offset = Vector((math.sin(a), -math.cos(a), 0.12)) * size * 4
    camera.location = center + offset
    aim(camera, center)
    scene.render.filepath = str(out_dir / f'{prefix}_{view}.png')
    bpy.ops.render.render(write_still=True)
print('MODULAR_PREVIEW_RENDERED', out_dir, prefix, flush=True)
