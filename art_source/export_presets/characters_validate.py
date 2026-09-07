"""Read/validate editable character sources; set the intended default viewport."""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.context.preferences.filepaths.save_version=0
reports=[]
for species in ['human','mosquito']:
    source=ROOT/'art_source/characters'/species
    bpy.ops.wm.open_mainfile(filepath=str(source/(species+'_lms06.blend')))
    manifest=json.loads((source/'manifest.json').read_text())
    rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
    maximum_weight_error=0;maximum_influences=0;bad_positions=0;zero_triangles=0
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        for vertex in obj.data.vertices:
            bad_positions+=int(not all(math.isfinite(value) for value in vertex.co))
            weights=[group.weight for group in vertex.groups if group.weight>1e-6]
            maximum_weight_error=max(maximum_weight_error,abs(sum(weights)-1))
            maximum_influences=max(maximum_influences,len(weights))
        obj.data.calc_loop_triangles()
        for triangle in obj.data.loop_triangles:
            a,b,c=(obj.data.vertices[index].co for index in triangle.vertices)
            zero_triangles+=int((b-a).cross(c-a).length_squared<1e-16)
        pieces=obj.name.split('_');visible=True
        if len(pieces)>2 and pieces[1] in manifest['default']:
            visible=int(pieces[2])==manifest['default'][pieces[1]]
        if species=='human' and pieces[1]=='hair':visible=visible and obj.name.endswith('_capped')
        obj.hide_render=not visible;obj.hide_set(not visible)
    max_action_error=0;worst={}
    if species=='human':
        payload=json.loads((source/'authoritative_pose_clips.json').read_text())
        for label,frames in payload['clips'].items():
            rig.animation_data.action=bpy.data.actions['human_'+label]
            for sample in [frames[0],frames[len(frames)//2],frames[-1]]:
                bpy.context.scene.frame_set(sample['frame']);bpy.context.view_layer.update()
                for name,key in [('torso','torso'),('head','head'),('hand_l','hand_l'),('hand_r','hand_r'),('shin_l','knee_l'),('shin_r','knee_r')]:
                    q=sample[key];target=Vector((q[0],-q[2],q[1]+sample['root_y']))
                    error=(rig.pose.bones[name].matrix.translation-target).length
                    if error>max_action_error:
                        max_action_error=error;worst={'clip':label,'bone':name,'frame':sample['frame'],'target':list(target),'actual':list(rig.pose.bones[name].matrix.translation)}
    rig.animation_data.action=None
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(species+'_lms06.blend')))
    report={'species':species,'bones':len(rig.data.bones),'actions':len([a for a in bpy.data.actions if a.name.startswith(species+'_')]),'maximum_weight_error':maximum_weight_error,'maximum_influences':maximum_influences,'bad_positions':bad_positions,'zero_area_triangles':zero_triangles,'max_authoritative_action_error_m':max_action_error}
    reports.append(report);print('CHARACTER_SOURCE_CHECK',json.dumps(report))
    assert maximum_weight_error<.0001 and maximum_influences<=4 and bad_positions==0
    print('ACTION_WORST',json.dumps(worst))
    assert max_action_error<.002
(ROOT/'art_source/characters/source_validation.json').write_text(json.dumps(reports,indent=2))
