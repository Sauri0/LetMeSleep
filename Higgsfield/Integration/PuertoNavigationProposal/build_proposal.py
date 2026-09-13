"""Author a bounded, source-backed Puerto proposal. Never opens Blender or Unity."""
import json,math
from pathlib import Path
import numpy as np
from source_geometry import Geometry,SOURCE,sha

HERE=Path(__file__).resolve().parent;OUT=SOURCE/'NavigationProposal';OUT.mkdir(exist_ok=True)
g=Geometry();plan=dict(schema_version=1,map_id='hf-puerto-del-faro-v1',zones=[],portals=[])
def zone(n,x0,x1,z0,z1,y0,y1):plan['zones'].append(dict(id=n,min=[x0,y0,z0],max=[x1,y1,z1]))
def link(n,a,b,p,d,w=1.5,h=1.5):plan['portals'].append(dict(id=n,**{'from':a},to=b,center=p,normal=d,width=w,height=h,door=False))

# Broad outdoor semantic regions contain local props; collision remains authoritative.
# No building-window shortcut is authored, even where panes are non-solid.
zone('plaza_and_central_streets',-10,10,-11,9.7,3.5,10)
zone('north_central_lane',-7,7,9.7,12.7,3.5,10)
zone('home02_approach',-17.8,-7,1.4,9.7,3.5,10)
zone('home01_approach',-22,-10,-11,-5.2,3.5,10)
zone('western_village_exterior',-35,-17.8,-11,30,3.5,15)
zone('above_village_roofs',-35,24,-32,34,10,19.5)
zone('harbor_and_piers_air',-35,40,-32,-11,1.6,10)
zone('east_workshop_lane',20,40,-11,19.5,4,15)
zone('eastern_trail',10,26,.5,19.5,4,14)
zone('north_trail',7,26,19.5,34,8.6,19.5)
zone('workshop_forecourt',10,20,-11,-8.15,3.6,6)
zone('lighthouse_south_approach',26,40,12,23.6,7.5,19.5)
zone('lighthouse_east_air',31.7,40,23.6,34,8.6,19.5)
zone('lighthouse_north_air',26,31.7,28.7,34,8.6,19.5)
zone('lighthouse_west_air',24,26.35,23.6,28.7,8.6,19.5)
link('central_north','plaza_and_central_streets','north_central_lane',[0,6,9.7],[0,0,1])
link('central_home02','plaza_and_central_streets','home02_approach',[-8.5,5,5],[-1,0,0])
link('central_home01','plaza_and_central_streets','home01_approach',[-10,5,-8],[ -1,0,0])
link('home01_west','home01_approach','western_village_exterior',[-19,6,-7],[ -1,0,0])
link('home02_west','home02_approach','western_village_exterior',[-17.8,6,5],[-1,0,0])
link('central_upper','plaza_and_central_streets','above_village_roofs',[3,10,0],[0,1,0])
link('north_upper','north_central_lane','above_village_roofs',[0,10,11],[0,1,0])
link('west_upper','western_village_exterior','above_village_roofs',[-23,11,4],[0,1,0])
link('central_harbor','plaza_and_central_streets','harbor_and_piers_air',[0,6,-11],[0,0,-1])
link('home01_harbor','home01_approach','harbor_and_piers_air',[-14,5,-11],[0,0,-1])
link('harbor_east','harbor_and_piers_air','east_workshop_lane',[22,6,-11],[0,0,1])
link('central_eastern','plaza_and_central_streets','eastern_trail',[10,6,5],[1,0,0])
link('eastern_workshop_lane','eastern_trail','east_workshop_lane',[23,7,4],[1,0,0])
link('eastern_north','eastern_trail','north_trail',[22,11,19.5],[0,0,1])
link('north_upper_village','north_trail','above_village_roofs',[20,12,25],[-1,0,0])
link('east_to_lighthouse_approach','north_trail','lighthouse_south_approach',[26,11,20],[1,0,0])
link('north_to_lighthouse_west','north_trail','lighthouse_west_air',[25,12,25],[1,0,0])
link('lighthouse_around_east','lighthouse_south_approach','lighthouse_east_air',[34,12,23.6],[0,0,1])
link('lighthouse_around_north','lighthouse_east_air','lighthouse_north_air',[31.7,13,31],[-1,0,0])
link('lighthouse_around_west','lighthouse_north_air','lighthouse_west_air',[26,13,28.7],[-.70710678,0,-.70710678])
link('workshop_from_harbor','harbor_and_piers_air','workshop_forecourt',[15,4.7,-10.7],[0,0,1])

