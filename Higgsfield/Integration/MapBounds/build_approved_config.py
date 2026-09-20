"""Compose root-approved limits and measured water prisms. Keeps inspect action; no Unity writes."""
import hashlib,json,math,re
from pathlib import Path

OUT=Path('N:/LetMeSleep/Validation/Higgsfield/CompleteScope/MapBounds')
SOURCE=Path('N:/LetMeSleep/Validation/Higgsfield/CompleteScope/Recovery/source/Gameplay.Unity/GameplayRecoveryVolume.cs')
BOUNDS=[([-54,-4,-49],[54,16,49]),([-25,-1,-22],[25,12,22]),([-50,-.5,-40],[55,10,40]),([-7,.05,-18],[7,12,18]),([-58,-2,-46],[58,22,46])]

def run():
    request=json.loads((OUT/'bounds-proposal.json').read_text())
    measured=json.loads((OUT/'source-envelope-measurements.json').read_text())['maps']
    waters=json.loads((OUT/'water-footprints-v3.json').read_text())['maps']
    exclusions=[];proof=[]
    for row,report,water_map,(lo,hi) in zip(request['maps'],measured,waters,BOUNDS):
        row.update(innerMin=lo,innerMax=hi,wallBottomY=-10)
        protected=[];excluded=[]
        for obj in report['allSolids']:
            outside=any(obj['min'][i]<lo[i]+.25 or obj['max'][i]>hi[i]-.25 for i in (0,2))
            if outside:
                assert re.match(r'^(CAMP_Terrain_Rim|CAMP_Pine_|CAMP_Prod_BankRock|BackdropPine_|Rock_Shore_)',obj['name']),obj['name']
                excluded.append(obj)
            else:protected.append(obj)
        row['protectedSolidMin']=[min(o['min'][i] for o in protected) for i in range(3)]
        row['protectedSolidMax']=[max(o['max'][i] for o in protected) for i in range(3)]
        row['protectedObjectNames']=[o['name'] for o in protected]
        exclusions.append(dict(mapId=row['mapId'],protectedCount=len(protected),externalScenery=excluded))
        recovery=dict(regionsApproved=True,humanFallZones=[],mosquitoFallZones=[],humanPolygonFallZones=[],mosquitoPolygonFallZones=[],
            humanSpawnPoints=[],mosquitoSpawnPoints=[],retryTicks=30,interiorMargin=.02,supportProbe=.08)
        row['waterGuards']=[]
        for water in water_map['waters']:
            base=water['unityMax'][1] if water['name']=='Well_DeepWater' else (water['unityMin'][1]+water['unityMax'][1])/2
            base=round(base,6)
            if row['mapId']=='hf-yate-a-la-deriva-v3':crest=base+.14;method='GPU native binding Amplitude .14'
            elif row['mapId']=='hf-puerto-del-faro-v1' and water['name']=='Water_Ocean_Pueblo':crest=base+.09;method='GPU native binding Amplitude .09'
            elif water['morphTargetBounds']:
                crest=max(t['maxY'] for p in water['morphTargetBounds'] for t in p['targets']);method='CPU complementary 0..100 blendshape weights: maximum of both GLB target heights, not Amplitude field'
            else:crest=base;method='Static top surface'
            row['waterGuards'].append(dict(objectName=water['name'],mode='gpu' if method.startswith('GPU') else 'cpu_morph_pair' if water['morphTargetBounds'] else 'static',
                expectedCrestMaxY=crest,expectedGpuAmplitude=.14 if row['mapId']=='hf-yate-a-la-deriva-v3' else .09 if method.startswith('GPU') else 0))
            for i,region in enumerate(water['projectedRegions']):
                for role in ('human','mosquito'):
                    max_y=base-.02 if role=='human' else math.ceil((crest+.055)*1000000)/1000000
                    recovery[role+'PolygonFallZones'].append(dict(id=water['name']+'-'+str(i),outer=region['outer'],
                        holes=[dict(vertices=ring) for ring in region['holes']],minY=-10,maxY=max_y,edgeTolerance=.001))
            proof.append(dict(mapId=row['mapId'],water=water['name'],baseY=base,crestMaxY=crest,
                humanFeetMaxY=base-.02,mosquitoCenterMaxY=math.ceil((crest+.055)*1000000)/1000000,method=method,
                regions=len(water['projectedRegions']),holes=[len(r['holes']) for r in water['projectedRegions']],
                sourceSha256=water_map['sourceSha256']))
        row['recovery']=recovery
    request.update(approvedExtents=True,action='inspect',receiptPath=str(OUT/'native-approved-envelope-inspection-PENDING.json'),
        recoveryScriptSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        scope='Root-approved extents/level policy for native preflight only. Current file/dependency guards must be refreshed after final Camp/visual updates. SafetyBounds and PlayBounds match innerMin/Max; wallBottomY=-10 is separate. Exact projected water polygons/holes, no floor. CPU crest bounds still require imported blendshape verification; native dry/bridge/hull/pozo checks pending.')
    raw=json.dumps(request,indent=2)+'\n'
    (OUT/'bounds-approved-preflight.json').write_text(raw)
    Path(__file__).with_name('bounds-approved-preflight.json').write_text(raw)
    (OUT/'external-scenery-outside-approved-play.json').write_text(json.dumps(exclusions,indent=2)+'\n')
    (OUT/'water-policy-proof.json').write_text(json.dumps(dict(scope='Measured sources and root-approved policy; not native movement or submersion certification.',waters=proof),indent=2)+'\n')
    print(json.dumps([dict(mapId=m['mapId'],bounds=[m['innerMin'],m['innerMax']],humanPrisms=len(m['recovery']['humanPolygonFallZones']),mosquitoPrisms=len(m['recovery']['mosquitoPolygonFallZones'])) for m in request['maps']]))

if __name__=='__main__':run()
