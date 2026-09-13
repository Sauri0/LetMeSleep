"""Run only in the coordinator-authorized visible Blender MCP session. No imports/exports."""
import hashlib
import json
import math
import sys
from pathlib import Path

BASE = Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo')
NATIVE = Path('N:/LetMeSleep/Validation/Higgsfield/PuertoV1-20260913/applied-01/puerto-semantic-apply.json')
SOURCE_SHA = 'ae73c46d661bbaaa6248627988ee13296dc5476038b06e0f55aa413c3e61c32a'
CONTENT_SHA = '3be1751178d5df73b75b089c315e617b8935466bf7814866c502824db9787fa9'


def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def digest(value): return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()


def mesh_state(mesh):
    return digest(dict(vertices=[list(v.co) for v in mesh.vertices],
        polygons=[(list(p.vertices), p.material_index, p.use_smooth) for p in mesh.polygons],
        uvLayers=[(layer.name, [list(loop.uv) for loop in layer.data]) for layer in mesh.uv_layers],
        keys=[(k.name,k.value,[list(v.co) for v in k.data]) for k in mesh.shape_keys.key_blocks] if mesh.shape_keys else []))


def object_state(obj):
    return digest(dict(matrix=[list(r) for r in obj.matrix_world], parent=obj.parent.name if obj.parent else None,
        data=obj.data.name if obj.data else None,
        materials=[(s.link,s.material.name if s.material else None) for s in obj.material_slots] if obj.type=='MESH' else []))


def bevel_mesh(bpy, source, native):
    """Clip only the six quads of the second step; retain all other polygon coordinates."""
    from mathutils import Vector
    from collections import Counter
    A=Vector(tuple(native['edgeStart'][k] for k in ('x','z','y')))
    B=Vector(tuple(native['edgeEnd'][k] for k in ('x','z','y')))
    D=Vector(tuple(native['inward'][k] for k in ('x','z','y')))
    E=(B-A).normalized();depth=math.hypot(.275,.23375);amount=native['bevel']
    assert abs(D.length-1)<1e-6 and abs((B-A).length-2.5)<1e-5 and amount==.03
    selected=[]
    for p in source.polygons:
        points=[source.vertices[i].co for i in p.vertices]
        if all(-1e-4<=(v-A).dot(D)<=depth+1e-4 and -.3001<=v.z-A.z<=1e-4
               and -1e-4<=(v-A).dot(E)<=2.5001 for v in points):selected.append(p.index)
    assert selected==list(range(6,12)) and all(len(source.polygons[i].vertices)==4 for i in selected)
    selected_vertices={i for p in selected for i in source.polygons[p].vertices}
    assert selected_vertices==set(range(8,16))
    assert not any(selected_vertices.intersection(p.vertices) for p in source.polygons if p.index not in selected)
    assert len(source.uv_layers)==0 and not source.shape_keys
    assert {a.name for a in source.attributes}<={'.select_vert','.select_edge','.select_poly','sharp_face','material_index','position','.edge_verts','.corner_vert','.corner_edge'}
    distance=lambda v: v.z-A.z-(v-A).dot(D)+amount
    original=[list(v.co) for v in source.vertices]
    vertices=original[:];faces=[];settings=[];cap_points=[]
    def index(point):
        for i,v in enumerate(vertices):
            if (Vector(v)-point).length<1e-6:return i
        vertices.append(list(point));return len(vertices)-1
    for p in source.polygons:
        ids=list(p.vertices)
        if p.index in selected:
            points=[Vector(original[i]) for i in ids];clipped=[]
            for a,b in zip(points, points[1:]+points[:1]):
                da,db=distance(a),distance(b)
                if da<=0:clipped.append(a)
                if (da<=0)!=(db<=0):
                    q=a+(b-a)*(da/(da-db));clipped.append(q);cap_points.append(q)
            ids=[index(v) for v in clipped]
        assert len(ids)>=3
        faces.append(ids);settings.append((p.material_index,p.use_smooth))
    cap_ids=set(index(v) for v in cap_points);assert len(cap_ids)==4
    normal=Vector((-D.x,-D.y,1)).normalized()
    center=sum((Vector(vertices[i]) for i in cap_ids),Vector())/len(cap_ids)
    u=(Vector(vertices[min(cap_ids)])-center).normalized();v=normal.cross(u)
    cap=sorted(cap_ids,key=lambda i:math.atan2((Vector(vertices[i])-center).dot(v),(Vector(vertices[i])-center).dot(u)))
    assert (Vector(vertices[cap[1]])-Vector(vertices[cap[0]])).cross(Vector(vertices[cap[2]])-Vector(vertices[cap[1]])).dot(normal)>0
    faces.append(cap);settings.append((source.polygons[11].material_index,False))
    used=sorted({i for face in faces for i in face});remap={old:new for new,old in enumerate(used)}
    mesh=bpy.data.meshes.new(source.name+'_UNITY_ADJUSTED')
    try:
        mesh.from_pydata([vertices[i] for i in used],[],[[remap[i] for i in face] for face in faces]);mesh.update()
        for material in source.materials:mesh.materials.append(material)
        for p,(material,smooth) in zip(mesh.polygons,settings):p.material_index=material;p.use_smooth=smooth
        mesh.calc_loop_triangles();assert len(mesh.vertices)==322 and len(mesh.polygons)==241 and len(mesh.loop_triangles)==484
        for p in source.polygons:
            if p.index in selected:continue
            new=mesh.polygons[p.index]
            assert [list(source.vertices[i].co) for i in p.vertices]==[list(mesh.vertices[i].co) for i in new.vertices]
            assert (p.material_index,p.use_smooth)==(new.material_index,new.use_smooth)
        edges=Counter(tuple(sorted((p.vertices[i],p.vertices[(i+1)%len(p.vertices)]))) for p in mesh.polygons for i in range(len(p.vertices)))
        assert set(edges.values())=={2}
        def volume(m, polygons):
            m.calc_loop_triangles()
            return sum((m.vertices[t.vertices[0]].co-A).dot((m.vertices[t.vertices[1]].co-A).cross(m.vertices[t.vertices[2]].co-A))/6
                       for t in m.loop_triangles if t.polygon_index in polygons)
        before=volume(source,set(selected));after=volume(mesh,set(selected)|{240})
        expected=2.5*.03*.03/2
        # Compute volume about the step to avoid cancellation of large world coordinates.
        assert before>0 and after>0 and abs(before-after-expected)<1e-6,(before,after,expected)
        cut_faces=[mesh.polygons[i] for i in selected+[240]]
        assert all(distance(mesh.vertices[i].co)<2e-6 for p in cut_faces for i in p.vertices)
        cap=mesh.polygons[240]
        assert all(abs(distance(mesh.vertices[i].co))<2e-6 for i in cap.vertices)
        assert cap.normal.dot(normal)>.999999 and cap.material_index==source.polygons[11].material_index
        return mesh,dict(selectedOriginalPolygons=selected,unchangedPolygons=234,unchangedTriangles=468,
            originalTriangles=480,adjustedTriangles=484,vertices=322,polygons=241,
            capMaterial=source.materials[cap.material_index].name,bevelMeters=amount,
            removedVolumeObserved=before-after,removedVolumeExpected=expected,
            nativePlane= '(Blender Z - edgeStart.z) - dot(P - edgeStart, inward) + 0.03 = 0',
            nativeTriangulationReplicated=False,scope='Same clipped step surface; triangulation differs from the native Unity triangle clip.')
    except Exception:
        assert mesh.users==0;bpy.data.meshes.remove(mesh);raise


