"""Lightweight source checks, no bpy, editor, renderer, or generated asset writes.

Checks actual custom topology and analytic gait reach against unchanged contracts.
Does not simulate skinning/FBX and cannot certify the visual or animated result.
"""
import ast
from collections import Counter
import hashlib
import json
import math
from pathlib import Path

import author_mosquito_geometry as geometry
import author_mosquito_motion as motion

ROOT = Path(__file__).resolve().parent


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def minus(a, b):
    return tuple(x - y for x, y in zip(a, b))


def inspect_mesh(name, vertices, faces):
    edges = Counter()
    directed = Counter()
    errors = []
    for face in faces:
        if len(set(face)) != len(face) or any(i < 0 or i >= len(vertices) for i in face):
            errors.append('invalid_face')
            continue
        for a, b in zip(face, face[1:] + face[:1]):
            edges[tuple(sorted((a, b)))] += 1
            directed[(a, b)] += 1
        for i in range(1, len(face) - 1):
            normal = cross(minus(vertices[face[i]], vertices[face[0]]), minus(vertices[face[i + 1]], vertices[face[0]]))
            area = math.sqrt(sum(x * x for x in normal)) * .5
            if area < 1e-12:
                errors.append('degenerate_fan_triangle')
    if any(n != 2 for n in edges.values()):
        errors.append('nonmanifold_edges')
    if any(directed[(a, b)] != directed[(b, a)] for a, b in edges):
        errors.append('inconsistent_winding')
    if any(not all(math.isfinite(c) for c in v) for v in vertices):
        errors.append('nonfinite_vertex')
    if len(vertices) - len(edges) + len(faces) != 2:
        errors.append('unexpected_closed_surface_euler_characteristic')
    return {'name': name, 'vertices': len(vertices), 'faces': len(faces), 'errors': errors}


def main():
    errors = []
    files = ('author_mosquito_geometry.py', 'author_mosquito_motion.py', 'build_mosquito_candidate.py',
             'check_mosquito_source.py', 'audit_mosquito_candidate.py')
    hashes = {}
    for name in files:
        data = (ROOT / name).read_bytes()
        ast.parse(data)
        hashes[name] = hashlib.sha256(data).hexdigest()
    meshes = []
    for name, sections in (('Thorax', geometry.THORAX_SECTIONS), ('Head', geometry.HEAD_SECTIONS),
                            ('Abdomen', geometry.ABDOMEN_SECTIONS)):
        meshes.append(inspect_mesh(name, *geometry.section_mesh(sections)))
    for side in (1, -1):
        meshes.append(inspect_mesh('Wing.' + str(side), *geometry.wing_mesh(side)))
        meshes.append(inspect_mesh('Brow.' + str(side), *geometry._brow_mesh(side)))
    errors += [f"{m['name']}: {error}" for m in meshes for error in m['errors']]

    # Compare literal contracts against the existing generated baseline, not a
    # duplicated expected rig assembled by this test.
    baseline_path = ROOT / 'mosquito' / 'audit.json'
    baseline = json.loads(baseline_path.read_text())
    bones = {bone['name']: bone for bone in baseline['bind_bones']}
    for name, head, tail, parent in geometry.SOCKETS:
        old = bones[name]
        if math.dist(head, old['head_blender_m']) > 1e-7 or math.dist(tail, old['tail_blender_m']) > 1e-7 or old['parent'] != parent:
            errors.append(name + ': bind contract changed')
    for actual, expected, name in ((geometry.COLLISION_RADIUS, .055, 'collision'),
                                    (geometry.UNITY_SCALE, .5, 'scale'),
                                    (geometry.SURFACE_ROOT_OFFSET, .057, 'support')):
        if actual != expected:
            errors.append(name + ': gameplay contract changed')

    reach = []
    minimum_supports = 6
    stance_error = 0
    for side, sign in (('L', 1), ('R', -1)):
        for leg in range(1, 4):
            a, b, foot, toe = geometry.leg_points(sign, leg)
            length = math.dist(a, b) + math.dist(b, foot)
            margin = math.inf
            for frame in range(601):
                t = frame / 600
                dy, dz, support = motion.surface_step(t, leg, side)
                target = (foot[0], foot[1] + dy, foot[2] + dz)
                margin = min(margin, length - math.dist(a, target))
                if support:
                    delta = 1e-7
                    later = motion.surface_step(t + delta, leg, side)
                    if later[2] and later[0] >= dy:
                        velocity = (later[0] - dy) / delta * geometry.UNITY_SCALE
                        stance_error = max(stance_error, abs(velocity - motion.SURFACE_DISTANCE_PER_CYCLE_M))
            if margin < .0002:
                errors.append(f'Leg{leg}.{side}: analytic IK reach exhausted')
            reach.append({'leg': leg, 'side': side, 'minimum_reach_margin_source_m': margin})
            if abs(toe[2] - (-.1126)) > 1e-7:
                errors.append(f'Leg{leg}.{side}: support height changed')
    for frame in range(601):
        minimum_supports = min(minimum_supports, sum(motion.surface_step(frame / 600, i, s)[2]
                                                    for i in range(1, 4) for s in ('L', 'R')))
    if minimum_supports < 3:
        errors.append('fewer than three authored support targets')
    if stance_error > 1e-6:
        errors.append('distance_per_cycle metadata does not match target velocity')
    for i in range(1, 4):
        for side in ('L', 'R'):
            if math.dist(motion.surface_step(0, i, side)[:2], motion.surface_step(1, i, side)[:2]) > 1e-10:
                errors.append('gait target seam')

    report = {'schema': 'lms-mosquito-source-check-v1', 'passed': not errors, 'errors': errors,
              'scope': 'Python syntax, custom shell/membrane topology, bind socket constants, analytic gait reach/target phase only',
              'bpy_executed': False, 'rendered': False, 'skin_or_fbx_verified': False, 'art_accepted': False,
              'source_sha256': hashes, 'comparison_asset_sha256': hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
              'custom_meshes': meshes, 'leg_reach': reach, 'minimum_support_targets': minimum_supports,
              'stance_velocity_metadata_error_m_per_cycle': stance_error,
              'surface_distance_per_cycle_unity_m': motion.SURFACE_DISTANCE_PER_CYCLE_M,
              'proboscis_descent_degrees': math.degrees(math.atan2(geometry.PROBOSCIS_BASE[2], abs(geometry.MOUTH[1] - geometry.PROBOSCIS_BASE[1])))}
    output = ROOT.parents[2] / 'docs' / 'unity' / 'mosquito' / 'SOURCE-CHECK-20260912.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding='utf8', newline='\n')
    print(json.dumps(report, indent=2))
    assert not errors, 'Source checks failed'


if __name__ == '__main__':
    main()
