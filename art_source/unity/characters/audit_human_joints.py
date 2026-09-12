"""Native joint diagnostics, including subframes, mesh fidelity and wrist section.
Measurements identify defects; they do not constitute visual acceptance.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parent;sys.path.insert(0,str(ROOT))
from verify_human_menu import activate,evaluated,action_for_rig
from human_menu_contract import CLIPS


def hull_area(points):
    points=sorted(set((round(x,8),round(y,8)) for x,y in points))
    if len(points)<3:return 0.0
    def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    def half(items):
        result=[]
        for point in items:
            while len(result)>=2 and cross(result[-2],result[-1],point)<=0:result.pop()
            result.append(point)
        return result
    hull=half(points)[:-1]+half(reversed(points))[:-1]
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(hull,hull[1:]+hull[:1])))*.5


def wrist_section(obj,positions,origin,normal):
    # Intersect actual evaluated skin triangles with a plane through the wrist.
    # A convex section area is a diagnostic; it cannot certify skin volume.
    axis=normal.cross(Vector((0,0,1)))
    if axis.length<.01:axis=normal.cross(Vector((0,1,0)))
    axis.normalize();other=normal.cross(axis);points=[]
    for triangle in obj.data.loop_triangles:
        if any(abs(obj.data.vertices[i].co.x)>.810 for i in triangle.vertices):continue
        verts=[positions[i] for i in triangle.vertices]
        for a,b in zip(verts,verts[1:]+verts[:1]):
            da=(a-origin).dot(normal);db=(b-origin).dot(normal)
            if da*db<0:
                point=a+(b-a)*(da/(da-db))-origin
                points.append((point.dot(axis),point.dot(other)))
    return {'convex_section_area_m2':hull_area(points),'intersection_points':len(points)}


def regions(obj):
    result={}
    if obj.name.startswith('HandSkin.'):
        result['wrist']=set(v.index for v in obj.data.vertices if abs(v.co.x)<.790)
        result['whole_hand']=set(range(len(obj.data.vertices)))
    if obj.name=='HumanBody':
        for side,sign in [('L',1),('R',-1)]:
            result['elbow.'+side]=set(v.index for v in obj.data.vertices if .43<sign*v.co.x<.605 and 1.06<v.co.z<1.26)
            result['knee.'+side]=set(v.index for v in obj.data.vertices if .025<sign*v.co.x<.235 and .345<v.co.z<.535)
    return {name:[(e.vertices[0],e.vertices[1],(obj.data.vertices[e.vertices[0]].co-obj.data.vertices[e.vertices[1]].co).length)
                   for e in obj.data.edges if all(i in ids for i in e.vertices)
                   and (obj.data.vertices[e.vertices[0]].co-obj.data.vertices[e.vertices[1]].co).length>.003]
            for name,ids in result.items()}


def load(path,kind):
    if kind=='blend':bpy.ops.wm.open_mainfile(filepath=str(path))
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    if kind=='fbx':
        bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
        for bone in rig.data.edit_bones:bone.use_connect=False
        bpy.ops.object.mode_set(mode='OBJECT')
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for obj in meshes:obj.data.calc_loop_triangles()
    for obj in meshes:
        if obj.data.shape_keys:obj.data.shape_keys.animation_data_clear()
    return rig,meshes


def inspect(root,label):
    rows=[];snapshots={};fidelity=[]
    for kind in ['blend','fbx']:
        path=root/('LMS_HumanMenu.'+kind);rig,meshes=load(path,kind)
        selections={o.name:regions(o) for o in meshes}
        for name in CLIPS:
            action=action_for_rig(name)
            start,end=action.frame_range
            phases=sorted(set([i/8 for i in range(9)]+([.28] if name=='MenuSwat' else [])))
            for phase in phases:
                frame=float(start+(end-start)*phase);activate(rig,action,frame)
                positions=evaluated(meshes);row={'asset':label,'format':kind,'clip':name,'phase':phase,'frame':frame,'joints':{},'regions':{}}
                matrices={b.name:b.matrix.copy() for b in rig.pose.bones}
                for side in ['L','R']:
                    hand=rig.pose.bones['Hand.'+side];lower=rig.pose.bones['LowerArm.'+side]
                    u=(lower.tail-lower.head).normalized();v=(hand.tail-hand.head).normalized()
                    normal=u+v
                    if normal.length<.01:normal=v.copy()
                    normal.normalize()
                    obj=next(o for o in meshes if o.name=='HandSkin.'+side)
                    row['joints']['wrist.'+side]={
                        'bend_degrees':math.degrees(u.angle(v)),
                        'hand_matrix_rows':[list(r) for r in hand.matrix],
                        'lower_arm_matrix_rows':[list(r) for r in lower.matrix],
                        **wrist_section(obj,positions[obj.name],hand.head,normal)}
                    for title,a,b in [('elbow','UpperArm.','LowerArm.'),('knee','UpperLeg.','LowerLeg.')]:
                        upper=rig.pose.bones[a+side];lower_bone=rig.pose.bones[b+side]
                        row['joints'][title+'.'+side]={'bend_degrees':math.degrees((upper.tail-upper.head).angle(lower_bone.tail-lower_bone.head))}
                for mesh_name,selection in selections.items():
                    for region,edges in selection.items():
                        values=[{'vertices':[a,b],'ratio':(positions[mesh_name][a]-positions[mesh_name][b]).length/rest,
                                 'extension_m':(positions[mesh_name][a]-positions[mesh_name][b]).length-rest} for a,b,rest in edges]
                        assert values,(mesh_name,region)
                        row['regions'][mesh_name+'/'+region]={
                            'edge_count':len(values),'max_extension':max(values,key=lambda v:v['extension_m']),
                            'max_ratio':max(values,key=lambda v:v['ratio']),
                            'min_ratio':min(values,key=lambda v:v['ratio'])}
                rows.append(row)
                key=(name,phase)
                if kind=='blend':snapshots[key]=(positions,matrices)
                else:
                    old_positions,old_matrices=snapshots[key]
                    assert {n:len(v) for n,v in positions.items()}=={n:len(v) for n,v in old_positions.items()}
                    maximum=max((v-q).length for n,vs in positions.items() for v,q in zip(vs,old_positions[n]))
                    rotation=max(abs(m[i][j]-old_matrices[n][i][j]) for n,m in matrices.items() for i in range(3) for j in range(3))
                    fidelity.append({'clip':name,'phase':phase,'max_mesh_difference_m':maximum,'max_rotation_matrix_element_difference':rotation})
    return {'asset':label,'source_sha256':hashlib.sha256((root/'LMS_HumanMenu.blend').read_bytes()).hexdigest(),
            'fbx_sha256':hashlib.sha256((root/'LMS_HumanMenu.fbx').read_bytes()).hexdigest(),
            'samples':rows,'source_fbx_fidelity':fidelity}


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--menu-root',type=Path,required=True)
    parser.add_argument('--baseline-root',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    reports=[inspect(args.baseline_root.resolve(),'menu-seated1'),inspect(args.menu_root.resolve(),'menu-joints2')]
    summary=[]
    for report in reports:
        for side in ['L','R']:
            samples=[r for r in report['samples'] if r['format']=='blend']
            summary.append({'asset':report['asset'],'side':side,
                'max_wrist_bend_degrees':max(r['joints']['wrist.'+side]['bend_degrees'] for r in samples),
                'min_wrist_section_m2':min(r['joints']['wrist.'+side]['convex_section_area_m2'] for r in samples)})
    candidate=reports[1]['source_fbx_fidelity']
    errors=[]
    if max(r['max_mesh_difference_m'] for r in candidate)>.002:errors.append('Source/FBX actual mesh mismatch')
    if max(r['max_rotation_matrix_element_difference'] for r in candidate)>.0001:errors.append('Source/FBX bone rotation mismatch')
    result={'scope':'37 critical poses per format per candidate, including Swat frame12.76; mesh/rotation fidelity and joint diagnostics, not full visual motion approval',
            'reports':reports,'summary':summary,'fidelity_errors':errors,'visual_approval':False,'grip_approval':False}
    (args.menu_root/'joint_audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf8',newline='\n')
    print(json.dumps({'summary':summary,'errors':errors}),flush=True)
    assert not errors


if __name__=='__main__':main()
