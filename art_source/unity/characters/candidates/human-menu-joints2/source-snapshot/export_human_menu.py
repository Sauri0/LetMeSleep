"""Export4 menu clips from an isolated, generated and roundtrip-verified base.
Blender only after a Director slot. Never writes to the input source or canons.
"""
import argparse,hashlib,json,sys,types
from pathlib import Path
import bpy

ROOT=Path(__file__).resolve().parent;sys.path.insert(0,str(ROOT))
from build_characters import Character
from author_human_menu_motion import author
from human_menu_contract import CLIPS


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


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
    assert audit['bones']==65 and len(audit['clips'])==15,'Expected coherent combat base, not a menu export'
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
                  actor_mapping='(-source_x,source_z,-source_y)',combat_clips_modified=False,
                  geometry_source_note='Coherent generated crown-fixed base; geometry unchanged by menu authoring')
    out.mkdir(parents=True)
    bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=241
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
