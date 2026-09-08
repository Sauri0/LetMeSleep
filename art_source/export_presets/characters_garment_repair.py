"""Apply garment-only repair to preserved current human source, then export.
No re-authoring of faces, hands, hair, materials, or rig. Frozen A/B untouched.
"""
import hashlib,json,sys
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'art_source/characters/shared'))
from garment_fit import repair_human_garments
SOURCE=ROOT/'art_source/characters/human'
OUTPUT=ROOT/'game/assets/art/characters/human'
BEFORE=ROOT/'outputs/0.9-facial-motion-witnesses/source-before'


def mesh_signature(obj):
    def points(mesh):return [list(v.co) for v in mesh.vertices]
    return {'vertices':points(obj.data),'polygons':[list(p.vertices) for p in obj.data.polygons],
            'weights':[[(obj.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in obj.data.vertices],
            'morphs':{k.name:[list(v.co) for v in k.data] for k in obj.data.shape_keys.key_blocks} if obj.data.shape_keys else {}}


path=BEFORE/'art_source/characters/human/human_lms06.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
bpy.context.preferences.filepaths.save_version=0
unchanged={o.name:mesh_signature(o) for o in bpy.data.objects if o.type=='MESH' and not o.name.startswith('human_outfit_')}
rigs=[o for o in bpy.data.objects if o.type=='ARMATURE']
assert len(rigs)==1 and len(rigs[0].data.bones)==36
record=repair_human_garments()
assert all(mesh_signature(bpy.data.objects[name])==signature for name,signature in unchanged.items())
model=json.loads((BEFORE/'game/assets/art/characters/human/model.json').read_text())
manifest=json.loads((BEFORE/'art_source/characters/human/manifest.json').read_text())
model['garment_fit09']=record;manifest['garment_fit09']=record
for part in model['parts']:
    if not part['name'].startswith('human_outfit_'):continue
    obj=bpy.data.objects[part['name']]
    q=[Vector((v.co.x,v.co.z,-v.co.y)) for v in obj.data.vertices]
    part['bounds_min_m']=[min(v[i] for v in q) for i in range(3)]
    part['bounds_max_m']=[max(v[i] for v in q) for i in range(3)]
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'human_lms06.blend'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for obj in bpy.context.scene.objects:obj.hide_set(False);obj.hide_render=False
bpy.ops.object.select_all(action='DESELECT')
for obj in rigs+meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=rigs[0]
bpy.ops.export_scene.gltf(filepath=str(OUTPUT/'human_lms06.glb'),export_format='GLB',use_selection=True,
    export_yup=True,export_apply=False,export_animations=True,export_nla_strips=True,
    export_skins=True,export_all_influences=False,export_def_bones=True,export_extras=True)
for folder in [SOURCE,OUTPUT]:
    (folder/'model.json').write_text(json.dumps(model,indent=2),encoding='utf8')
(SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
report={'records':record,'other_meshes_identical':list(unchanged),'bones':36,
        'input_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
        'output_glb_sha256':hashlib.sha256((OUTPUT/'human_lms06.glb').read_bytes()).hexdigest()}
(ROOT/'work/facial09-motion-consumer-garment-repair.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('GARMENT09_EXPORTED meshes='+str(len(record))+' other_meshes_unchanged='+str(len(unchanged)))