def run(visible_mcp_slot_granted=False):
    assert visible_mcp_slot_granted is True
    import bpy
    source=BASE/'HF_MAP_05_pueblo.blend';out=BASE/'UnityAdjustedSource'
    native=json.loads(NATIVE.read_text(encoding='utf-8-sig'));cut=native['puertoStair']
    assert native['status']=='APPLIED_READBACK_PASS' and native['prefabReadback'] is True and native['sceneReadback'] is True
    assert native['newContentHash']==CONTENT_SHA and cut['renderAndColliderMatch'] is True and cut['materialReferencesPreserved'] is True
    assert cut['candidateMeshSha256']=='9095d590b03a6d9be01a22adb40c1be388ef9201e0caf85a788bed4907bb54d9'
    assert sha(source)==SOURCE_SHA and not out.exists()
    assert bpy.context.scene.name=='HF_MAP_05_pueblo' and Path(bpy.data.filepath).resolve()==source.resolve()
    assert bpy.context.mode=='OBJECT' and not bpy.app.is_job_running('RENDER')
    assert not sys.modules['bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface']._scene_conversation().busy()
    scene=bpy.context.scene;active_file=bpy.data.filepath;obj=scene.objects[cut['objectName']];original=obj.data
    from mathutils import Vector
    audit=json.loads((BASE/'scene-audit.json').read_text(encoding='utf-8-sig'))
    assert set(scene.objects.keys())=={row['name'] for row in audit['objects']}
    for row in audit['objects']:
        current=scene.objects[row['name']]
        assert current.type==row['type'] and [list(r) for r in current.matrix_world]==row['matrix_world']
        assert (current.parent.name if current.parent else None)==row['parent']
        if current.type=='MESH':
            current.data.calc_loop_triangles();corners=[current.matrix_world@Vector(v) for v in current.bound_box]
            assert len(current.data.vertices)==row['vertices'] and len(current.data.loop_triangles)==row['triangles']
            assert [min(v[i] for v in corners) for i in range(3)]==row['bounds_min']
            assert [max(v[i] for v in corners) for i in range(3)]==row['bounds_max']
            assert [slot.material.name for slot in current.material_slots if slot.material]==row['materials']
    assert obj.name=='Lighthouse_Approach_StoneStairway' and obj.parent is None and not obj.constraints and not obj.modifiers and obj.animation_data is None
    assert tuple(obj.users_scene)==(scene,) and original.users==1
    assert [list(r) for r in obj.matrix_world]==[[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]]
    objects={o.name:object_state(o) for o in bpy.data.objects};meshes={m.name:mesh_state(m) for m in bpy.data.meshes}
    members={s.name:[o.name for o in s.objects] for s in bpy.data.scenes}
    preserved={str(p):sha(p) for p in BASE.iterdir() if p.is_file()}
    replacement=None;output=out/'HF_MAP_05_pueblo_UNITY_ADJUSTED.blend'
    try:
        replacement,geometry=bevel_mesh(bpy,original,cut);adjusted_hash=mesh_state(replacement)
        assert all(mesh_state(m)==meshes[m.name] for m in bpy.data.meshes if m!=replacement)
        obj.data=replacement
        assert all(object_state(o)==objects[o.name] for o in bpy.data.objects if o!=obj)
        out.mkdir()
        bpy.data.libraries.write(str(output),{scene},path_remap='RELATIVE_ALL',fake_user=False,compress=False)
    finally:
        obj.data=original
        if replacement is not None:
            assert replacement.users==0;bpy.data.meshes.remove(replacement)
    categories=['objects','meshes','materials','node_groups','images'];ids={key:set(getattr(bpy.data,key)) for key in categories}
    try:
        with bpy.data.libraries.load(str(output),link=False) as (available,requested):
            assert available.scenes==[scene.name] and len(available.objects)==len(scene.objects)
            assert set(available.objects)==set(scene.objects.keys())
            requested.objects=[obj.name]
        saved=requested.objects[0]
        assert saved is not None and saved!=obj and not saved.parent and not saved.constraints and not saved.modifiers and saved.animation_data is None
        assert mesh_state(saved.data)==adjusted_hash
        assert [list(r) for r in saved.matrix_basis]==[list(r) for r in obj.matrix_world]
        # Blender may suffix appended material names, so compare original names after removing readback suffix.
        assert len(saved.data.materials)==len(original.materials)
        for loaded,expected in zip(saved.data.materials,original.materials):
            assert loaded==expected or loaded.name.startswith(expected.name+'.')
    finally:
        for key in categories:
            collection=getattr(bpy.data,key)
            for block in set(collection)-ids[key]:
                if key=='objects':collection.remove(block,do_unlink=True)
                else:
                    assert block.users==0,(key,block.name);collection.remove(block)
    assert all(set(getattr(bpy.data,k))==ids[k] for k in categories)
    assert {o.name:object_state(o) for o in bpy.data.objects}==objects
    assert {m.name:mesh_state(m) for m in bpy.data.meshes}==meshes
    assert {s.name:[o.name for o in s.objects] for s in bpy.data.scenes}==members
    assert bpy.context.scene==scene and bpy.data.filepath==active_file and bpy.context.mode=='OBJECT'
    assert not bpy.app.is_job_running('RENDER') and not sys.modules['bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface']._scene_conversation().busy()
    assert all(sha(p)==h for p,h in preserved.items())
    receipt=dict(status='PASS_EXCLUSIVE_PUERTO_COPY_SECOND_STEP_BEVEL_READBACK_LIVE_RESTORED',
        source=str(output),sourceSha256=sha(output),originalSourceSha256=SOURCE_SHA,
        objectName=obj.name,geometry=geometry,adjustedMeshGeometrySha256=adjusted_hash,
        preservedFiles=preserved,nativeApplyReceipt=str(NATIVE),nativeApplySha256=sha(NATIVE),
        nativeContentHash=CONTENT_SHA,nativeStairMeshSha256=cut['candidateMeshSha256'],
        nativeAdjustment=cut,restoredScene=scene.name,objectTransformPreserved=True,
        newExportsOrUnityImport=False,scope='Exclusive source copy with final 30 mm second-step bevel; original blend/FBX/GLB unchanged; no whole-engine portable equivalence claimed.')
    (out/'adjustment-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
    return receipt
