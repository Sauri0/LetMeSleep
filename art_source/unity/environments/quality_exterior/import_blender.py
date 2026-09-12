"""Run only in a Director-authorized Blender slot; reconstruct exact Unity meshes."""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

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
bpy.context.view_layer.update()
source_meshes={m['name']:m for m in data['meshes']}
max_error=0
for spec in data['instances']:
    obj=bpy.data.objects[spec['name']]
    vertices=source_meshes[spec['mesh']]['vertices']
    for index in (0,len(vertices)//2,len(vertices)-1):
        v=vertices[index];p=spec['position'];s=spec['scale'];angle=math.radians(spec['yaw'])
        x,y,z=(v[k]*s[k] for k in ('x','y','z'))
        expected=Vector((p['x']+x*math.cos(angle)+z*math.sin(angle),
                         -(p['z']-x*math.sin(angle)+z*math.cos(angle)),p['y']+y))
        actual=obj.matrix_world@obj.data.vertices[index].co
        max_error=max(max_error,(actual-expected).length)
assert max_error<.00002, ('Blender/Unity transform mismatch',max_error)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'ExteriorWitnessKit.blend'))
print('LMS_EXTERIOR_BLEND_SAVED', len(meshes), len(data['instances']), 'max_world_vertex_error',max_error,flush=True)

# Optional shape-only inspection, only with an explicitly authorized CPU render slot.
# Helpers are created after saving, so the editable kit stays free of preview props.
if '--preview-tree' in sys.argv:
    for obj in bpy.context.scene.objects:
        obj.hide_render=obj.name!='Patio_Pine_W_Replacement'
    bpy.ops.mesh.primitive_plane_add(size=200,location=(2,-16.5,-.003))
    floor=bpy.context.object
    floor.data.materials.append(materials['Lawn'])
    camera_data=bpy.data.cameras.new('ShapeReviewCamera')
    camera=bpy.data.objects.new('ShapeReviewCamera',camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location=(6,-21,3.15)
    camera.rotation_euler=(Vector((2,-16.5,1.68))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO';camera_data.ortho_scale=4.05
    bpy.context.scene.camera=camera
    for name,position,power,size,color in (
        ('Key',(0,-20,6),750,4,(1,.89,.72)),('Fill',(6,-15,4),420,5,(.60,.73,1))):
        light_data=bpy.data.lights.new(name,'AREA');light_data.energy=power;light_data.shape='DISK';light_data.size=size;light_data.color=color
        light=bpy.data.objects.new(name,light_data);bpy.context.scene.collection.objects.link(light);light.location=position
        light.rotation_euler=(Vector((2,-16.5,1.6))-light.location).to_track_quat('-Z','Y').to_euler()
    world=bpy.data.worlds.new('ShapeReviewWorld');world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.20,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.35
    bpy.context.scene.world=world
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16
    scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.filepath=str(HERE/'.verification/tree-volume-exterior2.png')
    bpy.ops.render.render(write_still=True)
    print('LMS_EXTERIOR_TREE_SHAPE_PREVIEW_SAVED',scene.render.filepath,flush=True)
