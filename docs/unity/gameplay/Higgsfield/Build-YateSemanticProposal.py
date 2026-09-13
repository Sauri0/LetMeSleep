"""Author Yate v3 schema1 and bounded routes from exact repaired FBX/audit."""
import hashlib,importlib.util,json
from pathlib import Path
root=Path(__file__).resolve().parent
art=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/04-yate')
recipe=json.loads((art/'UnityRecipeV3/hf-yate-a-la-deriva-v3.recipe.json').read_text(encoding='utf-8-sig'))
audit=json.loads((art/'NormalsV3/scene-audit.json').read_text(encoding='utf-8-sig'))
objects={o['name']:o for o in audit['objects']}
source=art/'NormalsV3/HF_MAP_04_yate_UNITY_V3.fbx'
expected='217774581132711b6197a6115954e6e63acd3fdbf42b2705c0c115e6bc146870'
assert recipe['mapId']=='hf-yate-a-la-deriva-v3' and recipe['sourceSha256']==expected and hashlib.sha256(source.read_bytes()).hexdigest()==expected
spec=importlib.util.spec_from_file_location('fbx',root/'Inspect-IslaFbx.py');fbx=importlib.util.module_from_spec(spec);spec.loader.exec_module(fbx)
_,nodes,_=fbx.read_fbx(source);stairs={}
for obj in fbx.child({'children':nodes},'Objects')['children']:
    if obj['name']!='Geometry' or 'YATE_Stair_' not in str(obj['values']):continue
    name=obj['values'][1].split('_Mesh')[0];flat=fbx.child(obj,'Vertices')['values'][0];v=[flat[i:i+3] for i in range(0,len(flat),3)];polygon=[];tops=[]
    for index in fbx.child(obj,'PolygonVertexIndex')['values'][0]:
        polygon.append(index if index>=0 else -index-1)
        if index>=0:continue
        points=[v[i] for i in polygon];lo=[min(p[k] for p in points) for k in range(3)];hi=[max(p[k] for p in points) for k in range(3)];a,b,c=points[:3]
        normal_z=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
        if hi[2]-lo[2]<1e-5 and hi[0]-lo[0]>.8 and .1<hi[1]-lo[1]<.5 and normal_z>0:
            tops.append([(lo[0]+hi[0])/2,hi[2],(lo[1]+hi[1])/2])
        polygon=[]
    stairs[name]=sorted(tops,key=lambda p:p[1])
