"""Let me sleep: original, reproducible Unity alpha character sources.
Run after a Director slot: Blender --background --python build_characters.py -- --species Human
No rendering occurs. All output stays beside this script. Blender 5.2 LTS.
"""
import bpy
import math
import json
import hashlib
import sys
import argparse
from pathlib import Path
from mathutils import Vector, Matrix, Euler

OUT = Path(__file__).resolve().parent
sys.path.insert(0,str(OUT))
FPS = 30


def material(name, color, roughness=.72):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color[:3], color[3] if len(color) > 3 else 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = m.diffuse_color
    p.inputs['Roughness'].default_value = roughness
    p.inputs['Alpha'].default_value = m.diffuse_color[3]
    return m


def mesh(name, verts, faces, mat, bone=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if bone:
        obj.vertex_groups.new(name=bone).add(list(range(len(verts))), 1, 'REPLACE')
    return obj


def tube(name, centers, widths, depths, mat, bone=None, sides=12, cap_ends=(True,True)):
    """Loft elliptical sections normal to a path, preserving authored facets."""
    verts = []
    first_tangent=(Vector(centers[1])-Vector(centers[0])).normalized()
    reference = Vector((0, 1, 0)) if abs(first_tangent.y) < .9 else Vector((1, 0, 0))
    for i, c in enumerate(centers):
        tangent = Vector(centers[min(i+1, len(centers)-1)]) - Vector(centers[max(i-1, 0)])
        tangent.normalize()
        u = tangent.cross(reference).normalized()
        v = tangent.cross(u).normalized()
        for j in range(sides):
            a = 2*math.pi*j/sides
            verts.append(Vector(c) + widths[i]*math.cos(a)*u + depths[i]*math.sin(a)*v)
    faces = [tuple(reversed(range(sides)))] if cap_ends[0] else []
    for i in range(len(centers)-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    if cap_ends[1]:faces.append(tuple((len(centers)-1)*sides+j for j in range(sides)))
    return mesh(name, verts, faces, mat, bone)


def ellipsoid(name, center, scale, mat, bone, segments=16, rings=8):
    # UV sphere with applied geometry scale: no negative/mirrored object transforms.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=center)
    obj=bpy.context.object; obj.name=name
    for vert in obj.data.vertices:
        vert.co.x*=scale[0]; vert.co.y*=scale[1]; vert.co.z*=scale[2]
    obj.data.materials.append(mat)
    obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))),1,'REPLACE')
    # bake translation so every export object has identity transforms
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def strip(name, pts, width, mat, bone):
    return tube(name, pts, [width]*len(pts), [width*.45]*len(pts), mat, bone, 6)


