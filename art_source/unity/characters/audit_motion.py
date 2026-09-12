"""Read-only source/FBX movement audit. Requires a Director-coordinated CPU slot.
Blender --background --threads 2 --python-exit-code 1 --python audit_motion.py
No source saves, renders, Unity modifications, or geometric approval.
"""
import bpy
import json
import math
import hashlib
import sys
import argparse
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--species',choices=['Human','Mosquito'])
parser.add_argument('--all-frames',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
SPECIES=[args.species] if args.species else ['Human','Mosquito']
REPORT_PATH=ROOT/args.species.lower()/'motion_audit.json' if args.species else ROOT/'motion_audit.json'
RESULTS=[]
POSES={}
HANDS=[]
FACIAL=[]
EXPRESSIONS=[]
JAW_SEQUENCES=[]
HAND_SPACE_CASES=[]
ALL_FRAMES=args.all_frames

def activate(rig,action):
    rig.animation_data_create()
    rig.animation_data.action=None
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    rig.animation_data.action=action
    if len(action.slots)==1: rig.animation_data.action_slot=action.slots[0]

def sample(rig,meshes,frame):
    bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame))
    bpy.context.view_layer.update()
    graph=bpy.context.evaluated_depsgraph_get()
    heads={b.name:list(rig.matrix_world@b.head) for b in rig.pose.bones}
    verts={}
    for obj in meshes:
        evaluated=obj.evaluated_get(graph); data=evaluated.to_mesh()
        verts[obj.name]=[list(evaluated.matrix_world@v.co) for v in data.vertices]
        evaluated.to_mesh_clear()
    return heads,verts

def distance(a,b): return (Vector(a)-Vector(b)).length

