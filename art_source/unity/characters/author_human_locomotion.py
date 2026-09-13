"""Selective authoring on the existing Human rig. Import inside Blender only."""
import math
import types
import bpy
from mathutils import Vector
from author_motion import Pose
from build_characters import Character
from human_locomotion_contract import (PROFILES, REPLACED, GRIP_BONES, FPS, END_FRAME,
                                        foot, hip_height)
from verify_human_menu import activate, curves


def grip_pose(rig):
    return {name: dict(rotation_quaternion=tuple(rig.pose.bones[name].matrix_basis.to_quaternion()),
                       location=tuple(rig.pose.bones[name].location),
                       scale=tuple(rig.pose.bones[name].scale)) for name in GRIP_BONES}


def author(rig):
    assert bpy.context.scene.render.fps == FPS
    assert bpy.context.scene.render.fps_base == 1
    assert len(rig.data.bones) == 65
    assert all(name in rig.pose.bones for name in GRIP_BONES)
    assert not rig.animation_data or not rig.animation_data.nla_tracks
    assert not any(b.constraints for b in rig.pose.bones), 'Unexpected live constraints'
    for side in ('L', 'R'):
        assert abs(rig.data.bones['UpperLeg.' + side].length - .34) < 1e-6
        assert abs(rig.data.bones['LowerLeg.' + side].length - .32) < 1e-6
    # Cache before replacing either reference action. Never reset/rebuild the scene.
    cached = {}
    for name in sorted(REPLACED):
        action = bpy.data.actions[name]
        start, end = action.frame_range
        cached[name] = []
        for i in range(END_FRAME):
            activate(rig, action, start + (end - start) * i / (END_FRAME - 1))
            cached[name].append(grip_pose(rig))
    rig.animation_data.action = None
    for name in REPLACED:
        bpy.data.actions.remove(bpy.data.actions[name])
    c = types.SimpleNamespace(rig=rig, species='Human', clips=[])
    p = Pose(c)
    for profile in PROFILES:
        assert profile['clip'] not in bpy.data.actions, profile['clip']
        poses = []
        for i in range(END_FRAME):
            phase = i / (END_FRAME - 1)
            p.reset()
            p.translate('Hips', (0, 0, hip_height(phase, profile) - .78))
            p.rotate('Chest', (profile['lean'], 0, 0))
            p.rotate('Neck', (-profile['lean'] * .35, 0, 0))
            p.update()
            for side, sign, offset in (('L', 1, 0.), ('R', -1, .5)):
                forward, z, _ = foot(phase + offset, profile)
                target = Vector((sign * .125, -forward, z))
                upper = rig.pose.bones['UpperLeg.' + side]
                reach = (target - upper.head).length
                # Pose.chain clamps unreachable targets; reject BEFORE that clamp.
                assert abs(.34 - .32) + .02 < reach <= .34 + .32 - .02, (profile['clip'], phase, side, reach)
                p.chain('UpperLeg.' + side, 'LowerLeg.' + side, target,
                        (sign * .125, -.6, .42), 'Foot.' + side)
                p.rotate('UpperArm.' + side,
                         (sign * profile['arm'] * math.sin(2 * math.pi * phase), 0, -sign * 1.18))
                p.rotate('LowerArm.' + side, (profile['elbow'], 0, 0))
            pose = p.snapshot()
            # Replace whole transform dictionaries: never mix stale Euler and quaternion fields.
            pose.update(cached[profile['reference']][i])
            poses.append((i + 1, pose))
        Character.clip(c, profile['clip'].removeprefix('Human_'), END_FRAME, poses)
        c.clips[-1]['loop'] = True
        for curve in curves(bpy.data.actions[profile['clip']]):
            for key in curve.keyframe_points:
                key.interpolation = 'LINEAR'
    return dict(clips=c.clips, minimum_authored_extension_margin_m=p.minimum_reach_margin,
                grip_source_by_clip={p['clip']: p['reference'] for p in PROFILES})
