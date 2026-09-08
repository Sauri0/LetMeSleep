"""Serial guarded changed-component audit; retain original native285 provenance."""
import hashlib,json,os,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'work/garment09-trim-motion-final'
OUT.mkdir(exist_ok=True)
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
native_path=ROOT/'work/facial09-motion-export-fit4/index.json'
baseline_path=ROOT/'work/facial09-motion-consumer-fit4/consumer-index.json'
native,baseline=read(native_path),read(baseline_path)
inv=read(ROOT/'work/garment09-trim-invariance.json')
assert native['complete'] and not native['failures'] and baseline['complete_285'] and baseline['unexpected_contacts']==0
assert native['source_sha256']==baseline['source_sha256']
assert native['source_sha256']['res://assets/art/characters/human/human_lms06.glb']==inv['before_sha256']
assert sha(ROOT/'work/facial09-motion-consumer.py')==baseline['analyzer_sha256']
reports=[];seen=set();run=[]
for part in native['parts']:
    assert part['verified'] and part['exit_code']==0 and not part['timed_out']
    assert sha(part['file'])==part['sha256'].lower() and Path(part['stderr']).stat().st_size==0
    if part['role']!='human':continue
    output=OUT/(part['label']+'.json');stdout=OUT/(part['label']+'.stdout.log');stderr=OUT/(part['label']+'.stderr.log')
    if output.exists():
        r=read(output)
        assert r['part_sha256']==sha(part['file']) and r['current_glb_sha256']==inv['after_sha256']
        assert r['analyzer_sha256']==sha(ROOT/'work/garment09-trim-motion-check.py')
    else:
        start=time.monotonic()
        with stdout.open('w') as a,stderr.open('w') as b:
            p=subprocess.run([sys.executable,str(ROOT/'work/garment09-trim-motion-check.py'),'--part',part['file'],'--output',str(output)],
                             stdout=a,stderr=b,cwd=ROOT,timeout=50,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        assert p.returncode==0 and stderr.stat().st_size==0,(part['label'],p.returncode)
        r=read(output);run.append({'part':part['label'],'seconds':time.monotonic()-start,'exit':p.returncode})
    assert not r['unexpected_contacts']
    for case in r['cases']:
        assert case['id'] not in seen;seen.add(case['id'])
    reports.append({'path':str(output),'sha256':sha(output),'cases':len(r['cases']),'pairs':r['evaluated_changed_component_pairs'],'contacts':r['unexpected_contacts']})
    print('TRIM_MOTION09_PART '+part['label']+' cases='+str(len(seen)),flush=True)
assert seen=={'human-'+str(i) for i in range(243)}
assert sum(p['pairs'] for p in reports)==5832
current=read(ROOT/'work/garment09-trim-native-control.json')['source_sha256']
assert all(sha(ROOT/'game'/name.removeprefix('res://'))==value for name,value in current.items())
for name,value in native['source_sha256'].items():
    if name not in ['res://assets/art/characters/human/human_lms06.glb','res://assets/art/characters/human/model.json']:assert current[name]==value
result={'format':'LMS_TRIM09_FACTORED_CLOSURE','source_sha256':current,
        'original_native_index':{'path':str(native_path),'sha256':sha(native_path),'poses':285},
        'original_consumer_index':{'path':str(baseline_path),'sha256':sha(baseline_path),'case_pairs':13176,'unexpected_contacts':0},
        'glb_invariance':{'path':str(ROOT/'work/garment09-trim-invariance.json'),'sha256':sha(ROOT/'work/garment09-trim-invariance.json')},
        'native_control':{'path':str(ROOT/'work/garment09-trim-native-control.json'),'sha256':sha(ROOT/'work/garment09-trim-native-control.json'),'poses':1},
        'skin_control':{'path':str(ROOT/'work/garment09-trim-reskin-control-final.json'),'sha256':sha(ROOT/'work/garment09-trim-reskin-control-final.json')},
        'reports':reports,'runs':run,'changed_component_case_pairs':5832,'changed_components':3,'human_poses_recalculated':243,
        'mosquito_poses_retained_by_identity':42,'unexpected_contacts':0,'finite_pose_coverage_preserved':True,'new_native285_export':False,
        'limits':['Uses the original285 native world bone poses and morph-deformed invariant targets; only new trim is recalculated on CPU.',
                  'Every other mesh/trim triangle and skin driver is verified invariant. A current native pose verifies import order, topology and actual packed weights.',
                  'Floating-point comparison is bounded by the recorded native control; it is not bit-exact.',
                  'No new exhaustive gallery, all-continuous-pose, containment or visual approval is inferred.']}
(OUT/'index.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('TRIM_MOTION09_CLOSED human243 mosquito42_by_identity changed_pairs5832 contacts0',flush=True)