for species in SPECIES:
    source=json.loads((ROOT/species.lower()/'audit.json').read_text())
    for kind in ['blend','fbx']:
        path=ROOT/species.lower()/f'LMS_{species}_alpha.{kind}'
        if kind=='blend': bpy.ops.wm.open_mainfile(filepath=str(path))
        else:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
        rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
        # Blender's FBX importer infers connected bones and suppresses translation curves.
        # Sources explicitly use unconnected bones. Restore that contract for comparison;
        # the original FBX translation curves are present and must not be declared missing.
        if kind=='fbx':
            bpy.context.view_layer.objects.active=rig
            bpy.ops.object.mode_set(mode='EDIT')
            for bone in rig.data.edit_bones: bone.use_connect=False
            bpy.ops.object.mode_set(mode='OBJECT')
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
        rig.animation_data_create(); rig.animation_data.action=None
        for b in rig.pose.bones: b.matrix_basis.identity()
        bpy.context.view_layer.update()
        for clip in source['clips']:
            name=clip['name']; action=next(a for a in bpy.data.actions if a.name.endswith(name))
            activate(rig,action)
            start,end=map(float,action.frame_range)
            curves=[f for l in action.layers for s in l.strips for bag in s.channelbags for f in bag.fcurves]
            variable=[f.data_path for f in curves if len(f.keyframe_points)>1 and
                      max(k.co.y for k in f.keyframe_points)-min(k.co.y for k in f.keyframe_points)>1e-6]
            times=sorted(set([0,.125,.25,.375,.5,.625,.75,.875,1]+[(float(k.co.x)-start)/(end-start)
                       for f in curves for k in f.keyframe_points if end>start and len(f.keyframe_points)<10]))
            times=[t for t in times if 0<=t<=1]
            if ALL_FRAMES and end>start:
                times=sorted(set(times+[(f-start)/(end-start) for f in range(math.ceil(start),math.floor(end)+1)]))
            if name=='Human_Clap' and start<=23.5<=end:
                times=sorted(set(times+[(23.5-start)/(end-start)]))
            base_heads,base_mesh=sample(rig,meshes,start)
            sampled=[]; max_bone=0; max_mesh=0; min_z=999; max_stretch=1; nonfinite=0
            worst_edge=None
            rest_edges={o.name:[(e.vertices[0],e.vertices[1],(o.data.vertices[e.vertices[0]].co-o.data.vertices[e.vertices[1]].co).length)
                                  for e in o.data.edges] for o in meshes}
            for normalized in times:
                heads,verts=sample(rig,meshes,start+(end-start)*normalized)
                max_bone=max(max_bone,max(distance(heads[n],p) for n,p in base_heads.items()))
                for obj in meshes:
                    current=verts[obj.name]
                    max_mesh=max(max_mesh,max(distance(a,b) for a,b in zip(current,base_mesh[obj.name])))
                    min_z=min(min_z,min(v[2] for v in current))
                    nonfinite+=sum(not all(math.isfinite(c) for c in v) for v in current)
                    for a,b,length in rest_edges[obj.name]:
                        if length>.003:
                            deformed_length=distance(current[a],current[b])
                            ratio=deformed_length/length
                            if ratio>max_stretch:
                                max_stretch=ratio
                                worst_edge={'mesh':obj.name,'vertices':[a,b],'phase':normalized,
                                            'rest_length_m':length,'deformed_length_m':deformed_length,
                                            'world_endpoints_m':[current[a],current[b]],
                                            'weights':[{obj.vertex_groups[g.group].name:g.weight for g in obj.data.vertices[i].groups}
                                                       for i in (a,b)]}
                foot_names=['Socket.Foot.L','Socket.Foot.R'] if species=='Human' else ['Leg103.L','Leg203.L','Leg303.L','Leg103.R','Leg203.R','Leg303.R']
                sampled.append({'time':normalized,'heads':heads,
                                'feet_z':{n:heads[n][2] for n in foot_names},
                                'hips_z':heads['Hips'][2] if species=='Human' else None})
            final_heads,final_mesh=sample(rig,meshes,end)
            loop_bone=max(distance(final_heads[n],p) for n,p in base_heads.items())
            loop_mesh=max(distance(a,b) for obj in meshes for a,b in zip(final_mesh[obj.name],base_mesh[obj.name]))
            row={'species':species,'format':kind,'clip':name,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                 'variable_curve_count':len(variable),'max_bone_head_motion_m':max_bone,'max_mesh_vertex_motion_m':max_mesh,
                 'loop_expected':clip['loop'],'first_last_bone_difference_m':loop_bone,'first_last_mesh_difference_m':loop_mesh,
                 'minimum_mesh_z_m':min_z,'max_edge_stretch_ratio':max_stretch,'worst_stretched_edge':worst_edge,'nonfinite_vertices':nonfinite,
                 'max_root_head_motion_m':max(distance(s['heads']['Root'],base_heads['Root']) for s in sampled),
                 'samples':[{'time':s['time'],'feet_z':s['feet_z'],'hips_z':s['hips_z']} for s in sampled]}
            RESULTS.append(row); POSES[(kind,name)]={round(s['time'],6):s['heads'] for s in sampled}
            print('LMS_MOTION_CLIP_DONE '+json.dumps({'species':species,'format':kind,'clip':name,
                  'samples':len(sampled),'minimum_z_m':min_z,'max_edge_stretch_ratio':max_stretch}),flush=True)
        if species=='Human':
            # Bone axis names are insufficient: measure the eye surface's vertical extent.
            blink=next(a for a in bpy.data.actions if a.name.endswith('Human_Blink'))
            activate(rig,blink); start,end=map(float,blink.frame_range)
            extents=[]
            for normalized in [0,.45]:
                _,vertices=sample(rig,meshes,start+(end-start)*normalized)
                for obj in meshes:
                    group=obj.vertex_groups.get('Eye.L')
                    if not group: continue
                    eye=[vertices[obj.name][v.index] for v in obj.data.vertices
                         if any(g.group==group.index and g.weight>.8 for g in v.groups)]
                    if eye: extents.append((normalized,max(v[2] for v in eye)-min(v[2] for v in eye)))
            FACIAL.append({'format':kind,'blink_eye_height_samples':extents,
                           'closed_open_height_ratio':extents[-1][1]/extents[0][1] if len(extents)==2 else None})
            if rig.pose.bones.get('Jaw'):
                hit=next(a for a in bpy.data.actions if a.name.endswith('Human_Hit'))
                activate(rig,hit);start,end=map(float,hit.frame_range);heights=[]
                for normalized in [0,.5]:
                    _,vertices=sample(rig,meshes,start+(end-start)*normalized)
                    head_inverse=(rig.matrix_world@rig.pose.bones['Head'].matrix).inverted()
                    points=[]
                    for obj in meshes:
                        group=obj.vertex_groups.get('Jaw')
                        if group:
                            points.extend(head_inverse@Vector(vertices[obj.name][v.index]) for v in obj.data.vertices
                                if any(g.group==group.index and g.weight>.8 for g in v.groups))
                    assert points,'Missing jaw-weighted facial mesh'
                    # Head's local Y is vertical, independent of the animated head rotation.
                    heights.append(sum(p.y for p in points)/len(points))
                EXPRESSIONS.append({'format':kind,'clip':'Human_Hit',
                    'jaw_mesh_downward_motion_in_head_space_m':heights[0]-heights[1]})
                jaw_indices={}
                for obj in meshes:
                    group=obj.vertex_groups.get('Jaw')
                    if group:
                        jaw_indices[obj.name]=[v.index for v in obj.data.vertices
                                               if any(g.group==group.index and g.weight>.001 for g in v.groups)]
                for clip_name in ['Idle','Hit','Fall','Faint','Recover']:
                    action=next(a for a in bpy.data.actions if a.name.endswith('Human_'+clip_name))
                    activate(rig,action);start,end=map(float,action.frame_range)
                    frames=list(range(math.ceil(start),math.floor(end)+1)) if ALL_FRAMES else [start+(end-start)*t/8 for t in range(9)]
                    records=[]
                    for frame in frames:
                        _,vertices=sample(rig,meshes,frame)
                        head_world=rig.matrix_world@rig.pose.bones['Head'].matrix
                        head_inverse=head_world.inverted()
                        points=[head_inverse@Vector(vertices[name][i]) for name,indices in jaw_indices.items() for i in indices]
                        relative=head_inverse@rig.matrix_world@rig.pose.bones['Jaw'].matrix
                        records.append({'frame':frame,'phase':(frame-start)/(end-start),
                                        'jaw_relative_to_head':[list(row) for row in relative],
                                        'mixed_jaw_bounds_in_head_space':[[min(p[i] for p in points) for i in range(3)],
                                                                          [max(p[i] for p in points) for i in range(3)]]})
                    JAW_SEQUENCES.append({'format':kind,'clip':'Human_'+clip_name,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                                          'included_vertices':jaw_indices,'selection':'Every vertex with Jaw weight >.001, including mixed Head/Jaw skin and oral lining',
                                          'samples':records,'visual_seam_review_passed':False})
            action=next(a for a in bpy.data.actions if a.name.endswith('Human_FingerCurl'))
            activate(rig,action)
            start,end=map(float,action.frame_range)
            sample(rig,meshes,start)
            palm_frames={s:rig.matrix_world@rig.pose.bones['Hand.'+s].matrix.copy() for s in ['L','R']}
            # Compare distal joint heads, since FBX bone tails may have reconstructed lengths.
            open_joints={n:list(rig.matrix_world@rig.pose.bones[n].head) for s in ['L','R']
                         for d in ['Thumb','Index','Middle','Ring','Little'] for n in [d+'03.'+s]}
            open_hand_space={n:palm_frames[s].inverted()@Vector(open_joints[n]) for s in ['L','R']
                             for d in ['Thumb','Index','Middle','Ring','Little'] for n in [d+'03.'+s]}
            sample(rig,meshes,start+(end-start)*.5)
            for s in ['L','R']:
                # The source rest palm normal is world -Y; FBX is reimported into Blender axes.
                for d in ['Thumb','Index','Middle','Ring','Little']:
                    n=d+'03.'+s; delta=(rig.matrix_world@rig.pose.bones[n].head)-Vector(open_joints[n])
                    local=(rig.matrix_world@rig.pose.bones['Hand.'+s].matrix).inverted()@rig.matrix_world@rig.pose.bones[n].head
                    HANDS.append({'format':kind,'digit':n,'inward_distal_joint_displacement_m':-delta.y,
                                  'total_displacement_m':delta.length,
                                  'open_in_hand_space_m':list(open_hand_space[n]),'closed_in_hand_space_m':list(local),
                                  'hand_space_delta_m':list(local-open_hand_space[n])})
            # Swat rotates the arm while closing the right hand. Read in Hand
            # space to isolate actual finger flexion from that arm rotation.
            action=next(a for a in bpy.data.actions if a.name.endswith('Human_Swat'))
            activate(rig,action);start,end=map(float,action.frame_range)
            for phase in [0,.30,.48,1]:
                sample(rig,meshes,start+(end-start)*phase)
                hand_inverse=(rig.matrix_world@rig.pose.bones['Hand.R'].matrix).inverted()
                HAND_SPACE_CASES.append({'format':kind,'clip':'Human_Swat','phase':phase,
                    'side':'R','tool_geometry_included':False,
                    'distal_heads_in_hand_space_m':{d:list(hand_inverse@rig.matrix_world@rig.pose.bones[d+'03.R'].head)
                                                   for d in ['Thumb','Index','Middle','Ring','Little']}})

