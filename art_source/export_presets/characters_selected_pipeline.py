"""Promote the chosen authored bases without rewriting the frozen A/B samples.

Run with Blender 4.5.3: --background --python this.py -- --species both
The input .blend files already contain the selected topology, weights, cosmetic
parts and facial controls. Never remesh them a second time during promotion.
"""
import argparse, hashlib, json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
SELECTED={'human':'A','mosquito':'B'}
sys.path.insert(0,str(ROOT/'art_source/characters/shared'))
from facial_parts import separate_faces, add_human_facial_hair, weld_human_hair_roots
from garment_fit import repair_human_garments

def srgb_to_linear(value):
    return value/12.92 if value<=.04045 else ((value+.055)/1.055)**2.4

def refine_human_cheeks():
    """Flatten only the two inherited cheek lobes into the authored A skull.

    The frozen mesh is already a single welded surface. Preserve its topology,
    head weights, nose, jaw, all facial morph meshes and every body/hand vertex.
    Work in the measured local cheek region and relax the resulting transition.
    """
    head=bpy.data.objects['human_head']
    assert head.data.shape_keys is None
    def smooth(a,b,x):
        t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
    def godot(v):return Vector((v.x,v.z,-v.y))
    def blender(v):return Vector((v.x,-v.z,v.y))
    original=[godot(v.co) for v in head.data.vertices]
    weights=[]
    points=[]
    for q in original:
        # Exclude the central nose and ears, including their smooth junctions.
        weight=smooth(.065,.087,abs(q.x))*(1-smooth(.147,.175,abs(q.x)))
        weight*=smooth(1.425,1.447,q.y)*(1-smooth(1.539,1.566,q.y))
        weight*=smooth(-.165,-.145,q.z)*(1-smooth(-.090,-.061,q.z))
        weights.append(weight)
        r=q.copy()
        if weight>0:
            # Invert only A's scale mapping to recover the continuous skull's
            # elliptical cross section at this height (the sample stays intact).
            y=q.y
            for _ in range(8):y=1.55+(q.y-1.55)/(1-.06*smooth(1.36,1.48,y))
            vy=(y-1.55)/.222
            cross=math.sqrt(max(.01,1-vy*vy))
            rx=.169*(.8+.2*max(0,min(1,(vy+.85)/1.1)))*cross*(1+.08*smooth(1.36,1.48,y))
            rz=.151*cross
            shift=-.008*max(0,1-abs(vy+.42)*2)
            z=-.10+(q.z+.10)/.96 if q.z<-.10 else q.z
            radial=math.sqrt((q.x/rx)**2+((z-shift)/rz)**2)
            factor=1-weight*max(0,1-1.0/radial)
            r.x*=factor
            z=shift+(z-shift)*factor
            r.z=-.10+(z+.10)*.96 if z<-.10 else z
        points.append(r)
    neighbors=[set() for _ in points]
    for edge in head.data.edges:
        a,b=edge.vertices;neighbors[a].add(b);neighbors[b].add(a)
    for _ in range(12):
        result=[]
        for i,q in enumerate(points):
            if weights[i]>0 and neighbors[i]:
                average=sum((points[j] for j in neighbors[i]),Vector())/len(neighbors[i])
                result.append(q.lerp(average,.40*weights[i]))
            else:result.append(q.copy())
        points=result
    changed=[]
    for vertex,q,before in zip(head.data.vertices,points,original):
        vertex.co=blender(q)
        if (q-before).length>1e-7:changed.append((q-before).length)
    head.data.update()
    for polygon in head.data.polygons:polygon.use_smooth=True
    head['cheek_repair']='Continuous A skull envelope; local cheek volume only'
    return {'method':'Localized projection to authored A skull cross-section with twelve weighted relaxation passes',
        'modified_vertices':len(changed),'total_head_vertices':len(points),
        'max_displacement_m':max(changed,default=0),'topology_unchanged':True,
        'facial_morph_meshes_unchanged':True,'rig_weights_unchanged':True,
        'scope':'human_head cheeks only; excludes nose, ears, jaw and all body/hand meshes'}