class Character:
    def __init__(self, species):
        bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
        for action in list(bpy.data.actions): bpy.data.actions.remove(action)
        self.species=species; self.bones=[]; self.clips=[]; self.rig=None
        self.curl={}; self.finger_paths={}; self.contact={}
        bpy.context.scene.render.fps=FPS
        bpy.context.scene.unit_settings.system='METRIC'
        bpy.context.scene.unit_settings.scale_length=1

    def bone(self, name, head, tail, parent=None, deform=True, roll_front=True):
        self.bones.append((name,head,tail,parent,deform,roll_front))

    def bind(self):
        data=bpy.data.armatures.new('LMS_'+self.species+'Skeleton')
        obj=bpy.data.objects.new('LMS_'+self.species+'Rig',data)
        bpy.context.collection.objects.link(obj); bpy.context.view_layer.objects.active=obj
        obj.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
        for name,head,tail,parent,deform,roll_front in self.bones:
            b=data.edit_bones.new(name); b.head=head; b.tail=tail
            if parent: b.parent=data.edit_bones[parent]
            b.use_deform=deform
            if roll_front:b.align_roll(Vector(roll_front if isinstance(roll_front,(tuple,list)) else (0,-1,0)))
        bpy.ops.object.mode_set(mode='OBJECT')
        for m in [o for o in bpy.context.scene.objects if o.type=='MESH']:
            m.parent=obj
            mod=m.modifiers.new('Character skin','ARMATURE'); mod.object=obj
        for b in obj.pose.bones: b.rotation_mode='XYZ'
        obj.show_in_front=True; self.rig=obj

    def clip(self, name, end, poses):
        self.rig.animation_data_create(); self.rig.animation_data.action=None
        previous={}
        for frame, changes in poses:
            for b in self.rig.pose.bones:
                b.rotation_mode='QUATERNION'; b.rotation_quaternion=(1,0,0,0)
                b.location=(0,0,0); b.scale=(1,1,1)
            for bone, transform in changes.items():
                b=self.rig.pose.bones[bone]
                if isinstance(transform,dict):
                    for prop,val in transform.items():
                        if prop=='rotation_euler': b.rotation_quaternion=Euler(val,'XYZ').to_quaternion()
                        else: setattr(b,prop,val)
                else: b.rotation_quaternion=Euler(transform,'XYZ').to_quaternion()
            for b in self.rig.pose.bones:
                if b.name in previous: b.rotation_quaternion.make_compatible(previous[b.name])
                previous[b.name]=b.rotation_quaternion.copy()
                for prop in ('rotation_quaternion','location','scale'): b.keyframe_insert(prop,frame=frame)
        action=self.rig.animation_data.action
        action.name=self.species+'_'+name; action.use_fake_user=True
        self.clips.append({'name':action.name,'start':1,'end':end,'fps':FPS,'duration_seconds':(end-1)/FPS,
                           'loop':name in ['Idle','Walk','Run','Fly','Hover','PerchIdle','SurfaceWalk','BiteLoop'],'root_motion':False})
        self.rig.animation_data.action=None
        for b in self.rig.pose.bones:
            b.rotation_quaternion=(1,0,0,0); b.location=(0,0,0); b.scale=(1,1,1)
        bpy.context.view_layer.update()

    def export(self):
        scene=bpy.context.scene; scene.frame_start=1; scene.frame_end=61; scene.frame_set(1)
        self.rig.animation_data_create()
        self.rig.animation_data.action=None
        meshes=[o for o in scene.objects if o.type=='MESH']
        # Four human renderers permit hiding the head in first person and independent hands.
        buckets={}
        head_prefixes=('Head','Neck','Ear.','EyeWhite.','Pupil.','Brow.','Mouth','Nightcap')
        for obj in meshes:
            if obj.name.startswith('WingMembrane.'): key='MosquitoMembranes'
            elif obj.name.startswith(('WingLeadingEdge.','WingVein.')): key='MosquitoVeins'
            elif self.species=='Mosquito': key='MosquitoSkin'
            elif self.species=='Flyswatter': key='FlyswatterMesh'
            elif obj.name.startswith('HandSkin.'): key=obj.name
            elif obj.name.startswith('Nightcap'): key='HumanNightcap'
            elif obj.name.startswith(head_prefixes): key='HumanHead'
            else: key='HumanBody'
            buckets.setdefault(key,[]).append(obj)
        for name,objects in buckets.items():
            bpy.ops.object.select_all(action='DESELECT')
            for obj in objects: obj.select_set(True)
            bpy.context.view_layer.objects.active=objects[0]
            if len(objects)>1: bpy.ops.object.join()
            bpy.context.object.name=name
        meshes=[o for o in scene.objects if o.type=='MESH']
        if self.species=='Human':
            from author_human_facial import finish_shapes
            for obj in meshes:finish_shapes(self,obj)
        # Recalculate consistently outward normals after procedural lofts.
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True); bpy.context.view_layer.objects.active=obj
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
            obj.select_set(False)
        lid_winding=None
        if self.species=='Human':
            from author_human_facial import orient_lid_faces
            for obj in meshes:
                if obj.name=='HumanHead':lid_winding=orient_lid_faces(self,obj)
        audit={'species':self.species,'mesh_count':len(meshes),'bones':len(self.rig.data.bones),
               'triangles':0,'vertices':0,'unweighted_vertices':0,'bad_weight_sums':0,
               'degenerate_triangles':0,'nonfinite_vertices':0,'clips':self.clips,'curl':self.curl,'contact':self.contact}
        bounds=[]
        for o in meshes:
            o.data.calc_loop_triangles()
            audit['triangles']+=len(o.data.loop_triangles); audit['vertices']+=len(o.data.vertices)
            for t in o.data.loop_triangles:
                if t.area < 1e-12: audit['degenerate_triangles']+=1
            for v in o.data.vertices:
                p=o.matrix_world@v.co; bounds.append(list(p))
                if not all(math.isfinite(x) for x in p): audit['nonfinite_vertices']+=1
                if not v.groups: audit['unweighted_vertices']+=1
                if abs(sum(g.weight for g in v.groups)-1)>1e-5: audit['bad_weight_sums']+=1
        audit['bounds_min']=[min(v[i] for v in bounds) for i in range(3)]
        audit['bounds_max']=[max(v[i] for v in bounds) for i in range(3)]
        audit['dimensions']=[audit['bounds_max'][i]-audit['bounds_min'][i] for i in range(3)]
        audit['sockets']=[b.name for b in self.rig.data.bones if b.name.startswith('Socket.')]
        audit['renderers']=[{'name':o.name,'materials':[m.name for m in o.data.materials]} for o in meshes]
        audit['blend_shapes']={o.name:[k.name for k in o.data.shape_keys.key_blocks if k.name!='Basis']
                               for o in meshes if o.data.shape_keys}
        if self.species=='Human':
            audit['facial_contract']=getattr(self,'facial_contract',{})
            audit['eyelid_winding']=lid_winding
        audit['bone_names']=[b.name for b in self.rig.data.bones]
        audit['bind_bones']=[{'name':b.name,'parent':b.parent.name if b.parent else '',
                              'head_blender_m':list(b.head_local),'tail_blender_m':list(b.tail_local)} for b in self.rig.data.bones]
        audit['materials']=sorted({m.name for o in meshes for m in o.data.materials})
        audit['material_palette']=[{'name':m.name,'color':dict(zip(('r','g','b','a'),m.diffuse_color)),
                                    'roughness':m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value}
                                   for m in sorted({m for o in meshes for m in o.data.materials},key=lambda m:m.name)]
        errors=[k for k in ('unweighted_vertices','bad_weight_sums','degenerate_triangles','nonfinite_vertices') if audit[k]]
        if self.species=='Human' and not all(v['inward_displacement_m']>.01 for v in self.curl.values()): errors.append('finger_curl')
        if self.species=='Human' and abs(self.contact['clap_palm_center_distance_m']-.05)>.001: errors.append('clap_contact')
        audit['passed']=not errors; audit['errors']=errors
        folder=OUT/self.species.lower(); folder.mkdir(exist_ok=True)
        (folder/'audit.json').write_text(json.dumps(audit,indent=2),encoding='utf8',newline='\n')
        if errors: raise RuntimeError(audit)
        bpy.ops.object.select_all(action='DESELECT'); self.rig.select_set(True)
        for o in meshes: o.select_set(True)
        bpy.context.view_layer.objects.active=self.rig
        bpy.ops.wm.save_as_mainfile(filepath=str(folder/('LMS_'+self.species+'_alpha.blend')))
        bpy.ops.export_scene.fbx(filepath=str(folder/('LMS_'+self.species+'_alpha.fbx')),
            use_selection=True,object_types={'ARMATURE','MESH'},global_scale=1,
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
            use_mesh_modifiers=True,add_leaf_bones=False,use_armature_deform_only=False,
            bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0,path_mode='AUTO',mesh_smooth_type='FACE')
        return audit


