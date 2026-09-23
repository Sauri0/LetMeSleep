"""Render a UI portrait of a character .blend on a transparent background.

Usage:
  blender --background --factory-startup <file.blend> --python render_portrait.py -- \
      <out.png> <human|mosquito> <Action_Name> <phase 0..1> [size]

Three-quarter full-body view from a slightly low camera, warm key plus bluish rim light as in the
v0.3.0 sketches, EEVEE, film_transparent RGBA PNG. The pose is sampled from an existing action;
nothing is authored and the scene is never saved.
"""
import math
import sys
from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = Path(args[0])
kind = args[1]
action_name = args[2]
phase = float(args[3])
size = int(args[4]) if len(args) > 4 else 1024
human = kind == 'human'
out.parent.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
engines = {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items}
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
scene.render.resolution_x = scene.render.resolution_y = size
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.image_settings.color_depth = '8'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
try:
    scene.eevee.taa_render_samples = 64
except AttributeError:
    pass

# Night-blue ambient (not visible: film is transparent) so shade sides read cool like the sketches.
scene.world = scene.world or bpy.data.worlds.new('World')
scene.world.use_nodes = True
background = next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND')
background.inputs[0].default_value = (0.055, 0.075, 0.13, 1)
background.inputs[1].default_value = 1.0

for obj in [o for o in scene.objects if o.type in {'LIGHT', 'CAMERA'}]:
    bpy.data.objects.remove(obj, do_unlink=True)

rig = next(o for o in scene.objects if o.type == 'ARMATURE')
rig.animation_data_create()
rig.animation_data.action = None
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
action = bpy.data.actions[action_name]
rig.animation_data.action = action
if hasattr(action, 'slots') and len(action.slots) == 1:
    rig.animation_data.action_slot = action.slots[0]
start, end = action.frame_range
frame = start + (end - start) * phase
scene.frame_set(int(frame), subframe=frame - int(frame))
bpy.context.view_layer.update()

graph = bpy.context.evaluated_depsgraph_get()
points = []
for obj in [o for o in scene.objects if o.type == 'MESH' and o.visible_get()]:
    evaluated = obj.evaluated_get(graph)
    mesh = evaluated.to_mesh()
    points += [evaluated.matrix_world @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
center = (low + high) / 2
height = high.z - low.z
extent = max(high - low)


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def light(name, kind_, direction, energy, color, radius=0.0):
    data = bpy.data.lights.new(name, kind_)
    data.energy = energy
    data.color = color
    if kind_ == 'SUN':
        data.angle = math.radians(radius)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = center + Vector(direction).normalized() * extent * 4
    aim(obj, center)
    return obj


# Source frame: front is -Y, the character's left (.L) is +X. The camera sits front-left like the
# three-quarter sheet view; the warm key comes from the camera side and above, the cool rim from
# behind on the opposite side, a faint warm fill keeps the eye whites and dark legs readable.
light('PortraitKey', 'SUN', (0.9, -1.0, 1.25), 4.2, (1.0, 0.83, 0.62), 6)
light('PortraitRim', 'SUN', (-1.0, 0.9, 0.55), 7.0, (0.50, 0.64, 1.0), 3)
light('PortraitFill', 'SUN', (-1.0, -0.6, 0.15), 1.1, (1.0, 0.92, 0.85), 12)

camera_data = bpy.data.cameras.new('PortraitCamera')
camera_data.type = 'PERSP'
camera_data.lens = 50
camera_data.sensor_fit = 'AUTO'
camera = bpy.data.objects.new('PortraitCamera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

yaw = math.radians(34)
# Slightly low camera (contrapicado): eye below the figure's middle, looking up at the target.
target = center + Vector((0, 0, height * (0.06 if human else 0.04)))
camera_height = low.z + height * (0.33 if human else 0.30)
direction = Vector((math.sin(yaw), -math.cos(yaw), 0))
distance = extent * 3.2
camera.location = Vector((target.x, target.y, camera_height)) + direction * distance
aim(camera, target)
bpy.context.view_layer.update()

# Fit: at a fixed pose and position, focal length scales the projection about the principal point
# and shift translates it, so one measurement frames the whole body exactly with a margin.
projected = [world_to_camera_view(scene, camera, p) for p in points]
u = [p.x - .5 for p in projected]
v = [p.y - .5 for p in projected]
fill = 0.88
scale = fill / max(max(u) - min(u), max(v) - min(v))
camera_data.lens *= scale
camera_data.shift_x = scale * (max(u) + min(u)) / 2
camera_data.shift_y = scale * (max(v) + min(v)) / 2
bpy.context.view_layer.update()

scene.render.filepath = str(out)
bpy.ops.render.render(write_still=True)
print('LMS_PORTRAIT_RENDERED', out, kind, action_name, phase, flush=True)
