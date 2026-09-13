"""Native source/FBX gait gates. No render, activation, or perceptual approval.

Run only in a Director native slot, preferably export_human_locomotion.py --audit.
"""
import argparse
import importlib
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from human_locomotion_contract import (PROFILES, FPS, SOURCE_DURATION, BASELINE_BLEND_SHA256,
                                        GRIP_BONES, REPLACED, foot, hip_height, distance, sample_phases)
from export_human_locomotion import file_hash, write_json, scene_fingerprint, action_fingerprints
from verify_human_menu import activate, action_for_rig, evaluated, curves
from audit_human_joints import load, regions
from compare_fbx_winding_repair import collect


def matrices(rig, local=False, names=None):
    return {b.name: [list(row) for row in (b.matrix_basis if local else rig.matrix_world @ b.matrix)]
            for b in rig.pose.bones if names is None or b.name in names}


def matrix_error(a, b):
    assert a.keys() == b.keys()
    return max(abs(a[n][i][j] - b[n][i][j]) for n in a for i in range(4) for j in range(4))


def old_phases(name):
    return (0., .25, .5, .6, .75, 1.) if name == 'Human_Crouch' else (0., .25, .5, .75, 1.)


def capture_reference(baseline, kind, phases, preserved):
    rig, _ = load(baseline / ('LMS_Human_alpha.' + kind), kind)
    grips = {}
    for name in REPLACED:
        action = action_for_rig(name)
        start, end = action.frame_range
        grips[name] = []
        for phase in phases:
            activate(rig, action, start + (end - start) * phase)
            grips[name].append(matrices(rig, local=True, names=GRIP_BONES))
    other = {}
    for name in preserved:
        action = action_for_rig(name)
        start, end = action.frame_range
        samples = []
        for phase in old_phases(name):
            activate(rig, action, start + (end - start) * phase)
            samples.append(matrices(rig))
        other[name] = samples
    return grips, other


