"""Keep the first four handoff files immutable; revise only route budget and bridge approach."""
import copy,json,math
from pathlib import Path
import numpy as np
from source_geometry import Geometry,SOURCE,sha

HERE=Path(__file__).resolve().parent;OUT=SOURCE/'NavigationProposal';g=Geometry()
old=json.loads((OUT/'prepare.json').read_text());new=copy.deepcopy(old)
frozen={n:sha(OUT/n) for n in ['navigation.json','prepare.json','source-evidence.json','route-diagnostics.json']}
def points(seq):return [dict(x=x,y=y,z=z) for x,y,z in seq]
def p(x,z,y=4.8):return [x,y,z]
by={r['id']:r for r in new['routes']}
# End inside Cottage03 after visiting Cottage02: no redundant return to plaza.
by['plaza-and-northern-cottages']['points']=by['plaza-and-northern-cottages']['points'][:18]
# Return from Cottage01 along its west street, finishing on the northern lane.
by['cottage01-and-village-street']['points']=points([p(-14,-7),p(-14,-5.7),p(-14,-5),p(-14,-3.9),p(-13,-2.5),p(-14,-3.9),p(-14,-5.7),p(-14,-8),p(-19.2,-8),p(-19.2,4),p(-9,4)])
# At the north edge of each bridge end, pass outside the rail tip at |X|=8.4,
# then turn inside the deck. This reduces source rock protrusion; it does not
# assert that the runtime motor can resolve the remaining source overlap.
bridge=[p(7.4,-11.5),p(6.5,-12.2),p(4,-12.2),p(0,-12.2),p(-4,-12.2),p(-6.5,-12.2),p(-7.4,-11.5)]
by['footbridge-both-directions']['points']=points([p(8.4,-9),p(8.4,-10.5),p(8.4,-11.4)]+bridge+[p(-8.4,-11.4),p(-8.4,-10.5),p(-8.4,-9),p(-8.4,-10.5),p(-8.4,-11.4)]+list(reversed(bridge))+[p(8.4,-11.4),p(8.4,-10.5),p(8.4,-9)])

def length(r):
    names=g.recipe['humanSpawns' if r['role']=='human' else 'mosquitoSpawns'];o=g.objects[names[r['spawnIndex']]]
    prev=np.array([o['matrix_world'][i][3] for i in [0,2,1]]);distance=0
    for pnt in r['points']:
        v=np.array([pnt['x'],pnt['y'],pnt['z']]);distance+=np.linalg.norm((v-prev)[[0,2]]);prev=v
    return float(distance)
comparisons=[];probes=[]
for a,b in zip(old['routes'],new['routes']):
    if a.get('runtimePatrol'):continue
    comparisons.append(dict(route=a['id'],role=a['role'],oldPoints=len(a['points']),newPoints=len(b['points']),oldXZLengthIncludingSpawn=length(a),newXZLengthIncludingSpawn=length(b),idealTicksAtWalk3_1=length(b)/3.1*30 if b['role']=='human' else None))
    for i,pnt in enumerate(b['points']):
        if b['role']!='human':continue
        hit=g.floor(pnt['x'],pnt['z'],pnt['y'],eligible=True);assert hit
        probes.append(dict(route=b['id'],index=i,probe=pnt,whitelistedSupport=hit[1],sourceFloor=[pnt['x'],hit[0],pnt['z']]))

bridge_risk=[]
for side in [-1,1]:
    for x,z in [(8.4,-11.4),(8.2,-11.42),(8,-11.44),(7.8,-11.46),(7.4,-11.5)]:
        x*=side;support=g.floor(x,z,4.8,eligible=True);actual=g.floor(x,z,4.8)
        foot=[x,support[0]+.003,z];sweep=g.segment([x,foot[1]+.25,z],[x,foot[1]+1.45,z],.25)
        bridge_risk.append(dict(pointXZ=[x,z],eligibleFloor=support,highestSolidSurface=actual,surfaceAboveEligibleFloor=actual[0]-support[0],
            sampledCapsuleMinSurfaceDistance=sweep['minDistance'] if sweep else None,nearest=sweep['nearest'] if sweep else None,
            capsuleRadius=.25,staticPlacementRisk=bool(sweep and sweep['minDistance']<.248)))
assert max(r['idealTicksAtWalk3_1'] for r in comparisons if r['role']=='human')<500
receipt=dict(status='OFFLINE_V2_PROPOSAL_NATIVE_PENDING_WITH_BRIDGE_FOOT_CONTACT_RISK',frozenV1=frozen,navigationUnchanged=True,
    routeComparisons=comparisons,humanProbeEvidence=probes,bridgeContacts=bridge_risk,
    decisions=['Keep 8 human, 4 flight and 16 runtime routes; maxTicks stays 600; no sprint or engine change.',
    'Shorten plaza and Cottage01 by removing redundant return travel; retain all three cottages and street coverage.',
    'Bridge route follows the north end of the existing deck and land connectors; no source edits or artificial feet lift.',
    'The source rocks partly occupy eligible connector-floor space. Report this collision risk rather than claim a clear human bridge.',
    'Gameplay may measure actual rock support if the real motor can traverse it; native collision/step/slope verification is required.'],
    limitations=['XZ ideal duration includes spawn-to-first point but excludes acceleration/braking/turns, vertical stair travel and collisions.',
    'Bridge capsule surface samples are an offline static-placement diagnostic, not an actual dynamic sweep/solid-volume or engine result.'])
for n,obj in [('prepare-v2.json',new),('revision-v2.json',receipt)]:
    text=json.dumps(obj,indent=2,ensure_ascii=False)+'\n';(OUT/n).write_text(text,encoding='utf-8');(HERE/n).write_text(text,encoding='utf-8')
assert all(sha(OUT/n)==h for n,h in frozen.items())
print(json.dumps(dict(config=str(OUT/'prepare-v2.json'),sha256=sha(OUT/'prepare-v2.json'),comparisons=comparisons[:8],bridge=bridge_risk),indent=2))