for i,(x,z) in enumerate([(-14,-5),(-12,10),(2,13)],1):
    n=f'cottage{i}_interior';approach=f'cottage{i}_door_approach'
    zone(n,x-3.15,x+3.15,z+.15,z+5.6,3.55,6.2)
    zone(approach,x-1.1,x+1.1,z-1.6,z-.15,3.5,5.5)
    link(f'cottage{i}_open_door',approach,n,[x,4.6,z],[0,0,1],1.1,2.1)
    outer=['home01_approach','home02_approach','north_central_lane'][i-1]
    link(f'cottage{i}_approach',outer,approach,[x,4.6,z-1.15],[0,0,1],1.3,2)
zone('workshop_entry_aisle',13.65,16.35,-7.85,-6,3.55,6)
zone('workshop_west_aisle',12.6,13.65,-6.5,-.6,3.55,6)
zone('workshop_east_aisle',16.35,17.85,-6.5,-.6,3.55,6)
zone('workshop_rear_aisle',12.6,17.85,-1,-.4,3.55,6)
link('workshop_open_door','workshop_forecourt','workshop_entry_aisle',[15,4.7,-8],[0,0,1],3,2.4)
link('workshop_west_bypass','workshop_entry_aisle','workshop_west_aisle',[13.65,4.7,-6.25],[-1,0,0],.8,1.5)
link('workshop_east_bypass','workshop_entry_aisle','workshop_east_aisle',[16.35,4.7,-6.25],[1,0,0],.8,1.5)
link('workshop_west_rear','workshop_west_aisle','workshop_rear_aisle',[13.1,4.7,-1.1],[0,0,1],.8,1.5)
link('workshop_east_rear','workshop_east_aisle','workshop_rear_aisle',[17.1,4.7,-1.1],[0,0,1],.8,1.5)
zone('lighthouse_entry',28.3,29.7,23.55,24.35,7.5,9.55)
zone('lighthouse_spiral_lower',27,31,24.15,28.1,7.5,10.7)
zone('lighthouse_spiral_middle',27,31,24.15,28.1,10.7,13.6)
zone('lighthouse_spiral_upper',27,31,23.9,28.1,13.6,17.4)
link('lighthouse_open_door','lighthouse_south_approach','lighthouse_entry',[29,8.45,23.6],[0,0,1],1.6,2.4)
link('lighthouse_stair_start','lighthouse_entry','lighthouse_spiral_lower',[29,8.45,24.3],[0,0,1],1.1,1.6)
link('lighthouse_first_turn_air','lighthouse_spiral_lower','lighthouse_spiral_middle',[28.05,10.7,26],[0,1,0],.8,1)
link('lighthouse_second_turn_air','lighthouse_spiral_middle','lighthouse_spiral_upper',[28.05,13.6,26],[0,1,0],.8,1)

routes=[];floor_rows=[]
def p(x,z,y=4.8):return [x,y,z]
def route(n,spawn,points,role='human'):
    assert len(points)<=64,n
    if role=='human':
        for i,(x,y,z) in enumerate(points):
            hit=g.floor(x,z,y,eligible=True);assert hit is not None,(n,i,[x,y,z])
            assert 0<y-hit[0]<3,(n,i,hit)
            floor_rows.append(dict(route=n,index=i,probe=[x,y,z],sourceFloor=[x,hit[0],z],support=hit[1]))
    routes.append(dict(id=n,role=role,spawnIndex=spawn,maxTicks=600,points=[dict(x=float(x),y=float(y),z=float(z)) for x,y,z in points]))

