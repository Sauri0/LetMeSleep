"""Correct region-order ambiguity observed by Gameplay Runtime; keep previous files frozen."""
import copy,json,itertools
from pathlib import Path
import numpy as np
from source_geometry import Geometry,SOURCE,sha

HERE=Path(__file__).resolve().parent;OUT=SOURCE/'NavigationProposal';g=Geometry()
plan=json.loads((OUT/'navigation-v3.json').read_text());cfg=json.loads((OUT/'prepare-v3.json').read_text())
frozen={p.name:sha(p) for p in OUT.iterdir() if p.is_file() and p.suffix=='.json' and '-v4' not in p.name}
old=copy.deepcopy(plan);zones={z['id']:z for z in plan['zones']};portals={p['id']:p for p in plan['portals']}
def setbound(n,axis,lo=None,hi=None):
    if lo is not None:zones[n]['min'][axis]=lo
    if hi is not None:zones[n]['max'][axis]=hi
def zone(n,lo,hi):z=dict(id=n,min=lo,max=hi);plan['zones'].append(z);zones[n]=z
def link(n,a,b,c,d):plan['portals'].append(dict(id=n,**{'from':a},to=b,center=c,normal=d,width=1.5,height=1.5,door=False))
setbound('home02_approach',0,hi=-10)
setbound('lighthouse_entry',2,hi=24.55)
portals['central_home02']['center'][0]=-10
setbound('home01_approach',0,lo=-17.8)
portals['home01_west']['center'][0]=-17.8
setbound('western_village_exterior',1,hi=10)
portals['west_upper']['center'][1]=10
setbound('above_village_roofs',2,hi=19.5)
zone('above_northwest_village',[-35,10,19.5],[7,19.5,34])
link('upper_village_northwest','above_village_roofs','above_northwest_village',[-15,12,19.5],[0,0,1])
link('upper_northwest_northtrail','above_northwest_village','north_trail',[7,12,26],[1,0,0])
setbound('eastern_trail',0,hi=20)
setbound('eastern_trail',1,hi=10)
setbound('east_workshop_lane',1,hi=10)
portals['eastern_workshop_lane']['center'][0]=20
portals['eastern_north']['center']=[18,9.2,19.5]
portals['north_upper_village']['center']=[20,12,19.5];portals['north_upper_village']['normal']=[0,0,-1]
zone('above_east_harbor',[24,10,-11],[40,19.5,19.5])
link('east_lane_upper','east_workshop_lane','above_east_harbor',[32,10,-5],[0,1,0])
link('upper_village_east','above_village_roofs','above_east_harbor',[24,12,5],[1,0,0])
setbound('north_trail',0,hi=24)
setbound('lighthouse_south_approach',0,lo=24)
setbound('lighthouse_south_approach',2,lo=19.5)
portals['east_to_lighthouse_approach']['center']=[24,11,21]
portals['north_to_lighthouse_west']['center']=[24,12,25]
link('upper_east_to_lighthouse','above_east_harbor','lighthouse_south_approach',[32,13,19.5],[0,0,1])

plan['zones']=[z for z in plan['zones'] if z['id']!='harbor_and_piers_air']
zone('west_pier_air',[-35,1.6,-32],[-8,10,-11])
zone('harbor_bridge_air',[-8,1.6,-32],[8,10,-11])
zone('east_pier_air',[8,1.6,-32],[40,10,-11])
for p in plan['portals']:
    for key in ['from','to']:
        if p[key]=='harbor_and_piers_air':p[key]='west_pier_air' if p['center'][0]<-8 else 'east_pier_air' if p['center'][0]>8 else 'harbor_bridge_air'
