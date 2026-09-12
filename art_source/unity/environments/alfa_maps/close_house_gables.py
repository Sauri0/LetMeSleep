"""Add only the missing solid attic end walls to the existing house source."""
import bpy, bmesh, json
from pathlib import Path

HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'house_alfa_static.blend'))
root=bpy.data.objects['HouseAlfaSource']
manifest=json.loads((HERE/'source_manifest.json').read_text(encoding='utf-8-sig'))
house=manifest['house_alfa_static']
left=6.4-(8.04-6)*6.65/2.2
section=[(left,6),(12.8-left,6),(6.4,8.04)]
faces=[(2,1,0),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)]
for label,z0,z1 in (('Front',0,.18),('Back',11.22,11.4)):
    name='Gable_'+label
    assert name not in bpy.data.objects, 'Gable already exists; do not duplicate it'
    vertices=[(x,-z,y) for z in (z0,z1) for x,y in section]
    data=bpy.data.meshes.new(name+'_Mesh');data.from_pydata(vertices,[],faces);data.materials.append(bpy.data.materials['Plaster_Warm']);data.update()
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    assert all(edge.is_manifold for edge in bm.edges), 'Gable must be a closed solid'
    assert bm.calc_volume(signed=True)>0, 'Gable normals must enclose positive volume'
    bm.to_mesh(data);bm.free()
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.parent=root
    data.calc_loop_triangles()
    house['renderers'].append(dict(node=name,parent=root.name,materials=['Plaster_Warm']))
    house['mesh_colliders'].append(dict(node=name,surface='Stone'))
    house['triangles']+=len(data.loop_triangles)
    print('LMS_GABLE '+name+' triangles='+str(len(data.loop_triangles))+' thickness='+str(z1-z0),flush=True)
bpy.context.view_layer.update();bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'house_alfa_static.blend'))
bpy.ops.export_scene.fbx(filepath=str(HERE/'house_alfa_static.fbx'),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=False,add_leaf_bones=False)
(HERE/'source_manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
