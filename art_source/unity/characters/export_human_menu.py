"""Export the menu clips (4 seated + 2 sleeping, v0.3.0) from an isolated, generated and roundtrip-verified base.
Also solves the sleeping actor's bed placement and writes sleep_envelope.json (posed-body cover height field in
Prop_BedSleeper bed coordinates) beside the FBX; copy it to menu/ so props_catalog.bed_sleeper builds the quilt over it.
Blender only after a Director slot. Never writes to the input source or canons.
"""
import argparse,hashlib,json,sys,types
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

ROOT=Path(__file__).resolve().parent;sys.path.insert(0,str(ROOT))
from build_characters import Character
from author_human_menu_motion import author
from human_menu_contract import CLIPS,FPS,SLEEP_CLIPS,SLEEP_CONTRACT


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def sleep_fit(rig):
    """Bed placement of the sleeping actor (head centre on the pillow) and the cover height field of its body."""
    R=Matrix(SLEEP_CONTRACT['actor_to_bed_rows'])
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    action=bpy.data.actions['MenuSleep']
    def at(frame):
        rig.animation_data_create();rig.animation_data.action=action
        if len(action.slots)==1:rig.animation_data.action_slot=action.slots[0]
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
    at(1)
    head=rig.pose.bones['Head']
    rest_centre=Vector(SLEEP_CONTRACT['head_centre_rest_actor_m'])
    centre=rig.matrix_world@(head.matrix@head.bone.matrix_local.inverted())@rest_centre
    root=Vector(SLEEP_CONTRACT['head_bed_m'])-R@centre
    fold=SLEEP_CONTRACT['quilt_fold_bed_y_m'];top=SLEEP_CONTRACT['mattress_top_bed_m']
    step=.04;ys=[round(-1.04+step*i,3) for i in range(int((fold+1.04)/step)+2)];xs=[round(-.44+step*i,3) for i in range(23)]
    grid=[[None]*len(xs) for _ in ys];lowest=9;bounds=[[9,9,9],[-9,-9,-9]]
    frames=list(range(1,int(round(SLEEP_CLIPS['MenuSleep']*FPS))+2,3))
    graph=None
    for frame in frames:
        at(frame);graph=bpy.context.evaluated_depsgraph_get()
        for obj in meshes:
            view=obj.evaluated_get(graph);mesh=view.to_mesh()
            for v in mesh.vertices:
                b=R@(view.matrix_world@v.co)+root
                lowest=min(lowest,b.z)
                for k in range(3):bounds[0][k]=min(bounds[0][k],b[k]);bounds[1][k]=max(bounds[1][k],b[k])
                if b.y>fold+.02:continue
                i=round((b.y-ys[0])/step);j=round((b.x-xs[0])/step)
                if 0<=i<len(ys) and 0<=j<len(xs) and (grid[i][j] is None or b.z>grid[i][j]):grid[i][j]=round(b.z,4)
            view.to_mesh_clear()
    rig.animation_data.action=None
    return {'schema':'lms.menu.sleep-envelope/1','contract':SLEEP_CONTRACT['version'],
            'actor_to_bed_rows':SLEEP_CONTRACT['actor_to_bed_rows'],'root_bed_m':[round(v,4) for v in root],
            'head_centre_bed_m':SLEEP_CONTRACT['head_bed_m'],'head_centre_actor_m':[round(v,4) for v in centre],
            'forward_bed':[round(v,4) for v in R@Vector((0,-1,0))],'up_bed':[round(v,4) for v in R@Vector((0,0,1))],
            'lying_side':SLEEP_CONTRACT['lying_side'],'mattress_top_bed_m':top,'fold_bed_y_m':fold,
            'frames_sampled':len(frames),'lowest_body_bed_z_m':round(lowest,4),'body_bounds_bed_m':bounds,
            'rows_y':ys,'cols_x':xs,'max_z':grid,
            'note':'Cells hold the highest posed-body point (all MenuSleep samples, every mesh) below the quilt fold.'}


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--base-root',type=Path,required=True)
    parser.add_argument('--output-root',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    base=args.base_root.resolve()/'human';out=args.output_root.resolve()
    assert out!=base and not out.exists(),'Menu output must be a new isolated directory'
    source=base/'LMS_Human_alpha.blend';source_sha=sha(source)
    audit=json.loads((base/'audit.json').read_text());roundtrip=json.loads((base/'fbx_roundtrip.json').read_text())
    assert audit['passed'] and len(roundtrip)==1 and roundtrip[0]['passed']
    assert roundtrip[0]['fbx_sha256']==sha(base/'LMS_Human_alpha.fbx')
    assert roundtrip[0]['source_audit_sha256']==sha(base/'audit.json')
    assert audit['bones']==65 and len(audit['clips'])>=15 and not any('Menu' in (c['name'] if isinstance(c,dict) else c) for c in audit['clips']),'Expected coherent combat base, not a menu export'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    assert set(rig.data.bones.keys())==set(audit['bone_names'])
    rig.animation_data_clear()
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    c=types.SimpleNamespace(species='Human',rig=rig,clips=[])
    c.clip=types.MethodType(Character.clip,c)
    report=author(c)
    assert set(bpy.data.actions.keys())==set(CLIPS)
    report.update(base_blend_sha256=source_sha,base_fbx_sha256=sha(base/'LMS_Human_alpha.fbx'),
                  base_audit_sha256=sha(base/'audit.json'),bind_bones=audit['bind_bones'],
                  bone_names=audit['bone_names'],bones=65,source_up='+Z',source_forward='-Y',
                  material_palette=audit['material_palette'],renderers=audit['renderers'],triangles=audit['triangles'],vertices=audit['vertices'],
                  blend_shapes=audit.get('blend_shapes',{}),facial_contract=audit.get('facial_contract',{}),
                  eyelid_winding=audit.get('eyelid_winding'),
                  actor_mapping='(-source_x,source_z,-source_y)',combat_clips_modified=False,
                  geometry_source_note='Coherent generated crown-fixed base; geometry unchanged by menu authoring')
    out.mkdir(parents=True)
    envelope=sleep_fit(rig)
    report['sleep_fit']={k:envelope[k] for k in ('root_bed_m','head_centre_actor_m','lowest_body_bed_z_m','body_bounds_bed_m')}
    (out/'sleep_envelope.json').write_text(json.dumps(envelope,indent=1)+chr(10),encoding='utf8',newline=chr(10))
    bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=max(round(v*FPS)+1 for v in CLIPS.values())
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'LMS_HumanMenu.blend'))
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH':obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(out/'LMS_HumanMenu.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,use_armature_deform_only=False,
        bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
        path_mode='AUTO',mesh_smooth_type='FACE')
    report['files_sha256']={p.name:sha(p) for p in out.iterdir() if p.suffix in ['.blend','.fbx']}
    (out/'menu_audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8',newline='\n')
    assert sha(source)==source_sha,'Input source changed'
    print('LMS_HUMAN_MENU_EXPORTED '+json.dumps(report['files_sha256']),flush=True)


if __name__=='__main__':main()
