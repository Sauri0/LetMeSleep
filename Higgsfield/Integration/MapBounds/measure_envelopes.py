"""Read-only authored solid AABBs; proposal, never gameplay reachability certification."""
import hashlib
import json
import math
import re
from pathlib import Path

CENTRAL=Path('N:/LetMeSleep/Repository')
ART=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas')
OUT=Path('N:/LetMeSleep/Validation/Higgsfield/CompleteScope/MapBounds')
MAPS=[('01-isla','hf-isla-del-laguito-v2'),('02-casa','hf-casa-del-patio-v1'),
      ('03-campamento','hf-campamento-pinar-v2'),('04-yate','hf-yate-a-la-deriva-v3'),('05-pueblo','hf-puerto-del-faro-v1')]

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def vec(s):return [float(v) for v in re.findall(r'[xyz]: ([^,}]+)',s)]
def guard(relative):
    p=CENTRAL/'unity'/relative
    return dict(path=relative,guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1],sha256=sha(p),metaSha256=sha(Path(str(p)+'.meta')))

def measure():
    reports=[];configs=[]
    native_ids=json.loads((ART/'UnityPackage/catalog-receipt-night-final.json').read_text())['maps']
    for folder,map_id in MAPS:
        audit_path=ART/folder/'scene-audit.json';audit=json.loads(audit_path.read_text())
        prefix='Assets/LetMeSleep/Content/Environment/HiggsfieldMaps/'+map_id
        prefab=guard(prefix+'/Prefabs/'+map_id+'.prefab');scene=guard(prefix+'/Scenes/'+map_id+'.unity')
        prefab['dependencyHash']=next(row['prefabDependencyHash'] for row in native_ids if row['mapId']==map_id)
        text=(CENTRAL/'unity'/prefab['path']).read_text()
        content=re.search(r'^  ContentHash: (\w+)',text,re.M)[1]
        override_path=Path(__file__).with_name('native-dependency-overrides.json')
        overrides=json.loads(override_path.read_text()) if override_path.is_file() else {}
        if map_id in overrides:
            override=overrides[map_id]
            assert override['contentHash']==content and sha(Path(override['evidencePath']))==override['evidenceSha256']
            prefab['dependencyHash']=override['dependencyHash']
        bounds=re.search(r'  PlayBounds:\s*\n    m_Center: ({[^\n]+})\s*\n    m_Extent: ({[^\n]+})',text)
        center,extent=vec(bounds[1]),vec(bounds[2]);oldmin=[a-b for a,b in zip(center,extent)];oldmax=[a+b for a,b in zip(center,extent)]
        spatial_guid=re.search(r'^  SpatialData: \{fileID: [^,]+, guid: (\w+)',text,re.M)[1]
        spatial_meta=next(p for p in (CENTRAL/'unity'/prefix/'Data').glob('*.meta') if ('guid: '+spatial_guid) in p.read_text())
        spatial=guard(spatial_meta.relative_to(CENTRAL/'unity').as_posix()[:-5])
        solids=[]
        for o in audit['objects']:
            if o['type']!='MESH' or o['properties'].get('collision_role')!='static_solid':continue
            lo=[o['bounds_min'][i] for i in (0,2,1)];hi=[o['bounds_max'][i] for i in (0,2,1)]
            solids.append(dict(name=o['name'],min=lo,max=hi,collections=o.get('collections',[]),
                outsideDeclaredBounds=any(lo[i]<oldmin[i]-1e-4 or hi[i]>oldmax[i]+1e-4 for i in (0,2))))
        lo=[min(o['min'][i] for o in solids) for i in range(3)];hi=[max(o['max'][i] for o in solids) for i in range(3)]
        # Conservative alternative preserves even collidable scenery; root must approve actual play envelope.
        proposed_min=[min(oldmin[0],math.floor(lo[0]-.5)),-10,min(oldmin[2],math.floor(lo[2]-.5))]
        proposed_max=[max(oldmax[0],math.ceil(hi[0]+.5)),max(oldmax[1],math.ceil(hi[1]+1.72+.5)),max(oldmax[2],math.ceil(hi[2]+.5))]
        relevant=[o for o in solids if re.search(r'Rowboat|Moored|Jetty|Dock_|Pier_\d|Lighthouse_|Flybridge|SwimPlatform|Roof',o['name'],re.I)]
        reports.append(dict(mapId=map_id,audit=str(audit_path),auditSha256=sha(audit_path),
            authoredSolids=len(solids),solidBoundsMin=lo,solidBoundsMax=hi,declaredMin=oldmin,declaredMax=oldmax,
            topSolids=sorted(solids,key=lambda o:o['max'][1],reverse=True)[:24],
            outsideDeclared=[o for o in solids if o['outsideDeclaredBounds']],preserveCandidates=relevant,
            allSolids=solids,conservativeProposalMin=proposed_min,conservativeProposalMax=proposed_max,
            actorHeight=1.72,actorRadius=.25,mosquitoRadius=.055,ceilingExtraClearance=.5,
            scope='Audit AABB measurement in Unity X/Z-from-Blender-Y and Y-from-Blender-Z; no native collider query or reachability proof. All authored solids retained conservatively, including collidable scenery. Prior local source corrections are internal and do not raise the reported upper envelope.'))
        configs.append(dict(mapId=map_id,prefab=prefab,scene=scene,spatial=spatial,expectedContentHash=content,
            expectedPlayMin=oldmin,expectedPlayMax=oldmax,innerMin=proposed_min,innerMax=proposed_max,
            thickness=.5,layer=0,protectedSolidMin=lo,protectedSolidMax=hi,clearanceAboveSolid=2.22))
    request=dict(schemaVersion=1,action='inspect',approvedExtents=False,revision='map-boundaries-v1',
        receiptPath=str(OUT/'native-inspection-PENDING.json'),catalog=guard('Assets/LetMeSleep/Presentation/Generated/HiggsfieldFiveMaps.asset'),
        maps=configs,scope='PROPOSAL ONLY. Root must choose extents after measurements; four walls plus ceiling, no floor and no fall-recovery implementation.')
    OUT.mkdir(exist_ok=True)
    (OUT/'source-envelope-measurements.json').write_text(json.dumps(dict(schemaVersion=1,maps=reports),indent=2)+'\n')
    (OUT/'bounds-proposal.json').write_text(json.dumps(request,indent=2)+'\n')
    print(json.dumps([dict(mapId=r['mapId'],solids=r['authoredSolids'],outsideDeclared=len(r['outsideDeclared']),
        highest=r['topSolids'][0]['name'],maxY=r['solidBoundsMax'][1],proposal=[r['conservativeProposalMin'],r['conservativeProposalMax']]) for r in reports]))

if __name__=='__main__':measure()