assert [len(stairs[n]) for n in ['YATE_Stair_Interior_LowerToMain','YATE_Stair_AftToFlybridge','YATE_Stair_SwimToAft']]==[15,16,14]
plan=dict(schema_version=1,map_id=recipe['mapId'],zones=[],portals=[])
def zone(name,x0,x1,z0,z1,y0=3.7,y1=6):plan['zones'].append(dict(id=name,min=[x0,y0,z0],max=[x1,y1,z1]))
def link(name,a,b,p,n,width=1,height=1.5):plan['portals'].append(dict(id=name,**{'from':a},to=b,center=p,normal=n,width=width,height=height,door=False))
zone('aft_stern',-3.7,3.7,-13.6,-10.65)
zone('aft_center',-.88,3.7,-10.65,-4.6)
zone('aft_port',-3.7,-2.22,-10.65,-4.6)
zone('aft_stair',-2.22,-.88,-10.65,-5.6,3.6,8.3)
zone('aft_stair_front',-2.22,-.88,-5.6,-4.6)
zone('salon_aft',-2.25,2.25,-4.6,-.6,3.7,5.6)
zone('salon_port',-2.25,-.9,-.6,4.6,3.7,5.6)
zone('salon_starboard',.9,2.25,-.6,4.6,3.7,5.6)
zone('salon_fore',-2.25,2.25,4.6,7.4,3.7,5.6)
zone('main_port',-3.7,-2.65,-4.6,7.5)
zone('main_starboard',2.65,3.7,-4.6,7.5)
zone('foredeck',-3.7,3.7,7.5,14.7,3.7,6.1)
zone('flybridge_aft',-2.5,2.5,-5.6,-1.5,6.4,8.8)
zone('flybridge_mid',-2.5,2.5,-1.5,2.75,6.4,9.5)
zone('flybridge_fore',-2.5,2.5,2.75,6.5,6.4,9)
zone('lower_corridor',-.79,.79,-11.5,-.55,1,3.1)
zone('lower_port_cabin',-3.08,-.93,-11.5,-5.05,1,3.1)
zone('lower_starboard_cabin',.93,3.08,-11.5,-5.05,1,3.1)
zone('lower_bathroom',.93,3.08,-4.85,-1.48,1,3.1)
zone('lower_foyer_port',-3.08,-.8,-.55,7.55,1,3.1)
zone('lower_foyer_starboard',.8,3.08,-.55,7.55,1,3.1)
zone('lower_fore',-.8,.8,4.6,7.55,1,3.1)
zone('interior_stair',-.8,.8,-.55,4.6,1,5.6)
zone('swim_platform',-3.5,3.5,-15,-14.1,1.35,4.5)
zone('swim_stair',1.7,3,-14.1,-10.65,1.2,5.8)
link('stern_center','aft_stern','aft_center',[0,5,-10.65],[0,0,1])
link('stern_port','aft_stern','aft_port',[-3,5,-10.65],[0,0,1])
link('stern_aft_stair','aft_stern','aft_stair',[-1.55,5,-10.65],[0,0,1])
# The front under-stair space is distinct from the flight path above the treads.
# Connecting these at Y5.2 made bots target a point below the top flight.
link('aft_center_front','aft_stair_front','aft_center',[-.88,5.2,-5.05],[1,0,0])
link('aft_to_salon','aft_center','salon_aft',[0,5,-4.6],[0,0,1],1.6)
link('aft_to_port_walk','aft_port','main_port',[-3.1,5,-4.6],[0,0,1])
link('aft_to_starboard_walk','aft_center','main_starboard',[3.1,5,-4.6],[0,0,1])
link('port_to_foredeck','main_port','foredeck',[-3,5.3,7.5],[0,0,1])
link('starboard_to_foredeck','main_starboard','foredeck',[3,5.3,7.5],[0,0,1])
link('salon_port_lane','salon_aft','salon_port',[-1.35,5.2,-.6],[0,0,1])
link('salon_starboard_lane','salon_aft','salon_starboard',[1.35,5.2,-.6],[0,0,1])
link('salon_port_fore','salon_port','salon_fore',[-1.35,5.2,4.6],[0,0,1])
link('salon_starboard_fore','salon_starboard','salon_fore',[1.35,5.2,4.6],[0,0,1])
link('aft_stair_to_flybridge','aft_stair','flybridge_aft',[-1.55,7.7,-5.6],[0,0,1])
link('flybridge_aft_mid','flybridge_aft','flybridge_mid',[-.75,7.8,-1.5],[0,0,1])
link('flybridge_mid_fore','flybridge_mid','flybridge_fore',[1.4,8,2.75],[0,0,1])
link('salon_to_interior_stair','salon_fore','interior_stair',[0,4.9,4.6],[0,0,-1])
link('interior_stair_lower','lower_corridor','interior_stair',[0,2,-.55],[0,0,1])
link('corridor_port_cabin','lower_corridor','lower_port_cabin',[-.86,2.5,-6.1],[-1,0,0],1.1)
link('corridor_starboard_cabin','lower_corridor','lower_starboard_cabin',[.86,2.5,-6.1],[1,0,0],1.1)
link('corridor_bathroom','lower_corridor','lower_bathroom',[.86,2.3,-3.4],[1,0,0],1.1)
link('lower_corridor_foyer_port','lower_corridor','lower_foyer_port',[-.8,1.7,-.55],[-.70710678,0,.70710678])
link('lower_corridor_foyer_starboard','lower_corridor','lower_foyer_starboard',[.8,1.7,-.55],[.70710678,0,.70710678])
link('lower_port_fore','lower_foyer_port','lower_fore',[-.8,1.7,6],[1,0,0])
link('lower_starboard_fore','lower_foyer_starboard','lower_fore',[.8,1.7,6],[-1,0,0])
link('swim_platform_stair','swim_platform','swim_stair',[2.35,2.1,-14.1],[0,0,1])
link('swim_stair_stern','swim_stair','aft_stern',[2.35,4.9,-13.5],[0,0,1])

