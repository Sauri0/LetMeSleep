"""Measure mosquito support geometry under the agreed surface orientation.
Blender headless, two threads, no renders or source writes. Requires Director slot.
This never proves that runtime applies the orientation; W1/W2 own that evidence.
"""
import bpy
import json
import hashlib
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parent
source=json.loads((ROOT/'mosquito/audit.json').read_text())
assert source['contact']['gameplay_collision_radius_m']==.055
assert source['contact']['gameplay_surface_root_offset_m']==.057
assert max(abs(a-b) for a,b in zip(source['contact']['gameplay_tip_rest_unity_m'],[0,0,.095]))<1e-6

# Columns are Unity world right/up/forward; each basis maps local +Y outward.
frames={
    'floor':(Vector((0,1,0)),Matrix.Identity(3)),
    'wall':(Vector((0,0,1)),Matrix(((1,0,0),(0,0,-1),(0,1,0)))),
    'ceiling':(Vector((0,-1,0)),Matrix(((1,0,0),(0,-1,0),(0,0,-1))))}
rows=[];errors=[];hashes={};contract_heads={}
for kind in ['blend','fbx']:
    path=ROOT/'mosquito'/('LMS_Mosquito_alpha.'+kind)
    hashes[kind]=hashlib.sha256(path.read_bytes()).hexdigest()
    if kind=='blend':bpy.ops.wm.open_mainfile(filepath=str(path))
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    if kind=='fbx':
        bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
        for bone in rig.data.edit_bones:bone.use_connect=False
        bpy.ops.object.mode_set(mode='OBJECT')
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    rig.animation_data_create();rig.animation_data.action=None
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    bpy.context.view_layer.update()
    contract_heads[kind]={}
    for bone_name,expected in [('Root',(0,0,0)),('Socket.Mouth',(0,0,.095)),
                               ('Socket.GroundContact',(0,-.057,0))]:
        point=rig.matrix_world@rig.pose.bones[bone_name].head
        point=Vector((-point.x*.5,point.z*.5,-point.y*.5))
        contract_heads[kind][bone_name]=list(point)
        assert (point-Vector(expected)).length<1e-6,(kind,bone_name,list(point),expected)
    distal=[f'Leg{i}03.{side}' for side in ['L','R'] for i in range(1,4)]
    indices={}
    for obj in meshes:
        groups={g.index:g.name for g in obj.vertex_groups}
        indices[obj.name]={name:[v.index for v in obj.data.vertices if any(
            groups[g.group]==name and g.weight>.8 for g in v.groups)] for name in distal}
    for name in ['Idle','PerchIdle','SurfaceWalk','PerchEnter','Land']:
        rig.animation_data.action=None
        for bone in rig.pose.bones:bone.matrix_basis.identity()
        action=next(a for a in bpy.data.actions if a.name.endswith('Mosquito_'+name))
        rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
        start,end=map(float,action.frame_range)
        times=[1] if name in ['PerchEnter','Land'] else [i/16 for i in range(17)]
        for t in times:
            frame=start+(end-start)*t
            bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
            vertices=[];feet={n:[] for n in distal}
            graph=bpy.context.evaluated_depsgraph_get()
            for obj in meshes:
                evaluated=obj.evaluated_get(graph);data=evaluated.to_mesh()
                # FBX wrapper produces actor +Z forward, anatomical left on -X.
                points=[evaluated.matrix_world@v.co for v in data.vertices]
                points=[Vector((-p.x*.5,p.z*.5,-p.y*.5)) for p in points]
                vertices.extend(points)
                for leg in distal:feet[leg].extend(points[i] for i in indices[obj.name][leg])
                evaluated.to_mesh_clear()
            for surface,(normal,rotation) in frames.items():
                for offset in [.056,.057]:
                    origin=normal*offset
                    distance=lambda p:(origin+rotation@p).dot(normal)
                    minimum=min(distance(p) for p in vertices)
                    clearance={n:min(distance(p) for p in points) for n,points in feet.items() if points}
                    supporting=sum(abs(value)<=.0015 for value in clearance.values())
                    row={'format':kind,'clip':'Mosquito_'+name,'normalized_time':t,'surface':surface,
                         'root_offset_m':offset,'minimum_mesh_clearance_m':minimum,
                         'distal_foot_clearance_m':clearance,'supporting_feet':supporting}
                    rows.append(row)
                    required=3 if name=='SurfaceWalk' else 6
                    if minimum<-.0015 or supporting<required or len(clearance)!=6:
                        errors.append(row)
report={'schema':'lms-surface-support-v1','passed':not errors,
        'scope':'Source geometry under expected outward-normal orientation, not runtime/visual approval',
        'runtime_orientation_verified':False,'blender':bpy.app.version_string,'source_sha256':hashes,
        'contract':source['contact'],'measured_bind_anchors_unity_m':contract_heads,
        'samples':rows,'errors':errors}
(ROOT/'surface_support_audit.json').write_text(json.dumps(report,indent=2),encoding='utf8',newline='\n')
print(json.dumps({'passed':not errors,'samples':len(rows),'failures':len(errors),
                  'worst_clearance_m':min(r['minimum_mesh_clearance_m'] for r in rows),
                  'minimum_supporting_feet':min(r['supporting_feet'] for r in rows)}))
assert not errors,'Surface support geometry failed; inspect surface_support_audit.json'