def human():
    from author_human_geometry import head_and_cap,collar_and_pocket,slipper
    from author_human_joints import clothing,wrist_weights
    c=Character('Human')
    skin=material('Human_Skin',(.67,.43,.27),.82); blue=material('Human_Pajamas',(.12,.30,.47),.87)
    trim=material('Human_Piping',(.53,.67,.71),.87); sole=material('Human_SlipperSole',(.065,.10,.15),.90)
    white=material('Character_EyeWhite',(.84,.83,.76),.78); dark=material('Character_Expression',(.045,.032,.045),.82)
    c.bone('Root',(0,0,0),(0,0,.10),deform=False)
    c.bone('Hips',(0,0,.75),(0,0,.88),'Root')
    c.bone('Spine',(0,0,.88),(0,0,1.05),'Hips'); c.bone('Chest',(0,0,1.05),(0,0,1.23),'Spine')
    c.bone('Neck',(0,0,1.23),(0,0,1.34),'Chest'); c.bone('Head',(0,0,1.34),(0,0,1.70),'Neck')
    c.bone('Jaw',(0,-.015,1.46),(0,-.035,1.393),'Head')
    c.bone('Socket.Eye',(0,-.17,1.53),(0,-.22,1.53),'Head',False)
    c.bone('Socket.Head',(0,0,1.72),(0,0,1.77),'Head',False)
    c.bone('Socket.Back',(0,.15,1.09),(0,.20,1.09),'Chest',False)
    c.bone('Socket.AimChest',(0,-.15,1.09),(0,-.20,1.09),'Chest',False)
    for side,s in [('L',1),('R',-1)]:
        c.bone('Shoulder.'+side,(s*.05,0,1.20),(s*.25,0,1.17),'Chest')
        c.bone('UpperArm.'+side,(s*.25,0,1.17),(s*.52,0,1.16),'Shoulder.'+side)
        c.bone('LowerArm.'+side,(s*.52,0,1.16),(s*.75,0,1.15),'UpperArm.'+side)
        c.bone('Hand.'+side,(s*.75,0,1.15),(s*.837,0,1.15),'LowerArm.'+side)
        c.bone('Socket.Grip.'+side,(s*.85,-.040,1.15),(s*.85,-.090,1.15),'Hand.'+side,False)
        c.bone('UpperLeg.'+side,(s*.125,0,.78),(s*.125,0,.44),'Hips')
        c.bone('LowerLeg.'+side,(s*.125,0,.44),(s*.125,0,.12),'UpperLeg.'+side)
        c.bone('Foot.'+side,(s*.125,0,.12),(s*.125,-.12,.07),'LowerLeg.'+side)
        c.bone('Toe.'+side,(s*.125,-.12,.07),(s*.125,-.20,.07),'Foot.'+side)
        c.bone('Socket.Foot.'+side,(s*.125,-.07,0),(s*.125,-.12,0),'Foot.'+side,False)
        c.bone('Eye.'+side,(s*.081,-.108,1.558),(s*.081,-.168,1.558),'Head',roll_front=(0,0,1))
        c.bone('Brow.'+side,(s*.088,-.17,1.64),(s*.088,-.21,1.64),'Head')
        clothing(mesh,tube,side,s,blue,trim)
        tube('TrouserCuff.'+side,[(s*.125,0,.127),(s*.125,0,.164)],[.073]*2,[.082]*2,blue,'LowerLeg.'+side,10)
        tube('TrouserHem.'+side,[(s*.125,0,.128),(s*.125,0,.133)],[.074]*2,[.083]*2,trim,'LowerLeg.'+side,10)
        slipper(side,s,mesh,tube,strip,skin,blue,trim,sole)
        # Hands are a single welded surface per side; final weights follow the finger chains.
        parts=[tube('Palm.'+side,[(s*x,0,1.15) for x in [.687,.710,.735,.752,.775,.806,.835]],
                    [.030,.030,.029,.033,.044,.049,.047],
                    [.024,.023,.020,.019,.022,.024,.021],skin,'Hand.'+side,12)]
        paths=[]
        for digit,zoff,length in [('Index',.034,.104),('Middle',.009,.116),('Ring',-.017,.106),('Little',-.043,.082),('Thumb',.040,.070)]:
            length*=.85
            start=Vector((s*(.822 if digit!='Thumb' else .782),0,1.15+zoff))
            direction=Vector((s,0,.72 if digit=='Thumb' else 0)).normalized()
            lengths=[length*.43,length*.32,length*.25]; points=[start]
            parent='Hand.'+side
            for i,ln in enumerate(lengths):
                name=f'{digit}{i+1:02d}.{side}'; end=points[-1]+direction*ln
                c.bone(name,points[-1],end,parent); paths.append((name,points[-1].copy(),end.copy(),digit))
                points.append(end); parent=name
            radii=([.018,.014,.0105,.007] if digit=='Thumb' else
                   [.0095,.009,.0075,.005] if digit=='Little' else
                   [.011,.0095,.0085,.006])
            parts.append(tube(digit+'.'+side,points,radii,radii,skin,'Hand.'+side,8))
        bpy.ops.object.select_all(action='DESELECT')
        for p in parts: p.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
        hand=bpy.context.object; hand.name='HandSkin.'+side
        rem=hand.modifiers.new('Weld palm and finger webs','REMESH'); rem.mode='VOXEL'; rem.voxel_size=.003
        bpy.ops.object.modifier_apply(modifier=rem.name)
        sm=hand.modifiers.new('Soften webbing','SMOOTH'); sm.factor=.55; sm.iterations=3
        bpy.ops.object.modifier_apply(modifier=sm.name)
        dec=hand.modifiers.new('Controlled low poly hand','DECIMATE'); dec.ratio=.16
        bpy.ops.object.modifier_apply(modifier=dec.name)
        hand.vertex_groups.clear()
        groups={name:hand.vertex_groups.new(name=name) for name in ['Hand.'+side,'LowerArm.'+side]+[p[0] for p in paths]}
        from author_hand_weights import hand_weights
        assigned=hand_weights([tuple(v.co) for v in hand.data.vertices],
                              [tuple(edge.vertices) for edge in hand.data.edges],paths,side)
        for i,weight in enumerate(assigned):
            weight=wrist_weights(hand.data.vertices[i].co,side,weight)
            for bone,value in weight.items():groups[bone].add([i],value,'REPLACE')
        for poly in hand.data.polygons: poly.use_smooth=False
        c.finger_paths[side]=paths
    # Continuous trouser seat behind the jacket joins the legs above the crotch.
    tube('TrouserSeat',[(0,0,z) for z in [.685,.72,.77,.805]],
         [.170,.211,.216,.208],[.087,.112,.119,.113],blue,'Hips',12)
    torso=tube('PajamaJacket',[(0,0,z) for z in [.735,.78,.82,.96,1.07,1.17,1.235,1.285]],
               [.224,.224,.214,.185,.210,.232,.191,.070],
               [.116,.126,.124,.115,.129,.128,.120,.070],blue)
    groups={n:torso.vertex_groups.new(name=n) for n in ['Hips','Spine','Chest']}
    for v in torso.data.vertices:
        weights=(('Hips',1-max(0,min(1,(v.co.z-.79)/.15))),('Chest',max(0,min(1,(v.co.z-1.02)/.13))))
        wh,wc=[p[1] for p in weights]; ws=1-wh-wc
        for n,w in [('Hips',wh),('Spine',ws),('Chest',wc)]:
            if w>0: groups[n].add([v.index],w,'REPLACE')
    strip('JacketPlacket',[(0,y,z) for y,z in [(-.127,.815),(-.125,.86),(-.119,.96),(-.132,1.07),(-.133,1.16)]],.003,trim,'Chest')
    for y,z in [(-.132,.865),(-.125,.966),(-.138,1.072)]:
        ellipsoid('JacketButton',(0,y,z),(.006,.0025,.006),white,'Chest',8,4)
    collar_and_pocket(mesh,strip,blue,trim)
    # Jacket details deform with the same body weights instead of rotating rigidly
    # through the lower torso when crouching or leaning.
    for detail in [o for o in bpy.context.scene.objects if o.name.startswith(('JacketPlacket','JacketButton','PocketPiping','PocketPatch'))]:
        detail.vertex_groups.clear()
        detail_groups={n:detail.vertex_groups.new(name=n) for n in ['Hips','Spine','Chest']}
        for vertex in detail.data.vertices:
            wh=1-max(0,min(1,(vertex.co.z-.79)/.15));wc=max(0,min(1,(vertex.co.z-1.02)/.13))
            for name,weight in [('Hips',wh),('Spine',1-wh-wc),('Chest',wc)]:
                if weight>0:detail_groups[name].add([vertex.index],weight,'REPLACE')
    head_and_cap(c,mesh,tube,ellipsoid,strip,skin,blue,trim,white,dark)
    from author_human_facial import eyelids
    eyelids(c,mesh,skin)
    c.bind()
    # Evaluate positive local X curl against the palm direction on both actual rigs.
    for side in ['L','R']:
        b=c.rig.pose.bones['Index03.'+side]; rest=b.tail.copy()
        for i in range(1,4): c.rig.pose.bones[f'Index{i:02d}.'+side].rotation_euler.x=.55
        bpy.context.view_layer.update(); tip=b.tail.copy()
        c.curl[side]={'inward_displacement_m':(tip-rest).dot(Vector((0,-1,0))), 'angle_radians_per_joint':.55}
        for i in range(1,4): c.rig.pose.bones[f'Index{i:02d}.'+side].rotation_euler.x=0
        bpy.context.view_layer.update()
    from author_motion import human as animate_human
    animate_human(c)
    return c.export()


