"""Offline discriminating geometry checks; does not replace native movement/collision tests."""
import hashlib,json,sys
from pathlib import Path
OUT=Path('N:/LetMeSleep/Validation/Higgsfield/CompleteScope/MapBounds')
sys.path.insert(0,str(OUT/'python-libs'))
from shapely.geometry import Point,Polygon

def run():
    raw=(OUT/'bounds-approved-preflight.json').read_bytes();config=json.loads(raw);cases=[]
    def hazardous(map_input,role,p):
        for zone in map_input['recovery'][role+'PolygonFallZones']:
            if zone['minY']<=p[1]<=zone['maxY']:
                shape=Polygon(zone['outer'],[h['vertices'] for h in zone['holes']])
                if shape.contains(Point(p[0],p[2])):return True
        return False
    ids=[m['mapId'] for m in config['maps']];assert len(set(ids))==5
    for m in config['maps']:
        assert m['wallBottomY']==-10 and m['innerMin'][1]>-10
        assert m['innerMax'][1]>=m['protectedSolidMax'][1]+m['clearanceAboveSolid']
        for role in ('human','mosquito'):
            zones=m['recovery'][role+'PolygonFallZones'];assert len(zones)<=64
            for z in zones:
                p=Polygon(z['outer'],[h['vertices'] for h in z['holes']]);assert p.is_valid and p.area>0
                assert len(z['outer'])<=2048 and len(z['holes'])<=16 and z['minY']<z['maxY']
        cases.append(m['mapId']+': valid envelope, role prisms and topology')
    by={m['mapId']:m for m in config['maps']}
    samples=[
        ('hf-yate-a-la-deriva-v3','mosquito',[0,.1,0],False,'Yate hull hole remains dry'),
        ('hf-yate-a-la-deriva-v3','mosquito',[6,.15,0],True,'Yate sea crest guard .195'),
        ('hf-yate-a-la-deriva-v3','mosquito',[6,.25,0],False,'Yate flight above maximum crest remains safe'),
        ('hf-yate-a-la-deriva-v3','human',[0,1.1,-14],False,'Swim platform foot height safe'),
        ('hf-puerto-del-faro-v1','mosquito',[0,3.35,3],True,'Well submerged plaza acquisition area covered'),
        ('hf-puerto-del-faro-v1','mosquito',[1.1,3.4,3],False,'Well rim outside water disc remains dry'),
        ('hf-puerto-del-faro-v1','human',[-14,1.24,-25],False,'Puerto pier above water safe'),
        ('hf-campamento-pinar-v2','human',[33.930238,-.42,-16.106517],True,'Creek bed triggers without relying on global minimum'),
        ('hf-campamento-pinar-v2','mosquito',[33.930238,-.365,-16.106517],True,'Mosquito creek bed contact covered'),
        ('hf-campamento-pinar-v2','human',[33.75,.305,14.3],False,'Camp bridge support foot above water safe'),
        ('hf-campamento-pinar-v2','mosquito',[33.75,.360,14.3],False,'Camp bridge perched center above crest safe'),
        ('hf-isla-del-laguito-v2','human',[.558526,-1.783444,.014018],True,'Isla submerged bed covered'),
    ]
    for map_id,role,p,expected,label in samples:
        assert hazardous(by[map_id],role,p)==expected,label;cases.append(label)
    art=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas')
    for m,folder in zip(config['maps'],('01-isla','02-casa','03-campamento','04-yate','05-pueblo')):
        audit=json.loads((art/folder/'scene-audit.json').read_text())
        count=0
        for obj in audit['objects']:
            if not obj['name'].startswith(('Spawn_Human_','Spawn_Mosquito_')):continue
            role='human' if 'Human' in obj['name'] else 'mosquito';matrix=obj['matrix_world'];p=[matrix[0][3],matrix[2][3],matrix[1][3]]
            assert not hazardous(m,role,p),(folder,obj['name'],'spawn inside water recovery')
            assert all(m['innerMin'][i]<p[i]<m['innerMax'][i] for i in range(3)),(folder,obj['name'])
            count+=1
        cases.append(folder+': '+str(count)+' authored role spawn positions inside envelope and outside water zones')
    result=dict(status='PASS_OFFLINE_CONFIGURATION_AND_DISCRIMINATING_POINTS',nativeExecuted=False,
        configSha256=hashlib.sha256(raw).hexdigest(),cases=cases,
        limitations='Point membership uses planar polygon interiors at clear test positions, not native boundary tolerance/casts or actual route reachability. Spawn source positions predate the documented internal source corrections. Unity prefab/scene readback, idempotence, failure injection/rollback and all functional actor states remain pending assigned native slot.')
    (OUT/'configuration-checks.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result))

if __name__=='__main__':run()