route('plaza-and-northern-cottages',0,[p(-3,5),p(-4,7),p(-7,7),p(-12,7),p(-12,9.2),p(-12,10),p(-12,11.1),p(-11,12.5),p(-12,11.1),p(-12,9.2),p(-12,7),p(-7,7),p(0,7),p(2,10.7),p(2,12.2),p(2,13),p(2,14.1),p(3,15.5),p(2,14.1),p(2,12.2),p(2,10.7),p(3,8),p(3,3),p(3,0),p(-3,0)])
route('cottage01-and-village-street',2,[p(-14,-7),p(-14,-5.7),p(-14,-5),p(-14,-3.9),p(-13,-2.5),p(-14,-3.9),p(-14,-5.7),p(-14,-8),p(-10,-8),p(-7,-6),p(-7,3),p(-9,4),p(-19.2,4),p(-19.2,-7),p(-14,-8)])
route('workshop-open-door-and-boat-bypass',3,[p(9,-9.4),p(12,-10.6),p(15,-10.6),p(15,-9),p(15,-8),p(15,-6.4),p(13.1,-6.4),p(13.1,-1),p(17.1,-1),p(17.1,-6.4),p(15,-6.4),p(15,-8),p(15,-10.6)])
bridge=[p(x,-12.2) for x in np.linspace(8,-8,9)]
bridge=[p(7.4,-11.6),p(6.5,-12.2)]+bridge[2:-2]+[p(-6.5,-12.2),p(-7.4,-11.6)]
route('footbridge-both-directions',3,[p(8.4,-9),p(8.4,-10.5),p(8.4,-11.6)]+bridge+[p(-8.4,-11.6),p(-8.4,-10.5),p(-8.4,-9),p(-8.4,-10.5),p(-8.4,-11.6)]+list(reversed(bridge))+[p(8.4,-11.6),p(8.4,-10.5),p(8.4,-9)])
for side,x,spawn,deck_edge in [(1,-14,2,-19.234),(2,13.2,3,-19.0)]:
    zs=[deck_edge+.225+i*.45 for i in range(10)]
    steps=[]
    for z in reversed(zs):
        hit=g.floor(x,z,4);assert hit and hit[1]==f'Harbor_StairApproach_{side}'
        steps.append(p(x,z,hit[0]+.12))
    approach=[p(x,-9.8),p(x,-12),p(x,zs[-1]+.7)]
    if side==2:approach=[p(9,-9.4),p(12,-10.6),p(14,-10.6)]+approach[1:]
    route(f'pier{side}-stair-down-and-up',spawn,approach+steps+[p(x,deck_edge-.5,2.2),p(x,-27.8,2.2),p(x,deck_edge-.5,2.2)]+list(reversed(steps))+list(reversed(approach)))

def horizontal_levels(name):
    t=g.meshes[name];normal=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]);mask=(np.ptp(t[:,:,1],axis=1)<1e-5)&(normal[:,1]<-1e-7)
    groups={}
    for tri in t[mask]:groups.setdefault(round(float(tri[0,1]),4),[]).extend(tri.tolist())
    return [(y,np.unique(np.round(vs,6),axis=0).mean(axis=0)) for y,vs in sorted(groups.items())]
spiral=[v.tolist() for y,v in horizontal_levels('Lighthouse_ConnectedSpiralStair_56Treads')];assert len(spiral)==56
exterior=[v.tolist() for y,v in horizontal_levels('Lighthouse_Approach_StoneStairway') if y<7.265]
# The highest exterior landing comprises many coplanar treads: use measured centerline.
exterior.extend([[24,7.27,19.1],[26,7.27,20.8],[27.3,7.27,20.2],[28.6,7.27,21.8]])
down=[p(x,z,y+.1) for x,y,z in reversed(exterior)]
toe=[p(17.7,14.7,5.3),p(17.25,15.2,5.3),p(16.5,15.2,5.3)]
route('lighthouse-exterior-stairs-down-and-up',4,[p(29,21.8,8)]+down+toe+list(reversed(toe[:-1]))+list(reversed(down))+[p(29,21.8,8)])
route('lighthouse-56-tread-ascent',4,[p(29,22.5,8),p(29,23.35,8),p(29,23.95,8),p(29,24.1,8)]+[p(x,z,y+.08) for x,y,z in spiral]+[p(29.85,24.6,16.5),p(30.1,24.1,16.5)])
route('flight-cottage01-door',0,[[-9,7,-7],[-14,6,-7],[-14,4.6,-6],[-14,4.6,-5],[-14,4.6,-3.7],[-13,4.6,-2.5],[-14,4.6,-3.7],[-14,4.6,-6],[-14,6,-7]],'mosquito')
route('flight-harbor-and-bridge',10,[[-7,5,-25],[-14,3,-25],[-14,5,-16],[-8,5,-12.2],[0,5,-12.2],[8,5,-12.2],[14,5,-16],[14,3,-25]],'mosquito')
route('flight-lighthouse-spiral',6,[[32,14,18],[29,11,20],[29,8.45,22.5],[29,8.45,23.8]]+[[x,y+1,z] for x,y,z in spiral]+[[29.85,17.3,24.6],[30.1,17.3,24.1]],'mosquito')
route('flight-north-village-and-workshop-door',4,[[10,11,18],[10,11,10],[9,8,4],[9,7,-10.5],[15,4.7,-10.5],[15,4.7,-8],[15,4.7,-6.4],[17.1,4.7,-6.4],[17.1,4.7,-1],[13.1,4.7,-1],[13.1,4.7,-6.4],[15,4.7,-6.4],[15,4.7,-9]],'mosquito')
routes.extend(dict(id=f'runtime-patrol-{i+1}',role='mosquito',spawnIndex=i,maxTicks=600,runtimePatrol=True,points=[]) for i in range(16))

