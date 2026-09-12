"""Let me sleep: original, reproducible Unity alpha character sources.
Run: N:/Blender/blender.exe --background --factory-startup --python build_characters.py
No rendering occurs. All output stays beside this script. Blender 5.2 LTS.
"""
import bpy
import math
import json
import hashlib
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parent
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


def tube(name, centers, widths, depths, mat, bone=None, sides=12):
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
    faces = [tuple(reversed(range(sides)))]
    for i in range(len(centers)-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple((len(centers)-1)*sides+j for j in range(sides)))
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
            if roll_front: b.align_roll(Vector((0,-1,0)))
        bpy.ops.object.mode_set(mode='OBJECT')
        for m in [o for o in bpy.context.scene.objects if o.type=='MESH']:
            m.parent=obj
            mod=m.modifiers.new('Character skin','ARMATURE'); mod.object=obj
        for b in obj.pose.bones: b.rotation_mode='XYZ'
        obj.show_in_front=True; self.rig=obj

    def clip(self, name, end, poses):
        self.rig.animation_data_create(); self.rig.animation_data.action=None
        for frame, changes in poses:
            for b in self.rig.pose.bones:
                b.rotation_euler=(0,0,0); b.location=(0,0,0); b.scale=(1,1,1)
            for bone, transform in changes.items():
                b=self.rig.pose.bones[bone]
                if isinstance(transform,dict):
                    for prop,val in transform.items(): setattr(b,prop,val)
                else: b.rotation_euler=transform
            for b in self.rig.pose.bones:
                for prop in ('rotation_euler','location','scale'): b.keyframe_insert(prop,frame=frame)
        action=self.rig.animation_data.action
        action.name=self.species+'_'+name; action.use_fake_user=True
        self.clips.append({'name':action.name,'start':1,'end':end,'fps':FPS,'duration_seconds':(end-1)/FPS,
                           'loop':name in ['Idle','Walk','Run','Fly'],'root_motion':False})
        self.rig.animation_data.action=None
        for b in self.rig.pose.bones:
            b.rotation_euler=(0,0,0); b.location=(0,0,0); b.scale=(1,1,1)
        bpy.context.view_layer.update()

    def export(self):
        scene=bpy.context.scene; scene.frame_start=1; scene.frame_end=61; scene.frame_set(1)
        self.rig.animation_data.action=None
        meshes=[o for o in scene.objects if o.type=='MESH']
        # Four human renderers permit hiding the head in first person and independent hands.
        buckets={}
        head_prefixes=('Head','Neck','Ear.','EyeWhite.','Pupil.','Brow.','Mouth','Nightcap')
        for obj in meshes:
            if obj.name.startswith('WingMembrane.'): key='MosquitoMembranes'
            elif obj.name.startswith(('WingLeadingEdge.','WingVein.')): key='MosquitoVeins'
            elif self.species=='Mosquito': key='MosquitoSkin'
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
        # Recalculate consistently outward normals after procedural lofts.
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True); bpy.context.view_layer.objects.active=obj
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
            obj.select_set(False)
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
        audit['bone_names']=[b.name for b in self.rig.data.bones]
        audit['materials']=sorted({m.name for o in meshes for m in o.data.materials})
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
    c=Character('Human')
    skin=material('Human_Skin',(.72,.40,.25)); blue=material('Human_Pajamas',(.12,.32,.51))
    trim=material('Human_Piping',(.55,.78,.80)); sole=material('Human_SlipperSole',(.065,.10,.15))
    white=material('Character_EyeWhite',(.94,.92,.83)); dark=material('Character_Expression',(.045,.032,.045))
    c.bone('Root',(0,0,0),(0,0,.10),deform=False)
    c.bone('Hips',(0,0,.75),(0,0,.88),'Root')
    c.bone('Spine',(0,0,.88),(0,0,1.05),'Hips'); c.bone('Chest',(0,0,1.05),(0,0,1.23),'Spine')
    c.bone('Neck',(0,0,1.23),(0,0,1.34),'Chest'); c.bone('Head',(0,0,1.34),(0,0,1.70),'Neck')
    c.bone('Socket.Eye',(0,-.17,1.53),(0,-.22,1.53),'Head',False)
    c.bone('Socket.Head',(0,0,1.72),(0,0,1.77),'Head',False)
    c.bone('Socket.Back',(0,.15,1.09),(0,.20,1.09),'Chest',False)
    c.bone('Socket.AimChest',(0,-.15,1.09),(0,-.20,1.09),'Chest',False)
    for side,s in [('L',1),('R',-1)]:
        c.bone('Shoulder.'+side,(s*.05,0,1.20),(s*.25,0,1.17),'Chest')
        c.bone('UpperArm.'+side,(s*.25,0,1.17),(s*.52,0,1.16),'Shoulder.'+side)
        c.bone('LowerArm.'+side,(s*.52,0,1.16),(s*.75,0,1.15),'UpperArm.'+side)
        c.bone('Hand.'+side,(s*.75,0,1.15),(s*.837,0,1.15),'LowerArm.'+side)
        c.bone('Socket.Grip.'+side,(s*.85,-.025,1.15),(s*.85,-.075,1.15),'Hand.'+side,False)
        c.bone('UpperLeg.'+side,(s*.125,0,.78),(s*.125,0,.44),'Hips')
        c.bone('LowerLeg.'+side,(s*.125,0,.44),(s*.125,0,.12),'UpperLeg.'+side)
        c.bone('Foot.'+side,(s*.125,0,.12),(s*.125,-.12,.07),'LowerLeg.'+side)
        c.bone('Toe.'+side,(s*.125,-.12,.07),(s*.125,-.20,.07),'Foot.'+side)
        c.bone('Socket.Foot.'+side,(s*.125,-.07,0),(s*.125,-.12,0),'Foot.'+side,False)
        for eye in ['Eye','Brow']:
            z=1.55 if eye=='Eye' else 1.64
            c.bone(eye+'.'+side,(s*.088,-.17,z),(s*.088,-.21,z),'Head')
        # Tailored sleeves use a shared elbow loop and two bone blend weights.
        sleeve=tube('Sleeve.'+side,[(s*x,0,z) for x,z in [(.20,1.17),(.29,1.17),(.49,1.16),(.54,1.16),(.69,1.15),(.735,1.15)]],
                    [.06,.105,.088,.087,.061,.061],[.06,.105,.088,.087,.061,.061],blue)
        gu=sleeve.vertex_groups.new(name='UpperArm.'+side); gl=sleeve.vertex_groups.new(name='LowerArm.'+side)
        for v in sleeve.data.vertices:
            w=max(0,min(1,(abs(v.co.x)-.48)/.08))
            if w<1: gu.add([v.index],1-w,'REPLACE')
            if w>0: gl.add([v.index],w,'REPLACE')
        tube('Cuff.'+side,[(s*.706,0,1.15),(s*.744,0,1.15)],[.065]*2,[.065]*2,trim,'LowerArm.'+side)
        leg=tube('PajamaLeg.'+side,[(s*.125,0,z) for z in [.13,.18,.40,.47,.68,.78]],
                 [.086,.088,.093,.099,.119,.12],[.095,.095,.102,.11,.126,.125],blue)
        gu=leg.vertex_groups.new(name='UpperLeg.'+side); gl=leg.vertex_groups.new(name='LowerLeg.'+side)
        for v in leg.data.vertices:
            w=max(0,min(1,(v.co.z-.38)/.12))
            if w>0: gu.add([v.index],w,'REPLACE')
            if w<1: gl.add([v.index],1-w,'REPLACE')
        tube('TrouserCuff.'+side,[(s*.125,0,.13),(s*.125,0,.17)],[.09]*2,[.10]*2,trim,'LowerLeg.'+side)
        shoe=tube('Slipper.'+side,[(s*.125,-.07,z) for z in [0,.015,.035,.055,.10,.14,.15]],
                  [.097,.107,.107,.106,.095,.067,.04],[.169,.18,.18,.18,.155,.11,.07],blue,'Foot.'+side,16)
        shoe.data.materials.append(sole)
        for poly in shoe.data.polygons:
            if poly.center.z<.035: poly.material_index=1
        # Hands are a single welded surface per side; final weights follow the finger chains.
        parts=[ellipsoid('Palm.'+side,(s*.792,0,1.15),(.056,.024,.053),skin,'Hand.'+side,16,8)]
        paths=[]
        for digit,zoff,length in [('Index',.032,.104),('Middle',.009,.116),('Ring',-.015,.106),('Little',-.038,.082),('Thumb',.055,.070)]:
            start=Vector((s*(.822 if digit!='Thumb' else .782),0,1.15+zoff))
            direction=Vector((s,0,.52 if digit=='Thumb' else 0)).normalized()
            lengths=[length*.43,length*.32,length*.25]; points=[start]
            parent='Hand.'+side
            for i,ln in enumerate(lengths):
                name=f'{digit}{i+1:02d}.{side}'; end=points[-1]+direction*ln
                c.bone(name,points[-1],end,parent); paths.append((name,points[-1].copy(),end.copy(),digit))
                points.append(end); parent=name
            radii=[.011,.0105,.009,.005]
            parts.append(tube(digit+'.'+side,points,radii,radii,skin,'Hand.'+side,8))
        bpy.ops.object.select_all(action='DESELECT')
        for p in parts: p.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
        hand=bpy.context.object; hand.name='HandSkin.'+side
        rem=hand.modifiers.new('Weld palm and finger webs','REMESH'); rem.mode='VOXEL'; rem.voxel_size=.003
        bpy.ops.object.modifier_apply(modifier=rem.name)
        sm=hand.modifiers.new('Soften webbing','SMOOTH'); sm.factor=.55; sm.iterations=3
        bpy.ops.object.modifier_apply(modifier=sm.name)
        dec=hand.modifiers.new('Controlled low poly hand','DECIMATE'); dec.ratio=.28
        bpy.ops.object.modifier_apply(modifier=dec.name)
        hand.vertex_groups.clear()
        groups={name:hand.vertex_groups.new(name=name) for name in ['Hand.'+side]+[p[0] for p in paths]}
        for v in hand.data.vertices:
            candidates=[]
            for name,a,b,digit in paths:
                d=b-a; t=max(0,min(1,(v.co-a).dot(d)/d.length_squared)); dist=(v.co-(a+d*t)).length
                candidates.append((dist,name,a,b))
            _,name,a,b=min(candidates,key=lambda p:p[0])
            digit=name[:-4]; digitpaths=[p for p in paths if p[3]==digit]
            if not digitpaths: raise RuntimeError(name)
            start=digitpaths[0][1]; direction=(digitpaths[-1][2]-start).normalized()
            longitudinal=(v.co-start).dot(direction)
            blend=max(0,min(1,(longitudinal+.007)/.023))
            if blend<1: groups['Hand.'+side].add([v.index],1-blend,'REPLACE')
            if blend>0: groups[name].add([v.index],blend,'REPLACE')
        # Smooth skin weights across welded joints, preserving a four-influence ceiling.
        neighbors=[set() for _ in hand.data.vertices]
        for edge in hand.data.edges:
            a,b=edge.vertices; neighbors[a].add(b); neighbors[b].add(a)
        weights=[{g.group:g.weight for g in v.groups} for v in hand.data.vertices]
        for _ in range(3):
            smoothed=[]
            for i,weight in enumerate(weights):
                result={k:v*.55 for k,v in weight.items()}
                for j in neighbors[i]:
                    for k,v in weights[j].items(): result[k]=result.get(k,0)+v*.45/len(neighbors[i])
                strongest=sorted(result.items(),key=lambda kv:kv[1],reverse=True)[:4]
                total=sum(v for _,v in strongest); smoothed.append({k:v/total for k,v in strongest})
            weights=smoothed
        for group in hand.vertex_groups: group.remove(list(range(len(hand.data.vertices))))
        for i,weight in enumerate(weights):
            for group,w in weight.items(): hand.vertex_groups[group].add([i],w,'REPLACE')
        c.finger_paths[side]=paths
    torso=tube('PajamaJacket',[(0,0,z) for z in [.73,.81,.94,1.09,1.18,1.23,1.28]],
               [.235,.245,.23,.235,.255,.22,.095],[.135,.15,.142,.145,.14,.135,.105],blue)
    groups={n:torso.vertex_groups.new(name=n) for n in ['Hips','Spine','Chest']}
    for v in torso.data.vertices:
        weights=(('Hips',1-max(0,min(1,(v.co.z-.79)/.15))),('Chest',max(0,min(1,(v.co.z-1.02)/.13))))
        wh,wc=[p[1] for p in weights]; ws=1-wh-wc
        for n,w in [('Hips',wh),('Spine',ws),('Chest',wc)]:
            if w>0: groups[n].add([v.index],w,'REPLACE')
    strip('JacketPlacket',[(0,-.153,.83),(0,-.153,1.18)],.008,trim,'Chest')
    for z in [.91,1.015,1.12]: ellipsoid('JacketButton',(0,-.164,z),(.009,.004,.009),white,'Chest',8,4)
    strip('Collar.L',[(-.09,-.08,1.275),(-.075,-.144,1.205),(0,-.151,1.175)],.018,trim,'Chest')
    strip('Collar.R',[(.09,-.08,1.275),(.075,-.144,1.205),(0,-.151,1.175)],.018,trim,'Chest')
    strip('PocketPiping',[(.07,-.145,1.065),(.14,-.129,1.065)],.008,trim,'Chest')
    tube('Neck',[(0,0,1.22),(0,0,1.37)],[.071,.083],[.067,.075],skin,'Neck')
    head=tube('Head',[(0,0,z) for z in [1.30,1.35,1.47,1.58,1.67,1.72]],
              [.12,.19,.22,.215,.17,.08],[.115,.15,.185,.175,.145,.07],skin,'Head',20)
    # A small integral nose, never attached cheek spheres.
    for v in head.data.vertices:
        if abs(v.co.x)<.01 and v.co.y<-.18 and abs(v.co.z-1.47)<.005: v.co.y-=.025
    for side,s in [('L',1),('R',-1)]:
        ellipsoid('Ear.'+side,(s*.211,0,1.50),(.033,.026,.048),skin,'Head',12,6)
        ellipsoid('EyeWhite.'+side,(s*.087,-.169,1.552),(.072,.043,.077),white,'Eye.'+side)
        ellipsoid('Pupil.'+side,(s*.077,-.209,1.552),(.025,.009,.035),dark,'Eye.'+side,12,6)
        strip('Brow.'+side,[(s*.028,-.174,1.642),(s*.083,-.19,1.658),(s*.148,-.154,1.641)],.012,dark,'Brow.'+side)
    strip('Mouth',[(-.048,-.158,1.403),(0,-.177,1.393),(.048,-.158,1.403)],.006,dark,'Head')
    tube('Nightcap',[(0,0,1.666),(0,.015,1.75),(.045,.024,1.85),(.13,.025,1.88),(.20,.02,1.81)],
         [.174,.145,.09,.05,.009],[.145,.13,.081,.05,.009],blue,'Head',16)
    tube('NightcapBand',[(0,0,1.654),(0,.002,1.69)],[.177,.174],[.15,.151],trim,'Head',20)
    ellipsoid('NightcapPom',(.20,.02,1.80),(.037,.035,.045),white,'Head',12,6)
    c.bind()
    # Evaluate positive local X curl against the palm direction on both actual rigs.
    for side in ['L','R']:
        b=c.rig.pose.bones['Index03.'+side]; rest=b.tail.copy()
        for i in range(1,4): c.rig.pose.bones[f'Index{i:02d}.'+side].rotation_euler.x=.55
        bpy.context.view_layer.update(); tip=b.tail.copy()
        c.curl[side]={'inward_displacement_m':(tip-rest).dot(Vector((0,-1,0))), 'angle_radians_per_joint':.55}
        for i in range(1,4): c.rig.pose.bones[f'Index{i:02d}.'+side].rotation_euler.x=0
        bpy.context.view_layer.update()
    def relaxed(): return {'UpperArm.L':(0,0,-1.20),'UpperArm.R':(0,0,1.20),'LowerArm.L':(.12,0,0),'LowerArm.R':(.12,0,0)}
    idle=relaxed(); breathe={**idle,'Chest':(.025,0,0)}
    c.clip('Idle',61,[(1,idle),(31,breathe),(61,idle)])
    curled={name:(.65,0,0) for side in ['L','R'] for name,*_ in c.finger_paths[side]}
    c.clip('FingerCurl',61,[(1,{}),(16,curled),(46,curled),(61,{})])
    closed={**idle,'Eye.L':{'scale':(1,1,.09)},'Eye.R':{'scale':(1,1,.09)}}
    c.clip('Blink',31,[(1,idle),(12,idle),(15,closed),(18,idle),(31,idle)])
    for name,angle,duration in [('Walk',.40,31),('Run',.68,21)]:
        poses=[]
        for frame,sign in [(1,1),((duration+1)//2,-1),(duration,1)]:
            p=relaxed()
            for side,s in [('L',1),('R',-1)]:
                p['UpperLeg.'+side]=(sign*s*angle,0,0)
                p['LowerLeg.'+side]=(-max(0,sign*s)*angle*.85,0,0)
                p['UpperArm.'+side]=(-sign*s*angle*.6,0,-s*1.2)
            poses.append((frame,p))
        c.clip(name,duration,poses)
    crouch={**idle,'Hips':{'location':(0,-.30,0)},'UpperLeg.L':(.75,0,0),'UpperLeg.R':(.75,0,0),'LowerLeg.L':(-1.25,0,0),'LowerLeg.R':(-1.25,0,0),'Chest':(-.25,0,0)}
    c.clip('Crouch',31,[(1,idle),(16,crouch),(31,crouch)])
    c.clip('Jump',31,[(1,idle),(8,crouch),(16,{**idle,'Hips':{'location':(0,.15,0)},'LowerLeg.L':(-.5,0,0),'LowerLeg.R':(-.5,0,0)}),(25,crouch),(31,idle)])
    c.clip('Turn',31,[(1,idle),(16,{**idle,'Chest':(0,.28,0),'Head':(0,.22,0)}),(31,idle)])
    def solve_clap(separation):
        """Analytic two-bone reach; contact uses posed palms, not guessed angles."""
        for pb in c.rig.pose.bones: pb.rotation_euler=(0,0,0)
        bpy.context.view_layer.update()
        pose={}; centers=[]
        def point_bone(name,origin,direction,normal):
            y=direction.normalized(); z=(normal-y*normal.dot(y)).normalized(); x=y.cross(z).normalized()
            rotation=Matrix((x,y,z)).transposed().to_4x4(); rotation.translation=origin
            pb=c.rig.pose.bones[name]; pb.matrix=rotation
            bpy.context.view_layer.update(); pose[name]=tuple(pb.rotation_euler)
        for side,s in [('L',1),('R',-1)]:
            upper=c.rig.pose.bones['UpperArm.'+side]; lower=c.rig.pose.bones['LowerArm.'+side]
            shoulder=upper.head.copy(); wrist=Vector((s*separation,-.38,1.05))
            vector=wrist-shoulder; distance=vector.length; direction=vector.normalized()
            a=upper.length; b=lower.length
            along=(a*a-b*b+distance*distance)/(2*distance)
            pole=Vector((s*.65,-.12,.96))-shoulder
            perpendicular=(pole-direction*pole.dot(direction)).normalized()
            elbow=shoulder+direction*along+perpendicular*math.sqrt(max(0,a*a-along*along))
            point_bone('UpperArm.'+side,shoulder,elbow-shoulder,Vector((0,-1,0)))
            point_bone('LowerArm.'+side,elbow,wrist-elbow,Vector((0,-1,0)))
            point_bone('Hand.'+side,wrist,Vector((0,0,1)),Vector((-s,0,0)))
            centers.append(c.rig.pose.bones['Hand.'+side].matrix@Vector((0,.042,0)))
        if abs(separation-.025)<1e-6:
            c.contact={'clap_palm_center_distance_m':(centers[0]-centers[1]).length,
                       'approximate_palm_thickness_m':.048,'target_surface_gap_m':.002}
        for pb in c.rig.pose.bones: pb.rotation_euler=(0,0,0)
        bpy.context.view_layer.update()
        return pose
    clap_open=solve_clap(.15); clap_contact=solve_clap(.025)
    c.clip('Clap',25,[(1,idle),(8,clap_open),(12,clap_contact),(16,clap_open),(25,idle)])
    c.clip('Fall',31,[(1,idle),(16,{**idle,'Hips':{'rotation_euler':(1.2,0,.15),'location':(0,-.4,0)}}),(31,{**idle,'Hips':{'rotation_euler':(1.57,0,.15),'location':(0,-.60,0)}})])
    return c.export()


def mosquito():
    c=Character('Mosquito')
    shell=material('Mosquito_Shell',(.46,.12,.10)); belly=material('Mosquito_Abdomen',(.70,.24,.14))
    dark=material('Mosquito_Legs',(.095,.055,.075)); eye=material('Mosquito_EyeWhite',(.94,.91,.79))
    pupil=material('Mosquito_Expression',(.035,.027,.05)); wing=material('Mosquito_Wing',(.60,.76,.84,.72),.4)
    def p(x,y,z): return (x,y,z-.105)
    c.bone('Root',(0,0,0),(0,0,.03),deform=False)
    c.bone('Thorax',p(0,.015,.105),p(0,-.035,.105),'Root')
    c.bone('Head',p(0,-.045,.105),p(0,-.085,.105),'Thorax')
    c.bone('Abdomen01',p(0,.035,.103),p(0,.105,.083),'Thorax')
    c.bone('Abdomen02',p(0,.105,.083),p(0,.185,.046),'Abdomen01')
    c.bone('Proboscis',p(0,-.10,.09),p(0,-.23,.065),'Head')
    c.bone('Socket.Mouth',p(0,-.23,.065),p(0,-.24,.063),'Proboscis',False)
    c.bone('Socket.Back',p(0,.015,.145),p(0,.015,.16),'Thorax',False)
    c.bone('Socket.CameraTarget',(0,0,0),(0,-.02,0),'Thorax',False)
    c.bone('Socket.AimForward',(0,-.08,0),(0,-.12,0),'Head',False)
    c.bone('Socket.GroundContact',p(0,0,.0026),p(0,-.02,.0026),'Root',False)
    ellipsoid('Thorax',p(0,0,.105),(.036,.056,.038),shell,'Thorax')
    ellipsoid('Head',p(0,-.068,.105),(.041,.038,.039),shell,'Head')
    abdomen=tube('Abdomen',[p(0,y,z) for y,z in [(.03,.103),(.067,.098),(.106,.085),(.143,.068),(.17,.054),(.19,.042)]],
                   [.025,.033,.030,.024,.015,.003],[.025,.031,.028,.021,.013,.003],belly)
    a=abdomen.vertex_groups.new(name='Abdomen01'); b=abdomen.vertex_groups.new(name='Abdomen02')
    for v in abdomen.data.vertices:
        w=max(0,min(1,(v.co.y-.088)/.035))
        if w<1: a.add([v.index],1-w,'REPLACE')
        if w>0: b.add([v.index],w,'REPLACE')
    tube('Proboscis',[p(0,-.096,.092),p(0,-.15,.082),p(0,-.23,.065)],[.006,.004,.0014],[.005,.004,.0014],dark,'Proboscis',8)
    for side,s in [('L',1),('R',-1)]:
        ellipsoid('Eye.'+side,p(s*.022,-.097,.119),(.023,.016,.027),eye,'Head',12,6)
        ellipsoid('Pupil.'+side,p(s*.018,-.112,.118),(.008,.004,.012),pupil,'Head',12,6)
        strip('Brow.'+side,[p(s*.004,-.107,.145),p(s*.023,-.108,.149),p(s*.040,-.093,.141)],.0035,dark,'Head')
        strip('Antenna.'+side,[p(s*.013,-.076,.140),p(s*.026,-.08,.166),p(s*.043,-.086,.171)],.002,dark,'Head')
        c.bone('Wing.'+side,p(s*.017,.0,.132),p(s*.22,.065,.163),'Thorax')
        c.bone('Socket.WingRoot.'+side,p(s*.017,0,.132),p(s*.017,-.02,.132),'Thorax',False)
        verts=[p(s*x,y,z) for x,y,z in [(.017,0,.132),(.125,-.002,.163),(.24,.060,.173),(.193,.104,.167),(.067,.072,.143)]]
        # Thin solid double-sided geometry keeps FBX silhouette independent of culling settings.
        verts += [(x,y,z-.0006) for x,y,z in verts]
        faces=[(0,1,2,3,4),(9,8,7,6,5)]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)]
        mesh('WingMembrane.'+side,verts,faces,wing,'Wing.'+side)
        strip('WingLeadingEdge.'+side,[verts[i] for i in [0,1,2]],.0015,shell,'Wing.'+side)
        strip('WingVein.'+side,[verts[i] for i in [0,3]],.0008,shell,'Wing.'+side)
        for i,y in enumerate([-.032,.006,.040],1):
            pts=[p(s*.026,y,.104),p(s*.065,y-.013,.072),p(s*.092,y+.039,.009),p(s*.101,y+.055,.004)]
            parent='Thorax'
            for j in range(3):
                name=f'Leg{i}{j+1:02d}.{side}'
                c.bone(name,pts[j],pts[j+1],parent); parent=name
                tube('Limb_'+name,[pts[j],pts[j+1]],[.0035-j*.0007,.0030-j*.0007],[.0035-j*.0007,.0030-j*.0007],dark,name,6)
    c.bind()
    c.clip('Idle',31,[(1,{}),(16,{'Abdomen01':(.05,0,0)}),(31,{})])
    flight=[]
    for frame,sign in [(1,1),(3,-1),(5,1),(7,-1),(9,1),(11,-1),(13,1)]:
        pose={'Wing.L':(sign*.62,0,0),'Wing.R':(sign*.62,0,0),'Abdomen01':(-.08,0,0)}
        for side in ['L','R']:
            for i in range(1,4): pose[f'Leg{i}02.{side}']=(.55,0,0)
        flight.append((frame,pose))
    c.clip('Fly',13,flight)
    c.clip('Land',25,[(1,flight[0][1]),(13,{'Wing.L':(.15,.15,0),'Wing.R':(.15,-.15,0)}),(25,{})])
    c.clip('Bite',31,[(1,{}),(9,{'Head':(.22,0,0),'Proboscis':(.16,0,0)}),(23,{'Head':(.25,0,0),'Proboscis':(.16,0,0),'Abdomen01':(-.08,0,0)}),(31,{})])
    c.clip('Hit',25,[(1,{}),(9,{'Thorax':(.7,0,1.4),'Wing.L':(.8,0,0),'Wing.R':(-.3,0,0)}),(25,{'Thorax':(1.5,0,2.6)})])
    return c.export()


if __name__=='__main__':
    results=[human(),mosquito()]
    manifest={'version':'0.9.4-alpha-character-sample-1','generator':Path(__file__).name,
              'blender':bpy.app.version_string,'source_units':'meters','source_up':'+Z','source_forward':'-Y',
              'fbx_axis_forward':'-Z','fbx_axis_up':'Y','unity_human_scale':1,'unity_mosquito_scale':.5,
              'mosquito_collision_radius_m':.055,'human_capsule':{'radius':.25,'height':1.72,'crouched_height':1.0},
              'provenance':'Original authored geometry and materials for Let me sleep. User reference images guide shapes only; no external textures.',
              'rendered':False,'unity_import_verified':False,'audits':results,'files':{}}
    for p in OUT.rglob('*'):
        if p.is_file() and p.suffix in ['.blend','.fbx','.py']:
            manifest['files'][p.relative_to(OUT).as_posix()]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8',newline='\n')
    print('LMS_CHARACTER_GENERATION_PASSED',flush=True)
