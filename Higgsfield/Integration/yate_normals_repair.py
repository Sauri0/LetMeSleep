"""Execute ONLY through the granted visible Blender MCP slot. Never launch a new Blender."""
import bpy
import contextlib
import hashlib
import json
import sys
from pathlib import Path
from collections import Counter
from mathutils import Vector

ROOT=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas')
FOLDER=ROOT/'04-yate';BACKUP=FOLDER/'SourceBeforeNormals';OUT=FOLDER/'NormalsV3'
assert not OUT.exists(), 'Never overwrite a repair attempt'
assert bpy.context.mode=='OBJECT' and bpy.context.scene.name=='HF_MAP_05_pueblo'
assert not bpy.app.is_job_running('RENDER')
composer=sys.modules['bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface']
assert not composer._scene_conversation().busy(), 'Scene Builder must be idle'
window=bpy.context.window;previous_scene=window.scene;previous_layer=window.view_layer
previous_active=previous_layer.objects.active;previous_selected=list(bpy.context.selected_objects)
previous_file=bpy.data.filepath
assert Path(previous_file).resolve()==(ROOT/'05-pueblo/HF_MAP_05_pueblo.blend').resolve()
puerto_hash=hashlib.sha256(Path(previous_file).read_bytes()).hexdigest()
preservation=json.loads((BACKUP/'preservation-manifest.json').read_text(encoding='utf-8-sig'))
for item in preservation:
    assert hashlib.sha256((BACKUP/item['file']).read_bytes()).hexdigest()==item['sha256']
    assert hashlib.sha256((FOLDER/item['file']).read_bytes()).hexdigest()==item['sha256'], 'Yate source changed since preservation'
scene=bpy.data.scenes['HF_MAP_04_yate'];ocean=scene.objects['Water_Ocean'];mesh=ocean.data
assert mesh.users==1 and tuple(ocean.users_scene)==(scene,)
assert not mesh.has_custom_normals and not ocean.modifiers
assert [k.name for k in mesh.shape_keys.key_blocks]==['Basis','Wave_A','Wave_B']
assert len(mesh.vertices)==16160 and len(mesh.polygons)==32000
audit_before=json.loads((BACKUP/'scene-audit.json').read_text())
assert {o.name for o in scene.objects}=={o['name'] for o in audit_before['objects']}

def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True).encode()).hexdigest()
def mesh_state(m,winding=True):
    return digest(dict(vertices=[list(v.co) for v in m.vertices],
        polygons=[(list(p.vertices) if winding else sorted(p.vertices),p.material_index,p.use_smooth) for p in m.polygons],
        materials=[x.name if x else None for x in m.materials],
        keys=[dict(name=k.name,value=k.value,relative=k.relative_key.name,coords=[list(v.co) for v in k.data]) for k in m.shape_keys.key_blocks] if m.shape_keys else []))
def scene_state(s):
    return digest(dict(frame=s.frame_current,objects=[dict(name=o.name,type=o.type,parent=o.parent.name if o.parent else None,
        data=o.data.name if o.data else None,matrix=[list(row) for row in o.matrix_world],
        slots=[(m.link,m.material.name if m.material else None) for m in o.material_slots] if o.type=='MESH' else []) for o in s.objects]))
def directions():
    mesh.calc_loop_triangles();counts=Counter()
    for tri in mesh.loop_triangles:
        a,b,c=(ocean.matrix_world@mesh.vertices[i].co for i in tri.vertices);n=(b-a).cross(c-a).normalized()
        counts['up' if n.z>.1 else 'down' if n.z<-.1 else 'side']+=1
    return dict(counts)
