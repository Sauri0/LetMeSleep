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
               AlfaRolePortrait.RaiseArms), tight fists continuing the forearm with the palms turned
               toward the head, brows lifted, a wide open smile (render-only lip-corner edit, the jaw
               dropped), chest and head tipped back a little; shot and lit from the mirrored
               front-right so the pompom hangs hidden behind the head, the head turned 4 deg and the
               eyes a further 12 deg toward the viewer (round 8, review r2). Round 9 (review r8):
               the smile is twice as wide with crescent corners, render-only upper teeth and tongue.
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
# Round 8: the cheer is shot (and lit) from the front-right mirror of the idle portraits.
MIRROR = -1 if pose == 'cheer' else 1
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
    # Review r2: the pompom peeked out as a loose white sliver between the head and the raised left
    # arm. The cheer is shot from the front-RIGHT (MIRROR), so the pompom hangs behind the head on
    # the far side (a ray probe sees none of its vertices), and the head turns 4 deg toward it.
    turn_about('Head', (0, 0, 1), MIRROR * 4)
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
        # Review r2: the fists read as claws with a bent wrist; the hand now continues the forearm
        # and turns its palm 40 deg toward the head, so the curled fingers face the viewer.
        aim('Hand.' + side, (s * .18, -.05, .97))
        turn_about('Hand.' + side, (s * .18, -.05, .97), -s * 40)
        # Review r2: the pupils looked up-left, away from the viewer. With the head already turned
        # 4 deg, both eyes turn a further 12 deg toward the camera and 8 deg down (runtime limit 22).
        turn_about('Eye.' + side, (1, 0, 0), 8)
        turn_about('Eye.' + side, (0, 0, 1), MIRROR * 12)
    # Review r2: the small round 'o' read as fright. Review r8: the round-8 smile (corners 12 mm out,
    # 6 mm up) was still a small black 'D' that read as a surprised 'oh!', not the open smiles of
    # the RESULTS sketch. Now the mouth is 2.4 times as wide (corners at +/-48 mm, set back onto the
    # convex face), the corners rise 13 mm into a crescent and stay on the head, the upper lip
    # lifts 2 mm, the jaw drops .26 rad (the lower lip ~30 mm) and render-only upper teeth and a
    # tongue fill the dark cavity. Render-only edit of the unsaved scene.
    from author_human_geometry import HEAD_RINGS, MOUTH_HALF, MOUTH_Z
    head = scene.objects['HumanHead']
    jaw_group = head.vertex_groups['Jaw'].index
    head_group = head.vertex_groups['Head'].index
    basis = head.data.shape_keys.key_blocks['Basis'].data if head.data.shape_keys else None
    mouth = next(half for name, _, half in HEAD_RINGS if name == 'mouth')
    lip_y, corner_y = mouth[0][1], mouth[1][1]
    lips = {}
    for vertex in head.data.vertices:
        x, y, z = vertex.co
        if abs(y - (lip_y + corner_y) / 2) > .004 or abs(z - MOUTH_Z) > .0045 or abs(x) > MOUTH_HALF + .002:
            continue
        corner = abs(x) > .01
        # Lip heights (LIP_Z): upper centre +2.5 mm / corner -0.7 mm, lower centre -1.5 / corner -3.7.
        upper = z > MOUTH_Z - .0022 if corner else z > MOUTH_Z + .0005
        # The lip vertices of the skin and the cavity's front copies share these positions.
        key = ('u' if upper else 'l') + ('c' if not corner else ('p' if x > 0 else 'n'))
        lips.setdefault(key, vertex.index)
        if corner:
            vertex.co = (math.copysign(SMILE_CORNER[0], x), SMILE_CORNER[1], MOUTH_Z + (SMILE_CORNER[2] if upper else SMILE_CORNER[2] - .002))
            if not upper:
                # The lower corners stay with the head (the raised crescent ends).
                for group in vertex.groups:
                    if group.group == jaw_group:
                        group.weight = .10
                    elif group.group == head_group:
                        group.weight = .90
        elif upper:
            vertex.co = (x, y, z + .002)
        if basis is not None:
            # With shape keys the evaluated mesh starts from the Basis key (the blink keys do
            # not move the lips, and they stay at weight 0 here).
            basis[vertex.index].co = vertex.co
    assert set(lips) == {'uc', 'up', 'un', 'lc', 'lp', 'ln'}, sorted(lips)
    head.data.update()
    jaw = rig.pose.bones['Jaw']
    jaw.rotation_mode = 'XYZ'
    jaw.rotation_euler = (-SMILE_JAW, 0, 0)
    bpy.context.view_layer.update()
    smile_teeth_and_tongue(head, lips)


