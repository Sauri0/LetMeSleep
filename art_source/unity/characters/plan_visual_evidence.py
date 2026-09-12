"""Create renderer-neutral character evidence jobs after the motion gate passes.
This writes JSON only. Director owns the catalogue/index and native render slot.
"""
import json
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parent

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def plan():
    gate=json.loads((ROOT/'motion_gate.json').read_text())
    assert gate['passed'], 'Source motion gate must pass before planning evidence for delivery'
    assert gate['motion_audit_sha256']==sha(ROOT/'motion_audit.json'), 'Stale motion gate'
    report=json.loads((ROOT/'motion_audit.json').read_text())
    jobs=[];assets=[]
    def job(asset,kind,label,**extra):
        identifier=asset.lower()+'/'+kind+'/'+label
        jobs.append({'id':identifier,'asset':asset,'kind':kind,'label':label,
                     'generated':False,'reviewed':False,'reviewer':None,'findings':[],
                     'expected_artifact':identifier+('.mp4' if kind in ['turntable','animation'] else '.png'),**extra})
    for species in ['Human','Mosquito','Flyswatter']:
        folder=ROOT/species.lower();audit=json.loads((folder/'audit.json').read_text())
        fbx=folder/f'LMS_{species}_alpha.fbx';blend=folder/f'LMS_{species}_alpha.blend'
        if species!='Flyswatter':
            for kind,path in [('fbx',fbx),('blend',blend)]:
                assert all(a['source_sha256']==sha(path) for a in report['actions'] if a['species']==species and a['format']==kind),'Stale source motion audit'
        assets.append({'id':species,'cosmetic_variants':[],
                       'visibility_modes':['complete','first_person_head_hidden'] if species=='Human' else ['complete'],
                       'unity_visual_scale':.5 if species=='Mosquito' else 1,
                       'renderers':audit['renderers'],'bones':audit['bone_names'],
                       'source_files':{p.relative_to(ROOT).as_posix():sha(p) for p in [fbx,blend]},
                       'triangles':audit['triangles'],'material_palette':audit['material_palette']})
        idle=species+'_Idle' if species!='Flyswatter' else None
        for label,yaw in [('front',0),('profile_left',-90),('back',180),('profile_right',90),('three_quarter',35)]:
            job(species,'still',label,clip=idle,normalized_time=0,yaw_unity_degrees=yaw,framing='whole_asset_with_8_percent_margin')
        job(species,'turntable','360',clip=idle,freeze_normalized_time=0,frames=180,fps=30,
            yaw_unity_start=0,yaw_unity_end_exclusive=360,framing='whole_asset_with_8_percent_margin')
        for clip in audit['clips']:
            name=clip['name'];native_loop=clip['loop'];length=clip['duration_seconds']
            job(species,'animation',name,clip=name,normalized_start=0,normalized_end=1,fps=30,
                native_loop=native_loop,duration_seconds=length,repetitions=3 if native_loop else 1,
                final_pose_hold_seconds=0 if native_loop else 1,reset_not_part_of_animation=not native_loop,
                yaw_unity_degrees=35,framing='union_of_sampled_clip_bounds_with_8_percent_margin',
                required_numeric_evidence=['selected_clip','phase_advance','bone_position_or_rotation_delta','mesh_vertex_delta'])
            job(species,'contact_sheet',name,clip=name,normalized_times=[0,.25,.5,.75,1],
                cells=5,yaw_unity_degrees=35,framing='same_union_bounds_as_animation',
                visible_labels=['clip','seconds','normalized_time','engine','source_revision'])
    details={
        'Human':[
            ('face_front','Human_Idle',0,'Head',0),('face_profile','Human_Idle',0,'Head',90),
            ('face_hit','Human_Hit',.5,'Head',35),('face_swat','Human_Swat',.48,'Head',35),
            ('face_faint','Human_Faint',1,'Head',35),
            ('slipper_profile','Human_Idle',0,'Foot.L',90),
            ('collar_shoulders','Human_Clap',.35,'Chest',35),
            ('hand_left_open','Human_FingerCurl',0,'Hand.L',35),('hand_left_closed','Human_FingerCurl',.5,'Hand.L',35),
            ('hand_right_open','Human_FingerCurl',0,'Hand.R',-35),('hand_right_closed','Human_FingerCurl',.5,'Hand.R',-35),
            ('clap_shoulders_palms','Human_Clap',14/30,'Chest',35),
            ('crouch_hip_knees','Human_Crouch',.75,'Hips',90),
            ('swat_tool_grip','Human_Swat',.45,'Hand.R',-35),
            ('faint_support','Human_Faint',1,'Hips',90)],
        'Mosquito':[
            ('head_proboscis','Mosquito_BiteLoop',.5,'Head',90),
            ('wing_roots_light','Mosquito_Hover',0,'Thorax',35),
            ('wing_roots_dark','Mosquito_Hover',1/6,'Thorax',35),
            ('six_legs_support','Mosquito_SurfaceWalk',.5,'Thorax',35)],
        'Flyswatter': [('open_grid',None,0,'Socket.Impact',0),('handle_grip',None,0,'Socket.Grip',35)]}
    for species,views in details.items():
        for label,clip,t,focus,yaw in views:
            job(species,'detail',label,clip=clip,normalized_time=t,focus_bone=focus,yaw_unity_degrees=yaw,
                include_attached_tool=label=='swat_tool_grip',background='light' if label.endswith('_light') else 'dark')
    result={'schema':'lms-character-review-jobs-v1','status':'planned_not_rendered',
            'motion_gate_sha256':sha(ROOT/'motion_gate.json'),'assets':assets,'jobs':jobs,
            'capture_contract':{
                'preferred_engine':'Unity URP, imported production prefab, Director resident editor',
                'fallback_engine':'Blender source or FBX clearly labelled; never substitutes for Unity proof',
                'camera':'front is actor +Z in Unity; source -Y in Blender',
                'lighting':'fixed key in front of face, soft fill, constant exposure; no silhouette-only backlighting',
                'framing':'Do not reframe separately per sample; union of clip mesh bounds prevents clipping limbs/wings',
                'resolution_still':[1280,1280],'resolution_video':[960,960],
                'sampling':'Use evaluated Animator/Playable; verify actual bone/mesh change before rendering sequence',
                'source_identity':'Persist source SHA, FBX SHA, imported prefab GUID, Unity version and renderer in each receipt',
                'review_status':'Generated and reviewed are independent. Missing evidence stays pending, never marked approved.',
                'bite_contact':'Source pose shows animation only; contact evidence needs W1/W2 attachment on a real skin surface',
                'human_fp':'Visibility mode, not a cosmetic variant; first-person camera evidence belongs to integration capture'}}
    out=ROOT/'visual_evidence_jobs.json';out.write_text(json.dumps(result,indent=2),encoding='utf8',newline='\n')
    print(f'{len(assets)} real assets, {len(jobs)} planned jobs; no media generated or reviewed')

if __name__=='__main__':plan()