def audit_scene():
    items=[]
    for o in scene.objects:
        assert not o.get('export_exclude',False), 'Yate has no approved exclusions'
        item=dict(name=o.name,type=o.type,location=list(o.matrix_world.translation),scale=list(o.scale),parent=o.parent.name if o.parent else None,
            collections=[c.name for c in o.users_collection],matrix_world=[list(row) for row in o.matrix_world],
            properties={k:o[k] for k in o.keys() if isinstance(o[k],(str,int,float,bool))})
        if o.type=='CAMERA':item['camera']=dict(lens=o.data.lens,type=o.data.type,ortho_scale=o.data.ortho_scale)
        if o.type=='LIGHT':item['light']=dict(type=o.data.type,color=list(o.data.color),energy=o.data.energy)
        if o.type=='MESH':
            o.data.calc_loop_triangles();corners=[o.matrix_world@Vector(v) for v in o.bound_box]
            item.update(vertices=len(o.data.vertices),triangles=len(o.data.loop_triangles),bounds_min=[min(v[i] for v in corners) for i in range(3)],
                bounds_max=[max(v[i] for v in corners) for i in range(3)],materials=[m.material.name for m in o.material_slots if m.material],
                shape_keys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [])
        items.append(item)
    materials=[]
    for name in sorted({m for item in items for m in item.get('materials',[])}):
        m=bpy.data.materials[name];bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        materials.append(dict(name=name,diffuse=list(m.diffuse_color),base_color=list(bsdf.inputs['Base Color'].default_value),
            alpha=float(bsdf.inputs['Alpha'].default_value),emission_color=list(bsdf.inputs['Emission Color'].default_value),
            emission_strength=float(bsdf.inputs['Emission Strength'].default_value)))
    return dict(scene=scene.name,file=str(OUT/'HF_MAP_04_yate_NORMALS_V3.blend'),source_object_count=len(scene.objects),excluded_objects=[],
        objects=items,materials=materials,fps=scene.render.fps,frame_start=scene.frame_start,frame_end=scene.frame_end,
        camera=scene.camera.name if scene.camera else None,triangles=sum(x.get('triangles',0) for x in items),mesh_count=sum(x['type']=='MESH' for x in items))

# Confirm the scene in the currently open Puerto file is the same authored Yate export, before editing.
live_audit=audit_scene()
assert {x['name']:x for x in live_audit['objects']}=={x['name']:x for x in audit_before['objects']}, 'Visible Yate differs from preserved export audit'
assert live_audit['materials']==audit_before['materials']
before=directions();assert before=={'down':32000}
mesh_before={m.name:mesh_state(m) for m in bpy.data.meshes}
scene_before={s.name:scene_state(s) for s in bpy.data.scenes}
water_shape_before=mesh_state(mesh,False)
OUT.mkdir()
proof=dict(status='REPAIR_STARTED',blender=bpy.app.version_string,object='Water_Ocean',mesh=mesh.name,flippedPolygons=32000,before=before,
           sourceFileUnmodified=str(FOLDER/'HF_MAP_04_yate.blend'),puertoFile=previous_file,puertoFileBeforeSha256=puerto_hash)
