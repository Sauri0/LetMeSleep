"""Campamento v2 offline authoring from repaired audit/recipe; no Unity/Blender."""
import hashlib,json,math
from pathlib import Path

root=Path(__file__).resolve().parent
source=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento')
audit_path=source/'NormalsV2/scene-audit.json'
recipe_path=source/'UnityRecipeV2/hf-campamento-pinar-v2.recipe.json'
audit=json.loads(audit_path.read_text(encoding='utf-8-sig'))
recipe=json.loads(recipe_path.read_text(encoding='utf-8-sig'))
expected='3fd036eb8ede448aa48eb72764ae9f143dbf6ecff375e1ae10d144dfb44badc3'
assert recipe['mapId']=='hf-campamento-pinar-v2' and recipe['sourceSha256']==expected
assert hashlib.sha256((source/'NormalsV2/HF_MAP_03_campamento_UNITY_V2.fbx').read_bytes()).hexdigest()==expected
objects={o['name']:o for o in audit['objects']}
plan=dict(schema_version=1,map_id=recipe['mapId'],zones=[],portals=[])
def zone(name,x0,x1,z0,z1,y0=.35,y1=3.5):
    plan['zones'].append(dict(id=name,min=[x0,y0,z0],max=[x1,y1,z1]))
def link(name,a,b,center,normal,width=2,height=2.4):
    plan['portals'].append(dict(id=name,**{'from':a},to=b,center=center,normal=normal,width=width,height=height,door=False))

zone('fire_plaza',-6.5,6.5,-6.5,6.5)
zone('west_path',-20,-6.5,-2,10)
zone('tent_cluster_north_air',-20,-6.5,10,19,1.75)
zone('shelter_clearing',-29.4,-20,0,10)
zone('northwest_trail',-29.4,-20,10,18)
zone('washroom_approach',-26.8,-20,18,25)
zone('washroom_north',-29.4,-26.8,21.6,25)
zone('washroom_west',-36,-29.4,18,25)
zone('washroom_interior',-28.45,-27.55,19.55,20.45,.25,1.95)
zone('south_path',-6.5,6.5,-16,-6.5)
zone('tent_cluster_south_air',6.5,20,-11.8,-2,1.75)
zone('east_field',6.5,20,-2,12.2)
zone('north_central_path',-6.5,6.5,6.5,21)
zone('north_bridge_approach',20,29.5,-2,16.4)
zone('north_bridge_air',29.5,38,13.6,15,.5)
zone('east_bank',38,44,-11.8,18)
zone('creek_air',29.5,38,-8.5,13.6,1.6)
zone('south_bridge_approach',20,28.8,-16,-2)
zone('south_bridge_air',28.8,38,-10.5,-9.1,.5)
zone('lake_air',20,44,-30,-16)
link('plaza_west','fire_plaza','west_path',[-6.5,1.8,2],[-1,0,0])
link('plaza_north','fire_plaza','north_central_path',[.7,1.8,6.5],[0,0,1])
link('plaza_south','fire_plaza','south_path',[.7,1.8,-6.5],[0,0,-1])
link('plaza_east','fire_plaza','east_field',[6.5,1.8,0],[1,0,0])
link('west_tents_north','west_path','tent_cluster_north_air',[-10,2.1,10],[0,0,1])
link('north_tents_path','tent_cluster_north_air','north_central_path',[-6.5,2.1,15],[1,0,0])
link('west_shelter','west_path','shelter_clearing',[-20,1.5,5],[-1,0,0])
link('shelter_trail','shelter_clearing','northwest_trail',[-24,1.5,10],[0,0,1])
link('trail_tents','northwest_trail','tent_cluster_north_air',[-20,2.1,15],[1,0,0])
link('trail_washroom','northwest_trail','washroom_approach',[-24,1.5,18],[0,0,1])
link('washroom_around_north','washroom_approach','washroom_north',[-26.8,1.5,23],[-1,0,0])
link('washroom_around_west','washroom_north','washroom_west',[-29.4,1.5,23],[-1,0,0])
# Real rotated lintel center and front normal from its audited local+Y axis.
lintel=objects['CAMP_Washroom_Lintel'];mx=lintel['matrix_world']
front=[mx[0][1],0,mx[1][1]]
door=[(lintel['bounds_min'][0]+lintel['bounds_max'][0])/2,1.3,(lintel['bounds_min'][1]+lintel['bounds_max'][1])/2]
# Shift10cm outward to leave the exit waypoint safely inside its exterior zone.
link('washroom_open_entry','washroom_interior','washroom_approach',[door[i]+front[i]*.1 for i in range(3)],front,1,2.0)
link('east_tents_south','east_field','tent_cluster_south_air',[16,2.1,-2],[0,0,-1])
link('south_tents','south_path','tent_cluster_south_air',[6.5,2.1,-9],[1,0,0])
link('east_bridge_approach','east_field','north_bridge_approach',[20,1.8,8],[1,0,0])
link('tents_south_bridge','tent_cluster_south_air','south_bridge_approach',[20,2.1,-7],[1,0,0])
link('bridge_approaches','north_bridge_approach','south_bridge_approach',[25,1.8,-2],[0,0,-1])
link('bridge_north_west','north_bridge_approach','north_bridge_air',[29.5,1.8,14.3],[1,0,0],1.4)
link('bridge_north_east','north_bridge_air','east_bank',[38,1.8,14.3],[1,0,0],1.4)
link('bridge_south_west','south_bridge_approach','south_bridge_air',[28.8,1.8,-9.8],[1,0,0],1.4)
link('bridge_south_east','south_bridge_air','east_bank',[38,1.8,-9.8],[1,0,0],1.4)
link('creek_west','north_bridge_approach','creek_air',[29.5,2.1,7],[1,0,0])
link('creek_east','creek_air','east_bank',[38,2.1,7],[1,0,0])
link('lake_west_bank','lake_air','south_bridge_approach',[25,1.8,-16],[0,0,1])

