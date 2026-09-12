"""Verify a reopened .blend against its Unity export; no render or visual approval."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
source = args.source.resolve()
data = json.loads(source.read_text(encoding='utf-8'))
objects = {item.get('unity_path'): item for item in bpy.context.scene.objects if item.type == 'MESH'}
assert len(objects) == len(data['parts']), 'Unexpected mesh count after reopening .blend'
max_bound_error = 0
max_normal_error = 0
normal_corners = 0
triangles = 0
for part in data['parts']:
    item = objects[part['path']]
    assert len(item.data.polygons) == sum(len(sub['triangles']) // 3 for sub in part['submeshes'])
    assert item.data.uv_layers.get('AuthoredMetreUV') is not None
    assert [material.name for material in item.data.materials] == [sub['material'] for sub in part['submeshes']]
    if part.get('normals'):
        indices = [index for sub in part['submeshes'] for index in sub['triangles']]
        assert len(item.data.corner_normals) == len(indices)
        for actual_normal, index in zip(item.data.corner_normals, indices):
            x, y, z = part['normals'][index * 3:index * 3 + 3]
            error = math.sqrt(sum((a - b) ** 2 for a, b in zip(actual_normal.vector, (x, -z, y))))
            max_normal_error = max(max_normal_error, error)
            assert error < 0.002, ('Lost authored corner normal', part['path'], index, error)
            normal_corners += 1
    origin = part['origin']
    vertices = part['vertices']
    expected = [(vertices[i] + origin[0], -vertices[i + 2] - origin[2], vertices[i + 1] + origin[1]) for i in range(0, len(vertices), 3)]
    actual = [tuple(item.matrix_world @ vertex.co) for vertex in item.data.vertices]
    assert all(math.isfinite(value) for point in actual for value in point)
    for axis in range(3):
        for operation in (min, max):
            error = abs(operation(point[axis] for point in expected) - operation(point[axis] for point in actual))
            max_bound_error = max(max_bound_error, error)
            assert error < 0.000002, (part['path'], axis, error)
    triangles += len(item.data.polygons)
packed = [image.name for image in bpy.data.images if image.packed_file]
assert any('Quality_LinenWeave' in name for name in packed), 'Weave texture was not packed into the blend'
receipt = {
    'scope': 'Reopened Blender file: geometry bounds, face counts, material slots, UV availability, packed weave, explicit corner normals when exported; no render',
    'blend': bpy.data.filepath, 'blender': bpy.app.version_string,
    'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'mesh_objects': len(objects), 'triangles': triangles,
    'max_bound_error_metres': max_bound_error, 'packed_images': packed,
    'normal_corners_checked': normal_corners, 'max_normal_vector_error': max_normal_error,
    'pending': ['Visual comparison and neutral views'],
}
Path(bpy.data.filepath).with_suffix('.verify.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
print(json.dumps(receipt))