comparisons=[]
for species in SPECIES:
    names=[r['clip'] for r in RESULTS if r['species']==species and r['format']=='blend']
    for name in names:
        src=POSES[('blend',name)]; fbx=POSES[('fbx',name)]
        errors=[(distance(src[t][bone],fbx[t][bone]),t,bone) for t in src.keys()&fbx.keys() for bone in src[t]]
        worst=max(errors)
        comparisons.append({'clip':name,'max_source_fbx_head_difference_m':worst[0],'time':worst[1],'bone':worst[2]})
report={'scope':'Read-only Blender source and reimported FBX. Measures motion, not artistic approval or Unity playback.',
        'species':SPECIES,
        'sampling':'Every integer source frame plus fixed checkpoints and Clap23.5' if ALL_FRAMES else 'Nine fixed checkpoints plus Clap23.5',
        'blender':bpy.app.version_string,'actions':RESULTS,'source_fbx_comparison':comparisons,'hands':HANDS,'facial':FACIAL,'expressions':EXPRESSIONS,
        'jaw_sequences':JAW_SEQUENCES,'rotated_arm_hand_space_cases':HAND_SPACE_CASES,
        'fbx_evaluation_note':'FBX-imported connected bones are restored to the source unconnected contract before evaluation.'}
REPORT_PATH.write_text(json.dumps(report,indent=2),encoding='utf8',newline='\n')
print(json.dumps({'actions':[{k:r[k] for k in ['clip','format','max_mesh_vertex_motion_m','minimum_mesh_z_m','max_edge_stretch_ratio']} for r in RESULTS],
                  'source_fbx_comparison':comparisons},indent=2))