(OUT/'normals-repair.json').write_text(json.dumps(proof,indent=2))
target_selected=None;target_active=None;originals=[];copies=[]
try:
    for poly in mesh.polygons:poly.flip()
    mesh.update();assert directions()=={'up':32000}
    assert mesh_state(mesh,False)==water_shape_before
    assert all(mesh_state(m)==mesh_before[m.name] for m in bpy.data.meshes if m!=mesh)
    assert all(scene_state(s)==scene_before[s.name] for s in bpy.data.scenes)
    window.scene=scene
    bpy.context.view_layer.update()
    target_selected=list(bpy.context.selected_objects);target_active=bpy.context.view_layer.objects.active
    # Write only this scene and its dependencies; do not save or change the active Puerto filepath.
    bpy.data.libraries.write(str(OUT/'HF_MAP_04_yate_NORMALS_V3.blend'),{scene},path_remap='RELATIVE_ALL',fake_user=False,compress=False)
    assert bpy.data.filepath==previous_file
    with bpy.data.libraries.load(str(OUT/'HF_MAP_04_yate_NORMALS_V3.blend'),link=False) as (available,requested):
        assert available.scenes==['HF_MAP_04_yate'], available.scenes
        saved_scene_names=list(available.scenes)
    owned=list(scene.objects);assert len(owned)==213
    assert not [(o.name,m.name) for o in owned if o.type=='MESH' for m in o.modifiers if m.show_render]
    for o in owned:
        if o.type=='MESH':
            slots=[(s.link,s.material) for s in o.material_slots];originals.append((o,o.data,slots))
            dup=o.data.copy();copies.append(dup);o.data=dup
            for slot,(_,material) in zip(o.material_slots,slots):slot.link='DATA';slot.material=material
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in owned:o.select_set(True)
    assert set(bpy.context.selected_objects)==set(owned)
    bpy.context.view_layer.objects.active=ocean
    with (OUT/'fbx-export.log').open('w') as log,contextlib.redirect_stdout(log),contextlib.redirect_stderr(log):
        bpy.ops.export_scene.fbx(filepath=str(OUT/'HF_MAP_04_yate_UNITY_V3.fbx'),use_selection=True,object_types={'MESH','EMPTY','CAMERA','LIGHT'},
            global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,axis_forward='-Z',axis_up='Y',
            use_mesh_modifiers=False,mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False,use_custom_props=True)
    for o,data,slots in originals:
        o.data=data
        for slot,(link,material) in zip(o.material_slots,slots):slot.link=link;slot.material=material
    originals=[]
    for m in copies:
        assert m.users==0;bpy.data.meshes.remove(m)
    copies=[]
    with (OUT/'glb-export.log').open('w') as log,contextlib.redirect_stdout(log),contextlib.redirect_stderr(log):
        bpy.ops.export_scene.gltf(filepath=str(OUT/'HF_MAP_04_yate_UNITY_V3.glb'),export_format='GLB',use_selection=True,use_active_scene=True,
            export_animations=False,export_morph=True,export_morph_animation=False,export_extras=True,export_cameras=True,export_lights=True,export_apply=False)
    audit=audit_scene()
    assert {x['name']:x for x in audit['objects']}=={x['name']:x for x in audit_before['objects']}
    assert audit['materials']==audit_before['materials']
    (OUT/'scene-audit.json').write_text(json.dumps(audit,indent=2))
    previous_report=json.loads((BACKUP/'HF_MAP_04_yate_report.json').read_text())
    report=dict(map=scene.name,scene=scene.name,revision='v3 single-object ocean winding correction',counts=previous_report['counts'],
        dimensions_m=previous_report['dimensions_m'],water=previous_report['water'],previous_report=str(BACKUP/'HF_MAP_04_yate_report.json'),
        source=str(OUT/'HF_MAP_04_yate_NORMALS_V3.blend'),normals_correction='Only Water_Ocean polygon winding reversed; no vertices/morphs/materials moved',
        exports={ext:str(OUT/('HF_MAP_04_yate_UNITY_V3.'+ext)) for ext in ('fbx','glb')},nativeReview='Pending Unity v3 culling and GPU water checks')
    (OUT/'HF_MAP_04_yate_report.json').write_text(json.dumps(report,indent=2))
    assert set(m.name for m in bpy.data.meshes)==set(mesh_before)
    assert all(mesh_state(m)==mesh_before[m.name] for m in bpy.data.meshes if m!=mesh)
    assert mesh_state(mesh,False)==water_shape_before and directions()=={'up':32000}
    assert all(scene_state(s)==scene_before[s.name] for s in bpy.data.scenes)
    proof.update(status='PASS_SINGLE_OBJECT_WINDING_SAVED_AND_EXPORTED',after=directions(),savedScenes=saved_scene_names,
        invariants=['All mesh coordinates, morph coordinates/weights, material slots, polygon membership/count/smoothing preserved',
                    'All other meshes and all scene object transforms/parents/frame numbers preserved','Live Yate audit objects/materials equal preserved source audit',
                    'Standalone blend contains only HF_MAP_04_yate scene plus dependencies','Original Yate files and Puerto file never saved over'])
finally:
    for o,data,slots in originals:
        o.data=data
        for slot,(link,material) in zip(o.material_slots,slots):slot.link=link;slot.material=material
    for m in copies:
        if m.users==0:bpy.data.meshes.remove(m)
    if window.scene==scene and target_selected is not None:
        for o in bpy.context.selected_objects:o.select_set(False)
        for o in target_selected:o.select_set(True)
        bpy.context.view_layer.objects.active=target_active
    window.scene=previous_scene;window.view_layer=previous_layer
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in previous_selected:o.select_set(True)
    previous_layer.objects.active=previous_active
    assert bpy.context.scene==previous_scene and bpy.data.filepath==previous_file
    assert hashlib.sha256(Path(previous_file).read_bytes()).hexdigest()==puerto_hash
    proof.update(restoredActiveScene=bpy.context.scene.name,restoredActiveFile=bpy.data.filepath,puertoFileAfterSha256=puerto_hash)
    proof['files']={p.name:dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in OUT.iterdir() if p.name!='normals-repair.json'}
    (OUT/'normals-repair.json').write_text(json.dumps(proof,indent=2))
result={k:proof[k] for k in ('status','before','after','flippedPolygons','restoredActiveScene','puertoFileAfterSha256','savedScenes')}
