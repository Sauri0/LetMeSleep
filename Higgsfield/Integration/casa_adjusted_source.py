"""Granted visible MCP operation: save a Casa-only marker-adjusted copy, restore live source."""
import bpy
import hashlib
import json
import sys
from pathlib import Path

ROOT=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas');FOLDER=ROOT/'02-casa';OUT=FOLDER/'UnityAdjustedSource'
VERIFY_EXISTING=globals().get('VERIFY_EXISTING',False)
assert (VERIFY_EXISTING and OUT.is_dir() and not (OUT/'marker-adjustment.json').exists()) or not OUT.exists(), 'Never overwrite a completed adjusted-source attempt'
assert bpy.context.mode=='OBJECT' and bpy.context.scene.name=='HF_MAP_05_pueblo'
assert not bpy.app.is_job_running('RENDER')
assert not sys.modules['bl_ext.user_default.higgsfield_blender.features.overlays.composer_surface']._scene_conversation().busy()
scene=bpy.data.scenes['HF_MAP_02_casa'];marker=scene.objects['Spawn_Human_03.001']
assert marker.type=='EMPTY' and marker.parent is None and not marker.constraints and marker.animation_data is None
assert tuple(marker.users_scene)==(scene,)
current_file=bpy.data.filepath;current_scene=bpy.context.scene
assert Path(current_file).resolve()==(ROOT/'05-pueblo/HF_MAP_05_pueblo.blend').resolve()
paths=[FOLDER/'HF_MAP_02_casa.blend',FOLDER/'HF_MAP_02_casa.fbx',FOLDER/'HF_MAP_02_casa_UNITY.fbx',
       FOLDER/'HF_MAP_02_casa_UNITY.glb',FOLDER/'scene-audit.json',FOLDER/'UnityFinal/unity-native-review.json',Path(current_file)]
file_hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
audit=json.loads((FOLDER/'scene-audit.json').read_text());native=json.loads((FOLDER/'UnityFinal/unity-native-review.json').read_text())
original=next(o for o in audit['objects'] if o['name']==marker.name)
native_marker=next(o for o in native['humanSpawns'] if o['name']==marker.name)
old_matrix=marker.matrix_world.copy();old_xyz=list(old_matrix.translation)
assert old_xyz==[original['matrix_world'][i][3] for i in range(3)]
assert abs(old_xyz[0]+2.5)<1e-6 and abs(old_xyz[1]-2.1)<1e-6 and old_xyz[2]==.25
target_z=native_marker['position']['y'];assert abs(target_z-.2841115)<1e-6
assert abs(native_marker['position']['x']-old_xyz[0])<1e-6 and abs(native_marker['position']['z']-old_xyz[1])<1e-6

def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True).encode()).hexdigest()
def mesh_state(m):
    return digest(dict(vertices=[list(v.co) for v in m.vertices],polygons=[(list(p.vertices),p.material_index,p.use_smooth) for p in m.polygons],
        materials=[x.name if x else None for x in m.materials],
        keys=[dict(name=k.name,value=k.value,coords=[list(v.co) for v in k.data]) for k in m.shape_keys.key_blocks] if m.shape_keys else []))
def object_state(o):
    return digest(dict(name=o.name,parent=o.parent.name if o.parent else None,matrix=[list(row) for row in o.matrix_world],
        data=o.data.name if o.data else None,slots=[(s.link,s.material.name if s.material else None) for s in o.material_slots] if o.type=='MESH' else []))
before_objects={o.name:object_state(o) for o in bpy.data.objects};before_meshes={m.name:mesh_state(m) for m in bpy.data.meshes}
scene_members={s.name:[o.name for o in s.objects] for s in bpy.data.scenes}
source=OUT/'HF_MAP_02_casa_UNITY_ADJUSTED.blend';loaded=None
if not VERIFY_EXISTING:
    OUT.mkdir()
    try:
        adjusted=old_matrix.copy();adjusted.translation.z=target_z;marker.matrix_world=adjusted
        actual_xyz=list(marker.matrix_world.translation)
        assert actual_xyz[:2]==old_xyz[:2] and abs(actual_xyz[2]-target_z)<1e-7
        assert all(object_state(o)==before_objects[o.name] for o in bpy.data.objects if o!=marker)
        bpy.data.libraries.write(str(source),{scene},path_remap='RELATIVE_ALL',fake_user=False,compress=False)
    finally:
        marker.matrix_world=old_matrix
else:
    assert source.is_file(), 'Recovery only verifies the existing saved copy; never writes a blend'
assert object_state(marker)==before_objects[marker.name]
# Read back the saved EMPTY without opening the file or linking it to a live scene.
try:
    with bpy.data.libraries.load(str(source),link=False) as (available,requested):
        assert available.scenes==['HF_MAP_02_casa']
        assert marker.name in available.objects
        requested.objects=[marker.name]
    loaded=requested.objects[0]
    assert loaded!=marker and loaded.type=='EMPTY' and loaded.parent is None and not loaded.users_scene
    assert not loaded.constraints and loaded.animation_data is None
    # An unlinked EMPTY has no evaluated matrix_world cache. With no parent/constraints,
    # matrix_basis is its exact authored world transform, including delta transforms.
    saved_xyz=list(loaded.matrix_basis.translation)
    assert saved_xyz[:2]==old_xyz[:2] and abs(saved_xyz[2]-target_z)<1e-7
finally:
    if loaded is not None:bpy.data.objects.remove(loaded,do_unlink=True)
assert set(o.name for o in bpy.data.objects)==set(before_objects)
assert all(object_state(o)==before_objects[o.name] for o in bpy.data.objects)
assert set(m.name for m in bpy.data.meshes)==set(before_meshes)
assert all(mesh_state(m)==before_meshes[m.name] for m in bpy.data.meshes)
assert {s.name:[o.name for o in s.objects] for s in bpy.data.scenes}==scene_members
assert bpy.context.scene==current_scene and bpy.data.filepath==current_file
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in file_hashes.items())
receipt=dict(status='PASS_EXCLUSIVE_CASA_COPY_MARKER_DELTA_AND_SAVED_EMPTY_READBACK',source=str(source),
    sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),savedScene='HF_MAP_02_casa',marker=marker.name,
    originalBlenderXYZ=old_xyz,adjustedBlenderXYZ=saved_xyz,unityTargetXYZ=native_marker['position'],actualDeltaZ=saved_xyz[2]-old_xyz[2],
    preservedFiles=file_hashes,restoredActiveScene=bpy.context.scene.name,restoredLiveMarkerXYZ=list(marker.matrix_world.translation),
    invariants=['Only temporary marker Z changed before writing the standalone copy','Live marker restored and all live objects/meshes/scenes identical to before',
        'Original Casa source/exports/audit and Puerto file hashes unchanged','Saved marker independently read back; no scene opened or linked','No new FBX/GLB exports or Unity reimport'],
    readbackMethod='Saved unlinked EMPTY matrix_basis; parent/constraints/animation absent. matrix_world is unevaluated until scene-linked.',
    portableUnityEquivalence='Only this marker delta matches the documented Unity adjustment; full engine/source equivalence is not claimed')
(OUT/'marker-adjustment.json').write_text(json.dumps(receipt,indent=2))
result=receipt
