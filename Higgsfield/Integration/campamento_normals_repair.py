"""One-shot, hash-pinned Campamento winding repair. Run only headless with autoexec disabled."""
import bpy
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path
from mathutils import Vector

FOLDER = Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento')
BACKUP = FOLDER / 'SourceBeforeNormals'
OUTPUT = FOLDER / 'NormalsV2'
SOURCE = FOLDER / 'HF_MAP_03_campamento.blend'
manifest = json.loads((BACKUP/'preservation-manifest.json').read_text(encoding='utf-8-sig'))
for item in manifest:
    assert hashlib.sha256((BACKUP/item['file']).read_bytes()).hexdigest() == item['sha256']
source_hash = next(x['sha256'] for x in manifest if x['file'] == SOURCE.name)
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == source_hash, 'Source changed after preservation'
assert not OUTPUT.exists(), 'Never overwrite an existing v2 export attempt'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE), load_ui=False)
scene = bpy.data.scenes['HF_MAP_03_campamento']
bpy.context.window.scene = scene

def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()

def mesh_state(mesh, winding=True):
    result = dict(vertices=[list(v.co) for v in mesh.vertices],
                  polygons=[(list(p.vertices) if winding else sorted(p.vertices), p.material_index, p.use_smooth) for p in mesh.polygons],
                  materials=[m.name if m else None for m in mesh.materials],
                  keys=[dict(name=k.name, value=k.value, relative=k.relative_key.name, coords=[list(v.co) for v in k.data]) for k in mesh.shape_keys.key_blocks] if mesh.shape_keys else [])
    return digest(result)

def scene_state(s):
    return digest([dict(name=o.name, type=o.type, parent=o.parent.name if o.parent else None,
                        matrix=[list(row) for row in o.matrix_world], data=o.data.name if o.data else None,
                        slots=[(m.link, m.material.name if m.material else None) for m in o.material_slots] if o.type=='MESH' else [])
                   for o in s.objects])

def tri_state(obj):
    mesh=obj.data;mesh.calc_loop_triangles()
    points=[obj.matrix_world @ v.co for v in mesh.vertices]
    center=sum(points,Vector())/len(points)
    dirs=Counter();poly_dirs=defaultdict(list);volume=0.
    for t in mesh.loop_triangles:
        a,b,c=(points[i] for i in t.vertices)
        n=(b-a).cross(c-a).normalized()
        key='up' if n.z>.1 else 'down' if n.z<-.1 else 'side'
        dirs[key]+=1;poly_dirs[t.polygon_index].append(key)
        volume+=(a-center).dot((b-center).cross(c-center))/6
    return dict(triangles=len(mesh.loop_triangles), directions=dict(dirs), signedWorldVolume=volume),poly_dirs

upward_names = ['CAMP_Terrain_Grass','CAMP_Lake_Bed','Water_Creek','Water_Creek_Deep','Water_Creek_Shallow',
                'Water_Lake','Water_Lake_Deep','Water_Lake_Shallow','CAMP_Prod_LakeCenter','CAMP_Prod_LakeSeam',
                'CAMP_Shelter_Roof_R']
board_name='CAMP_Prod_ChoppingBoard'
plans=[]
for name in upward_names+[board_name]:
    obj=scene.objects[name];mesh=obj.data
    assert mesh.users==1 and tuple(obj.users_scene)==(scene,), 'Shared geometry requires separate scope review'
    assert not mesh.has_custom_normals, 'Custom normals require explicit separate handling'
    assert not obj.modifiers, 'Do not change evaluated topology'
    before,poly_dirs=tri_state(obj)
    if name==board_name:
        edges=Counter((a,b) for p in mesh.polygons for a,b in zip(list(p.vertices),list(p.vertices)[1:]+list(p.vertices)[:1]))
        assert all(count==1 and edges[(b,a)]==1 for (a,b),count in edges.items()), 'Board is not consistently oriented closed manifold'
        assert before['signedWorldVolume'] < -1e-6
        ids=list(range(len(mesh.polygons)))
    else:
        assert all(set(v) in ({'up'},{'down'}) for v in poly_dirs.values()), 'Mixed triangles within a polygon require geometry review'
        ids=[i for i,v in poly_dirs.items() if set(v)=={'down'}]
        assert ids and (name=='CAMP_Prod_LakeCenter' or len(ids)==len(mesh.polygons))
    plans.append((obj,ids,before))

