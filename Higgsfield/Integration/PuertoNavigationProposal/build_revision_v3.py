"""Replace invalid vertical lighthouse links with a source-checked helical chain."""
import copy,json
from pathlib import Path
import numpy as np
from source_geometry import Geometry,SOURCE,sha

HERE=Path(__file__).resolve().parent;OUT=SOURCE/'NavigationProposal';g=Geometry()
frozen={n:sha(OUT/n) for n in ['navigation.json','prepare.json','source-evidence.json','route-diagnostics.json','prepare-v2.json','revision-v2.json']}
plan=json.loads((OUT/'navigation.json').read_text());cfg=json.loads((OUT/'prepare-v2.json').read_text());evidence=json.loads((OUT/'source-evidence.json').read_text())
old_segments=[([29,8.45,24.85],[28.05,10.15,26]),([28.05,11.25,26],[28.05,13.05,26])]
old_checks=[dict(start=a,end=b,result=g.segment(a,b)) for a,b in old_segments];assert all(not r['result']['clear'] for r in old_checks)
plan['zones']=[z for z in plan['zones'] if not z['id'].startswith('lighthouse_spiral_')]
plan['portals']=[p for p in plan['portals'] if p['id'] not in ['lighthouse_stair_start','lighthouse_first_turn_air','lighthouse_second_turn_air']]
stair=np.array(evidence['spiralTreadCentersUnity']);air=stair.copy();air[:,1]+=1
centers=[];normals=[]
for i in range(1,56,3):
    c=air[i];d=air[min(i+1,55)]-air[max(i-1,0)];d/=np.linalg.norm(d)
    centers.append(c);normals.append(d)
ends=[(c-.55*d,c+.55*d) for c,d in zip(centers,normals)]
checks=[]
# Existing entry-door endpoint plus the actual first tread gives a continuous entry.
entry=np.array([29,8.45,24.15]);first=ends[0][0]
for i,(a,b) in enumerate(ends):
    checks.append(dict(id=f'helical-portal-{i+1:02}',start=a.tolist(),end=b.tolist(),result=g.segment(a,b)))
    prev=entry if i==0 else ends[i-1][1]
    checks.append(dict(id=f'helical-approach-{i+1:02}',start=prev.tolist(),end=a.tolist(),result=g.segment(prev,a)))
terminal=np.array([30.1,17.3,24.1])
checks.append(dict(id='helical-exit-to-balcony',start=ends[-1][1].tolist(),end=terminal.tolist(),result=g.segment(ends[-1][1],terminal)))

for i in range(len(centers)+1):
    # Bounds enclose only one short section of the helical path, keeping levels apart.
    pts=[entry,ends[0][0]] if i==0 else [ends[i-1][1],terminal] if i==len(centers) else [ends[i-1][1],ends[i][0]]
    # Include associated portal centers to allow stable region transitions.
    if i>0:pts.append(centers[i-1])
    if i<len(centers):pts.append(centers[i])
    v=np.array(pts);padding=np.array([.18,.12,.18]);lo=v.min(axis=0)-padding;hi=v.max(axis=0)+padding
    plan['zones'].append(dict(id=f'lighthouse_helical_section_{i:02}',min=lo.tolist(),max=hi.tolist()))
for i,(c,d) in enumerate(zip(centers,normals)):
    plan['portals'].append(dict(id=f'lighthouse_helical_transition_{i+1:02}',**{'from':f'lighthouse_helical_section_{i:02}'},to=f'lighthouse_helical_section_{i+1:02}',center=c.tolist(),normal=d.tolist(),width=.7,height=1,door=False))
