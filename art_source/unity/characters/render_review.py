"""Render review views ONLY when Director grants a render slot. Does not save source changes."""
import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
OUTPUT=ROOT/'review'
OUTPUT.mkdir(exist_ok=True)

def aim(obj,point): obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()

species_list=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Human','Mosquito']
for species in species_list:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/species.lower()/f'LMS_{species}_alpha.blend'))
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=24
    scene.render.resolution_x=720; scene.render.resolution_y=900; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'; scene.view_settings.view_transform='AgX'
    scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.19,.25,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
    human=species=='Human'; scale=1 if human else .25; center=(0,0,.95) if human else (0,0,0)
    for name,pos,power,size in [('Key',(-3,-4,5),400,4),('Fill',(3,-1,2),150,3),('Rim',(1,3,4),300,3)]:
        data=bpy.data.lights.new(name,'AREA'); data.energy=power*scale*scale; data.shape='DISK'; data.size=size*scale
        o=bpy.data.objects.new(name,data); scene.collection.objects.link(o)
        o.location=Vector(center)+Vector(pos)*scale; aim(o,center)
    data=bpy.data.cameras.new('ReviewCamera'); camera=bpy.data.objects.new('ReviewCamera',data)
    scene.collection.objects.link(camera); scene.camera=camera; data.type='ORTHO'; data.ortho_scale=2.25 if human else .72
    rig=next(o for o in scene.objects if o.type=='ARMATURE')
    rig.animation_data.action=None
    if human:
        rig.pose.bones['UpperArm.L'].rotation_euler.z=-1.2
        rig.pose.bones['UpperArm.R'].rotation_euler.z=1.2
    for view,delta in [('front',(0,-5,.05)),('side',(5,0,.05)),('back',(0,5,.05)),('threequarter',(3,-5,1.2))]:
        camera.location=Vector(center)+Vector(delta)*scale; aim(camera,center)
        scene.render.filepath=str(OUTPUT/f'{species.lower()}_{view}.png'); bpy.ops.render.render(write_still=True)
    if human:
        rig.animation_data.action=bpy.data.actions['Human_Clap']; scene.frame_set(12)
        camera.location=(2,-4,1.65); aim(camera,(0,-.12,1.17)); data.ortho_scale=1.1
        scene.render.filepath=str(OUTPUT/'human_clap_contact.png'); bpy.ops.render.render(write_still=True)
        rig.animation_data.action=None
        for b in rig.pose.bones: b.rotation_euler=(0,0,0)
        for state,angle in [('open',0),('curl',.65)]:
            for side in ['L','R']:
                for digit in ['Thumb','Index','Middle','Ring','Little']:
                    for i in range(1,4): rig.pose.bones[f'{digit}{i:02d}.{side}'].rotation_euler.x=angle
            center=(.84,-.025,1.17); camera.location=(1.13,-.62,1.48); aim(camera,center); data.ortho_scale=.25
            scene.render.filepath=str(OUTPUT/f'human_hand_{state}.png'); bpy.ops.render.render(write_still=True)
print('LMS_CHARACTER_REVIEW_RENDERED',flush=True)
