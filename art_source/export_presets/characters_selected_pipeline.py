"""Promote the chosen authored bases without rewriting the frozen A/B samples.

Run with Blender 4.5.3: --background --python this.py -- --species both
The input .blend files already contain the selected topology, weights, cosmetic
parts and facial controls. Never remesh them a second time during promotion.
"""
import argparse, hashlib, json, sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
SELECTED={'human':'A','mosquito':'B'}

def srgb_to_linear(value):
    return value/12.92 if value<=.04045 else ((value+.055)/1.055)**2.4

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
    thumb_bones=add_thumb_controls(rigs[0],frozen_manifest['bones']) if species=='human' else {}
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
    (source/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    (output/'rig_contract.json').write_text(json.dumps({'id':manifest['id'],'rig_version':manifest['rig_version'],'selected_base':variant,'bones':manifest['bones']},indent=2),encoding='utf8')
    model=json.loads((frozen/'model.json').read_text())
    model['bones']=manifest['bones']
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