routes=[]
def route(name,spawn,points,role='human'):
    # Human Y is a downward probe origin only, replaced by measured floor+.003 in native preparation.
    routes.append(dict(id=name,role=role,spawnIndex=spawn,maxTicks=600,points=[dict(x=p[0],y=(1.8 if role=='human' else p[1]),z=(p[1] if role=='human' else p[2])) for p in points]))
route('plaza-north-ring',0,[(.7,7),(-2.5,10.5),(3,11),(8,9),(9.5,4.5),(8.5,0),(6,-4.5),(.7,-5.5)])
route('plaza-south-ring',0,[(-4.5,5.5),(-8.5,2),(-10,0),(-8,-4),(-4,-7.5),(1.5,-7.5),(6,-4.5),(5.5,.2)])
route('tent-cluster-north-circulation',1,[(-13.6,9.5),(-16.5,10),(-16.5,17.5),(-9,17.5),(-7,12),(-6.5,9)])
# Revision02: the original point(11.5,-2) intersects Crate02's rotated slat.
# Its full source AABB is X[11.505,12.495], Z[-2.634,-1.766].
crate_detour=[(10.9,-3.3),(10.9,-1.2),(15.8,-1.2)]
route('tent-cluster-south-circulation',2,crate_detour+[(15.8,-11),(9,-11),(8,-6),(8.5,0)])
# CookTable ends at X=-23,Z=5.91; walk east at X=-22.65, then return
# west before the shelter post at Z=7. Preserve the earlier path crossing.
cook_detour=[(-23.2,4.6),(-22.65,4.6),(-22.65,6.35),(-23.2,6.35)]
route('shelter-passage-and-west-path',3,[(-23.2,3.5)]+cook_detour+[(-23.2,7.5),(-24.5,8.5),(-23.2,7.5)]+list(reversed(cook_detour))+[(-22,5.333),(-19,5),(-13.5,4),(-8.5,2)])
route('washroom-entry-return',3,cook_detour+[(-23.2,7.5),(-24.5,7.5),(-25.5,10.5),(-27,15.5),(-26,18),(-26.1,20.895),(door[0],door[2]),(-27.8,20.35),(door[0],door[2]),(-26.1,20.895),(-26,18),(-27,15.5),(-25.5,10.5)])
bridge1=[(29,15.7),(29,14.3),(29.5,14.3),(30.05,14.3),(30.6,14.3),(33.75,14.3),(36.9,14.3),(37.45,14.3),(38,14.3),(38.8,14.3)]
route('bridge-north-both-directions',4,bridge1+list(reversed(bridge1[:-1])))
bridge2=[(28.8,-9.8),(29.35,-9.8),(29.9,-9.8),(33.4,-9.8),(36.9,-9.8),(37.45,-9.8),(38,-9.8),(38.8,-9.8)]
route('bridge-south-both-directions',2,crate_detour+[(17,-4),(19,-13.5),(24,-12),(27.6,-10.9)]+bridge2+list(reversed(bridge2[:-1])))
route('flight-north-bridge',11,[(28,2.1,14.3),(30,1.8,14.3),(34,1.8,14.3),(38.5,1.8,14.3),(34,1.8,14.3),(28,2.1,14.3)],'mosquito')
route('flight-south-bridge',13,[(25,2.1,-17),(27,2.1,-12),(28.8,1.8,-9.8),(33.4,1.8,-9.8),(38.5,1.8,-9.8),(33.4,1.8,-9.8),(28,2.1,-9.8)],'mosquito')
route('flight-shelter',10,[(-23.2,1.5,7.5),(-23.2,1.5,5.5),(-23.2,1.5,3.5),(-23.2,1.5,5.5),(-20,1.5,5)],'mosquito')
route('flight-washroom',15,[(-31,2.1,23),(-27,2.1,23),(-25,2.1,23),(-26.1,1.3,20.895),(door[0],1.3,door[2]),(-28,1.3,20),(door[0],1.3,door[2]),(-26.1,1.3,20.895)],'mosquito')
routes += [dict(id=f'runtime-patrol-{i+1}',role='mosquito',spawnIndex=i,maxTicks=600,runtimePatrol=True,points=[]) for i in range(16)]
nav=root/'camp-v2.semantic-navigation.json';nav.write_text(json.dumps(plan,indent=2)+'\n')
config=dict(mapId=recipe['mapId'],prefabPath='Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/hf-campamento-pinar-v2/Prefabs/hf-campamento-pinar-v2.prefab',action='prepare-camp',navigationOverridePath=nav.as_posix(),humanPool=5,mosquitoPool=16,routes=routes)
(root/'camp-v2.prepare.json').write_text(json.dumps(config,indent=2)+'\n')
def contains(zone,p):return all(a-.04<=x<=b+.04 for a,x,b in zip(zone['min'],p,zone['max']))
known={z['id']:z for z in plan['zones']};reached={next(iter(known))}
for _ in known:
    for p in plan['portals']:
        if p['from'] in reached:reached.add(p['to'])
        if p['to'] in reached:reached.add(p['from'])
assert reached==set(known)
for p in plan['portals']:
    for sign,key in [(-1,'from'),(1,'to')]:
        assert contains(known[p[key]],[a+sign*.55*b for a,b in zip(p['center'],p['normal'])]),p['id']
spawns=[]
for name in recipe['mosquitoSpawns']:
    x,z,y=objects[name]['location'];matches=[n for n,v in known.items() if contains(v,[x,y,z])];assert matches,name;spawns.append(dict(name=name,unityPosition=[x,y,z],zones=matches))
receipt=dict(status='OFFLINE_TOPOLOGY_ONLY_NATIVE_PENDING',mapId=recipe['mapId'],sourceSha256=expected,auditSha256=hashlib.sha256(audit_path.read_bytes()).hexdigest(),recipeSha256=hashlib.sha256(recipe_path.read_bytes()).hexdigest(),zones=len(known),portals=len(plan['portals']),plannedHumanRoutes=8,plannedFlightRoutes=4,plannedRealPatrols=16,spawnAssignments=spawns,washroomDoorCenter=door,washroomFrontNormal=front)
(root/'camp-v2.offline-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps({k:v for k,v in receipt.items() if k!='spawnAssignments'},indent=2))
