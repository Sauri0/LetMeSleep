"""Full-frame mosquito source/FBX audit, only with a Director CPU slot.

Read-only assets; writes mosquito/candidate_motion_audit.json. No render or Unity.
Restores unconnected FBX bones because the Blender importer infers use_connect.
"""
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent
TRANSITIONS = (('PerchEnter', 'PerchIdle'), ('Land', 'PerchIdle'),
               ('BiteStart', 'BiteLoop'), ('BiteLoop', 'Detach'),
               ('Fall', 'Recover'), ('Recover', 'PerchIdle'),
               ('Detach', 'Fly'), ('Detach', 'Hover'),
               ('Fly', 'PerchEnter'), ('Hover', 'PerchEnter'))


def quaternion_angle_degrees(a, b):
    """Sign-invariant angle, for unit quaternions in the same deformation space."""
    norm = math.sqrt(sum(v * v for v in a) * sum(v * v for v in b))
    if norm < 1e-12:
        raise ValueError('Invalid zero quaternion')
    dot = abs(sum(x * y for x, y in zip(a, b)) / norm)
    return math.degrees(2 * math.acos(max(0, min(1, dot))))


def compare_poses(a, b):
    # A stationary joint origin must not hide a rotating wing/foot.
    return {
        'max_head_difference_source_m': max(math.dist(a['heads'][n], b['heads'][n]) for n in a['heads']),
        'max_deformation_rotation_difference_degrees': max(quaternion_angle_degrees(
            a['rotations'][n], b['rotations'][n]) for n in a['rotations']),
        'max_mesh_marker_difference_source_m': max(math.dist(a['markers'][n], b['markers'][n]) for n in a['markers']),
    }


def threshold_errors(delta):
    return [name for name, tolerance in (
        ('max_head_difference_source_m', .002),
        ('max_mesh_marker_difference_source_m', .002),
        ('max_deformation_rotation_difference_degrees', 2.0)) if delta[name] > tolerance]


