"""Seal measured sources and current review renders after both Blender checks pass."""
import json
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parent
path=ROOT/'manifest.json'
manifest=json.loads(path.read_text())
roundtrip=json.loads((ROOT/'fbx_roundtrip.json').read_text())
assert all(a['passed'] for a in roundtrip)
assert all(a['passed'] for a in manifest['audits'])
expected=[f'{s}_{view}.png' for s in ['human','mosquito'] for view in ['front','side','back','threequarter']]
expected+=['human_hand_open.png','human_hand_curl.png','human_clap_contact.png']
for name in expected:
    png=ROOT/'review'/name
    species='Human' if name.startswith('human') else 'Mosquito'
    source=ROOT/species.lower()/f'LMS_{species}_alpha.blend'
    assert png.exists() and png.stat().st_mtime>=source.stat().st_mtime, f'Stale or missing render: {name}'
manifest['rendered']=True
manifest['review_renderer']={'engine':'Cycles','device':'CPU','threads':2,'samples':24,'resolution':[720,900]}
manifest['fbx_roundtrip_verified']=True
manifest['unity_import_verified']=False
manifest['files']={}
for file in sorted(ROOT.rglob('*')):
    if file.is_file() and file.name!='manifest.json' and file.suffix in ['.blend','.fbx','.py','.png','.json']:
        manifest['files'][file.relative_to(ROOT).as_posix()]={'sha256':hashlib.sha256(file.read_bytes()).hexdigest(),'bytes':file.stat().st_size}
manifest['reference_images']={}
for relative in ['work/references094/expanded/human-turnarounds.png','work/references094/expanded/mosquito-turnarounds.png','work/references094/characters.png']:
    reference=ROOT.parents[2]/relative
    assert reference.is_file(), relative
    manifest['reference_images'][relative]=hashlib.sha256(reference.read_bytes()).hexdigest()
path.write_text(json.dumps(manifest,indent=2),encoding='utf8',newline='\n')
print('LMS_CHARACTER_MANIFEST_SEALED')