def p(x,z,y=5.2):return [x,y,z] # Human downward probe, not feet.
def stair_probes(name):return [[x,y+.08,z] for x,y,z in stairs[name]]
aft=stair_probes('YATE_Stair_AftToFlybridge');lower=stair_probes('YATE_Stair_Interior_LowerToMain');swim=stair_probes('YATE_Stair_SwimToAft')
routes=[]
def route(name,spawn,points,role='human'):routes.append(dict(id=name,role=role,spawnIndex=spawn,maxTicks=600,points=[dict(x=x,y=y,z=z) for x,y,z in points]))
route('aft-stair-up',0,[p(-1.55,-10.9)]+aft+[p(-1.55,-5.3,8),p(-.5,-5.3,8),p(0,-4.9,8)])
route('aft-stair-down',4,[p(-.5,-5.3,8),p(-1.55,-5.3,8)]+list(reversed(aft))+[p(-1.55,-10.9),p(-.4,-10.75)])
route('main-port-to-foredeck',0,[p(.8,-11),p(.8,-12.95),p(-2.9,-12.95),p(-2.9,-12),p(-3.45,-12),p(-3.45,-7),p(-3.25,-3),p(-3.25,2),p(-3.0,7.7),p(-2.3,10.5),p(-1.8,12),p(0,12.75)])
route('main-starboard-to-aft',3,[p(1.8,12),p(2.3,10.5),p(3,7.7),p(3.28,2),p(3.28,-3),p(3.2,-7),p(3.2,-10.4),p(-.4,-10.75)])
lower_approach=[p(1.35,-3.6),p(1.35,4.95),p(0,4.95)]
route('interior-stair-roundtrip',1,lower_approach+list(reversed(lower))+[p(0,-.85,2.5),p(0,-1.2,2.5),p(0,-.85,2.5)]+lower+[p(0,4.95),p(1.35,4.95)])
route('lower-cabins-and-bathroom',1,lower_approach+list(reversed(lower))+[p(0,-1.2,2.5),p(0,-6.1,2.5),p(-1.45,-6.1,2.5),p(-1.45,-7,2.5),p(-1.45,-6.1,2.5),p(0,-6.1,2.5),p(1.45,-6.1,2.5),p(1.45,-7,2.5),p(1.45,-6.1,2.5),p(0,-6.1,2.5),p(0,-3.4,2.5),p(1.6,-3.4,2.5),p(0,-3.4,2.5)])
route('swim-stair-roundtrip',0,[p(2.35,-10.35)]+list(reversed(swim))+[p(2.35,-14.4,2.5),p(0,-14.5,2.5),p(2.35,-14.4,2.5)]+swim+[p(2.35,-10.35)])
route('flybridge-circulation',4,[p(-.75,-4.7,8),p(-.75,-1.5,8),p(-.75,1.8,8),p(1.4,1.8,8),p(1.4,4,8),p(1.4,1.8,8),p(-.75,1.8,8),p(-.75,-4.7,8)])
route('flight-interior-stair',14,[[0,2,-1.2]]+[[x,y+1,z] for x,y,z in stairs['YATE_Stair_Interior_LowerToMain']]+[[0,4.9,5.2]],'mosquito')
route('flight-aft-stair',0,[[-1.55,5,-10.8]]+[[x,y+1.2,z] for x,y,z in stairs['YATE_Stair_AftToFlybridge']]+[[-.5,7.7,-5]],'mosquito')
route('flight-main-perimeter',3,[[-3.1,5.4,7.7],[0,5.4,12.8],[3.1,5.4,7.7],[3.1,5.4,-4.9],[0,5.4,-11.2]],'mosquito')
route('flight-swim-stair',2,[[2.35,4.9,-10.3]]+[[x,y+.7,z] for x,y,z in reversed(stairs['YATE_Stair_SwimToAft'])]+[[2.35,2,-14.6],[0,2,-14.6]],'mosquito')
routes += [dict(id=f'runtime-patrol-{i+1}',role='mosquito',spawnIndex=i,maxTicks=600,runtimePatrol=True,points=[]) for i in range(16)]
known={z['id']:z for z in plan['zones']}
def contains(z,p):return all(a-.04<=v<=b+.04 for a,v,b in zip(z['min'],p,z['max']))
for link in plan['portals']:
    for sign,key in [(-1,'from'),(1,'to')]:assert contains(known[link[key]],[a+sign*.55*b for a,b in zip(link['center'],link['normal'])]),link['id']
spawn_zones=[]
for name in recipe['mosquitoSpawns']:
    x,z,y=objects[name]['location'];matches=[key for key,value in known.items() if contains(value,[x,y,z])];assert matches,name;spawn_zones.append(dict(name=name,zones=matches))
nav=root/'yate-v3.semantic-navigation.json';nav.write_text(json.dumps(plan,indent=2)+'\n')
config=dict(mapId=recipe['mapId'],prefabPath='Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/'+recipe['mapId']+'/Prefabs/'+recipe['mapId']+'.prefab',action='prepare-remaining-map',navigationOverridePath=str(nav),humanPool=5,mosquitoPool=16,routes=routes)
(root/'yate-v3.prepare.json').write_text(json.dumps(config,indent=2)+'\n')
receipt=dict(status='OFFLINE_ONLY_NATIVE_PENDING',sourceSha256=expected,stairs=stairs,zones=len(known),portals=len(plan['portals']),plannedCases=21+len(routes),spawnZones=spawn_zones)
(root/'yate-v3.offline-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps({k:v for k,v in receipt.items() if k not in ['stairs','spawnZones']},indent=2))
