"""Offline diagnostic: sampled torso/head corridor and flight center sphere checks."""
import argparse,json,math
from pathlib import Path
import numpy as np
from source_geometry import Geometry,SOURCE,point_triangles

def capsule_body(g,feet):
    x,y,z=feet;a=np.array([x,y+.60,z]);b=np.array([x,y+1.45,z]);radius=.25
    ix=np.where(np.all(g.lo<=b+radius,axis=1)&np.all(g.hi>=a-radius,axis=1))[0]
    if not len(ix):return None
    t=g.tris[ix];best=(1e9,None)
    for point in np.linspace(a,b,19):
        dist=point_triangles(point,t);j=int(np.argmin(dist))
        if dist[j]<best[0]:best=(float(dist[j]),g.names[g.ids[ix[j]]])
    return dict(distance=best[0],blocker=best[1]) if best[0]<.248 else None

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--revision',choices=['v1','v2'],default='v2');args=parser.parse_args()
    cfg_name,evidence_name,output_name=('prepare-v2.json','revision-v2.json','route-diagnostics-v2.json') if args.revision=='v2' else ('prepare.json','source-evidence.json','route-diagnostics.json')
    out=SOURCE/'NavigationProposal'
    if args.revision=='v1' and (out/output_name).exists():raise RuntimeError('V1 delivery is frozen; inspect a new revision instead')
    g=Geometry();cfg=json.loads((out/cfg_name).read_text());report=[]
    evidence=json.loads((out/evidence_name).read_text());floors={(r['route'],r['index']):r for r in evidence['humanProbeEvidence']}
    for r in cfg['routes']:
        if r.get('runtimePatrol'):continue
        points=[[p['x'],p['y'],p['z']] for p in r['points']];issues=[];samples=0
        spawn=g.objects[g.recipe['humanSpawns' if r['role']=='human' else 'mosquitoSpawns'][r['spawnIndex']]]
        prev=np.array([spawn['matrix_world'][i][3] for i in [0,2,1]])
        for i,p in enumerate(points):
            if r['role']=='human':
                end=np.array(floors[(r['id'],i)]['sourceFloor']);end[1]+=.003
                for a in np.linspace(prev,end,max(2,math.ceil(np.linalg.norm((end-prev)[[0,2]])/.25)+1)):
                    support=g.floor(a[0],a[2],a[1]+.45,eligible=True)
                    if not support:issues.append(dict(segment=i,issue='no support',point=a.tolist()));continue
                    feet=[a[0],support[0]+.003,a[2]];check=capsule_body(g,feet);samples+=1
                    if check:issues.append(dict(segment=i,issue='torso/head obstacle',point=feet,**check))
                prev=end
            else:
                check=g.segment(prev,p);samples+=1
                if check and not check['clear']:issues.append(dict(segment=i,issue='flight source triangle clearance',**check))
                prev=np.array(p)
        # Keep the worst contact per segment/blocker; retain scope and sample count.
        compact={}
        for issue in issues:
            key=(issue['segment'],issue.get('blocker',issue.get('nearest','missing floor')))
            if key not in compact or issue.get('distance',1)<compact[key].get('distance',1):compact[key]=issue
        report.append(dict(route=r['id'],role=r['role'],samples=samples,issues=list(compact.values())))
    result=dict(status='OFFLINE_DIAGNOSTIC_NATIVE_PENDING',scope='Human torso/head only: radius .25 from feet+.60 to feet+1.45, sampled every .25m; excludes step/foot physics. Flight source-surface sweep radius .055 at <=1cm. Not full motor/capsule validation.',routes=report)
    (out/output_name).write_text(json.dumps(result,indent=2)+'\n');Path(__file__).with_name(output_name).write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps([dict(route=r['route'],samples=r['samples'],issues=r['issues']) for r in report],indent=2))

if __name__=='__main__':main()
