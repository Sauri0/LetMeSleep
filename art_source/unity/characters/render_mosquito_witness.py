"""Actual saved mosquito actions, Cycles CPU2. Requires a Director render slot.

Only reads mosquito assets; writes under review/mosquito-*/. Never saves the scene.
Unity-look yaw is mirrored onto source X to match the exported actor convention.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
VIEWS = {
    'front': (0, 8, (0, 0, .055), .66),
    'three_quarter': (35, 8, (0, 0, .055), .66),
    'profile_right': (90, 8, (0, 0, .055), .66),
    'back': (180, 8, (0, 0, .055), .66),
    'profile_left': (270, 8, (0, 0, .055), .66),
    'three_quarter_reverse': (325, 8, (0, 0, .055), .66),
    'face_front': (0, 5, (0, -.075, .108), .19),
    'face_profile': (90, 5, (0, -.060, .108), .24),
    'legs_front': (0, 8, (0, .025, -.020), .34),
    'wing_transmission': (180, 52, (0, .045, .075), .58),
}


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def activate(rig, clip, phase):
    rig.animation_data_create()
    rig.animation_data.action = None
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    action = bpy.data.actions['Mosquito_' + clip]
    rig.animation_data.action = action
    if len(action.slots) == 1:
        rig.animation_data.action_slot = action.slots[0]
    start, end = action.frame_range
    frame = start + (end - start) * phase
    bpy.context.scene.frame_set(int(frame), subframe=frame - int(frame))
    bpy.context.view_layer.update()
    return frame


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--views', nargs='+', choices=list(VIEWS), default=list(VIEWS))
    parser.add_argument('--output-name', default='mosquito-planar-r2')
    parser.add_argument('--samples', type=int, default=12)
    parser.add_argument('--resolution', type=int, default=768)
    parser.add_argument('--clip', default='Idle')
    parser.add_argument('--phase', type=float, default=0)
    parser.add_argument('--sequence', action='store_true')
    parser.add_argument('--loop-count', dest='cycles', type=int, default=1)
    parser.add_argument('--playback', type=float, default=1)
    parser.add_argument('--gaze-yaw', type=float, default=0)
    parser.add_argument('--gaze-pitch', type=float, default=0)
    parser.add_argument('--blink-left', type=float, default=0)
    parser.add_argument('--blink-right', type=float, default=0)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    source = ROOT / 'mosquito/LMS_Mosquito_alpha.blend'
    output = (ROOT / 'review' / args.output_name).resolve()
    if not output.is_relative_to((ROOT / 'review').resolve()) or not args.output_name.startswith('mosquito-'):
        raise ValueError('Output must remain in mosquito review folder')
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.render.resolution_x = args.resolution
    scene.render.resolution_y = args.resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'AgX'
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes['Background']
    background.inputs[0].default_value = (.17, .19, .24, 1)
    background.inputs[1].default_value = .5
    lighting_scale = .25
    for name, position, power, size in (
            ('Key', (-3, -4, 5), 450, 4), ('Fill', (3, -2, 3), 230, 3), ('Rim', (1, 3, 4), 260, 3)):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = power * lighting_scale ** 2
        data.shape = 'DISK'
        data.size = size * lighting_scale
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = Vector(position) * lighting_scale + Vector((0, 0, .04))
        aim(light, (0, 0, .04))
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, -.1141))
    floor = bpy.context.object
    floor.name = 'WitnessFloor'
    floor_material = bpy.data.materials.new('WitnessFloorNeutral')
    floor_material.use_nodes = True
    floor_principled = floor_material.node_tree.nodes['Principled BSDF']
    floor_principled.inputs['Base Color'].default_value = (.11, .135, .18, 1)
    floor_principled.inputs['Roughness'].default_value = .9
    floor.data.materials.append(floor_material)
    checker = floor_material.node_tree.nodes.new('ShaderNodeTexChecker')
    checker.inputs['Color1'].default_value = (.045, .055, .070, 1)
    checker.inputs['Color2'].default_value = (.56, .57, .59, 1)
    checker.inputs['Scale'].default_value = 20
    coordinates = floor_material.node_tree.nodes.new('ShaderNodeTexCoord')
    floor_material.node_tree.links.new(coordinates.outputs['Object'], checker.inputs['Vector'])
    data = bpy.data.cameras.new('WitnessCamera')
    data.type = 'ORTHO'
    data.clip_start = .001
    camera = bpy.data.objects.new('WitnessCamera', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    rig = next(obj for obj in scene.objects if obj.type == 'ARMATURE')
    source_sha = hashlib.sha256(source.read_bytes()).hexdigest()
    source_audit = json.loads((ROOT / 'mosquito/audit.json').read_text())
    clip_info = next(clip for clip in source_audit['clips'] if clip['name'] == 'Mosquito_' + args.clip)
    receipts = []
    for label in args.views:
        yaw, pitch, focus, framing = VIEWS[label]
        a, b = math.radians(-yaw), math.radians(pitch)
        camera.location = Vector(focus) + Vector((math.sin(a) * math.cos(b), -math.cos(a) * math.cos(b), math.sin(b))) * 2
        aim(camera, focus)
        data.ortho_scale = framing
        for link in list(floor_principled.inputs['Base Color'].links):
            floor_material.node_tree.links.remove(link)
        if label == 'wing_transmission':
            floor_material.node_tree.links.new(checker.outputs['Color'], floor_principled.inputs['Base Color'])
        if args.sequence:
            duration = clip_info['duration_seconds'] * args.cycles / args.playback
            count = max(2, round(duration * 30))
            phases = [((index / 30) * args.playback / clip_info['duration_seconds']) % 1
                      if clip_info['loop'] else min(1, index / (count - 1)) for index in range(count)]
        else:
            phases = [args.phase]
        for index, phase in enumerate(phases):
            frame = activate(rig, args.clip, phase)
            if any((args.gaze_yaw, args.gaze_pitch, args.blink_left, args.blink_right)):
                from author_mosquito_face import apply_facial_pose
                # Preserve the evaluated body pose; rendering must not reapply
                # neutral facial curves over this explicit diagnostic overlay.
                rig.animation_data.action = None
                apply_facial_pose(rig, args.gaze_yaw, args.gaze_pitch, args.blink_left, args.blink_right)
            filename = f'{args.clip}_{label}_{index:04d}.png' if args.sequence else label + '.png'
            png = output / filename
            scene.render.filepath = str(png)
            bpy.ops.render.render(write_still=True)
            receipts.append({'image': png.as_posix(), 'source_sha256': source_sha, 'clip': 'Mosquito_' + args.clip,
                             'phase': phase, 'source_frame': frame, 'view': label, 'yaw_unity_degrees': yaw,
                             'source_camera_yaw_degrees': -yaw, 'pitch_degrees': pitch, 'focus': focus, 'framing': framing})
    report = {'source': source.as_posix(), 'source_sha256': source_sha, 'renderer': 'Cycles CPU',
              'blender': bpy.app.version_string, 'threads': 2, 'samples': args.samples,
              'resolution': [args.resolution, args.resolution], 'actual_action_evaluated': True,
              'image_count': len(receipts), 'sequence': args.sequence, 'output_fps': 30 if args.sequence else None,
              'playback_multiplier': args.playback, 'comparison_limit': 'same named angles, neutral source renderer; not the old Unity camera/light instance',
              'wing_material_policy': 'saved source material, including both physical faces; Unity culling/shader comparison remains pending',
              'runtime_evidence': False, 'art_accepted': False, 'images': receipts}
    report['facial_overlay'] = {'yaw_degrees': args.gaze_yaw, 'pitch_degrees': args.gaze_pitch,
                               'blink_left': args.blink_left, 'blink_right': args.blink_right,
                               'driver': 'source QA overlay, not the Unity facial component'}
    (output / 'witness.json').write_text(json.dumps(report, indent=2), encoding='utf8', newline='\n')
    print('LMS_MOSQUITO_WITNESS_DONE', json.dumps({'images': len(receipts), 'source_sha256': source_sha}), flush=True)


if __name__ == '__main__':
    main()
