"""Authored structural proposal. Writes external JSON only; no Unity/scene mutation."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent
plan = dict(schema_version=1, map_id='hf-casa-del-patio-v1', zones=[], portals=[])
def zone(name, lo, hi):
    plan['zones'].append(dict(id=name, min=lo, max=hi))
def portal(name, source, target, center, normal, width=1.2, height=2.2):
    plan['portals'].append(dict(id=name, **{'from':source}, to=target, center=center,
                                normal=normal, width=width, height=height, door=False))

zone('patio_front',[-20,.1,-18],[20,2.65,-5.05])
zone('garden_west',[-20,.1,-5.05],[-7.1,2.65,5.05])
zone('garden_east',[7.1,.1,-5.05],[20,2.65,5.05])
zone('patio_rear',[-20,.1,5.05],[20,2.65,18])
for side,x in [('west',-10),('east',10)]:
    portal('front_'+side,'patio_front','garden_'+side,[x,1.6,-5.05],[0,0,1],3,2.4)
    portal(side+'_rear','garden_'+side,'patio_rear',[x,1.6,5.05],[0,0,1],3,2.4)
for level,lo,hi,y in [('gf',.4,2.65,1.5),('uf',3.3,5.15,4.4)]:
    zone(level+'_front_hall',[-1.25,lo,-4.76],[2.05,hi,-1.65])
    zone(level+'_west_hall',[-1.25,lo,-1.65],[.3,hi,3.15])
    zone(level+'_rear_hall',[-1.25,lo,3.15],[2.05,hi,4.76])
    zone(level+'_west_room',[-6.76,lo,-4.76],[-1.55,hi,4.76])
    zone(level+'_east_room',[2.35,lo,-4.76],[6.76,hi,1.75])
    zone(level+'_bathroom',[2.35,lo,2.05],[6.76,hi,4.76])
    portal(level+'_front_west_hall',level+'_front_hall',level+'_west_hall',[-.55,y,-1.65],[0,0,1],1.5)
    portal(level+'_west_rear_hall',level+'_west_hall',level+'_rear_hall',[-.55,y,3.15],[0,0,1],1.5)
    portal(level+'_west_door',level+'_west_room',level+'_west_hall',[-1.4,y,-.25],[1,0,0],1.3)
    portal(level+'_east_door',level+'_front_hall',level+'_east_room',[2.2,y,-3],[1,0,0],1.3)
    portal(level+'_bath_door',level+'_rear_hall',level+'_bathroom',[2.2,y,3.95],[1,0,0],1.2)
portal('front_entry','patio_front','gf_front_hall',[-.4,1.5,-5],[0,0,1],1.3)
portal('rear_entry','gf_rear_hall','patio_rear',[-.4,1.5,5],[0,0,1],1.3)
# Schema1's two-flight fields encode one straight stair with duplicate middle
# points and a short final reverse segment. They do not describe a second stair.
plan['stair']=dict(id='straight_stair', **{'from':'gf_front_hall'}, to='uf_rear_hall',
    lower_flight=dict(clear_x=[.65,1.8],start_y=.55,end_y=1.753,start_z=-1.55,end_z=.63),
    upper_flight=dict(clear_x=[.65,1.8],start_y=1.753,end_y=3.35,start_z=.63,end_z=4.25),
    mid_landing=dict(id='schema_midpoint_on_straight_flight',min=[.65,1.7,.62],max=[1.8,1.8,.64]))
path=root/'casa-v1.semantic-navigation.json'
path.write_text(json.dumps(plan,indent=2)+'\n')
config=json.loads((root/'casa-v1.routes.json').read_text(encoding='utf-8-sig'))
config['navigationOverridePath']=path.as_posix()
config['routes'] += [dict(id=f'runtime-patrol-{i+1}',role='mosquito',spawnIndex=i,maxTicks=600,
                         runtimePatrol=True,points=[]) for i in range(16)]
(root/'casa-v1.semantic-checks.json').write_text(json.dumps(config,indent=2)+'\n')
isla=json.loads((root/'isla-v2.collider-candidate.json').read_text(encoding='utf-8-sig'))
isla['colliderCandidate']='south-arrival-complete-convex-support'
(root/'isla-v2.complete-support.json').write_text(json.dumps(isla,indent=2)+'\n')
# Structural topology only. Native collider clearance and actual runtime remain pending.
known={z['id'] for z in plan['zones']}
reached={plan['zones'][0]['id']}
links=plan['portals']+[plan['stair']]
for _ in known:
    for p in links:
        if p['from'] in reached: reached.add(p['to'])
        if p['to'] in reached: reached.add(p['from'])
assert reached==known
for p in plan['portals']:
    for sign,key in [(-1,'from'),(1,'to')]:
        point=[a+sign*.55*b for a,b in zip(p['center'],p['normal'])]
        z=next(z for z in plan['zones'] if z['id']==p[key])
        assert all(a-.04<=v<=b+.04 for a,v,b in zip(z['min'],point,z['max'])),p['id']
print(f'External proposal: {len(known)} semantic zones, {len(plan["portals"])} portals, one straight stair; graph/endpoints valid offline.')
