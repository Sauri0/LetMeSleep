"""Read-only v2 source/physics correlation, no Unity or Blender required."""
import argparse, importlib.util, json, math
from pathlib import Path

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--report',default='N:/LetMeSleep/Validation/Higgsfield/CampV2-20260913/semantic-native-01/map-checks-20260913-074953-500.json')
    parser.add_argument('--output',default='N:/LetMeSleep/Validation/Higgsfield/CampV2-20260913/path-edge-analysis.json')
    args=parser.parse_args()
    spec=importlib.util.spec_from_file_location('fbx',Path(__file__).with_name('Inspect-IslaFbx.py'))
    fbx=importlib.util.module_from_spec(spec);spec.loader.exec_module(fbx)
    source=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento/NormalsV2/HF_MAP_03_campamento_UNITY_V2.fbx')
    version,nodes,sha=fbx.read_fbx(source)
    assert sha=='3fd036eb8ede448aa48eb72764ae9f143dbf6ecff375e1ae10d144dfb44badc3'
    geometry=[o for o in fbx.child({'children':nodes},'Objects')['children'] if o['name']=='Geometry' and 'CAMP_Terrain_Paths' in str(o['values'])]
    assert len(geometry)==1
    flat=fbx.child(geometry[0],'Vertices')['values'][0];vertices=[flat[i:i+3] for i in range(0,len(flat),3)]
    polygons=[];polygon=[]
    for index in fbx.child(geometry[0],'PolygonVertexIndex')['values'][0]:
        polygon.append(index if index>=0 else -index-1)
        if index<0:polygons.append(polygon);polygon=[]
    assert len(vertices)==320 and len(polygons)==149 and all(abs(v[2]-.04)<1e-7 for v in vertices)
    def edge(p,a,b):
        dx,dz=b[0]-a[0],b[1]-a[1]
        t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/(dx*dx+dz*dz)))
        q=[a[0]+t*dx,a[1]+t*dz]
        return math.dist(p,q),q
    def closest(p,indices):
        points=[vertices[i][:2] for i in indices];inside=False
        for a,b in zip(points,points[1:]+points[:1]):
            if (a[1]>p[1])!=(b[1]>p[1]) and p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]:inside=not inside
        distance,point=min(edge(p,a,b) for a,b in zip(points,points[1:]+points[:1]))
        return (0,p) if inside else (distance,point)
    def explain(position):
        ((distance,point),index)=min((closest([position['x'],position['z']],p),i) for i,p in enumerate(polygons))
        vertical=position['y']+.25-vertices[0][2]
        return dict(polygon=index,sourceEdgeClosestUnity=[point[0],vertices[0][2],point[1]],horizontalDistance=distance,
                    lowerSphereCenterY=position['y']+.25,analyticPenetration=max(0,.25-math.hypot(distance,vertical)))
    report=json.loads(Path(args.report).read_text(encoding='utf-8-sig'));rows=[]
    for case in report['cases']:
        if 'CAMP_Terrain_Paths' not in str(case.get('peakBlocker')):continue
        position=case['peakPosition'];row=dict(case=case['id'],tick=case['peakTick'],position=position,nativePenetration=case['maxPenetration'],**explain(position))
        row['difference']=row['analyticPenetration']-row['nativePenetration']
        row['previousSample']=next((s for s in case['samples'] if s['tick']==case['peakTick']-1),None);rows.append(row)
    spawn=dict(x=29.8,y=-.025,z=15.4);spawn_row=explain(spawn);distance=spawn_row['horizontalDistance']
    spawn_row.update(position=spawn,minimumFeetYAtFixedXZ=.04+math.sqrt(.25**2-distance**2)-.25)
    result=dict(status='SOURCE_EDGE_CONTACT_CONFIRMED_ENGINE_CAUSE_NOT_YET_ISOLATED',source=str(source),sourceSha256=sha,
                nativeReport=args.report,scope='Exact source sheet geometry versus recorded full capsule. Does not prove which internal cast/projection produced the overlap.',
                vertices=len(vertices),quads=len(polygons),pathY=.04,grassY=-.035,rows=rows,spawn05=spawn_row)
    Path(args.output).write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(output=args.output,matchedPeaks=len(rows),maximumAbsoluteDifference=max(abs(r['difference']) for r in rows),spawnMinimumFeetY=spawn_row['minimumFeetYAtFixedXZ']),indent=2))

if __name__=='__main__':main()
