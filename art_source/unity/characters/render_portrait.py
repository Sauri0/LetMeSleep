"""Render a UI portrait of a character .blend on a transparent background.

Usage:
  blender --background --factory-startup <file.blend> --python render_portrait.py -- \
      <out.png> <human|mosquito> <Action_Name> <phase 0..1> <width> <height> <top_px> [idle|cheer]

Three-quarter full-body view from a slightly low camera, warm key plus bluish rim light as in the
v0.3.0 sketches, EEVEE, film_transparent RGBA PNG. The pose is sampled from an existing action;
nothing is saved.

Framing (review round 2): the UI draws Resources/AlfaUiPortraits/<Role> twice, "cover" into the
546 x 296 training-card image and "contain" standing on the results floor line. So the figure
always stands on the bottom edge (no transparent padding under the feet) and is centred
horizontally; its top sits <top_px> rows below the top edge, or lower if the width limits it.
  Human.png    1024 x 1024, top 250: the card's centred cover crop keeps rows ~234-790, i.e. the
               nightcap down to the shins (head in the upper third); results show the whole figure.
  Mosquito.png 1092 x 592 (the card aspect, so the card shows it whole), top 18.
  HumanWinner  1024 x 1024, top 16, pose "cheer": both fists up (the arm aim of the UI's own
               AlfaRolePortrait.RaiseArms), tight fists, brows lifted, the jaw dropped in a shout
               (the dark mouth cavity shows), chest and head tipped back a little. The pupils stay on the
               Eye bones' rest aim: they are decals on the faceted globes and sink into its facets
               when turned far.
"""
import math
import sys
from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = Path(args[0])
kind = args[1]
action_name = args[2]
phase = float(args[3])
width = int(args[4]) if len(args) > 4 else 1024
height_px = int(args[5]) if len(args) > 5 else width
top_px = float(args[6]) if len(args) > 6 else 16
pose = args[7] if len(args) > 7 else 'idle'
human = kind == 'human'
out.parent.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
engines = {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items}
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
scene.render.resolution_x = width
scene.render.resolution_y = height_px
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


def freeze_pose():
    """Keep the sampled pose as static bone transforms so it can be edited."""
    basis = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
    rig.animation_data.action = None
    for b in rig.pose.bones:
        b.matrix_basis = basis[b.name]
    bpy.context.view_layer.update()


def aim(bone_name, direction):
    """Rotate a pose bone about its head (armature space) so its Y axis points along direction."""
    pb = rig.pose.bones[bone_name]
    matrix = pb.matrix.copy()
    head = matrix.translation.copy()
    current = (matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
    turn = current.rotation_difference(Vector(direction).normalized()).to_matrix().to_4x4()
    pb.matrix = Matrix.Translation(head) @ turn @ Matrix.Translation(-head) @ matrix
    bpy.context.view_layer.update()


def turn_about(bone_name, axis, degrees):
    pb = rig.pose.bones[bone_name]
    matrix = pb.matrix.copy()
    head = matrix.translation.copy()
    turn = Matrix.Rotation(math.radians(degrees), 4, Vector(axis))
    pb.matrix = Matrix.Translation(head) @ turn @ Matrix.Translation(-head) @ matrix
    bpy.context.view_layer.update()


def cheer():
    """UI-06 screen 9: the human celebrates with both fists up and a happy shout."""
    from author_motion import (FINGER_JOINT_ANGLES, FINGER_REST_AMOUNT, IDLE_FIST_ANGLES, RELAXED_THUMB,
                               THUMB_CURL_FACTOR)
    freeze_pose()
    turn_about('Chest', (1, 0, 0), -6)      # lean back a little (source -Y is the front)
    turn_about('Head', (1, 0, 0), -6)       # chin up
    for side, s in (('L', 1), ('R', -1)):
        # Same arm aim as the UI's AlfaRolePortrait.RaiseArms (up, a little out and forward).
        aim('UpperArm.' + side, (s * .34, -.08, .93))
        aim('LowerArm.' + side, (s * .18, -.05, .97))
        # Tight fists: the idle fist closed a further 20%, thumb over the fingers.
        for digit in ('Index', 'Middle', 'Ring', 'Little'):
            for i, (angle, full) in enumerate(zip(IDLE_FIST_ANGLES, FINGER_JOINT_ANGLES), 1):
                bone = rig.pose.bones[f'{digit}{i:02d}.{side}']
                bone.rotation_mode = 'XYZ'
                bone.rotation_euler = (1.2 * angle - full * FINGER_REST_AMOUNT, 0, 0)
        for i, angle in enumerate(FINGER_JOINT_ANGLES, 1):
            bone = rig.pose.bones[f'Thumb{i:02d}.{side}']
            bone.rotation_mode = 'XYZ'
            bone.rotation_euler = (angle * (min(1.0, RELAXED_THUMB * 1.25) - FINGER_REST_AMOUNT) * THUMB_CURL_FACTOR, 0, 0)
        # Brows lifted evenly (higher they would hide under the cap band; tilting them reads as
        # worried or angry).
        brow = rig.pose.bones['Brow.' + side]
        brow.matrix = Matrix.Translation((0, 0, .005)) @ brow.matrix
        bpy.context.view_layer.update()
    jaw = rig.pose.bones['Jaw']
    jaw.rotation_mode = 'XYZ'
    jaw.rotation_euler = (-.34, 0, 0)      # a wide cheering shout (Hit opens it by .18 rad)
    bpy.context.view_layer.update()


if pose == 'cheer':
    cheer()

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


def aim_object(obj, point):
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
    aim_object(obj, center)
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
aim_object(camera, target)
bpy.context.view_layer.update()

# Fit: at a fixed pose and position, focal length scales the projection about the principal point
# and shift translates it (in units of the larger frame side), so one measurement places the figure
# exactly: feet on the bottom edge, top at top_px, centred, at most 96% of the width.
projected = [world_to_camera_view(scene, camera, p) for p in points]
u = [p.x for p in projected]
v = [p.y for p in projected]
aspect = width / height_px
wide = max(width, height_px)
scale = min((1 - top_px / height_px) / (max(v) - min(v)), .96 / (max(u) - min(u)))
camera_data.lens *= scale
# u, v in [0, 1] of the frame; after scaling about .5: .5 + scale * (x - .5), minus shift * wide / side.
camera_data.shift_x = (.5 + scale * ((max(u) + min(u)) / 2 - .5) - .5) * width / wide
camera_data.shift_y = (.5 + scale * (min(v) - .5)) * height_px / wide
bpy.context.view_layer.update()
check = [world_to_camera_view(scene, camera, p) for p in points]
print('LMS_PORTRAIT_FIT', {'bottom_v': round(min(p.y for p in check), 5), 'top_v': round(max(p.y for p in check), 5),
                           'left_u': round(min(p.x for p in check), 5), 'right_u': round(max(p.x for p in check), 5)}, flush=True)

scene.render.filepath = str(out)
bpy.ops.render.render(write_still=True)
print('LMS_PORTRAIT_RENDERED', out, kind, action_name, phase, width, height_px, top_px, pose, flush=True)
