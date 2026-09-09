"""Rebuild only selected-A glasses from the exact preserved trim2 source."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'art_source/characters/shared'))
from glasses_fit import repair_human_glasses
parser=argparse.ArgumentParser();parser.add_argument('--promote',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
BEFORE=ROOT/'outputs/0.9-glasses-fit/source-trim2'
source=ROOT/'art_source/characters/human' if args.promote else ROOT/'work/glasses09-candidate'
output=ROOT/'game/assets/art/characters/human' if args.promote else source
source.mkdir(exist_ok=True,parents=True)
path=BEFORE/'art_source/characters/human/human_lms06.blend'
bpy.ops.wm.open_mainfile(filepath=str(path));bpy.context.preferences.filepaths.save_version=0
def signature(obj):
    mesh=obj.data
    return {'p':[list(v.co) for v in mesh.vertices],'n':[list(v.normal) for v in mesh.vertices],
        'weights':[[[obj.vertex_groups[g.group].name,g.weight] for g in v.groups] for v in mesh.vertices],
        'polygons':[(list(p.vertices),p.material_index,p.use_smooth) for p in mesh.polygons],
        'uv':[[list(v.uv) for v in layer.data] for layer in mesh.uv_layers],
        'morphs':{k.name:[list(v.co) for v in k.data] for k in mesh.shape_keys.key_blocks} if mesh.shape_keys else {},
        'matrix':[list(row) for row in obj.matrix_world],'materials':[m.name for m in mesh.materials]}
meshes=[o for o in bpy.data.objects if o.type=='MESH'];unchanged=[o for o in meshes if o.name!='human_accessory_2']
before={o.name:signature(o) for o in unchanged}
record,details=repair_human_glasses()
assert all(signature(o)==before[o.name] for o in unchanged)
rigs=[o for o in bpy.data.objects if o.type=='ARMATURE'];assert len(rigs)==1 and len(rigs[0].data.bones)==36
model=json.loads((BEFORE/'game/assets/art/characters/human/model.json').read_text())
manifest=json.loads((BEFORE/'art_source/characters/human/manifest.json').read_text())
detail_path=ROOT/'work/glasses09-construction.json';detail_path.write_text(json.dumps(details,indent=2))
record.update(construction_report='work/glasses09-construction.json',construction_sha256=hashlib.sha256(detail_path.read_bytes()).hexdigest())
model['glasses_fit09']=record;manifest['glasses_fit09']=record
obj=bpy.data.objects['human_accessory_2']
for part in model['parts']:
    if part['name']!=obj.name:continue
    prior=part['triangles'];part['vertices']=len(obj.data.vertices);part['triangles']=record['triangles']
    if part.get('selected_default'):model['triangles_default']+=part['triangles']-prior
    points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in obj.data.vertices]
    part['bounds_min_m']=[min(p[i] for p in points) for i in range(3)]
    part['bounds_max_m']=[max(p[i] for p in points) for i in range(3)]
for part in manifest.get('meshes',[]):
    if part['name']==obj.name:
        part['vertices']=len(obj.data.vertices);part['triangles']=record['triangles']
bpy.ops.wm.save_as_mainfile(filepath=str(source/'human_lms06.blend'))
for o in bpy.context.scene.objects:o.hide_set(False);o.hide_render=False
bpy.ops.object.select_all(action='DESELECT')
for o in rigs+meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rigs[0]
bpy.ops.export_scene.gltf(filepath=str(output/'human_lms06.glb'),export_format='GLB',use_selection=True,
    export_yup=True,export_apply=False,export_animations=True,export_nla_strips=True,
    export_skins=True,export_all_influences=False,export_def_bones=True,export_extras=True)
for folder in set([source,output]):(folder/'model.json').write_text(json.dumps(model,indent=2),encoding='utf8')
(source/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
report={'promoted':args.promote,'record':record,'unchanged_meshes':[o.name for o in unchanged],'bones':36,
    'before_blend_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'glb_sha256':hashlib.sha256((output/'human_lms06.glb').read_bytes()).hexdigest()}
(ROOT/'work/glasses09-repair.json').write_text(json.dumps(report,indent=2))
print('GLASSES09_EXPORTED unchanged_meshes='+str(len(unchanged))+' vertices='+str(record['vertices']),flush=True)