def audit_candidate(baseline, candidate):
    import json
    receipt = json.loads((candidate / 'preservation.json').read_text(encoding='utf8'))
    assert file_hash(baseline / 'LMS_Human_alpha.blend') == BASELINE_BLEND_SHA256
    for name, expected in receipt['files_sha256'].items():
        assert file_hash(candidate / name) == expected, ('Candidate changed', name)
    phases = sample_phases()
    preserved = receipt['preserved_action_names']
    assert len(preserved) == 13
    # Compare actual FBX payloads as well as the saved-source invariants. New
    # animation payloads intentionally differ; geometry/morph/skin/winding do not.
    parser = importlib.import_module('io_scene_fbx.parse_fbx')
    raw_before, faces_before = collect(baseline / 'LMS_Human_alpha.fbx', parser)
    raw_after, faces_after = collect(candidate / 'LMS_Human_alpha.fbx', parser)
    raw_preserved = {k: raw_before['hashes'][k] == raw_after['hashes'][k]
                     for k in raw_before['hashes'] if k != 'animation_curve_payloads'}
    raw_preserved['polygon_winding'] = faces_before == faces_after
    write_json(candidate / 'locomotion_fbx_preservation.json',
               dict(before=raw_before, after=raw_after, preserved=raw_preserved))
    assert all(raw_preserved.values()), ('FBX preservation failed', raw_preserved)
    reports, errors, source_poses, arm_rows = [], [], {}, []
    for kind in ('blend', 'fbx'):
        grips, other = capture_reference(baseline, kind, phases, preserved)
        rig, meshes = load(candidate / ('LMS_Human_alpha.' + kind), kind)
        if kind == 'blend':
            assert scene_fingerprint() == receipt['scene_before'] == receipt['scene_after']
            fingerprints = action_fingerprints()
            assert fingerprints == receipt['actions_after']
            assert all(fingerprints[n] == receipt['actions_before'][n] for n in preserved)
        actions = {p['clip']: action_for_rig(p['clip']) for p in PROFILES}
        old_actions = {n: action_for_rig(n) for n in preserved}
        bone_actions = [a for a in bpy.data.actions if any(f.data_path.startswith('pose.bones[') for f in curves(a))]
        assert len(bone_actions) == 17 and set(bone_actions) == set(actions.values()) | set(old_actions.values())
        assert len(rig.data.bones) == 65
        head = next(o for o in meshes if o.name == 'HumanHead')
        assert len(head.data.shape_keys.key_blocks) == 9, 'Expected Basis and eight blink samples'
        unchanged_error = 0.
        for name, action in old_actions.items():
            start, end = action.frame_range
            for phase, prior in zip(old_phases(name), other[name]):
                activate(rig, action, start + (end - start) * phase)
                unchanged_error = max(unchanged_error, matrix_error(matrices(rig), prior))
                if name in ('Human_Idle', 'Human_Crouch'):
                    sides = {}
                    for side in ('L', 'R'):
                        points = {n: rig.matrix_world @ rig.pose.bones[n + '.' + side].head
                                  for n in ('UpperArm', 'LowerArm', 'Hand', 'Socket.Grip')}
                        sides[side] = dict(source_m={n: list(v) for n, v in points.items()},
                                           actor_m={n: [-v.x, v.z, -v.y] for n, v in points.items()},
                                           upper_arm_length_m=(points['LowerArm'] - points['UpperArm']).length,
                                           forearm_length_m=(points['Hand'] - points['LowerArm']).length,
                                           shoulder_to_hand_m=(points['Hand'] - points['UpperArm']).length,
                                           shoulder_to_grip_m=(points['Socket.Grip'] - points['UpperArm']).length)
                    arm_rows.append(dict(kind=kind, clip=name, phase=phase, frame=float(start + (end - start) * phase), sides=sides))
        if unchanged_error > 1e-5:
            errors.append(f'{kind}: unrelated clip samples changed {unchanged_error}')
        sole = {}
        for side, sign in (('L', 1), ('R', -1)):
            sole[side] = {}
            for obj in meshes:
                indices = {i for p in obj.data.polygons
                           if obj.data.materials[p.material_index].name == 'Human_SlipperSole'
                           for i in p.vertices if abs(obj.data.vertices[i].co.z) < 1e-5
                           and sign * obj.data.vertices[i].co.x > 0}
                if indices:
                    sole[side][obj.name] = sorted(indices)
            assert sum(map(len, sole[side].values())) >= 12, ('Actual sole selection missing', kind, side)
        region_edges = {o.name: regions(o) for o in meshes}
        for profile in PROFILES:
            name = profile['clip']
            action = actions[name]
            start, end = map(float, action.frame_range)
            duration = (end - start) / (bpy.context.scene.render.fps / bpy.context.scene.render.fps_base)
            assert abs(duration - SOURCE_DURATION) < 1e-5 and abs(start - 1) < 1e-5, (kind, name, start, end, duration)
            peaks = dict(ankle_error_m=0., hip_error_m=0., scale_error=0., limb_length_error_m=0.,
                         child_attachment_error_m=0., root_error_m=0., stance_sole_height_error_m=0.,
                         stance_sole_world_drift_m=0., grip_local_matrix_error=0., source_fbx_matrix_error=0.)
            floor = math.inf
            knee_flex = 0.
            stretches, support_origins, rows, poses = {}, {}, [], []
            for i, phase in enumerate(phases):
                activate(rig, action, start + (end - start) * phase)
                verts = evaluated(meshes)
                assert all(math.isfinite(v) for points in verts.values() for point in points for v in point)
                floor = min(floor, *(v.z for values in verts.values() for v in values))
                pose = matrices(rig)
                poses.append(pose)
                peaks['grip_local_matrix_error'] = max(peaks['grip_local_matrix_error'],
                    matrix_error(matrices(rig, local=True, names=GRIP_BONES), grips[profile['reference']][i]))
                peaks['root_error_m'] = max(peaks['root_error_m'], (rig.matrix_world @ rig.pose.bones['Root'].head).length)
                for bone in rig.pose.bones:
                    peaks['scale_error'] = max(peaks['scale_error'], *(abs(s - 1) for s in bone.scale))
                for side in ('L', 'R'):
                    arm, forearm, hand = [rig.pose.bones[n + '.' + side] for n in ('UpperArm', 'LowerArm', 'Hand')]
                    for segment in (arm, forearm):
                        actual_length = (rig.matrix_world.to_3x3() @ (segment.tail - segment.head)).length
                        peaks['limb_length_error_m'] = max(peaks['limb_length_error_m'], abs(actual_length - segment.bone.length))
                    peaks['child_attachment_error_m'] = max(peaks['child_attachment_error_m'],
                                                           (forearm.head - arm.tail).length, (hand.head - forearm.tail).length)
                row = dict(phase=phase, feet={})
                for side, sign, offset in (('L', 1, 0.), ('R', -1, .5)):
                    forward, z, stance = foot(phase + offset, profile)
                    upper, lower, ankle = [rig.pose.bones[n + '.' + side] for n in ('UpperLeg', 'LowerLeg', 'Foot')]
                    actual = rig.matrix_world @ ankle.head
                    target = Vector((sign * .125, -forward, z))
                    peaks['ankle_error_m'] = max(peaks['ankle_error_m'], (actual - target).length)
                    peaks['hip_error_m'] = max(peaks['hip_error_m'], abs((rig.matrix_world @ upper.head).z - hip_height(phase, profile)))
                    for segment, length in ((upper, .34), (lower, .32)):
                        peaks['limb_length_error_m'] = max(peaks['limb_length_error_m'], abs((rig.matrix_world.to_3x3() @ (segment.tail - segment.head)).length - length))
                    peaks['child_attachment_error_m'] = max(peaks['child_attachment_error_m'],
                                                           (lower.head - upper.tail).length, (ankle.head - lower.tail).length)
                    knee_flex = max(knee_flex, math.degrees((upper.tail - upper.head).angle(lower.tail - lower.head)))
                    points = [verts[obj][j] for obj, ids in sole[side].items() for j in ids]
                    if stance and phase < 1:
                        peaks['stance_sole_height_error_m'] = max(peaks['stance_sole_height_error_m'], *(abs(v.z) for v in points))
                        world = [v + Vector((0, -distance(profile) * phase, 0)) for v in points]
                        contact = (side, math.floor(phase + offset))
                        if contact not in support_origins:
                            support_origins[contact] = world
                        peaks['stance_sole_world_drift_m'] = max(peaks['stance_sole_world_drift_m'],
                            *((a - b).length for a, b in zip(world, support_origins[contact])))
                    row['feet'][side] = dict(ankle_source_m=list(actual), stance=stance,
                                             sole_min_z_m=min(v.z for v in points), sole_max_z_m=max(v.z for v in points))
                for obj, regions_ in region_edges.items():
                    for region, edges in regions_.items():
                        key = obj + '/' + region
                        for a, b, rest_length in edges:
                            stretches[key] = max(stretches.get(key, 0.), (verts[obj][a] - verts[obj][b]).length / rest_length)
                if kind == 'fbx':
                    peaks['source_fbx_matrix_error'] = max(peaks['source_fbx_matrix_error'], matrix_error(pose, source_poses[name][i]))
                rows.append(row)
            peaks['loop_seam_matrix_error'] = matrix_error(poses[0], poses[-1])
            if kind == 'blend':
                source_poses[name] = poses
            limits = dict(ankle_error_m=.003, hip_error_m=.001, scale_error=1e-4,
                          limb_length_error_m=1e-4, child_attachment_error_m=1e-4,
                          root_error_m=1e-5, stance_sole_height_error_m=.003,
                          stance_sole_world_drift_m=.006, grip_local_matrix_error=.001,
                          source_fbx_matrix_error=.001, loop_seam_matrix_error=1e-4)
            for metric, limit in limits.items():
                if peaks[metric] > limit:
                    errors.append(f'{kind}/{name}: {metric}={peaks[metric]} > {limit}')
            if floor < -.003:
                errors.append(f'{kind}/{name}: evaluated mesh below floor {floor}')
            reports.append(dict(kind=kind, clip=name, duration_seconds=duration, samples=len(phases),
                                peaks=peaks, limits=limits, minimum_evaluated_mesh_z_m=floor,
                                maximum_knee_flex_degrees=knee_flex, region_edge_stretch_diagnostic=stretches,
                                unrelated_clip_sample_matrix_error=unchanged_error, rows=rows))
    result = dict(status='numeric-pass-visual-audio-pending' if not errors else 'numeric-fail',
                  files_sha256=receipt['files_sha256'], fbx_preserved=raw_preserved, reports=reports, errors=errors,
                  scope='Flat ground, individual cycles, dense source and FBX bone/sole samples. Edge stretch is a diagnostic, not an articulation approval. No Unity, blend, terrain, collision or perceptual acceptance.')
    write_json(candidate / 'locomotion_native_audit.json', result)
    write_json(candidate / 'human_arm_reach_samples.json', dict(files_sha256=receipt['files_sha256'], rows=arm_rows,
               scope='Evaluated Idle/Crouch source/FBX arm origins and distances, in metres at human scale1. Static samples; no runtime IK, capsule, sweep or tool reach certification.'))
    assert not errors, errors
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-human', type=Path, required=True)
    parser.add_argument('--candidate-human', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    audit_candidate(args.baseline_human.resolve(), args.candidate_human.resolve())


if __name__ == '__main__':
    main()