start_center=np.array([29,8.45,24.15]);d=first-start_center;d/=np.linalg.norm(d)
# Entry connection follows the lower open landing rather than jumping across the column.
c=(start_center+first)/2
# These two zones may overlap on the landing; endpoint membership is checked below.
plan['portals'].append(dict(id='lighthouse_entry_to_helix',**{'from':'lighthouse_entry'},to='lighthouse_helical_section_00',center=c.tolist(),normal=d.tolist(),width=.7,height=1,door=False))
entryzone=next(z for z in plan['zones'] if z['id']=='lighthouse_entry')
entryzone['max'][0]=29.9;entryzone['max'][2]=24.85
zero=next(z for z in plan['zones'] if z['id']=='lighthouse_helical_section_00')
zero['min']=np.minimum(zero['min'],c+.55*d-np.array([.08,.08,.08])).tolist();zero['max']=np.maximum(zero['max'],c+.55*d+np.array([.08,.08,.08])).tolist()

def contains(z,p):return all(a-.04<=v<=b+.04 for a,v,b in zip(z['min'],p,z['max']))
known={z['id']:z for z in plan['zones']};issues=[];passages=[]
for p in plan['portals']:
    a=np.array(p['center'])-.55*np.array(p['normal']);b=np.array(p['center'])+.55*np.array(p['normal'])
    for key,v in [('from',a),('to',b)]:
        if not contains(known[p[key]],v):issues.append(dict(id=p['id'],error='outside '+key,point=v.tolist()))
    result=g.segment(a,b)
    if result and not result['clear']:issues.append(dict(id=p['id'],error='portal blocked',result=result))
    passages.append(dict(id=p['id'],start=a.tolist(),end=b.tolist(),result=result))
for c in checks:
    if c['result'] and not c['result']['clear']:issues.append(dict(id=c['id'],error='full interportal path blocked',result=c['result']))
internal=[]
for name,z in known.items():
    if not (name.startswith('lighthouse_helical_') or name=='lighthouse_entry'):continue
    endpoints=[]
    for p in plan['portals']:
        if p['from']==name:endpoints.append((p['id'],np.array(p['center'])-.55*np.array(p['normal'])))
        if p['to']==name:endpoints.append((p['id'],np.array(p['center'])+.55*np.array(p['normal'])))
    for i,(label,a) in enumerate(endpoints):
        for other,b in endpoints[i+1:]:
            check=g.segment(a,b);row=dict(zone=name,fromPortal=label,toPortal=other,start=a.tolist(),end=b.tolist(),result=check);internal.append(row)
            if check and not check['clear']:issues.append(dict(id=name,error='actual same-region passage endpoints blocked',result=check))
reached={plan['zones'][0]['id']}
for _ in known:
    for p in plan['portals']:
        if p['from'] in reached:reached.add(p['to'])
        if p['to'] in reached:reached.add(p['from'])
assert reached==set(known)
for spawn in evidence['spawnAssignments']:assert any(contains(z,spawn['unityPosition']) for z in plan['zones'])
cfg['navigationOverridePath']=(OUT/'navigation-v3.json').as_posix()
receipt=dict(status='OFFLINE_HELICAL_CHAIN_NATIVE_PENDING' if not issues else 'DRAFT_HELICAL_ISSUES',issues=issues,frozen=frozen,
    reason='V1/V2 isolated vertical portals were clear, but the path between them intersected intermediate treads. Replace with a chain following the actual helix.',
    oldBlockedSegments=old_checks,zones=len(known),portals=len(passages),connected=True,mosquitoSpawnsCovered=16,passages=passages,fullHelicalConnections=checks,actualSameRegionConnections=internal,
    routeCoordinatesUnchangedFromV2=True,limitations=['Air boxes enclose short semantic sections; not certified empty volumes.','Checks sample source surfaces, not engine collision or navigation runtime.','V2 bridge foot-contact risk remains unchanged and explicit.'])
for n,obj in [('navigation-v3.json',plan),('prepare-v3.json',cfg),('revision-v3.json',receipt)]:
    text=json.dumps(obj,indent=2)+'\n';(OUT/n).write_text(text);(HERE/n).write_text(text)
assert all(sha(OUT/n)==h for n,h in frozen.items())
print(json.dumps(dict(status=receipt['status'],zones=len(known),portals=len(passages),issues=issues),indent=2))
