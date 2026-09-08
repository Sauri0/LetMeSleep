"""Promote the chosen authored bases without rewriting the frozen A/B samples.

Run with Blender 4.5.3: --background --python this.py -- --species both
The input .blend files already contain the selected topology, weights, cosmetic
parts and facial controls. Never remesh them a second time during promotion.
"""
import argparse, hashlib, json, sys
from pathlib import Path
import bpy

ROOT=Path(__file__).resolve().parents[2]
SELECTED={'human':'A','mosquito':'B'}

def srgb_to_linear(value):
    return value/12.92 if value<=.04045 else ((value+.055)/1.055)**2.4

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
    manifest=json.loads((frozen/'manifest.json').read_text())
    manifest.update({'id':'lms07_selected_'+species,'selected_base':variant,'selected_source':str(source_file.relative_to(ROOT)),
        'selected_source_sha256':before,'authoring':'art_source/export_presets/characters_selected_pipeline.py',
        'source':str((source/(species+'_lms06.blend')).relative_to(ROOT)),
        'glb':str((output/(species+'_lms06.glb')).relative_to(ROOT))})
    manifest['colour_management']=corrected
    (source/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    (output/'rig_contract.json').write_text(json.dumps({'id':manifest['id'],'rig_version':manifest['rig_version'],'selected_base':variant,'bones':manifest['bones']},indent=2),encoding='utf8')
    model=json.loads((frozen/'model.json').read_text())
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