def add_thumb_controls(rig, bones):
    """Local hand binding repair. The selected vertex positions remain untouched."""
    def to_blender(q):return Vector((q.x,-q.z,q.y))
    additions={}
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='EDIT')
    for side,sign in [('l',-1),('r',1)]:
        wrist=Vector(bones['hand_'+side]['from'])
        direction=(Vector(bones['hand_'+side]['to'])-wrist).normalized()
        align=Vector((0,-1,0)).rotation_difference(direction)
        for finger in range(4):
            x=sign*(-.041+finger*.027)
            base=wrist+align@Vector((x,-.052,-.006))
            joint=wrist+align@Vector((x,-.072,-.015))
            tip=Vector(bones['finger%d_b_%s'%(finger,side)]['to'])
            for part,a,b,parent in [('a',base,joint,'hand_'+side),('b',joint,tip,'finger%d_a_%s'%(finger,side))]:
                name='finger%d_%s_%s'%(finger,part,side)
                bone=rig.data.edit_bones[name];bone.head=to_blender(a);bone.tail=to_blender(b)
                additions[name]={'from':list(a),'to':list(b),'parent':parent}
        points=[wrist+align@Vector((-sign*.038,-.012,0)),
                wrist+align@Vector((-sign*.068,-.038,-.006)),
                wrist+align@Vector((-sign*.058,-.069,-.021))]
        for part,a,b,parent in [('a',points[0],points[1],'hand_'+side),('b',points[1],points[2],'thumb_a_'+side)]:
            name='thumb_'+part+'_'+side
            bone=rig.data.edit_bones.new(name)
            bone.head=to_blender(a);bone.tail=to_blender(b);bone.parent=rig.data.edit_bones[parent];bone.use_deform=True
            additions[name]={'from':list(a),'to':list(b),'parent':parent}
    bpy.ops.object.mode_set(mode='OBJECT')
    core=bpy.data.objects['human_core']
    for side,sign in [('l',-1),('r',1)]:
        wrist=Vector(bones['hand_'+side]['from'])
        direction=(Vector(bones['hand_'+side]['to'])-wrist).normalized()
        inverse=Vector((0,-1,0)).rotation_difference(direction).inverted()
        core.vertex_groups.new(name='thumb_a_'+side);core.vertex_groups.new(name='thumb_b_'+side)
        for vertex in core.data.vertices:
            world=core.matrix_world@vertex.co
            q=inverse@(Vector((world.x,world.z,-world.y))-wrist)
            if abs(q.y)>.11 or q.y>-.004 or q.z<-.060 or q.z>.038:continue
            existing=[(core.vertex_groups[item.group],item.weight) for item in vertex.groups]
            if not any(group.name.endswith('_'+side) and weight>.1 for group,weight in existing):continue
            thumb=max(0,min(1,(-sign*q.x-.046)/.011))*max(0,min(1,(.081+q.y)/.014))
            distal=max(0,min(1,(-q.y-.050)/.016))
            weights={'hand_'+side:(1-thumb)*(1-distal)}
            if distal>0:
                finger=min(range(4),key=lambda f:abs(q.x-sign*(-.041+f*.027)))
                second=max(0,min(1,(-q.y-.065)/.014))
                weights['finger%d_a_%s'%(finger,side)]=(1-thumb)*distal*(1-second)
                weights['finger%d_b_%s'%(finger,side)]=(1-thumb)*distal*second
            if thumb>0:
                second=max(0,min(1,(-q.y-.034)/.025))
                weights['thumb_a_'+side]=thumb*(1-second);weights['thumb_b_'+side]=thumb*second
            for group,_ in existing:group.remove([vertex.index])
            for name,weight in weights.items():
                if weight>0:core.vertex_groups[name].add([vertex.index],weight,'REPLACE')
    return additions