link('harbor_west_bridge','west_pier_air','harbor_bridge_air',[-8,5,-24],[1,0,0])
link('harbor_bridge_east','harbor_bridge_air','east_pier_air',[8,5,-24],[1,0,0])
# Door approach boxes overlapped their parent street. Use the real open doorway
# directly between exterior and interior, preserving its source-backed center.
for i,outer in [(1,'home01_approach'),(2,'home02_approach'),(3,'north_central_lane')]:
    plan['zones']=[z for z in plan['zones'] if z['id']!=f'cottage{i}_door_approach']
    plan['portals']=[p for p in plan['portals'] if p['id']!=f'cottage{i}_approach']
    next(p for p in plan['portals'] if p['id']==f'cottage{i}_open_door')['from']=outer

def contains(z,p,tol=0):return all(a-tol<=v<=b+tol for a,v,b in zip(z['min'],p,z['max']))
known={z['id']:z for z in plan['zones']};issues=[];rows=[]
for p in plan['portals']:
    a=np.array(p['center'])-.55*np.array(p['normal']);b=np.array(p['center'])+.55*np.array(p['normal']);assignments=[]
    for key,v in [('from',a),('to',b)]:
        matches=[z['id'] for z in plan['zones'] if contains(z,v)]
        assignments.append(dict(endpoint=key,point=v.tolist(),expected=p[key],firstRegion=matches[0] if matches else None,allRegions=matches))
        if not contains(known[p[key]],v,.04):issues.append(dict(id=p['id'],error='endpoint outside '+key,point=v.tolist()))
        if not matches or matches[0]!=p[key]:issues.append(dict(id=p['id'],error='first-match region ambiguity '+key,expected=p[key],matches=matches))
    result=g.segment(a,b)
    if result and not result['clear']:issues.append(dict(id=p['id'],error='source passage blocked',result=result))
    rows.append(dict(id=p['id'],assignments=assignments,sourceSphereSweep=result))
reached={plan['zones'][0]['id']}
for _ in known:
    for p in plan['portals']:
        if p['from'] in reached:reached.add(p['to'])
        if p['to'] in reached:reached.add(p['from'])
assert reached==set(known)
spawns=[]
for name in g.recipe['mosquitoSpawns']:
    o=g.objects[name];p=np.array([o['matrix_world'][i][3] for i in [0,2,1]]);matches=[z['id'] for z in plan['zones'] if contains(z,p)];assert matches,name
    # Distance to each outgoing approach includes vertical distance and position.
    choices=[]
    for q in plan['portals']:
        if q['from']==matches[0]:target=np.array(q['center'])-.55*np.array(q['normal'])
        elif q['to']==matches[0]:target=np.array(q['center'])+.55*np.array(q['normal'])
        else:continue
        choices.append(dict(portal=q['id'],distance=float(np.linalg.norm(target-p))))
    spawns.append(dict(name=name,position=p.tolist(),firstRegion=matches[0],allRegions=matches,outgoingChoices=choices))
cfg['navigationOverridePath']=(OUT/'navigation-v4.json').as_posix()
native=Path('N:/LetMeSleep/Validation/Higgsfield/PuertoV1-20260913/semantic-native-01/map-checks-20260913-084257-741.json')
receipt=dict(status='OFFLINE_REGION_BOUNDARIES_NATIVE_PENDING' if not issues else 'DRAFT_REGION_ISSUES',issues=issues,frozen=frozen,nativeFeedbackSource=str(native),nativeFeedbackSha256=sha(native),
    zones=len(known),portals=len(rows),connected=True,passages=rows,spawnAssignments=spawns,routeCoordinatesUnchanged=True,
    decisions=['Place portal endpoints on opposite exclusive street boundaries under first-match region lookup.','Split harbor air into west pier, bridge and east pier regions; give nearby logical exits to Runtime12.','Preserve the complete source-checked lighthouse helix and every authored route.','Do not change native fixture thresholds, movement speed, colliders or physics.'])
for n,obj in [('navigation-v4.json',plan),('prepare-v4.json',cfg),('revision-v4.json',receipt)]:
    text=json.dumps(obj,indent=2)+'\n';(OUT/n).write_text(text);(HERE/n).write_text(text)
assert all(sha(OUT/n)==h for n,h in frozen.items())
print(json.dumps(dict(status=receipt['status'],zones=len(known),portals=len(rows),issues=issues),indent=2))
