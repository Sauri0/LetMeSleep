"""Compatibility export of an existing authored map; restores every original mesh/slot."""
import argparse,json,site
site.addsitedir(r'C:\Users\brank\AppData\Roaming\Blender Foundation\Blender\5.2\extensions\.local\lib\python3.13\site-packages')
from blmcp.tools_helpers.connection import send_code
parser=argparse.ArgumentParser();parser.add_argument('--map',required=True,choices=['01-isla','02-casa','03-campamento','04-yate','05-pueblo']);args=parser.parse_args()
code='scene_name='+repr('HF_MAP_'+args.map.replace('-','_'))+'\nfolder_name='+repr('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/'+args.map)+'\n'+'''
import bpy,importlib,contextlib,json
from pathlib import Path
s=importlib.import_module('bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface')
assert not s._scene_conversation().busy(),'Generation still active'
scene=bpy.context.scene;assert scene.name==scene_name
folder=Path(folder_name);assert Path(bpy.data.filepath).parent==folder
modifiers=[(o.name,m.name,m.type) for o in scene.objects if o.type=='MESH' for m in o.modifiers if m.show_render]
assert not modifiers,'Inspect/evaluate structural modifiers before raw-mesh export: '+repr(modifiers[:20])
selected=list(bpy.context.selected_objects);active=bpy.context.view_layer.objects.active
originals=[];copies=[]
try:
    for o in scene.objects:
        if o.type=='MESH':
            slots=[(slot.link,slot.material) for slot in o.material_slots]
            originals.append((o,o.data,slots));dup=o.data.copy();copies.append(dup);o.data=dup
            for slot,(_,material) in zip(o.material_slots,slots):slot.link='DATA';slot.material=material
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in scene.objects:o.select_set(True)
    assert len(bpy.context.selected_objects)==len(scene.objects),'Hidden objects require explicit inspection'
    bpy.context.view_layer.objects.active=next(o for o in scene.objects if o.type=='MESH')
    with (folder/'unity-clean-export.log').open('w',encoding='utf8') as log,contextlib.redirect_stdout(log):
        bpy.ops.export_scene.fbx(filepath=str(folder/(scene_name+'_UNITY.fbx')),use_selection=True,object_types={'MESH','EMPTY','CAMERA','LIGHT'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False,use_custom_props=True)
finally:
    for o,data,slots in originals:
        o.data=data
        for slot,(link,material) in zip(o.material_slots,slots):slot.link=link;slot.material=material
    for mesh in copies:
        if mesh.users==0:bpy.data.meshes.remove(mesh)
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in selected:o.select_set(True)
    bpy.context.view_layer.objects.active=active
logs=(folder/'unity-clean-export.log').read_text(encoding='utf8')
result={'file':str(folder/(scene_name+'_UNITY.fbx')),'bytes':(folder/(scene_name+'_UNITY.fbx')).stat().st_size,'objects':len(scene.objects),'warnings':logs.count('WARNING'),'original_materials_restored':all([slot.material for slot in o.material_slots]==[material for link,material in slots] for o,data,slots in originals)}
(folder/'unity-clean-export-receipt.json').write_text(json.dumps(result,indent=2),encoding='utf8')
'''
response=send_code(code,strict_json=True)
print(json.dumps(response,ensure_ascii=False))
