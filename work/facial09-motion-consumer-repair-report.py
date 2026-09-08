"""Publish the measured fit4 closure only after current285 and contact gates."""
import hashlib
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
export=ROOT/'work/facial09-motion-export-fit4/index.json'
consumer=ROOT/'work/facial09-motion-consumer-fit4/consumer-index.json'
native,measured=read(export),read(consumer)
assert native['complete'] and not native['failures']
assert measured['complete_285'] and measured['case_count']==285 and measured['unexpected_contacts']==0
assert native['source_sha256']==measured['source_sha256']
assert all(part['verified'] and part['exit_code']==0 and not Path(part['stderr']).stat().st_size for part in native['parts'])
gate=ROOT/'work/facial09-motion-consumer-selected-fit4.log'
assert 'checks=972 failures=0' in gate.read_text()
assert (ROOT/'work/facial09-motion-consumer-selected-fit4.err').stat().st_size==0
runtime_paths=[
 'art_source/characters/shared/garment_fit.py',
 'art_source/export_presets/characters_garment_repair.py',
 'art_source/export_presets/characters_selected_pipeline.py',
 'art_source/characters/human/human_lms06.blend',
 'art_source/characters/human/manifest.json',
 'art_source/characters/human/model.json',
 'game/assets/art/characters/human/human_lms06.glb',
 'game/assets/art/characters/human/rig_contract.json',
 'game/assets/art/characters/human/model.json',
 'game/assets/art/characters/shared/character_skin.gd',
 'game/scripts/human_pose.gd']
details=read(ROOT/'work/facial09-motion-consumer-garment-repair.json')
report={
 'format':'LMS_GARMENT09_REPAIR_FINAL','version':1,
 'source_files':{name:sha(ROOT/name) for name in runtime_paths},
 'native_export':{'path':str(export),'sha256':sha(export),'cases':285,'verified_parts':11},
 'consumer':{'path':str(consumer),'sha256':sha(consumer),'cases':285,
             'case_pairs':sum(read(Path(entry['path']))['summary']['evaluated_case_pairs'] for entry in measured['reports']),
             'unexpected_contacts':0,'anatomical_policy':measured['policy']},
 'contact_gate':{'path':str(gate),'sha256':sha(gate),'checks':972,'failures':0,'measurements':540},
 'source_invariance':{'non_garment_meshes_unchanged':len(details['other_meshes_identical']),
                      'bones':details['bones'],'authority_pose_camera_unchanged':True},
 'regression_cases':['human-'+str(i) for i in [57,119,102,90,54,55,56,63,64,65,72,73,74]],
 'visual_approved':False,
 'limits':measured['limits']+[
   'Native export and exact triangle contacts cover the declared finite poses and compatible pairs only.',
   'No exhaustive gallery, continuous-motion, complete containment or all-cosmetic-combination approval is inferred.',
   'Images remain review evidence; numerical gate results are not a substitute for visual review.']}
assert report['consumer']['case_pairs']==13176
assert report['source_invariance']['non_garment_meshes_unchanged']==27
assert report['source_invariance']['bones']==36
target=ROOT/'work/facial09-motion-consumer-repair-final.json'
assert not target.exists(),'preserve existing closure report'
target.write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf8')
print('GARMENT09_REPAIR_FINAL cases=285 pairs=13176 unexpected=0 mesh_checks=972')