def mosquito():
    from author_mosquito_geometry import create_mosquito
    from author_mosquito_motion import mosquito as animate_mosquito
    c=create_mosquito(Character=Character, material=material, tube=tube,
                      ellipsoid=ellipsoid, strip=strip, mesh=mesh)
    animate_mosquito(c)
    return c.export()


def flyswatter():
    c=Character('Flyswatter')
    plastic=material('Tool_Teal',(.10,.43,.42)); grip=material('Tool_Grip',(.055,.09,.12))
    c.bone('Root',(0,0,0),(0,0,.1))
    c.bone('Socket.Grip',(0,0,0),(0,-.03,0),'Root',False)
    c.bone('Socket.Impact',(0,-.005,.365),(0,-.035,.365),'Root',False)
    tube('Handle',[(0,0,z) for z in [-.065,-.025,.19,.265,.28]],
         [.014,.016,.009,.013,.024],[.009,.011,.006,.007,.006],plastic,'Root',10)
    tube('GripSleeve',[(0,0,z) for z in [-.045,.055]],[.017,.015],[.012,.011],grip,'Root',10)
    count=24; verts=[]
    for y,rx,rz in [(-.005,.085,.105),(-.005,.074,.094),(.005,.085,.105),(.005,.074,.094)]:
        verts += [(rx*math.cos(i*2*math.pi/count),y,.365+rz*math.sin(i*2*math.pi/count)) for i in range(count)]
    faces=[]
    for i in range(count):
        j=(i+1)%count
        faces += [(i,j,count+j,count+i),(2*count+j,2*count+i,3*count+i,3*count+j),
                  (j,i,2*count+i,2*count+j),(count+i,count+j,3*count+j,3*count+i)]
    mesh('OpenFrame',verts,faces,plastic,'Root')
    for i in range(-5,6):
        x=i*.012; z=.094*math.sqrt(1-(x/.074)**2)
        strip('VerticalGrid',[(x,0,.365-z),(x,0,.365+z)],.0017,plastic,'Root')
    for i in range(-7,8):
        z=i*.012; x=.074*math.sqrt(1-(z/.094)**2)
        strip('HorizontalGrid',[(-x,0,.365+z),(x,0,.365+z)],.0017,plastic,'Root')
    c.bind()
    return c.export()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description='Generate explicitly selected character assets only.')
    parser.add_argument('--species',nargs='+',choices=['Human','Mosquito','Flyswatter'],required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    selected=list(dict.fromkeys(args.species))
    builders={'Human':human,'Mosquito':mosquito,'Flyswatter':flyswatter}
    results=[builders[species]() for species in selected]
    for species in builders:
        if species not in selected:
            audit_path=OUT/species.lower()/'audit.json'
            if audit_path.exists():results.append(json.loads(audit_path.read_text(encoding='utf8')))
    manifest={'version':'0.9.4-alpha-human-reference-quality-revision-5','generator':Path(__file__).name,
              'blender':bpy.app.version_string,'source_units':'meters','source_up':'+Z','source_forward':'-Y',
              'fbx_axis_forward':'-Z','fbx_axis_up':'Y','unity_human_scale':1,'unity_mosquito_scale':.5,
              'mosquito_collision_radius_m':.055,'human_capsule':{'radius':.25,'height':1.72,'crouched_height':1.0},
              'provenance':'Original authored geometry and materials for Let me sleep. User reference images guide shapes only; no external textures.',
              'rendered':False,'unity_import_verified':False,'audits':results,'files':{}}
    manifest['generated_species']=selected
    manifest['review_status']='New selected-species source; motion, renders and Unity import pending'
    for p in OUT.rglob('*'):
        if p.is_file() and p.suffix in ['.blend','.fbx','.py']:
            manifest['files'][p.relative_to(OUT).as_posix()]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8',newline='\n')
    print('LMS_CHARACTER_GENERATION_PASSED',flush=True)
