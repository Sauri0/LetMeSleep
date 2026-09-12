"""Native menu source/FBX roundtrip, anchors, tool sweep and sampled support."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parent;sys.path.insert(0,str(ROOT))
from human_menu_contract import CLIPS,CONTRACT,FPS,source_point


def activate(rig,action,frame):
    rig.animation_data_create();rig.animation_data.action=None
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    rig.animation_data.action=action
    if len(action.slots)==1:rig.animation_data.action_slot=action.slots[0]
    bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()


def evaluated(meshes):
    graph=bpy.context.evaluated_depsgraph_get();result={}
    for obj in meshes:
        view=obj.evaluated_get(graph);mesh=view.to_mesh()
        result[obj.name]=[view.matrix_world@v.co for v in mesh.vertices];view.to_mesh_clear()
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--menu-root',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=args.menu_root.resolve()
    source=json.loads((root/'menu_audit.json').read_text());errors=[];rows=[];heads={};boundaries={};layouts={}
    for kind in ['blend','fbx']:
        path=root/('LMS_HumanMenu.'+kind)
        assert hashlib.sha256(path.read_bytes()).hexdigest()==source['files_sha256'][path.name]
        if kind=='blend':bpy.ops.wm.open_mainfile(filepath=str(path))
        else:
            bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
        rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
        if kind=='fbx':
            bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
            for bone in rig.data.edit_bones:bone.use_connect=False
            bpy.ops.object.mode_set(mode='OBJECT')
        assert len(rig.data.bones)==65 and set(rig.data.bones.keys())==set(source['bone_names'])
        bind={b.name:b for b in rig.data.bones}
        for expected in source['bind_bones']:
            b=bind[expected['name']]
            assert (b.head_local-Vector(expected['head_blender_m'])).length<1e-5
            assert (b.parent.name if b.parent else '')==expected['parent']
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
        for obj in meshes:obj.data.calc_loop_triangles()
        layouts[kind]={'meshes':{o.name:len(o.data.vertices) for o in meshes},'triangles':sum(len(o.data.loop_triangles) for o in meshes)}
        actions={name:next(a for a in bpy.data.actions if a.name.endswith(name)) for name in CLIPS}
        assert len(bpy.data.actions)==4
        grip_rest=rig.matrix_world@rig.data.bones['Socket.Grip.R'].matrix_local
        pelvis_mask={}
        for obj in meshes:
            if obj.name!='HumanBody':continue
            group=obj.vertex_groups.get('Hips')
            pelvis_mask[obj.name]=[v.index for v in obj.data.vertices if .675<=v.co.z<=.82 and abs(v.co.x)<.25
                                   and any(g.group==group.index and g.weight>.8 for g in v.groups)]
        for name,action in actions.items():
            start,end=map(float,action.frame_range)
            assert abs(start-1)<1e-6 and abs((end-start)/FPS-CLIPS[name])<1e-6
            times=sorted(set(list(range(1,int(end)+1))+([22.0] if name=='MenuSwat' else [])))
            records=[];head_samples={};poses=[];floor=999;root_error=0;foot_error=0;seat_min=999;intrusions=0
            for frame in times:
                activate(rig,action,frame);verts=evaluated(meshes)
                current={b.name:list(rig.matrix_world@b.head) for b in rig.pose.bones};head_samples[frame]=current
                minimum=min(v.z for values in verts.values() for v in values);floor=min(floor,minimum)
                root_error=max(root_error,Vector(current['Root']).length)
                feet={s:(Vector(current['Socket.Foot.'+s])-Vector(source_point(p))).length for s,p in CONTRACT['sole_actor_m'].items()}
                foot_error=max(foot_error,*feet.values())
                pelvis=[verts[n][i] for n,indices in pelvis_mask.items() for i in indices
                        if -.32<=-verts[n][i].y<=.355]
                assert pelvis,'Missing actual cloth/pelvis contact selection'
                support=min(v.z for v in pelvis);seat_min=min(seat_min,support)
                body=verts['HumanBody']
                # Conservative interior of the known flat cushion, excluding its bevel.
                inside=sum(abs(v.x)<.865 and -.275<-v.y<.355 and .45<v.z<.572 for v in body)
                intrusions=max(intrusions,inside)
                grip=rig.matrix_world@rig.pose.bones['Socket.Grip.R'].matrix
                delta=grip@grip_rest.inverted()
                impact=grip.translation+delta.to_3x3()@Vector((0,-.005,.365))
                target=Vector(source_point(CONTRACT['mosquito_pass_actor_m']))
                eye=Vector(current['Socket.Eye']);forward=(rig.matrix_world@rig.pose.bones['Head'].matrix).to_3x3()@Vector((0,0,1))
                gaze=math.degrees(forward.angle(target-eye))
                records.append({'frame':frame,'phase':(frame-start)/(end-start),'feet_error_m':feet,
                    'hips_source_m':current['Hips'],'pelvis_cloth_min_height_m':support,'seat_interior_vertex_count':inside,
                    'grip_source_m':list(grip.translation),'grip_delta_rotation_rows':[list(row) for row in delta.to_3x3()],
                    'impact_source_m':list(impact),'impact_to_pass_distance_m':(impact-target).length,'gaze_error_degrees':gaze})
                if frame in [start,end]:poses.append(verts)
            if floor<-.003:errors.append(kind+'/'+name+': floor penetration')
            if root_error>.0001:errors.append(kind+'/'+name+': Root moved')
            if foot_error>.002:errors.append(kind+'/'+name+': feet targets missed')
            heads[(kind,name)]=head_samples;boundaries[(kind,name)]=poses
            rows.append({'format':kind,'clip':name,'samples':records,'minimum_z_m':floor,
                         'max_root_error_m':root_error,'max_foot_error_m':foot_error,'minimum_pelvis_cloth_height_m':seat_min,
                         'maximum_seat_interior_vertices':intrusions,'source_sha256':source['files_sha256'][path.name]})
            print('LMS_MENU_AUDITED '+json.dumps({'format':kind,'clip':name,'samples':len(records),'feet_m':foot_error,'seat_interior_vertices':intrusions}),flush=True)
    assert layouts['blend']==layouts['fbx'],'Menu mesh topology changed in FBX'
    fidelity=[];links=[]
    for name in CLIPS:
        a,b=heads[('blend',name)],heads[('fbx',name)]
        maximum=max((Vector(a[f][n])-Vector(b[f][n])).length for f in a for n in a[f])
        fidelity.append({'clip':name,'max_bone_difference_m':maximum})
        if maximum>.002:errors.append(name+': source/FBX pose mismatch')
    for kind in ['blend','fbx']:
        for left,right in [('MenuSeatedIdle','MenuSeatedIdle'),('MenuSeatedIdle','MenuLook'),('MenuLook','MenuSwat'),('MenuSwat','MenuReturn'),('MenuReturn','MenuSeatedIdle')]:
            a=boundaries[(kind,left)][-1];b=boundaries[(kind,right)][0]
            maximum=max((p-q).length for name in a for p,q in zip(a[name],b[name]))
            links.append({'format':kind,'from':left,'to':right,'max_mesh_boundary_difference_m':maximum})
            if maximum>.002:errors.append(kind+'/'+left+'->'+right+': boundary mismatch')
    result={'passed_numeric_gates':not errors,'errors':errors,'scope':'Integer-frame native source/FBX, fixed feet/Root and clip boundaries; not visual/grip/seat approval',
            'actions':rows,'source_fbx':fidelity,'boundaries':links,'layouts':layouts,'tool_geometry_included':False,
            'tool_trace_note':'Production grip rotation-offset convention applied to source grip and impact point; full real prop shown in witness',
            'seat_shape_note':'Flat-interior diagnostic from agreed production bounds; mesh collision and clothing review still required',
            'seat_approval':False,'grip_approval':False,'visual_approval':False}
    (root/'menu_motion_audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf8',newline='\n')
    print(json.dumps({'passed':not errors,'errors':errors,'max_bone_difference_m':max(r['max_bone_difference_m'] for r in fidelity)}),flush=True)
    assert not errors


if __name__=='__main__':main()
