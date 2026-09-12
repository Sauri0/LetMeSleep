"""Run only in a Director-authorized Blender slot; reconstruct exact Unity meshes."""
import bpy
import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
data = json.loads((HERE/'generated_exterior.json').read_text(encoding='utf-8'))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
materials = {}
for spec in data['materials']:
    material = bpy.data.materials.new('Exterior_'+spec['name'])
    color = tuple(spec['color'][k] for k in ('r','g','b','a'))
    material.diffuse_color = color
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = .87
    materials[spec['name']] = material

meshes = {}
for spec in data['meshes']:
    mesh = bpy.data.meshes.new(spec['name'])
    # Proper rotation, determinant +1: Unity Y-up to Blender Z-up.
    vertices = [(v['x'], -v['z'], v['y']) for v in spec['vertices']]
    faces, slots = [], []
    for slot, sub in enumerate(spec['submeshes']):
        indices = sub['triangles']
        faces.extend(tuple(indices[i:i+3]) for i in range(0,len(indices),3))
        slots.extend([slot]*(len(indices)//3))
        mesh.materials.append(materials[sub['material']])
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for polygon, slot in zip(mesh.polygons, slots): polygon.material_index = slot
    meshes[spec['name']] = mesh

zones = {}
for spec in data['instances']:
    zone = spec['zone']
    if zone not in zones:
        collection = bpy.data.collections.new(zone)
        bpy.context.scene.collection.children.link(collection)
        zones[zone] = collection
    obj = bpy.data.objects.new(spec['name'], meshes[spec['mesh']])
    zones[zone].objects.link(obj)
    p,s = spec['position'],spec['scale']
    obj.location = (p['x'],-p['z'],p['y'])
    obj.scale = (s['x'],s['z'],s['y'])
    # +90 Unity yaw maps local +Z to +X: Blender local -Y to +X, hence +Z rotation.
    obj.rotation_euler.z = math.radians(spec['yaw'])
    obj['unity_zone'] = zone
    obj['collision'] = 'visual only; preserve existing Unity colliders'

bpy.context.scene['source'] = 'build_exterior.py -> generated_exterior.json; exact Unity mesh triangles'
bpy.context.scene['art_approval'] = 'PENDING native Unity view review'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'ExteriorWitnessKit.blend'))
print('LMS_EXTERIOR_BLEND_SAVED', len(meshes), len(data['instances']), flush=True)