# Round 9 smile: lip corners (x, y, z above MOUTH_Z) in the bind frame, and the jaw drop (rad).
SMILE_CORNER = (.048, -.166, .013)
SMILE_JAW = .26


def smile_teeth_and_tongue(head, lips):
    """Render-only upper teeth (a white band just behind the upper lip) and a pink tongue on the
    dropped lower lip, placed from the evaluated lip positions in the posed head frame."""
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = head.evaluated_get(graph)
    mesh = evaluated.to_mesh()
    at = {k: head.matrix_world @ mesh.vertices[i].co for k, i in lips.items()}
    evaluated.to_mesh_clear()
    bone = rig.pose.bones['Head']
    frame = (rig.matrix_world @ bone.matrix @ rig.data.bones['Head'].matrix_local.inverted()).to_3x3().normalized()
    back, up = (frame @ Vector((0, 1, 0))).normalized(), (frame @ Vector((0, 0, 1))).normalized()
    teeth_material = bpy.data.materials['Character_EyeWhite']
    tongue_material = bpy.data.materials.new('PortraitTongue')
    tongue_material.use_nodes = True
    node = next(n for n in tongue_material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (.60, .13, .17, 1)
    node.inputs['Roughness'].default_value = .6

    def along(t):
        # Upper lip polyline n-corner -> centre -> p-corner, t in [-1, 1].
        a, b = (at['un'], at['uc']) if t < 0 else (at['uc'], at['up'])
        return a.lerp(b, t + 1 if t < 0 else t)
    samples = [i / 5 - 1 for i in range(11)]
    verts, faces = [], []
    for t in samples:
        top = along(t * .92) + back * .004 - up * .0005
        height = .009 * (1 - .55 * abs(t))
        for offset in (0.0, .002):
            verts += [top + back * offset, top - up * height + back * (offset + .001)]
    for k in range(len(samples) - 1):
        a, b = 4 * k, 4 * (k + 1)
        faces += [(a, b, b + 1, a + 1), (a + 2, a + 3, b + 3, b + 2), (a, a + 2, b + 2, b), (a + 1, b + 1, b + 3, a + 3)]
    teeth = bpy.data.meshes.new('PortraitTeeth')
    teeth.from_pydata([tuple(v) for v in verts], [], faces)
    teeth.materials.append(teeth_material)
    scene.collection.objects.link(bpy.data.objects.new('PortraitTeeth', teeth))
    centre = at['lc'] + back * .010 + up * .007
    lateral = (at['up'] - at['un']).normalized()
    tongue = bpy.data.meshes.new('PortraitTongue')
    rings, segments, verts, faces = 5, 10, [], []
    for i in range(rings + 1):
        polar = math.pi * i / rings
        for j in range(segments):
            a = 2 * math.pi * j / segments
            verts.append(tuple(centre + lateral * (.019 * math.sin(polar) * math.cos(a))
                               + back * (.012 * math.sin(polar) * math.sin(a)) + up * (.006 * math.cos(polar))))
    for i in range(rings):
        for j in range(segments):
            faces.append((i * segments + j, i * segments + (j + 1) % segments,
                          (i + 1) * segments + (j + 1) % segments, (i + 1) * segments + j))
    tongue.from_pydata(verts, [], faces)
    tongue.materials.append(tongue_material)
    scene.collection.objects.link(bpy.data.objects.new('PortraitTongue', tongue))
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
light('PortraitKey', 'SUN', (MIRROR * 0.9, -1.0, 1.25), 4.2, (1.0, 0.83, 0.62), 6)
light('PortraitRim', 'SUN', (MIRROR * -1.0, 0.9, 0.55), 7.0, (0.50, 0.64, 1.0), 3)
light('PortraitFill', 'SUN', (MIRROR * -1.0, -0.6, 0.15), 1.1, (1.0, 0.92, 0.85), 12)

camera_data = bpy.data.cameras.new('PortraitCamera')
camera_data.type = 'PERSP'
camera_data.lens = 50
camera_data.sensor_fit = 'AUTO'
camera = bpy.data.objects.new('PortraitCamera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

yaw = math.radians(MIRROR * 34)
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
