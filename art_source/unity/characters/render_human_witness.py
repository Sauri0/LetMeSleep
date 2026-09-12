"""CPU witness of the saved human and actual actions; requires a Director slot.
Blender -b -t 2 --python-exit-code 1 --python render_human_witness.py -- --views front face_front
Does not save the opened source or touch any other species.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
OUTPUT=ROOT/'review'/'human-reference8'
VIEWS={
    'front':('Idle',0,0,(0,0,.92),2.05),
    'profile_left':('Idle',0,-90,(0,0,.92),2.05),
    'back':('Idle',0,180,(0,0,.92),2.05),
    'profile_right':('Idle',0,90,(0,0,.92),2.05),
    'three_quarter':('Idle',0,35,(0,0,.92),2.05),
    'face_front':('Idle',0,0,(0,-.02,1.56),.58),
    'face_profile':('Idle',0,90,(0,-.02,1.56),.58),
    'face_hit':('Hit',.5,35,(0,-.02,1.53),.58),
    'face_blink':('Blink',.45,0,(0,-.02,1.56),.58),
    'hand_open':('FingerCurl',0,35,(.83,-.01,1.17),.26),
    'hand_curl':('FingerCurl',.5,35,(.83,-.04,1.17),.26),
    'collar':('Idle',0,35,(0,-.03,1.14),.65),
    'slipper':('Idle',0,90,(.125,-.06,.10),.39),
    'clap':('Clap',14/30,35,(0,-.12,1.1),1.0),
    'crouch':('Crouch',.75,90,(0,0,.65),1.6),
}


def aim(obj,point):
    obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()


def activate(rig,name,phase):
    rig.animation_data_create()
    rig.animation_data.action=None
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    action=bpy.data.actions['Human_'+name]
    rig.animation_data.action=action
    if len(action.slots)==1:rig.animation_data.action_slot=action.slots[0]
    start,end=action.frame_range
    frame=start+(end-start)*phase
    bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame))
    bpy.context.view_layer.update()
    return frame


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--views',nargs='+',choices=list(VIEWS),default=list(VIEWS)[:5])
    parser.add_argument('--samples',type=int,default=16)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    source=ROOT/'human'/'LMS_Human_alpha.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    OUTPUT.mkdir(parents=True,exist_ok=True)
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=args.samples
    scene.cycles.use_denoising=True
    scene.render.threads_mode='FIXED'
    scene.render.threads=2
    scene.render.resolution_x=576
    scene.render.resolution_y=720
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    scene.view_settings.view_transform='AgX'
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes['Background']
    bg.inputs[0].default_value=(.17,.19,.24,1)
    bg.inputs[1].default_value=.5
    for name,pos,power,size in [('Key',(-3,-4,5),450,4),('Fill',(3,-2,3),230,3),('Rim',(1,3,4),260,3)]:
        data=bpy.data.lights.new(name,'AREA')
        data.energy=power;data.shape='DISK';data.size=size
        light=bpy.data.objects.new(name,data);scene.collection.objects.link(light)
        light.location=pos;aim(light,(0,0,1))
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.001))
    floor=bpy.context.object;floor.name='WitnessFloor'
    mat=bpy.data.materials.new('WitnessFloorNeutral');mat.diffuse_color=(.11,.135,.18,1)
    mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=mat.diffuse_color
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9
    floor.data.materials.append(mat)
    data=bpy.data.cameras.new('WitnessCamera')
    camera=bpy.data.objects.new('WitnessCamera',data);scene.collection.objects.link(camera)
    scene.camera=camera;data.type='ORTHO'
    rig=next(o for o in scene.objects if o.type=='ARMATURE')
    sha=hashlib.sha256(source.read_bytes()).hexdigest()
    receipts=[]
    for label in args.views:
        clip,phase,yaw,focus,framing=VIEWS[label]
        frame=activate(rig,clip,phase)
        a=math.radians(yaw)
        camera.location=Vector(focus)+Vector((math.sin(a)*5,-math.cos(a)*5,.025))
        aim(camera,focus);data.ortho_scale=framing
        png=OUTPUT/(label+'.png');scene.render.filepath=str(png)
        bpy.ops.render.render(write_still=True)
        receipt={'image':png.as_posix(),'source':source.as_posix(),'source_sha256':sha,
                 'engine':'Blender '+bpy.app.version_string,'renderer':'Cycles CPU','threads':2,
                 'samples':args.samples,'resolution':[576,720],'clip':'Human_'+clip,
                 'phase':phase,'frame':frame,'yaw_degrees':yaw,'focus':focus,'orthographic_scale':framing,
                 'actual_action_evaluated':True,'visual_approval':False,'unity_evidence':False}
        png.with_suffix('.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf8')
        receipts.append(receipt)
    print('LMS_HUMAN_WITNESS_DONE '+json.dumps({'images':len(receipts),'source_sha256':sha}),flush=True)


if __name__=='__main__':main()
