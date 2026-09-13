"""Native facial bind/morph fidelity and actual closed-lid occlusion diagnostic."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parent;sys.path.insert(0,str(ROOT))
from author_human_facial import CONTRACT,preview_pose
from audit_human_joints import load
from verify_human_menu import activate,action_for_rig


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--menu-root',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=args.menu_root.resolve()
    reports=[];source_shapes=None;errors=[]
    for kind in ['blend','fbx']:
        path=root/('LMS_HumanMenu.'+kind);rig,meshes=load(path,kind)
        head=next(o for o in meshes if o.name=='HumanHead')
        action=action_for_rig('MenuSeatedIdle')
        activate(rig,action,1)
        shapes={key.name:[v.co.copy() for v in key.data] for key in head.data.shape_keys.key_blocks}
        expected={name for names in CONTRACT['blink_samples'].values() for name in names}
        assert set(shapes)==expected,(kind,set(shapes),expected)
        bind={}
        for side,center in CONTRACT['eye_bind_centers_source_m'].items():
            bone=rig.data.bones['Eye.'+side]
            bind[side]={'center_source_m':list(bone.head_local),
                        'forward_source':list(bone.matrix_local.to_3x3()@Vector((0,1,0))),
                        'up_source':list(bone.matrix_local.to_3x3()@Vector((0,0,1)))}
            assert (bone.head_local-Vector(center)).length<1e-5
            assert (bone.matrix_local.to_3x3()@Vector((0,1,0))-Vector((0,-1,0))).length<1e-5
            assert (bone.matrix_local.to_3x3()@Vector((0,0,1))-Vector((0,0,1))).length<1e-5
        if kind=='blend':source_shapes=shapes
        difference=0 if kind=='blend' else max((v-q).length for name,points in shapes.items() for v,q in zip(points,source_shapes[name]))
        if difference>1e-5:errors.append('Morph source/FBX mismatch')
        white_ids={v for p in head.data.polygons if head.data.materials[p.material_index].name=='Character_EyeWhite' for v in p.vertices}
        assert white_ids
        open_white=None;rows=[]
        for closure in [0,.125,.25,.375,.5,.625,.75,.875,1]:
            preview_pose(rig,head,closure);bpy.context.view_layer.update()
            view=head.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=view.to_mesh();mesh.calc_loop_triangles()
            points=[view.matrix_world@v.co for v in mesh.vertices]
            white=[points[i].copy() for i in sorted(white_ids)]
            if open_white is None:open_white=white
            white_delta=max((a-b).length for a,b in zip(white,open_white))
            assert white_delta<1e-7,'Blink changed the volume or position of the eyeball'
            triangles=[tuple(t.vertices) for t in mesh.loop_triangles]
            materials=[head.data.materials[mesh.polygons[t.polygon_index].material_index].name for t in mesh.loop_triangles]
            tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
            frame=rig.matrix_world@rig.pose.bones['Head'].matrix
            forward=(frame.to_3x3()@Vector((0,0,1))).normalized();up=(frame.to_3x3()@Vector((0,1,0))).normalized();right=up.cross(forward)
            hits={}
            for side in ['L','R']:
                center=rig.matrix_world@rig.pose.bones['Eye.'+side].head;counts={}
                for x in [-.034,-.017,0,.017,.034]:
                    for z in [-.034,-.017,0,.017,.034]:
                        origin=center+forward*.20+right*x+up*z
                        _,normal,index,_=tree.ray_cast(origin,-forward,.30)
                        material='miss' if index is None else materials[index]
                        if index is not None and normal.dot(forward)<=0:
                            material='backface:'+material
                        counts[material]=counts.get(material,0)+1
                hits[side]=counts
                if closure==1 and counts.get('Human_Skin',0)!=25:errors.append(kind+'/'+side+': closed lid exposes eyeball or gap')
            rows.append({'closure':closure,'white_max_vertex_displacement_m':white_delta,'front_eye_ray_material_counts':hits})
            view.to_mesh_clear()
        reports.append({'format':kind,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'eye_bind':bind,
                        'shape_names':sorted(shapes),'max_source_fbx_morph_difference_m':difference,'samples':rows})
    result={'contract':CONTRACT,'scope':'Native bind/morph fidelity; nine partial closures and frontal25ray samples per eye requiring front-facing skin at full closure; not Unity/procedural tracking approval',
            'reports':reports,'errors':errors,'passed_numeric_gates':not errors,'visual_approval':False}
    (root/'facial_audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf8',newline='\n')
    print(json.dumps({'passed':not errors,'errors':errors}),flush=True)
    assert not errors


if __name__=='__main__':main()
