"""Rebuild the exact Unity witness meshes as an editable Blender file, without rendering.

Run only in the Director-assigned Blender slot:
blender --background --python import_generated_meshes.py -- --input generated_meshes.json
Unity's AlfaMapBuilder produces the JSON and weave PNG alongside this script.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--input', type=Path, default=Path(__file__).with_name('generated_meshes.json'))
parser.add_argument('--output', type=Path)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
source = args.input.resolve()
destination = (args.output or source.with_name('LivingWitnessKit.blend')).resolve()
data = json.loads(source.read_text(encoding='utf-8'))
assert data['schema'] == 'lms-quality-meshes-v1'
assert data['units'] == 'metres' and data['parts']
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
materials = {}
parents = {}
image_path = source.with_name('Quality_LinenWeave.png')
weave_image = bpy.data.images.load(str(image_path)) if image_path.exists() else None
if weave_image:
    weave_image.colorspace_settings.name = 'Non-Color'
    weave_image.pack()


def material(spec):
    name = spec['material']
    if name in materials:
        return materials[name]
    result = bpy.data.materials.new(name)
    result.diffuse_color = spec['color']
    result.use_nodes = True
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = spec['color']
    shader.inputs['Roughness'].default_value = 1 - spec['smoothness']
    shader.inputs['Metallic'].default_value = spec['metallic']
    shader.inputs['Emission Color'].default_value = (*spec['emission'], 1)
    shader.inputs['Emission Strength'].default_value = 1
    if spec['weave']:
        assert weave_image, 'Missing authored weave PNG; cannot produce faithful editable material'
        nodes = result.node_tree.nodes
        links = result.node_tree.links
        coordinates = nodes.new('ShaderNodeTexCoord')
        scale = nodes.new('ShaderNodeVectorMath')
        scale.operation = 'MULTIPLY'
        scale.inputs[1].default_value = (12, 12, 1)
        texture = nodes.new('ShaderNodeTexImage')
        texture.image = weave_image
        texture.extension = 'REPEAT'
        multiply = nodes.new('ShaderNodeMixRGB')
        multiply.blend_type = 'MULTIPLY'
        multiply.inputs[0].default_value = 1
        multiply.inputs[1].default_value = spec['color']
        links.new(coordinates.outputs['UV'], scale.inputs[0])
        links.new(scale.outputs['Vector'], texture.inputs['Vector'])
        links.new(texture.outputs['Color'], multiply.inputs[2])
        links.new(multiply.outputs['Color'], shader.inputs['Base Color'])
    materials[name] = result
    return result


def parent(path):
    if not path:
        return None
    if path not in parents:
        item = bpy.data.objects.new(path.rsplit('/', 1)[-1], None)
        scene.collection.objects.link(item)
        item['unity_path'] = path
        item.parent = parent(path.rsplit('/', 1)[0] if '/' in path else '')
        parents[path] = item
    return parents[path]


def unity_point(values):
    return values[0], -values[2], values[1]


triangles = 0
for part in data['parts']:
    coordinates = part['vertices']
    original_vertices = [unity_point(coordinates[i:i + 3]) for i in range(0, len(coordinates), 3)]
    # Unity has separate face vertices for crisp normals. Weld coincident points
    # into editable topology while preserving the original UV at each face corner.
    vertices = []
    lookup = {}
    remap = []
    for vertex in original_vertices:
        key = tuple(round(component, 7) for component in vertex)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(vertex)
        remap.append(lookup[key])
    faces = []
    face_uv_indices = []
    slots = []
    for slot, submesh in enumerate(part['submeshes']):
        indices = submesh['triangles']
        assert len(indices) % 3 == 0
        faces.extend(tuple(remap[index] for index in indices[i:i + 3]) for i in range(0, len(indices), 3))
        face_uv_indices.extend(indices)
        slots.extend([slot] * (len(indices) // 3))
    assert all(all(0 <= i < len(vertices) for i in face) for face in faces)
    name = part['path'].rsplit('/', 1)[-1]
    mesh = bpy.data.meshes.new(name + '_EditableMesh')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    assert not mesh.validate(clean_customdata=False), 'Export required geometry repairs: ' + part['path']
    for submesh in part['submeshes']:
        mesh.materials.append(material(submesh))
    for polygon, slot in zip(mesh.polygons, slots):
        polygon.material_index = slot
        polygon.use_smooth = False
    if len(part['uv']) == len(original_vertices) * 2:
        uv = mesh.uv_layers.new(name='AuthoredMetreUV')
        for loop in mesh.loops:
            index = face_uv_indices[loop.index] * 2
            uv.data[loop.index].uv = part['uv'][index:index + 2]
    item = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(item)
    item.parent = parent(part['path'].rsplit('/', 1)[0])
    item.location = unity_point(part['origin'])
    item['unity_path'] = part['path']
    item['source_mesh_export'] = source.name
    triangles += len(faces)

scene['source_sha256'] = hashlib.sha256(source.read_bytes()).hexdigest()
scene['evidence_scope'] = 'Exact editable mesh/material reconstruction; no render or Unity visual approval'
destination.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(destination))
receipt = {
    'source': str(source), 'source_sha256': scene['source_sha256'],
    'blend': str(destination), 'blender': bpy.app.version_string,
    'mesh_objects': len(data['parts']), 'triangles': triangles,
    'scope': scene['evidence_scope'], 'pending': ['Neutral asset views', 'Unity visual comparison', 'Gameplay traversal'],
}
destination.with_suffix('.receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
print(json.dumps(receipt))