mesh_before={m.name:mesh_state(m) for m in bpy.data.meshes}
scene_before={s.name:scene_state(s) for s in bpy.data.scenes}
unchanged_geometry={obj.name:mesh_state(obj.data,False) for obj,_,_ in plans}
changes=[]
for obj,ids,before in plans:
    for i in ids:obj.data.polygons[i].flip()
    obj.data.update()
    after,_=tri_state(obj)
    assert before['triangles']==after['triangles']
    if obj.name!=board_name:assert after['directions']=={'up':after['triangles']}
    else:assert after['signedWorldVolume']>0
    assert mesh_state(obj.data,False)==unchanged_geometry[obj.name], 'Non-winding geometry changed'
    changes.append(dict(object=obj.name,mesh=obj.data.name,flippedPolygonIndices=ids,before=before,after=after))
changed_meshes={obj.data.name for obj,_,_ in plans}
assert all(mesh_state(m)==mesh_before[m.name] for m in bpy.data.meshes if m.name not in changed_meshes)
assert all(scene_state(s)==scene_before[s.name] for s in bpy.data.scenes)
OUTPUT.mkdir()
proof=dict(status='PASS_WINDING_ONLY_IN_MEMORY',blender=bpy.app.version_string,sourceBeforeSha256=source_hash,
           flippedObjects=len(changes),flippedPolygons=sum(len(c['flippedPolygonIndices']) for c in changes),changes=changes,
           invariants=['All vertex and shape-key positions/weights unchanged','Polygon membership/count/material indices/smoothing unchanged',
                       'All object transforms/parents/material slots unchanged in every saved scene','All other mesh geometry unchanged, including preserved exclusions'],
           otherScenes=[s.name for s in bpy.data.scenes if s!=scene])
(OUTPUT/'normals-repair.json').write_text(json.dumps(proof,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),check_existing=False)

excluded=[o for o in scene.objects if o.get('export_exclude',False)]
assert all(o.type=='MESH' and o.hide_render for o in excluded)
owned=[o for o in scene.objects if o not in excluded]
assert len(owned)==1493 and sum(o.type=='MESH' for o in owned)==1436
assert not [(o.name,m.name) for o in owned if o.type=='MESH' for m in o.modifiers if m.show_render]
selected=list(bpy.context.selected_objects);active=bpy.context.view_layer.objects.active
originals=[];copies=[]
try:
    for obj in owned:
        if obj.type=='MESH':
            slots=[(slot.link,slot.material) for slot in obj.material_slots]
            originals.append((obj,obj.data,slots));dup=obj.data.copy();copies.append(dup);obj.data=dup
            for slot,(_,material) in zip(obj.material_slots,slots):slot.link='DATA';slot.material=material
    for obj in bpy.context.selected_objects:obj.select_set(False)
    for obj in owned:obj.select_set(True)
    assert set(bpy.context.selected_objects)==set(owned)
    bpy.context.view_layer.objects.active=next(o for o in owned if o.type=='MESH')
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'HF_MAP_03_campamento_UNITY_V2.fbx'),use_selection=True,
        object_types={'MESH','EMPTY','CAMERA','LIGHT'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,mesh_smooth_type='FACE',
        bake_anim=False,add_leaf_bones=False,use_custom_props=True)
finally:
    for obj,data,slots in originals:
        obj.data=data
        for slot,(link,material) in zip(obj.material_slots,slots):slot.link=link;slot.material=material
    for mesh in copies:
        if mesh.users==0:bpy.data.meshes.remove(mesh)
bpy.ops.export_scene.gltf(filepath=str(OUTPUT/'HF_MAP_03_campamento_UNITY_V2.glb'),export_format='GLB',
    use_selection=True,use_active_scene=True,export_animations=False,export_morph=True,export_morph_animation=False,
    export_extras=True,export_cameras=True,export_lights=True,export_apply=False)
