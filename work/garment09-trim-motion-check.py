"""Focused semantic contacts for the changed piping, reusing invariant native poses."""
import argparse,copy,hashlib,importlib.util,json,time
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,ROOT/path);out=importlib.util.module_from_spec(spec);spec.loader.exec_module(out);return out
r=module('trim_reskin','work/garment09-trim-reskin.py')
k=module('contact_kernel','work/facial09-motion-consumer.py')
def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def central_mask(mesh):
    points=mesh['positions'];parent=list(range(len(points)))
    def root(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    def join(i,j):parent[root(i)]=root(j)
    welded={}
    for i,p in enumerate(points):
        key=tuple(np.round(p,7))
        if key in welded:join(i,welded[key])
        else:welded[key]=i
    idx=mesh['indices']
    for i in range(0,len(idx),3):join(idx[i],idx[i+1]);join(idx[i+1],idx[i+2])
    groups={}
    for i in range(len(points)):groups.setdefault(root(i),[]).append(i)
    selected=[]
    for group in groups.values():
        p=points[group]
        if np.ptp(p[:,0])<.02 and np.ptp(p[:,1])>.30:selected+=group
    assert len(selected)>1000
    return set(selected)
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--part',required=True);ap.add_argument('--output',required=True);args=ap.parse_args()
    start=time.monotonic();deadline=start+45
    assert not Path(args.output).exists(),'preserve previous evidence'
    invariance=read(ROOT/'work/garment09-trim-invariance.json');control=read(ROOT/'work/garment09-trim-reskin-control-final.json')
    assert not invariance['failures'] and len(invariance['unchanged_complete_meshes'])==30
    assert control['current_control_supplied'] and control['maximum_error_m']<=.00015
    glb_path=ROOT/'game/assets/art/characters/human/human_lms06.glb'
    assert sha(glb_path)==invariance['after_sha256']
    native=read(ROOT/'work/garment09-trim-native-control.json');data=read(args.part)
    assert not native['summary']['failures']
    differences=[]
    for path,old in data['source_sha256'].items():
        actual=sha(ROOT/'game'/path.removeprefix('res://'))
        if actual!=old:differences.append(path)
        assert actual==native['source_sha256'][path],('current sources',path)
    assert set(differences)=={'res://assets/art/characters/human/human_lms06.glb','res://assets/art/characters/human/model.json'}
    glb=r.reader.GLB(glb_path);first=native['cases'][0];prepared={}
    for name in r.NAMES:
        top=native['topologies'][name];mesh=r.load_mesh(glb,name,top);mask=central_mask(mesh)
        expected=np.asarray(native['geometries'][first['meshes'][name]['geometry']]['vertices'])
        mesh=r.ordered_mesh(mesh,top,first,expected)
        selected={i for i,j in enumerate(mesh['order']) if int(j) in mask}
        triangles=np.asarray(top['indices']).reshape(-1,3)
        indices=[int(i) for tri in triangles if all(int(i) in selected for i in tri) for i in tri]
        focused=copy.deepcopy(top);focused['indices']=indices
        focused['surfaces']=[{'triangle_start':0,'triangle_count':len(indices)//3,'material':'secondary'}]
        prepared[name]=(mesh,top,focused)
    meshes={};memo={};cases=[];contacts=[];pairs=0;candidate_count=0
    for case in data['cases']:
        assert case['role']=='human' and case['skin_transform']['origin']==[0,0,0]
        result={'id':case['id'],'pairs':0,'contacts':0}
        for name,(mesh,top,focused) in prepared.items():
            vertices=r.deform(mesh,top,case)
            fingerprint=hashlib.sha256(vertices.tobytes()).hexdigest()
            if fingerprint not in meshes:meshes[fingerprint]=k.Mesh(focused,{'vertices':vertices})
            a=meshes[fingerprint];assert not a.degenerate
            for pair in data['compatible_pairs']['human']:
                if pair['a']==name:target=pair['b']
                elif pair['b']==name:target=pair['a']
                else:continue
                gid=case['meshes'][target]['geometry'];key=(fingerprint,gid)
                if gid not in meshes:meshes[gid]=k.Mesh(data['topologies'][target],data['geometries'][gid])
                b=meshes[gid]
                if key not in memo:
                    found,count=k.contacts(a,b,deadline);memo[key]=found;candidate_count+=count
                found=memo[key];result['pairs']+=1;pairs+=1;result['contacts']+=len(found)
                if found:contacts.append({'case':case['id'],'trim':name,'target':target,'contacts':found})
        assert result['pairs']==24;cases.append(result)
        if time.monotonic()>deadline:raise TimeoutError('part guard45s')
    assert sha(glb_path)==invariance['after_sha256']
    report={'format':'LMS_TRIM09_REUSED_NATIVE_MOTION','part':str(Path(args.part).resolve()),'part_sha256':sha(args.part),
            'analyzer_sha256':sha(__file__),'reskin_script_sha256':sha(ROOT/'work/garment09-trim-reskin.py'),'kernel_sha256':sha(ROOT/'work/facial09-motion-consumer.py'),
            'current_glb_sha256':invariance['after_sha256'],'native_control_sha256':sha(ROOT/'work/garment09-trim-native-control.json'),
            'invariance_sha256':sha(ROOT/'work/garment09-trim-invariance.json'),'reskin_control_sha256':sha(ROOT/'work/garment09-trim-reskin-control-final.json'),
            'cases':cases,'evaluated_changed_component_pairs':pairs,'candidate_triangle_pairs':candidate_count,'contacts':contacts,
            'unexpected_contacts':sum(len(v['contacts']) for v in contacts),'elapsed_seconds':time.monotonic()-start,
            'basis':'Current imported trim topology/weights and GLB rest positions; frozen native world bone matrices; original invariant head geometry and morphs.',
            'limits':['Recalculated CPU trim geometry, not285 newly rendered or newly exported poses.','Float JSON precision1e-7; native control maximum error is recorded, not bit-exact.',
                      'Only declared changed-component semantic pairs are retested. Prior finite-pose clearance is reused outside those components via complete GLB invariance.','No all-continuous-pose, containment or visual approval.']}
    Path(args.output).write_text(json.dumps(report,indent=2),encoding='utf8')
    print('TRIM_MOTION09 cases='+str(len(cases))+' pairs='+str(pairs)+' contacts='+str(report['unexpected_contacts'])+' seconds='+str(round(report['elapsed_seconds'],2)),flush=True)
    return 0 if not report['unexpected_contacts'] else 2
if __name__=='__main__':raise SystemExit(main())