def main():
    import bpy
    from mathutils import Vector
    from author_mosquito_geometry import MOUTH, SUPPORT_Z, SOCKETS
    from author_mosquito_motion import surface_step

    source = json.loads((ROOT / 'mosquito/audit.json').read_text())
    errors = []
    rows = []
    poses = {}
    hashes = {}
    transitions = {}
    marker_definitions = {}
    marker_matches = {}
    feet = [f'Leg{i}03.{side}' for side in ('L', 'R') for i in range(1, 4)]
    names = {clip['name'] for clip in source['clips']}
    if len(names) != 15 or source['bones'] != 33:
        errors.append('expected existing 15 clips and 33 bones')
    for kind in ('blend', 'fbx'):
        path = ROOT / 'mosquito' / ('LMS_Mosquito_alpha.' + kind)
        hashes[kind] = hashlib.sha256(path.read_bytes()).hexdigest()
        if kind == 'blend':
            bpy.ops.wm.open_mainfile(filepath=str(path))
        else:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
        rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
        if kind == 'fbx':
            bpy.context.view_layer.objects.active = rig
            bpy.ops.object.mode_set(mode='EDIT')
            for bone in rig.data.edit_bones:
                bone.use_connect = False
            bpy.ops.object.mode_set(mode='OBJECT')
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
        rig.animation_data_create()
        rig.animation_data.action = None
        for bone in rig.pose.bones:
            bone.matrix_basis.identity()
        bpy.context.view_layer.update()
        bind_heads = {b.name: rig.matrix_world @ b.head for b in rig.pose.bones}
        for name, head, tail, parent in SOCKETS:
            if (bind_heads[name] - Vector(head)).length > 1e-6:
                errors.append(kind + '/' + name + ': bind contract drift')
        if bind_heads['Root'].length > 1e-6:
            errors.append(kind + ': Root bind changed')
        if len(rig.pose.bones) != 33:
            errors.append(kind + ': rig bone count changed')
        indices = {}
        for obj in meshes:
            group_names = {g.index: g.name for g in obj.vertex_groups}
            indices[obj.name] = {foot: [v.index for v in obj.data.vertices if any(
                group_names[g.group] == foot and g.weight > .8 for g in v.groups)] for foot in feet}

        # Three noncollinear actual mesh vertices per rigid bone: farthest point,
        # opposite point and widest transverse point. FBX may duplicate/reorder
        # vertices, so match their bind positions/weights rather than vertex IDs.
        marker_bones = ['Wing.L', 'Wing.R', 'Head', 'Proboscis'] + feet
        marker_indices = {}
        marker_matches[kind] = []
        for bone_name in marker_bones:
            candidates = []
            for obj in meshes:
                group = obj.vertex_groups.get(bone_name)
                if group is None:
                    continue
                for vertex in obj.data.vertices:
                    if any(g.group == group.index and g.weight > .99 for g in vertex.groups):
                        candidates.append((obj.name, vertex.index, obj.matrix_world @ vertex.co))
            if len(candidates) < 3:
                raise AssertionError(kind + '/' + bone_name + ': missing rigid mesh marker candidates')
            if kind == 'blend':
                center = sum((point for _, _, point in candidates), Vector()) / len(candidates)
                first = max(candidates, key=lambda item: (item[2] - center).length_squared)
                second = max(candidates, key=lambda item: (item[2] - first[2]).length_squared)
                axis = second[2] - first[2]
                third = max(candidates, key=lambda item: axis.cross(item[2] - first[2]).length_squared)
                if axis.cross(third[2] - first[2]).length < 1e-10:
                    raise AssertionError(bone_name + ': mesh markers are collinear')
                marker_definitions[bone_name] = [list(item[2]) for item in (first, second, third)]
            for index, position in enumerate(marker_definitions[bone_name]):
                matched = min(candidates, key=lambda item: (item[2] - Vector(position)).length_squared)
                error = (matched[2] - Vector(position)).length
                marker_name = bone_name + '/' + str(index)
                marker_indices[marker_name] = matched[:2]
                marker_matches[kind].append({'name': marker_name, 'bind_match_error_source_m': error})
                if error > 1e-6:
                    errors.append(kind + '/' + marker_name + ': FBX bind mesh marker mismatch')

        def sample(frame):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            graph = bpy.context.evaluated_depsgraph_get()
            heads = {bone.name: list(rig.matrix_world @ bone.head) for bone in rig.pose.bones}
            vertices = {}
            for obj in meshes:
                evaluated = obj.evaluated_get(graph)
                data = evaluated.to_mesh()
                vertices[obj.name] = [tuple(evaluated.matrix_world @ v.co) for v in data.vertices]
                evaluated.to_mesh_clear()
            # Cancel each bone's imported bind basis; reconstructed FBX tails or
            # local rolls are not compared. Both results map global bind to pose.
            rotations = {bone.name: tuple((rig.matrix_world @ bone.matrix @
                bone.bone.matrix_local.inverted() @ rig.matrix_world.inverted()).to_quaternion().normalized())
                for bone in rig.pose.bones}
            markers = {name: vertices[obj][index] for name, (obj, index) in marker_indices.items()}
            return {'heads': heads, 'rotations': rotations, 'markers': markers}, vertices

        for clip in source['clips']:
            name = clip['name']
            rig.animation_data.action = None
            for bone in rig.pose.bones:
                bone.matrix_basis.identity()
            action = next(a for a in bpy.data.actions if a.name.endswith(name))
            rig.animation_data.action = action
            if len(action.slots) == 1:
                rig.animation_data.action_slot = action.slots[0]
            start, end = map(round, action.frame_range)
            first_pose, first_vertices = sample(start)
            frames = []
            poses[kind, name] = []
            max_motion = 0
            max_root = 0
            max_mouth = 0
            max_target_error = 0
            min_z = math.inf
            min_supports = 6
            for frame in range(start, end + 1):
                pose, vertices = sample(frame)
                heads = pose['heads']
                time = (frame - start) / (end - start)
                points = [point for values in vertices.values() for point in values]
                if not all(math.isfinite(c) for point in points for c in point):
                    errors.append(f'{kind}/{name}/{frame}: nonfinite mesh')
                lowest = min(v[2] for v in points)
                min_z = min(min_z, lowest)
                max_motion = max(max_motion, max(math.dist(a, b) for obj in meshes
                    for a, b in zip(vertices[obj.name], first_vertices[obj.name])))
                max_root = max(max_root, math.dist(heads['Root'], (0, 0, 0)))
                max_mouth = max(max_mouth, math.dist(heads['Socket.Mouth'], MOUTH))
                clearances = {}
                for foot in feet:
                    foot_points = [vertices[obj.name][i] for obj in meshes for i in indices[obj.name][foot]]
                    if foot_points:
                        clearances[foot] = min(v[2] for v in foot_points) - SUPPORT_Z
                supporting = sum(abs(clearance) <= .003 for clearance in clearances.values())
                min_supports = min(min_supports, supporting)
                if name == 'Mosquito_SurfaceWalk':
                    for side in ('L', 'R'):
                        for leg in range(1, 4):
                            foot = f'Leg{leg}03.{side}'
                            dy, dz, _ = surface_step(time, leg, side)
                            expected = bind_heads[foot] + Vector((0, dy, dz))
                            max_target_error = max(max_target_error, (Vector(heads[foot]) - expected).length)
                    if supporting < 3 or lowest < SUPPORT_Z - .003:
                        errors.append(f'{kind}/{name}/{frame}: support penetration or fewer than three feet')
                if name in ('Mosquito_Idle', 'Mosquito_PerchIdle') or (name in ('Mosquito_Land', 'Mosquito_PerchEnter', 'Mosquito_Recover') and frame == end):
                    if supporting < 6 or lowest < SUPPORT_Z - .003:
                        errors.append(f'{kind}/{name}/{frame}: six-foot support not met')
                if name in ('Mosquito_Fall', 'Mosquito_Recover') and lowest < SUPPORT_Z - .003:
                    errors.append(f'{kind}/{name}/{frame}: below support plane')
                poses[kind, name].append(pose)
                frames.append({'frame': frame, 'minimum_z_source_m': lowest,
                               'supporting_feet': supporting, 'foot_clearance_source_m': clearances})
            final_pose, final_vertices = pose, vertices
            seam = max(math.dist(a, b) for obj in meshes for a, b in zip(first_vertices[obj.name], final_vertices[obj.name]))
            if max_root > .0001:
                errors.append(kind + '/' + name + ': Root animated')
            if max_motion < .0001:
                errors.append(kind + '/' + name + ': no measurable mesh motion')
            if clip['loop'] and seam > .002:
                errors.append(kind + '/' + name + ': loop seam')
            if name in ('Mosquito_BiteStart', 'Mosquito_BiteLoop', 'Mosquito_Bite') and max_mouth > .0001:
                errors.append(kind + '/' + name + ': Mouth moved in feed clip')
            if max_target_error > .001:
                errors.append(kind + '/' + name + ': foot missed authored target')
            transitions[kind, name] = (first_pose, final_pose)
            rows.append({'format': kind, 'clip': name, 'sample_count': len(frames),
                         'max_mesh_motion_source_m': max_motion, 'loop_seam_source_m': seam,
                         'max_root_motion_source_m': max_root, 'max_mouth_bind_displacement_source_m': max_mouth,
                         'max_gait_target_error_source_m': max_target_error, 'minimum_mesh_z_source_m': min_z,
                         'minimum_supporting_feet': min_supports, 'frames': frames})
    comparisons = []
    for name in sorted(names):
        a, b = poses['blend', name], poses['fbx', name]
        if len(a) != len(b):
            errors.append(name + ': source/FBX frame count differs')
            continue
        per_frame = [compare_poses(source_pose, export_pose) for source_pose, export_pose in zip(a, b)]
        delta = {key: max(frame[key] for frame in per_frame) for key in per_frame[0]}
        comparisons.append({'clip': name, **delta})
        for field in threshold_errors(delta):
            errors.append(name + ': source/FBX mismatch/' + field)
    transition_rows = []
    for kind in ('blend', 'fbx'):
        for left, right in TRANSITIONS:
            a = transitions[kind, 'Mosquito_' + left][1]
            b = transitions[kind, 'Mosquito_' + right][0]
            delta = compare_poses(a, b)
            transition_rows.append({'format': kind, 'from': left, 'to': right, **delta})
            for field in threshold_errors(delta):
                errors.append(kind + '/' + left + '->' + right + ': endpoint mismatch/' + field)
    report = {'schema': 'lms-mosquito-full-frame-audit-v2', 'passed': not errors, 'errors': errors,
              'scope': 'Every integer authored frame in source/FBX; rig/deformation orientation, rigid mesh markers, full-mesh loop seams, bind sockets, feed tip, support and ten endpoint pairs in both formats',
              'sampling': {'integer_frames_only': True, 'subframes_sampled': False, 'runtime_blends_sampled': False,
                           'endpoint_phase_policy': 'last frame of outgoing -> first frame of incoming; arbitrary runtime loop exit phase unverified'},
              'source_sha256': hashes, 'actions': rows, 'source_fbx_comparison': comparisons,
              'mesh_markers_bind_source_m': marker_definitions, 'mesh_marker_matches': marker_matches,
              'transition_endpoints': transition_rows, 'runtime_verified': False, 'art_accepted': False}
    (ROOT / 'mosquito/candidate_motion_audit.json').write_text(json.dumps(report, indent=2), encoding='utf8', newline='\n')
    print(json.dumps({'passed': not errors, 'errors': errors[:25], 'error_count': len(errors),
                      'clips_per_format': len(names), 'sample_count': sum(row['sample_count'] for row in rows)}, indent=2), flush=True)
    assert not errors, 'Mosquito candidate audit failed'


if __name__ == '__main__':
    main()
