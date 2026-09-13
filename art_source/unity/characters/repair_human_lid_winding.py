"""Two isolated exports from joints3; only eyelid polygon winding is changed.

Requires a Director native slot. Never overwrites inputs or central assets.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import types
import bpy

ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
from author_human_facial import eyelids,orient_lid_faces


def digest(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def invariant():
    meshes=[]
    for obj in sorted((o for o in bpy.context.scene.objects if o.type=='MESH'),key=lambda o:o.name):
        mesh=obj.data
        meshes.append({'name':obj.name,'transform':[list(row) for row in obj.matrix_world],
            'vertices':[list(v.co) for v in mesh.vertices],
            'groups':[g.name for g in obj.vertex_groups],
            'weights':[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices],
            'polygons':[(sorted(p.vertices),p.material_index,p.use_smooth) for p in mesh.polygons],
            'shapes':[(key.name,[list(v.co) for v in key.data]) for key in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
            'materials':[m.name for m in mesh.materials]})
    rigs=[]
    for rig in sorted((o for o in bpy.context.scene.objects if o.type=='ARMATURE'),key=lambda o:o.name):
        rigs.append({'name':rig.name,'transform':[list(row) for row in rig.matrix_world],
            'bones':[(b.name,b.parent.name if b.parent else None,list(b.head_local),list(b.tail_local),
                      [list(row) for row in b.matrix_local],b.use_deform) for b in rig.data.bones]})
    actions=[]
    for action in sorted(bpy.data.actions,key=lambda a:a.name):
        curves=[]
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        curves.append((curve.data_path,curve.array_index,
                            [(list(k.co),list(k.handle_left),list(k.handle_right),k.interpolation) for k in curve.keyframe_points]))
        actions.append((action.name,curves))
    return {'meshes':digest(meshes),'rigs':digest(rigs),'clips':digest(actions)}


def main():
    arg=argparse.ArgumentParser();arg.add_argument('--baseline-root',type=Path,required=True)
    arg.add_argument('--output-root',type=Path,required=True)
    args=arg.parse_args(sys.argv[sys.argv.index('--')+1:])
    baseline=args.baseline_root.resolve();out=args.output_root.resolve()
    assert baseline!=out and not out.exists(),'Candidate directory must be new'
    out.mkdir(parents=True)
    receipts=[]
    for part,stem,audit_name in [('human','LMS_Human_alpha','audit.json'),('menu','LMS_HumanMenu','menu_audit.json')]:
        source=baseline/part/(stem+'.blend');source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
        bpy.ops.wm.open_mainfile(filepath=str(source))
        before=invariant()
        c=types.SimpleNamespace();eyelids(c,lambda *args:None,None)
        head=bpy.data.objects['HumanHead']
        polygons=[list(p.vertices) for p in head.data.polygons]
        winding=orient_lid_faces(c,head)
        after=invariant()
        assert before==after,('Winding repair changed source invariants',part)
        changed=[i for i,p in enumerate(head.data.polygons) if list(p.vertices)!=polygons[i]]
        assert len(changed)==120 and winding['flipped_faces']==120,(part,len(changed),winding)
        for i in changed:
            actual=list(head.data.polygons[i].vertices);reverse=list(reversed(polygons[i]))
            # MeshPolygon.flip preserves a different starting corner than
            # list.reverse; cyclic rotations describe the same reversed face.
            assert any(actual==reverse[k:]+reverse[:k] for k in range(len(reverse)))
        folder=out/part;folder.mkdir()
        bpy.ops.wm.save_as_mainfile(filepath=str(folder/(stem+'.blend')))
        bpy.ops.object.select_all(action='DESELECT')
        rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
        rig.select_set(True)
        for obj in bpy.context.scene.objects:
            if obj.type=='MESH':obj.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(folder/(stem+'.fbx')),use_selection=True,
            object_types={'ARMATURE','MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
            axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,use_armature_deform_only=False,
            bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
            path_mode='AUTO',mesh_smooth_type='FACE')
        audit=json.loads((baseline/part/audit_name).read_text())
        hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.iterdir() if p.suffix in ['.blend','.fbx']}
        audit.update(eyelid_winding=winding,files_sha256=hashes,
            winding_repair={'baseline_blend_sha256':source_hash,'before':before,'after':after,
                'scope':'Existing geometry/skin/rig/clips audit retained under exact invariants; new winding checked on all five samples. Visual review pending.'})
        (folder/audit_name).write_text(json.dumps(audit,indent=2),encoding='utf8',newline='\n')
        assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
        receipts.append({'part':part,'baseline_blend_sha256':source_hash,'invariants_before':before,
            'invariants_after':after,'changed_polygon_indices':changed,'winding':winding,'files':hashes})
        print('WINDING_CANDIDATE '+json.dumps(receipts[-1]),flush=True)
    (out/'winding_repair.json').write_text(json.dumps(receipts,indent=2),encoding='utf8',newline='\n')
    # Fresh one-sided closure/ray and source-FBX checks in this same native slot.
    import audit_human_facial
    sys.argv=['audit_human_facial.py','--','--menu-root',str(out/'menu')]
    audit_human_facial.main()


if __name__=='__main__':main()
