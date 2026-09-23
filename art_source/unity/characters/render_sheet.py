"""Render a comparison sheet (front / three-quarter / side / back) of a character .blend.

Usage: blender --background <file.blend> --python render_sheet.py -- <out_dir> <prefix> [human|mosquito]
EEVEE, flat slate-blue background like the user sketches. Never saves the scene.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out_dir = Path(args[0] if args else '.')
prefix = args[1] if len(args) > 1 else 'sheet'
kind = args[2] if len(args) > 2 else 'human'
out_dir.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items} else 'BLENDER_EEVEE'
scene.render.resolution_x = 900
scene.render.resolution_y = 1100
scene.render.film_transparent = False
scene.view_settings.view_transform = 'Standard'
scene.world = scene.world or bpy.data.worlds.new('World')
scene.world.use_nodes = True
bg = scene.world.node_tree.nodes['Background']
bg.inputs[0].default_value = (0.105, 0.135, 0.20, 1)
bg.inputs[1].default_value = 1.0

for obj in [o for o in scene.objects if o.type in {'LIGHT', 'CAMERA'}]:
    bpy.data.objects.remove(obj, do_unlink=True)

meshes = [o for o in scene.objects if o.type == 'MESH' and o.visible_get()]
rig = next((o for o in scene.objects if o.type == 'ARMATURE'), None)
if rig and rig.animation_data:
    rig.animation_data.action = None
    for b in rig.pose.bones:
        b.matrix_basis.identity()
bpy.context.view_layer.update()

pts = []
deps = bpy.context.evaluated_depsgraph_get()
for o in meshes:
    ev = o.evaluated_get(deps)
    for v in ev.data.vertices:
        pts.append(ev.matrix_world @ v.co)
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
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
cam_data.ortho_scale = size * 1.25
camera = bpy.data.objects.new('SheetCamera', cam_data)
scene.collection.objects.link(camera)
scene.camera = camera

views = (('front', 0), ('threequarter', 35), ('side', 90), ('back', 180))
for view, yaw in views:
    a = math.radians(yaw)
    offset = Vector((math.sin(a), -math.cos(a), 0.12)) * size * 4
    camera.location = center + offset
    aim(camera, center)
    scene.render.filepath = str(out_dir / f'{prefix}_{view}.png')
    bpy.ops.render.render(write_still=True)
print('SHEET_RENDERED', out_dir, flush=True)
