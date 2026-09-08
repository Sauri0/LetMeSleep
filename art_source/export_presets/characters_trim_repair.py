"""Repair central piping from the exact preserved fit4 source; keep other art intact."""
import hashlib,json,sys
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'art_source/characters/shared'))
from garment_trim import conform_human_central_trim,central_component
BEFORE=ROOT/'outputs/0.9-garment-trim/source-fit4'
SOURCE=ROOT/'art_source/characters/human'
OUTPUT=ROOT/'game/assets/art/characters/human'


def signature(obj,skip=()):
    skip=set(skip);mesh=obj.data
    faces=[p for p in mesh.polygons if not any(i in skip for i in p.vertices)]
    loops=[i for p in faces for i in p.loop_indices]
    return {'vertices':{v.index:list(v.co) for v in mesh.vertices if v.index not in skip},
            'weights':{v.index:[(obj.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in mesh.vertices if v.index not in skip},
            'indices':[list(p.vertices) for p in faces],
            'uv':[[list(layer.data[i].uv) for i in loops] for layer in mesh.uv_layers],
            'materials':[m.name for m in mesh.materials],
            'morphs':{k.name:[list(v.co) for v in k.data] for k in mesh.shape_keys.key_blocks} if mesh.shape_keys else {}}


path=BEFORE/'art_source/characters/human/human_lms06.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
bpy.context.preferences.filepaths.save_version=0
meshes=[o for o in bpy.data.objects if o.type=='MESH']
masks={o.name:central_component(o) if o.name in ['human_outfit_%d_trim'%i for i in range(3)] else [] for o in meshes}
before={o.name:signature(o,masks[o.name]) for o in meshes}
rigs=[o for o in bpy.data.objects if o.type=='ARMATURE'];assert len(rigs)==1 and len(rigs[0].data.bones)==36
records=conform_human_central_trim()
after_masks={o.name:central_component(o) if masks[o.name] else [] for o in meshes}
assert all(signature(o,after_masks[o.name])==before[o.name] for o in meshes)
model=json.loads((BEFORE/'game/assets/art/characters/human/model.json').read_text())
manifest=json.loads((BEFORE/'art_source/characters/human/manifest.json').read_text())
model['garment_trim09']=records;manifest['garment_trim09']=records
for part in model['parts']:
    if not masks.get(part['name']):continue
    obj=bpy.data.objects[part['name']]
    previous_triangles=part['triangles']
    part['vertices']=len(obj.data.vertices)
    part['triangles']=sum(len(p.vertices)-2 for p in obj.data.polygons)
    if part.get('selected_default'):model['triangles_default']+=part['triangles']-previous_triangles
    q=[Vector((v.co.x,v.co.z,-v.co.y)) for v in obj.data.vertices]
    part['bounds_min_m']=[min(v[i] for v in q) for i in range(3)]
    part['bounds_max_m']=[max(v[i] for v in q) for i in range(3)]
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'human_lms06.blend'))
for obj in bpy.context.scene.objects:obj.hide_set(False);obj.hide_render=False
bpy.ops.object.select_all(action='DESELECT')
for obj in rigs+meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=rigs[0]
bpy.ops.export_scene.gltf(filepath=str(OUTPUT/'human_lms06.glb'),export_format='GLB',use_selection=True,
    export_yup=True,export_apply=False,export_animations=True,export_nla_strips=True,
    export_skins=True,export_all_influences=False,export_def_bones=True,export_extras=True)
for folder in [SOURCE,OUTPUT]:(folder/'model.json').write_text(json.dumps(model,indent=2),encoding='utf8')
(SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
report={'records':records,'unchanged_meshes':[name for name in masks if not masks[name]],
        'unchanged_other_trim_vertices':True,'bones':36,'before_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
        'after_glb_sha256':hashlib.sha256((OUTPUT/'human_lms06.glb').read_bytes()).hexdigest()}
(ROOT/'work/garment09-trim-repair.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('GARMENT_TRIM09_EXPORTED meshes=3 unchanged_meshes='+str(len(report['unchanged_meshes'])))
