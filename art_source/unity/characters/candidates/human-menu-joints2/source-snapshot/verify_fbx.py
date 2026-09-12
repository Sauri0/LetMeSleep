"""Independent FBX import audit, no rendering. Run with Blender --background."""
import bpy
import json
import hashlib
import argparse
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--species',choices=['Human','Mosquito','Flyswatter'])
parser.add_argument('--asset-root',type=Path,default=ROOT)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ROOT=args.asset_root.resolve()
species_list=[args.species] if args.species else ['Human','Mosquito','Flyswatter']
report_path=ROOT/args.species.lower()/'fbx_roundtrip.json' if args.species else ROOT/'fbx_roundtrip.json'
results=[]
for species in species_list:
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions): bpy.data.actions.remove(action)
    folder=ROOT/species.lower()
    source=json.loads((folder/'audit.json').read_text())
    bpy.ops.import_scene.fbx(filepath=str(folder/f'LMS_{species}_alpha.fbx'),use_anim=True)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1, (species,len(rigs))
    rig=rigs[0]
    if rig.animation_data: rig.animation_data.action=None
    for b in rig.pose.bones:
        b.rotation_euler=(0,0,0); b.rotation_quaternion=(1,0,0,0); b.location=(0,0,0); b.scale=(1,1,1)
    bpy.context.view_layer.update()
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    dimensions=[max(v[i] for v in points)-min(v[i] for v in points) for i in range(3)]
    actions=[a.name for a in bpy.data.actions]
    sockets=[b.name for b in rig.data.bones if b.name.startswith('Socket.')]
    triangles=0
    for o in meshes:
        o.data.calc_loop_triangles(); triangles+=len(o.data.loop_triangles)
    errors=[]
    if any(abs(dimensions[i]-source['dimensions'][i])>1e-4 for i in range(3)): errors.append('dimensions_changed')
    if set(sockets)!=set(source['sockets']): errors.append('sockets_changed')
    if len(rig.data.bones)!=source['bones']: errors.append('bone_count_changed')
    if triangles!=source['triangles']: errors.append('triangles_changed')
    blend_shapes={o.name:[k.name for k in o.data.shape_keys.key_blocks if k.name!='Basis'] for o in meshes if o.data.shape_keys}
    if blend_shapes!=source.get('blend_shapes',{}):errors.append('blend_shapes_changed')
    if not all(any(clip['name'] in a for a in actions) for clip in source['clips']): errors.append('missing_action')
    results.append({'species':species,'fbx_sha256':hashlib.sha256((folder/f'LMS_{species}_alpha.fbx').read_bytes()).hexdigest(),
                    'source_audit_sha256':hashlib.sha256((folder/'audit.json').read_bytes()).hexdigest(),
                    'dimensions':dimensions,'triangles':triangles,'bones':len(rig.data.bones),
                    'actions':actions,'sockets':sockets,'blend_shapes':blend_shapes,'errors':errors,'passed':not errors})
report_path.write_text(json.dumps(results,indent=2),encoding='utf8',newline='\n')
assert all(r['passed'] for r in results), results
print('LMS_FBX_ROUNDTRIP_PASSED',flush=True)