def contains(z,p):return all(a-.04<=v<=b+.04 for a,v,b in zip(z['min'],p,z['max']))
known={z['id']:z for z in plan['zones']};issues=[];passages=[]
for portal in plan['portals']:
    a=np.array(portal['center'])-.55*np.array(portal['normal']);b=np.array(portal['center'])+.55*np.array(portal['normal'])
    for key,pnt in [('from',a),('to',b)]:
        if not contains(known[portal[key]],pnt):issues.append(dict(portal=portal['id'],issue='endpoint outside '+key,point=pnt.tolist()))
    clearance=g.segment(a,b)
    if clearance and not clearance['clear']:issues.append(dict(portal=portal['id'],issue='source triangle clearance',**clearance))
    passages.append(dict(id=portal['id'],points=[a.tolist(),b.tolist()],sourceSphereSweep=clearance or dict(clear=True,noTrianglesWithinSweepAabb=True)))
reached={plan['zones'][0]['id']}
for _ in known:
    for ptl in plan['portals']:
        if ptl['from'] in reached:reached.add(ptl['to'])
        if ptl['to'] in reached:reached.add(ptl['from'])
assert reached==set(known)
spawns=[]
for name in g.recipe['mosquitoSpawns']:
    o=g.objects[name];pnt=[o['matrix_world'][i][3] for i in [0,2,1]];matches=[n for n,z in known.items() if contains(z,pnt)]
    if not matches:issues.append(dict(spawn=name,issue='not covered',point=pnt))
    spawns.append(dict(name=name,unityPosition=pnt,zones=matches))
config=dict(mapId=plan['map_id'],prefabPath='Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-puerto-del-faro-v1/Prefabs/hf-puerto-del-faro-v1.prefab',action='prepare-remaining-map',navigationOverridePath=(OUT/'navigation.json').as_posix(),humanPool=5,mosquitoPool=16,routes=routes)
receipt=dict(status='OFFLINE_PROPOSAL_NATIVE_PENDING' if not issues else 'DRAFT_SOURCE_CHECK_ISSUES',issues=issues,coordinatePolicy='Blender XYZ to Unity XZY; GLB world XYZ to Unity XY-negativeZ; reflected winding accounted for floor normals',
    sourceFiles={n:sha(SOURCE/n) for n in ['scene-audit.json','HF_MAP_05_pueblo_report.json','HF_MAP_05_pueblo_UNITY.fbx','HF_MAP_05_pueblo.glb','UnityRecipeGpu/hf-puerto-del-faro-v1.recipe.json']},
    meshes=730,solidMeshes=458,maxSolidVertexOutsideAuditedBounds=g.max_bounds_error,zones=len(known),portals=len(passages),connected=True,
    humanRoutes=8,flightRoutes=4,runtimePatrols=16,spawnAssignments=spawns,passages=passages,humanProbeEvidence=floor_rows,spiralTreadCentersUnity=spiral,
    limitations=['Source triangle checks are not Unity collision/motor validation.','Air zones contain props; runtime collision avoidance remains authoritative.','Sphere samples are spaced <=1cm and require an extra 5mm clearance margin.','Human support is sampled from solid triangles only; thresholds/edges/capsule penetration require native checks.','No window portals; panes and visible path/cobble/floor boards are non-solid decoration.'])
for name,obj in [('navigation.json',plan),('prepare.json',config),('source-evidence.json',receipt)]:
    text=json.dumps(obj,indent=2,ensure_ascii=False)+'\n';(OUT/name).write_text(text,encoding='utf-8');(HERE/name).write_text(text,encoding='utf-8')
print(json.dumps(dict(status=receipt['status'],zones=len(known),portals=len(passages),routes=len(routes),humanPoints=len(floor_rows),issues=issues),indent=2))