for obj in bpy.context.selected_objects:obj.select_set(False)
for obj in selected:obj.select_set(True)
bpy.context.view_layer.objects.active=active
assert all(scene_state(s)==scene_before[s.name] for s in bpy.data.scenes)
assert all(mesh_state(m)==mesh_before[m.name] for m in bpy.data.meshes if m.name not in changed_meshes)
assert all(mesh_state(obj.data,False)==unchanged_geometry[obj.name] for obj,_,_ in plans)

items=[]
for obj in owned:
    item=dict(name=obj.name,type=obj.type,location=list(obj.matrix_world.translation),scale=list(obj.scale),
        parent=obj.parent.name if obj.parent else None,collections=[c.name for c in obj.users_collection],
        matrix_world=[list(row) for row in obj.matrix_world],properties={k:obj[k] for k in obj.keys() if isinstance(obj[k],(str,int,float,bool))})
    if obj.type=='CAMERA':item['camera']=dict(lens=obj.data.lens,type=obj.data.type,ortho_scale=obj.data.ortho_scale)
    if obj.type=='LIGHT':item['light']=dict(type=obj.data.type,color=list(obj.data.color),energy=obj.data.energy)
    if obj.type=='MESH':
        obj.data.calc_loop_triangles();corners=[obj.matrix_world @ Vector(v) for v in obj.bound_box]
        item.update(vertices=len(obj.data.vertices),triangles=len(obj.data.loop_triangles),bounds_min=[min(v[i] for v in corners) for i in range(3)],
            bounds_max=[max(v[i] for v in corners) for i in range(3)],materials=[s.material.name for s in obj.material_slots if s.material],
            shape_keys=[k.name for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else [])
    items.append(item)
materials=[]
for name in sorted({m for o in items for m in o.get('materials',[])}):
    mat=bpy.data.materials[name];bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    materials.append(dict(name=name,diffuse=list(mat.diffuse_color),base_color=list(bsdf.inputs['Base Color'].default_value),
        emission_color=list(bsdf.inputs['Emission Color'].default_value),emission_strength=float(bsdf.inputs['Emission Strength'].default_value)))
audit=dict(scene=scene.name,file=bpy.data.filepath,source_object_count=len(scene.objects),
    excluded_objects=[dict(name=o.name,type=o.type,reason='authored export_exclude=true, hide_render=true') for o in excluded],
    objects=items,materials=materials,fps=scene.render.fps,frame_start=scene.frame_start,frame_end=scene.frame_end,
    camera=scene.camera.name if scene.camera else None,triangles=sum(o.get('triangles',0) for o in items),mesh_count=1436)
assert audit['triangles']==76947 and len(materials)==40
(OUTPUT/'scene-audit.json').write_text(json.dumps(audit,indent=2))
previous=json.loads((BACKUP/'HF_MAP_03_campamento_report.json').read_text())
report=dict(scene=scene.name,revision='v2 winding-only repair of final Astra source',counts=previous['counts'],
    source=str(SOURCE),previous_report=str(BACKUP/'HF_MAP_03_campamento_report.json'),normals_repair=str(OUTPUT/'normals-repair.json'),
    exports={ext:str(OUTPUT/('HF_MAP_03_campamento_UNITY_V2.'+ext)) for ext in ('fbx','glb')},
    remaining_limitations=['Native Unity import, ground tests and visuals still required','No geometry added, no global double-sided material workaround'])
(OUTPUT/'HF_MAP_03_campamento_report.json').write_text(json.dumps(report,indent=2))
proof.update(status='PASS_WINDING_ONLY_SAVED_AND_EXPORTED_REQUIRES_NATIVE_REVIEW',sourceAfterSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    outputs={p.name:dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in OUTPUT.iterdir() if p.name!='normals-repair.json'})
(OUTPUT/'normals-repair.json').write_text(json.dumps(proof,indent=2))
print('REPAIR_COMPLETE',json.dumps({k:proof[k] for k in ('status','flippedObjects','flippedPolygons','sourceAfterSha256')}))
