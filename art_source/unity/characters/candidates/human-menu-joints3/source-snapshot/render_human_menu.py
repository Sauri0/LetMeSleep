"""Seated witness with the real kit sofa and production flyswatter, CPU2 only."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector,Matrix

ROOT=Path(__file__).resolve().parent;sys.path.insert(0,str(ROOT))
from human_menu_contract import CLIPS,CONTRACT,source_point
from render_human_witness import aim
from verify_human_menu import activate


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--menu-root',type=Path,required=True)
    parser.add_argument('--sequence',action='store_true')
    parser.add_argument('--joint-details',action='store_true')
    parser.add_argument('--joint-review-pair',action='store_true')
    parser.add_argument('--face-details',action='store_true')
    parser.add_argument('--face-cases',nargs='*',choices=['eyes_open','eyes_half','eyes_closed','eyes_left','eyes_right','eyes_down','eyes_light_half','eyes_dark_half'])
    parser.add_argument('--review-name',default='review-full-frame')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=args.menu_root.resolve()
    source=root/'LMS_HumanMenu.blend';sofa_source=Path('N:/LetMeSleep/Repository/art_source/unity/environments/alfa_maps/furniture_kit_alfa.blend')
    tool_source=ROOT/'flyswatter/LMS_Flyswatter_alpha.fbx'
    bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
    rig=next(o for o in scene.objects if o.type=='ARMATURE');actions={name:bpy.data.actions[name] for name in CLIPS}
    grip_rest=rig.matrix_world@rig.data.bones['Socket.Grip.R'].matrix_local
    with bpy.data.libraries.load(str(sofa_source),link=False) as (available,loaded):
        loaded.objects=[name for name in available.objects if name=='Kit_Sofa' or name.startswith('Kit_Sofa_')]
    for obj in loaded.objects:scene.collection.objects.link(obj)
    sofa=next(o for o in loaded.objects if o.name=='Kit_Sofa')
    sofa.location=(0,0,0);sofa.rotation_euler=(0,0,math.pi)
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(tool_source),use_anim=False)
    tool=next(o for o in set(bpy.data.objects)-before if o.type=='ARMATURE');tool_rest=tool.matrix_world.copy()
    for bone in tool.pose.bones:bone.matrix_basis.identity()
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=4 if args.sequence else 16
    scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=640 if args.face_details else 480 if args.sequence else 960
    scene.render.resolution_y=640 if args.face_details else 360 if args.sequence else 720
    scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX';scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes['Background'];bg.inputs[0].default_value=(.12,.16,.22,1);bg.inputs[1].default_value=.35
    for name,pos,power,size,color in [('WarmKey',(-2,-3,4),500,3,(1,.77,.54)),('CoolFill',(3,-2,3),250,3,(.63,.78,1)),('Rim',(0,3,4),280,3,(.70,.80,1))]:
        data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
        light=bpy.data.objects.new(name,data);scene.collection.objects.link(light);light.location=pos;aim(light,(0,-.2,1))
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.001));floor=bpy.context.object
    material=bpy.data.materials.new('MenuControlFloor');material.diffuse_color=(.13,.15,.17,1);floor.data.materials.append(material)
    cam_data=bpy.data.cameras.new('MenuWitnessCamera');camera=bpy.data.objects.new('MenuWitnessCamera',cam_data)
    scene.collection.objects.link(camera);scene.camera=camera;cam_data.type='ORTHO';cam_data.ortho_scale=2.9
    output=(root/('sequence_frames' if args.sequence else args.review_name)).resolve()
    assert output.is_relative_to(root)
    output.mkdir(exist_ok=True)
    provenance={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'sofa_source':str(sofa_source),'sofa_sha256':hashlib.sha256(sofa_source.read_bytes()).hexdigest(),
                'sofa_objects':[o.name for o in loaded.objects],'tool_source':str(tool_source),'tool_sha256':hashlib.sha256(tool_source.read_bytes()).hexdigest(),
                'sofa_note':'Exact Kit_Sofa geometry recentered and yaw180 into human source axes; lobby throw/cushion absent',
                'light_note':'Blender control lighting; not final menu/runtime approval','actual_actions_evaluated':True,'unity_evidence':False,'visual_approval':False}

    def render(label,clip,phase,yaw,detail=None,facial=None):
        action=actions[clip];start,end=action.frame_range;frame=start+(end-start)*phase
        activate(rig,action,frame)
        if facial:
            from author_human_facial import preview_pose
            head_mesh=bpy.data.objects['HumanHead']
            preview_pose(rig,head_mesh,facial['closure'],facial.get('eye_yaw',0),facial.get('eye_pitch',0))
            skin=bpy.data.materials['Human_Skin'];color=facial.get('skin_color',[.67,.43,.27,1])
            skin.diffuse_color=color;skin.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
            bpy.context.view_layer.update()
        grip=rig.matrix_world@rig.pose.bones['Socket.Grip.R'].matrix;delta=grip@grip_rest.inverted()
        rotation=delta.to_3x3().to_4x4();rotation.translation=grip.translation;tool.matrix_world=rotation@tool_rest
        a=math.radians(yaw);focus=Vector((0,-.18,.87));cam_data.ortho_scale=2.9
        if detail=='wrist':
            focus=(rig.pose.bones['Hand.R'].head+rig.pose.bones['LowerArm.R'].head)*.5
            focus.z+=.055;cam_data.ortho_scale=.68
        elif detail=='knees':
            focus=(rig.pose.bones['LowerLeg.R'].head+rig.pose.bones['LowerLeg.L'].head)*.5
            cam_data.ortho_scale=1.15
        elif detail=='face':
            focus=rig.pose.bones['Socket.Eye'].head.copy();focus.z+=.005;cam_data.ortho_scale=.43
        camera.location=focus+Vector((math.sin(a)*5,-math.cos(a)*5,1.0))
        aim(camera,focus);path=output/(label+'.png');scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True)
        return {'image':str(path),'clip':clip,'phase':phase,'frame':float(frame),'yaw_degrees':yaw,'focus':list(focus),
                'resolution':[scene.render.resolution_x,scene.render.resolution_y],'samples':scene.cycles.samples,'detail':detail,'facial':facial}

    receipts=[]
    assert sum([args.sequence,args.joint_details,args.face_details,args.joint_review_pair])<=1
    if args.face_details:
        for label,facial in [
            ('eyes_open',{'closure':0}),('eyes_half',{'closure':.5}),('eyes_closed',{'closure':1}),
            ('eyes_left',{'closure':0,'eye_yaw':22}),('eyes_right',{'closure':0,'eye_yaw':-22}),
            ('eyes_down',{'closure':.125,'eye_pitch':-15}),
            ('eyes_light_half',{'closure':.5,'skin_color':[.86,.67,.48,1]}),
            ('eyes_dark_half',{'closure':.5,'skin_color':[.22,.095,.045,1]})]:
            if args.face_cases and label not in args.face_cases:continue
            row=render(label,'MenuSeatedIdle',0,0,'face',facial);receipts.append(row)
            (output/(label+'.json')).write_text(json.dumps({**provenance,**row},indent=2)+'\n',encoding='utf8',newline='\n')
    elif args.joint_review_pair:
        for label,yaw,detail in [('wrist_idle',35,'wrist'),('seated_three_quarter',35,None)]:
            row=render(label,'MenuSeatedIdle',0,yaw,detail);receipts.append(row)
            (output/(label+'.json')).write_text(json.dumps({**provenance,**row},indent=2)+'\n',encoding='utf8',newline='\n')
    elif args.joint_details:
        for label,clip,phase,yaw,detail in [
            ('wrist_idle','MenuSeatedIdle',0,35,'wrist'),
            ('wrist_idle_reverse','MenuSeatedIdle',0,-65,'wrist'),
            ('wrist_windup','MenuSwat',.28,35,'wrist'),
            ('wrist_strike','MenuSwat',.5,35,'wrist'),
            ('wrist_follow','MenuSwat',1,35,'wrist'),
            ('knees_side','MenuSeatedIdle',0,90,'knees')]:
            row=render(label,clip,phase,yaw,detail);receipts.append(row)
            (output/(label+'.json')).write_text(json.dumps({**provenance,**row},indent=2)+'\n',encoding='utf8',newline='\n')
    elif args.sequence:
        fps=12;total=sum(CLIPS.values());count=math.ceil(total*fps)
        for index in range(count):
            seconds=min(index/fps,total);offset=0
            for clip,duration in CLIPS.items():
                if seconds<=offset+duration+1e-8:
                    row=render(f'{index:04d}',clip,min(1,max(0,(seconds-offset)/duration)),35);break
                offset+=duration
            row['sequence_seconds']=seconds;receipts.append(row)
            if index%12==0:print('LMS_MENU_SEQUENCE_FRAME '+str(index)+'/'+str(count),flush=True)
        provenance.update(playback_fps=fps,sequence_duration_seconds=count/fps,frames=receipts)
        (output/'sequence.json').write_text(json.dumps(provenance,indent=2)+'\n',encoding='utf8',newline='\n')
    else:
        for label,clip,phase,yaw in [('seated_front','MenuSeatedIdle',0,0),('seated_side','MenuSeatedIdle',0,90),
                                    ('seated_three_quarter','MenuSeatedIdle',0,35),('reaction','MenuSwat',.5,35)]:
            row=render(label,clip,phase,yaw);receipts.append(row)
            (output/(label+'.json')).write_text(json.dumps({**provenance,**row},indent=2)+'\n',encoding='utf8',newline='\n')
    print('LMS_MENU_WITNESS_DONE '+json.dumps({'images':len(receipts),'sequence':args.sequence}),flush=True)


if __name__=='__main__':main()