def export_selected(species):
    variant=SELECTED[species]
    frozen=ROOT/'art_source/samples07/characters'/variant/species
    source_file=frozen/(species+'_lms06.blend')
    before=hashlib.sha256(source_file.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source_file))
    bpy.context.preferences.filepaths.save_version=0
    # These three authored hex colours were incorrectly stored as linear BSDF
    # values. Runtime primary/accent/cloth colours are already managed by Godot
    # and deliberately do not pass through this correction.
    corrected={}
    if species=='human':
        for name,hex_value in {'skin':'e3ac83','skin_shadow':'c88e6a','lip':'ab735a'}.items():
            material=bpy.data.materials.get(name)
            if material is None:continue
            srgb=[int(hex_value[i:i+2],16)/255 for i in (0,2,4)]
            rgba=tuple(srgb_to_linear(value) for value in srgb)+(1.0,)
            material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=rgba
            material.diffuse_color=rgba
            corrected[name]={'intended_srgb':hex_value,'export_linear':list(rgba)}
    source=ROOT/'art_source/characters'/species
    output=ROOT/'game/assets/art/characters'/species
    source.mkdir(parents=True,exist_ok=True);output.mkdir(parents=True,exist_ok=True)
    rigs=[obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE']
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    assert len(rigs)==1
    frozen_manifest=json.loads((frozen/'manifest.json').read_text())
    cheek_repair=refine_human_cheeks() if species=='human' else {}
    thumb_bones=add_thumb_controls(rigs[0],frozen_manifest['bones']) if species=='human' else {}
    garment_fit=repair_human_garments() if species=='human' else []
    separate_faces(species)
    if species=='human':
        add_human_facial_hair(rigs[0])
        weld_human_hair_roots(rigs[0])
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    default=dict(frozen_manifest['default'])
    legacy_face=default.pop('face',0)
    default.update(eyes=legacy_face,brows=legacy_face,mouth=legacy_face)
    if species=='human':default.update(mustache=0,beard=0,hair_color=0)
    for obj in meshes:
        parts=obj.name.split('_')
        visible=len(parts)<3 or parts[1] not in default or int(parts[2])==default[parts[1]]
        if species=='human' and parts[1]=='hair':visible=visible and obj.name.endswith('_capped')
        obj.hide_set(not visible);obj.hide_render=not visible
    # Store the default selection in the editable source before showing all
    # alternative pieces for explicit GLB export.
    bpy.context.scene['selected_base']=variant
    bpy.context.scene['selected_source_sha256']=before
    bpy.context.scene['authoring_pipeline']='art_source/export_presets/characters_selected_pipeline.py'
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(species+'_lms06.blend')))
    for obj in bpy.context.scene.objects:obj.hide_set(False);obj.hide_render=False
    bpy.ops.object.select_all(action='DESELECT')
    for obj in rigs+meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=rigs[0]
    bpy.ops.export_scene.gltf(filepath=str(output/(species+'_lms06.glb')),export_format='GLB',use_selection=True,
        export_yup=True,export_apply=False,export_animations=True,export_nla_strips=True,
        export_skins=True,export_all_influences=False,export_def_bones=True,export_extras=True)
    manifest=frozen_manifest
    manifest['bones'].update(thumb_bones)
    if thumb_bones:manifest['rig_version']='LMS07.grip1'
    manifest.update({'id':'lms07_selected_'+species,'selected_base':variant,'selected_source':str(source_file.relative_to(ROOT)),
        'selected_source_sha256':before,'authoring':'art_source/export_presets/characters_selected_pipeline.py',
        'source':str((source/(species+'_lms06.blend')).relative_to(ROOT)),
        'glb':str((output/(species+'_lms06.glb')).relative_to(ROOT))})
    manifest['colour_management']=corrected
    manifest['default']=default
    manifest['facial_parts']={'categories':['eyes','brows','mouth'],'variants_each':3,'public_morph_channels_each':10,
        'mosquito_brow_correctives':['BrowUpBlinkL','BrowUpBlinkR','BrowDownBlinkL','BrowDownBlinkR','BrowUpBrowDown','BrowUpBrowDownBlinkL','BrowUpBrowDownBlinkR'] if species=='mosquito' else [],
        'human_hair_options':{'mustache':['none','short','drooping'],'beard':['none','goatee','short']} if species=='human' else {},
        'rest_variation':{'eyes2':'authored curved lid aperture','brows1':'28% authored brow lift'} if species=='human' else {},
        'eyelid_arc_correctives':[blink+'Arc'+str(k) for blink in ['BlinkL','BlinkR'] for k in range(1,8)]}
    if cheek_repair:manifest['cheek_repair']=cheek_repair
    if garment_fit:manifest['garment_fit09']=garment_fit
    (source/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    (output/'rig_contract.json').write_text(json.dumps({'id':manifest['id'],'rig_version':manifest['rig_version'],'selected_base':variant,'bones':manifest['bones']},indent=2),encoding='utf8')
    model=json.loads((frozen/'model.json').read_text())
    model['bones']=manifest['bones']
    model['facial_parts']=manifest['facial_parts']
    if garment_fit:model['garment_fit09']=garment_fit
    if cheek_repair:
        model['cheek_repair']=cheek_repair
        for part in model['parts']:
            if part['name']!='human_head':continue
            points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in bpy.data.objects['human_head'].data.vertices]
            part['bounds_min_m']=[min(q[i] for q in points) for i in range(3)]
            part['bounds_max_m']=[max(q[i] for q in points) for i in range(3)]
    model['parts']=[]
    for obj in meshes:
        pieces=obj.name.split('_');category=pieces[1]
        points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in obj.data.vertices]
        selected=len(pieces)<3 or category not in default or int(pieces[2])==default[category]
        if species=='human' and category=='hair':selected=selected and obj.name.endswith('_capped')
        model['parts'].append({'name':obj.name,'category':category,'selected_default':selected,
            'partition_proof':json.loads(obj.get('partition_proof','{}')),
            'surface_attachment':obj.get('surface_attachment',''),
            'eyelid_replacement':json.loads(obj.get('eyelid_replacement','{}')) if category=='eyes' else {},
            'root_union':json.loads(obj.get('root_union','{}')),
            'vertices':len(points),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),
            'materials':sorted({obj.data.materials[p.material_index].name for p in obj.data.polygons}),
            'bounds_min_m':[min(q[i] for q in points) for i in range(3)],
            'bounds_max_m':[max(q[i] for q in points) for i in range(3)],
            'morphs':[key.name for key in obj.data.shape_keys.key_blocks if key.name!='Basis'] if obj.data.shape_keys else []})
    model['triangles_default']=sum(p['triangles'] for p in model['parts'] if p['selected_default'])
    model['surfaces_default']=sum(len(p['materials']) for p in model['parts'] if p['selected_default'])
    model['implemented']=['Independent eyes, brows and mouth selections with ten shared expression controls',
        'Original selected base and shared gameplay skeleton preserved']+(['Optional mustaches and conforming jaw/chin beards; independent hair palette'] if species=='human' else [])
    model['pending']=[]
    manifest['meshes']=[{'name':part['name'],'vertices':part['vertices'],'triangles':part['triangles']} for part in model['parts']]
    (source/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    model.update({'status':'Selected production base; integration evidence in outputs/0.7-integracion','selected_source_sha256':before,
        'authoring':'art_source/export_presets/characters_selected_pipeline.py'})
    (source/'model.json').write_text(json.dumps(model,indent=2),encoding='utf8')
    (output/'model.json').write_text(json.dumps(model,indent=2),encoding='utf8')
    assert hashlib.sha256(source_file.read_bytes()).hexdigest()==before,'Frozen sample was modified'
    print('SELECTED_CHARACTER_EXPORTED',species,variant,before)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--species',choices=['human','mosquito','both'],default='both')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for role in ['human','mosquito'] if args.species=='both' else [args.species]:export_selected(role)
