"""Prepared offline only. Requires final native evidence and coordinator's live MCP slot.
Never invoke via CLI/background Blender, and never execute while pending-adjustment.ready is false.
"""
import hashlib,json,sys
from pathlib import Path

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def digest(obj):return hashlib.sha256(json.dumps(obj,sort_keys=True).encode()).hexdigest()

def run(config_path,visible_mcp_slot_granted=False):
    cfg=json.loads(Path(config_path).read_text())
    assert cfg['ready'] is True and visible_mcp_slot_granted is True, 'Final apply receipt and coordinator visible MCP slot are required'
    assert cfg['finalNativeApplyReceipt'] and cfg['finalNativeApplySha256'] and isinstance(cfg['finalNativeAdjustmentKeyPath'],list)
    assert sha(cfg['finalNativeApplyReceipt'])==cfg['finalNativeApplySha256']
    applied=json.loads(Path(cfg['finalNativeApplyReceipt']).read_text(encoding='utf-8-sig'))
    native=applied
    for key in cfg['finalNativeAdjustmentKeyPath']:native=native[key]
    names=['YATE_DeckStorage_01','YATE_DeckStorage_Lid_01']
    assert cfg['objectNames']==names and set(p['name'] for p in native['parts'])==set(names)
    assert cfg['unityWorldDelta']==[-1.4,0,2] and cfg['blenderWorldDelta']==[-1.4,2,0]
    assert [native['worldDelta'][k] for k in 'xyz']==cfg['unityWorldDelta']
    for p in native['parts']:
        assert [p['before'][k] for k in 'xyz']==[0,0,0]
        assert [p['after'][k] for k in 'xyz']==cfg['unityWorldDelta']
    assert isinstance(cfg['finalNativeBulkheadKeyPath'],list), 'Storage-only copy is not authorized: include final bulkhead correction'
    native_wall=applied
    for key in cfg['finalNativeBulkheadKeyPath']:native_wall=native_wall[key]
    cut=cfg['bulkhead']
    assert native_wall['objectName']==cut['objectName']=='YATE_Lower_EndBulkhead_-11.6'
    assert abs(native_wall['openingMinX']-cut['openingMinX'])<1e-6
    assert abs(native_wall['openingMaxX']-cut['openingMaxX'])<1e-6
    assert abs(native_wall['remainingTopY']-cut['remainingTopBlenderZ'])<1e-6
    assert native_wall['renderAndColliderMatch'] and native_wall['materialReferencesPreserved']
    source=Path(cfg['sourceBlend']);out=Path(cfg['outputDirectory']);root=source.parents[2]
    assert sha(source)==cfg['sourceSha256']=='17b2a4196712dc55c2675b83ff0b2110886b142b9399b295f0df2565274c9769'
    assert out.resolve()==(source.parent.parent/'UnityAdjustedSource').resolve() and not out.exists()
    import bpy
    from mathutils import Vector
    assert bpy.context.mode=='OBJECT' and bpy.context.scene.name=='HF_MAP_05_pueblo'
    assert not bpy.app.is_job_running('RENDER')
    assert not sys.modules['bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface']._scene_conversation().busy()
    active_file=bpy.data.filepath;active_scene=bpy.context.scene
    assert Path(active_file).resolve()==(root/'05-pueblo/HF_MAP_05_pueblo.blend').resolve()
    scene=bpy.data.scenes['HF_MAP_04_yate'];audit=json.loads((source.parent/'scene-audit.json').read_text())
    assert set(scene.objects.keys())=={o['name'] for o in audit['objects']}
    for a in audit['objects']:
        o=scene.objects[a['name']]
        assert o.type==a['type'] and [list(row) for row in o.matrix_world]==a['matrix_world']
        assert (o.parent.name if o.parent else None)==a['parent']
        if o.type=='MESH':
            o.data.calc_loop_triangles();corners=[o.matrix_world@Vector(v) for v in o.bound_box]
            assert len(o.data.vertices)==a['vertices'] and len(o.data.loop_triangles)==a['triangles']
            assert [min(v[i] for v in corners) for i in range(3)]==a['bounds_min']
            assert [max(v[i] for v in corners) for i in range(3)]==a['bounds_max']
            assert [s.material.name for s in o.material_slots if s.material]==a['materials']
    water=scene.objects['Water_Ocean'];water.data.calc_loop_triangles()
    assert len(water.data.loop_triangles)==32000
    for t in water.data.loop_triangles:
        a,b,c=[water.matrix_world@water.data.vertices[i].co for i in t.vertices]
        assert (b-a).cross(c-a).z>0
    parts=[scene.objects[n] for n in names]
    for o in parts:
        assert o.type=='MESH' and o.parent is None and not o.constraints and o.animation_data is None
        assert tuple(o.users_scene)==(scene,) and not o.modifiers and list(o.matrix_world.translation)==[0,0,0]
    wall=scene.objects[cut['objectName']];original_wall=wall.data
    assert wall.type=='MESH' and wall.parent is None and not wall.constraints and wall.animation_data is None and not wall.modifiers
    assert tuple(wall.users_scene)==(scene,) and original_wall.users==1 and len(original_wall.vertices)==8 and not original_wall.shape_keys
    assert len(wall.material_slots)==1 and wall.material_slots[0].material.name=='YATE_Cream'
    def mesh_state(m):return digest(dict(vertices=[list(v.co) for v in m.vertices],polygons=[(list(p.vertices),p.material_index,p.use_smooth) for p in m.polygons],uvLayers=[(layer.name,[list(loop.uv) for loop in layer.data]) for layer in m.uv_layers],keys=[(k.name,k.value,[list(v.co) for v in k.data]) for k in m.shape_keys.key_blocks] if m.shape_keys else []))
    def object_state(o):return digest(dict(matrix=[list(r) for r in o.matrix_world],parent=o.parent.name if o.parent else None,data=o.data.name if o.data else None,materials=[(s.link,s.material.name if s.material else None) for s in o.material_slots] if o.type=='MESH' else []))
    objects={o.name:object_state(o) for o in bpy.data.objects};meshes={m.name:mesh_state(m) for m in bpy.data.meshes}
    members={s.name:[o.name for o in s.objects] for s in bpy.data.scenes};matrices={o.name:o.matrix_world.copy() for o in parts}
    preserved={str(p):sha(p) for p in source.parent.iterdir() if p.is_file()};preserved[active_file]=sha(active_file)
    output=out/'HF_MAP_04_yate_UNITY_ADJUSTED.blend';saved=[];replacement=None;wall_hash=None
    out.mkdir()
    try:
        for o in parts:
            matrix=matrices[o.name].copy();matrix.translation+=Vector(cfg['blenderWorldDelta']);o.matrix_world=matrix
        assert all(object_state(o)==objects[o.name] for o in bpy.data.objects if o not in parts)
        assert all(mesh_state(m)==meshes[m.name] for m in bpy.data.meshes)
        replacement=notched_mesh(bpy,original_wall,cut)
        wall.data=replacement;wall_hash=mesh_state(replacement)
        assert all(object_state(o)==objects[o.name] for o in bpy.data.objects if o not in parts and o!=wall)
        bpy.data.libraries.write(str(output),{scene},path_remap='RELATIVE_ALL',fake_user=False,compress=False)
    finally:
        for o in parts:o.matrix_world=matrices[o.name]
        wall.data=original_wall
        if replacement is not None:
            assert replacement.users==0;bpy.data.meshes.remove(replacement)
    # Read back only the three changed objects. Never open or link another scene.
    categories=['objects','meshes','materials','node_groups','images']
    ids={key:set(getattr(bpy.data,key)) for key in categories}
    try:
        with bpy.data.libraries.load(str(output),link=False) as (available,requested):
            assert available.scenes==['HF_MAP_04_yate'] and all(n in available.objects for n in names+[wall.name])
            requested.objects=names+[wall.name]
        for original,loaded in zip(parts,requested.objects[:2]):
            assert loaded is not None and loaded!=original and loaded.parent is None and not loaded.constraints and loaded.animation_data is None
            assert mesh_state(loaded.data)==meshes[original.data.name]
            matrix=loaded.matrix_basis
            assert all(abs(a-b)<1e-6 for a,b in zip(matrix.translation,cfg['blenderWorldDelta']))
            assert all(abs(matrix[i][j]-matrices[original.name][i][j])<1e-7 for i in range(3) for j in range(3))
            saved.append(dict(name=original.name,blenderWorldXYZ=list(matrix.translation),meshGeometrySha256=mesh_state(loaded.data)))
        saved_wall=requested.objects[2]
        assert saved_wall.parent is None and not saved_wall.constraints and not saved_wall.modifiers and saved_wall.animation_data is None
        assert mesh_state(saved_wall.data)==wall_hash and [list(r) for r in saved_wall.matrix_basis]==[list(r) for r in wall.matrix_world]
        saved.append(dict(name=wall.name,meshGeometrySha256=wall_hash,vertices=len(saved_wall.data.vertices),polygons=len(saved_wall.data.polygons),notch=cut))
    finally:
        for key in categories:
            collection=getattr(bpy.data,key)
            for block in set(collection)-ids[key]:
                if key=='objects':collection.remove(block,do_unlink=True)
                else:
                    assert block.users==0, ('Unexpected dependent ID still in use',key,block.name)
                    collection.remove(block)
    assert all(set(getattr(bpy.data,key))==ids[key] for key in categories)
    assert {o.name:object_state(o) for o in bpy.data.objects}==objects
    assert {m.name:mesh_state(m) for m in bpy.data.meshes}==meshes
    assert {s.name:[o.name for o in s.objects] for s in bpy.data.scenes}==members
    assert bpy.context.scene==active_scene and bpy.data.filepath==active_file
    assert all(sha(p)==h for p,h in preserved.items())
    receipt=dict(status='PASS_EXCLUSIVE_YATE_COPY_STORAGE_AND_BULKHEAD_READBACK_LIVE_RESTORED',source=str(output),sourceSha256=sha(output),parts=saved,
        preservedFiles=preserved,nativeApplyReceipt=cfg['finalNativeApplyReceipt'],nativeApplySha256=cfg['finalNativeApplySha256'],
        restoredScene=active_scene.name,restoredLiveMatrices={n:[list(r) for r in m] for n,m in matrices.items()},
        newExportsOrUnityImport=False,scope='Confirmed storage translation and bulkhead notch synchronized; no claim of whole-engine portable equivalence.')
    (out/'adjustment-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    return receipt

def notched_mesh(bpy,source,cut):
    """Extrude the exact eight-point notched profile; interpolate exterior loop UVs."""
    from collections import Counter
    from mathutils import Vector
    source.calc_loop_triangles();lo=[min(v.co[i] for v in source.vertices) for i in range(3)];hi=[max(v.co[i] for v in source.vertices) for i in range(3)]
    assert all(abs(a-b)<1e-5 for a,b in zip(lo,[-3.2,-11.67,.8])) and all(abs(a-b)<1e-5 for a,b in zip(hi,[3.2,-11.53,3.18]))
    left,right,top=cut['openingMinX'],cut['openingMaxX'],cut['remainingTopBlenderZ']
    profile=[(lo[0],lo[2]),(hi[0],lo[2]),(hi[0],hi[2]),(right,hi[2]),(right,top),(left,top),(left,hi[2]),(lo[0],hi[2])]
    vertices=[(x,y,z) for y in [lo[1],hi[1]] for x,z in profile]
    faces=[list(range(8)),list(reversed(range(8,16)))]+[[i,i+8,(i+1)%8+8,(i+1)%8] for i in range(8)]
    mesh=bpy.data.meshes.new(source.name+'_UNITY_ADJUSTED')
    try:
        mesh.from_pydata(vertices,[],faces);mesh.update()
        for material in source.materials:mesh.materials.append(material)
        for polygon in mesh.polygons:polygon.material_index=0;polygon.use_smooth=False
        mesh.calc_loop_triangles();assert len(mesh.loop_triangles)==28
        edges=Counter(tuple(sorted((p.vertices[i],p.vertices[(i+1)%len(p.vertices)]))) for p in mesh.polygons for i in range(len(p.vertices)))
        assert set(edges.values())=={2}
        volume=sum(mesh.vertices[t.vertices[0]].co.dot(mesh.vertices[t.vertices[1]].co.cross(mesh.vertices[t.vertices[2]].co))/6 for t in mesh.loop_triangles)
        expected=(hi[0]-lo[0])*(hi[1]-lo[1])*(hi[2]-lo[2])-(right-left)*(hi[2]-top)*(hi[1]-lo[1])
        assert abs(volume-expected)<1e-5 and volume>0
        for layer in source.uv_layers:
            target=mesh.uv_layers.new(name=layer.name)
            for polygon in mesh.polygons:
                for loop in polygon.loop_indices:
                    p=mesh.vertices[mesh.loops[loop].vertex_index].co;uv=None
                    for t in source.loop_triangles:
                        a,b,c=[source.vertices[i].co for i in t.vertices];ab=b-a;ac=c-a;normal=ab.cross(ac)
                        if normal.length<1e-10 or normal.normalized().dot(polygon.normal)<.999 or abs((p-a).dot(normal.normalized()))>1e-5:continue
                        aa=ab.dot(ab);bb=ab.dot(ac);cc=ac.dot(ac);pa=(p-a).dot(ab);pc=(p-a).dot(ac);det=aa*cc-bb*bb
                        if abs(det)<1e-12:continue
                        v=(cc*pa-bb*pc)/det;w=(aa*pc-bb*pa)/det;u=1-v-w
                        if min(u,v,w)>=-.0001:
                            uv=layer.data[t.loops[0]].uv*u+layer.data[t.loops[1]].uv*v+layer.data[t.loops[2]].uv*w;break
                    target.data[loop].uv=uv if uv is not None else (p.x,p.z)
        return mesh
    except Exception:
        assert mesh.users==0;bpy.data.meshes.remove(mesh);raise

# The MCP caller must explicitly call run with the granted slot after the config is finalized.
