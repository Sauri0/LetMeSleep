"""Extract exact projected GLB water boundary loops, including holes; no runtime authoring."""
import hashlib,json,struct,sys
from collections import Counter,defaultdict
from pathlib import Path
import numpy as np

BASE=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas')
OUT=Path('N:/LetMeSleep/Validation/Higgsfield/CompleteScope/MapBounds')
SOURCES=[('01-isla','HF_MAP_01_isla.glb'),('02-casa','HF_MAP_02_casa_UNITY.glb'),
    ('03-campamento','NormalsV2/HF_MAP_03_campamento_UNITY_V2.glb'),
    ('04-yate','NormalsV3/HF_MAP_04_yate_UNITY_V3.glb'),('05-pueblo','HF_MAP_05_pueblo.glb')]

def meshes(path,names,morph_bounds=None):
    raw=path.read_bytes();length,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
    doc=json.loads(raw[20:20+length]);start=20+length;length,kind=struct.unpack_from('<II',raw,start);assert kind==0x004e4942
    binary=raw[start+8:start+8+length]
    def accessor(index):
        a=doc['accessors'][index];assert 'sparse' not in a
        v=doc['bufferViews'][a['bufferView']];width={'SCALAR':1,'VEC3':3}[a['type']]
        dtype=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']])
        return np.ndarray((a['count'],width),dtype=dtype,buffer=binary,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',width*dtype.itemsize),dtype.itemsize)).copy()
    found={}
    def visit(i,parent):
        n=doc['nodes'][i]
        if 'matrix' in n:local=np.array(n['matrix']).reshape(4,4).T
        else:
            x,y,z,w=n.get('rotation',[0,0,0,1]);local=np.eye(4)
            local[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(n.get('scale',[1,1,1]))
            local[:3,3]=n.get('translation',[0,0,0])
        world=parent@local
        if 'mesh' in n and n['name'] in names:
            parts=[]
            bounds=[]
            for p in doc['meshes'][n['mesh']]['primitives']:
                assert p.get('mode',4)==4
                points=accessor(p['attributes']['POSITION'])@world[:3,:3].T+world[:3,3];points[:,2]*=-1
                ids=accessor(p['indices']).reshape(-1,3);parts.append(points[ids])
                targets=[]
                for target in p.get('targets',[]):
                    delta=accessor(target['POSITION'])@world[:3,:3].T
                    targets.append(dict(minY=float((points[:,1]+delta[:,1]).min()),maxY=float((points[:,1]+delta[:,1]).max()),maxAbsDeltaY=float(np.abs(delta[:,1]).max())))
                if targets:bounds.append(dict(targets=targets,targetNames=doc['meshes'][n['mesh']].get('extras',{}).get('targetNames',[])))
            found[n['name']]=np.concatenate(parts)
            if morph_bounds is not None:morph_bounds[n['name']]=bounds
        for child in n.get('children',[]):visit(child,world)
    for i in doc['scenes'][doc.get('scene',0)]['nodes']:visit(i,np.eye(4))
    return found

def loops(triangles):
    edges=Counter()
    for t in triangles:
        p=[tuple(np.round(v[[0,2]],5)) for v in t]
        for a,b in zip(p,p[1:]+p[:1]):
            if a!=b:edges[tuple(sorted((a,b)))]+=1
    boundary=[e for e,n in edges.items() if n==1];adj=defaultdict(list)
    for a,b in boundary:adj[a].append(b);adj[b].append(a)
    assert all(len(v)==2 for v in adj.values()),'Non-simple projected water boundary; manual topology review required'
    unused=set(boundary);result=[]
    while unused:
        a,b=min(unused);ring=[a];prev=a;current=b;unused.remove(tuple(sorted((a,b))))
        while current!=ring[0]:
            ring.append(current);following=next(v for v in adj[current] if v!=prev)
            unused.remove(tuple(sorted((current,following))));prev,current=current,following
        area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(ring,ring[1:]+ring[:1]))/2
        result.append(dict(points=[list(p) for p in ring],signedArea=area))
    return sorted(result,key=lambda r:abs(r['signedArea']),reverse=True)

def sample_under_water(solid,water,max_y):
    n=np.cross(solid[:,1]-solid[:,0],solid[:,2]-solid[:,0]);length=np.linalg.norm(n,axis=1)
    # Handedness flips from GLB to Unity. Source outward-up is negative cross Y.
    good=(length>1e-10)&(-n[:,1]>.55*length)
    points=solid[good].mean(axis=1);points=points[points[:,1]<max_y-.02]
    found=[]
    for p in points:
        a=water[:,0];b=water[:,1]-a;c=water[:,2]-a;det=b[:,0]*c[:,2]-b[:,2]*c[:,0]
        valid=np.abs(det)>1e-10
        u=np.divide((p[0]-a[:,0])*c[:,2]-(p[2]-a[:,2])*c[:,0],det,out=np.zeros(len(a)),where=valid)
        v=np.divide(b[:,0]*(p[2]-a[:,2])-b[:,2]*(p[0]-a[:,0]),det,out=np.zeros(len(a)),where=valid)
        valid&=(u>=-1e-7)&(v>=-1e-7)&(u+v<=1+1e-7)
        if valid.any():found.append(p.tolist())
        if len(found)>=3:break
    return found

def run():
    sys.path.insert(0,str(OUT/'python-libs'))
    from shapely.geometry import Polygon
    from shapely import union_all
    reports=[]
    for folder,relative in SOURCES:
        audit=json.loads((BASE/folder/'scene-audit.json').read_text());objects={o['name']:o for o in audit['objects']}
        water_names=[n for n,o in objects.items() if o['type']=='MESH' and ('water' in n.lower())]
        # Explicit source name selection is for survey only; runtime must use approved authored regions.
        selected=set(water_names)
        solid_names=[n for n,o in objects.items() if o['type']=='MESH' and o['properties'].get('collision_role')=='static_solid']
        for n in solid_names:
            o=objects[n]
            if any(o['bounds_min'][2]<objects[w]['bounds_max'][2]-.02 and all(o['bounds_min'][i]<=objects[w]['bounds_max'][i] and o['bounds_max'][i]>=objects[w]['bounds_min'][i] for i in (0,1)) for w in water_names):selected.add(n)
        path=BASE/folder/relative;morph_bounds={};geometry=meshes(path,selected,morph_bounds) if selected else {}
        waters=[]
        for name in water_names:
            t=geometry[name];o=objects[name]
            # Original meshes can contain overlapping triangles/opposite winding. Union their
            # projected surface, rather than tracing a self-intersecting raw index boundary.
            polygons=[Polygon(triangle[:,[0,2]]) for triangle in t]
            footprint=union_all([p for p in polygons if p.area>1e-9],grid_size=.00001)
            assert footprint.is_valid and not footprint.is_empty,name
            # Prefer the mesh's explicit boundary topology when valid. Independently
            # quantized collinear internal edges otherwise produce micrometre cracks.
            normals=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0])
            authored=loops(t[normals[:,1]<-1e-10])
            boundary_shape=Polygon(authored[0]['points'],[r['points'] for r in authored[1:]])
            method='Projected triangle union (raw mesh boundary self-intersects).'
            seam_area=0.0
            if boundary_shape.is_valid:
                difference=boundary_shape.symmetric_difference(footprint)
                assert difference.buffer(-.00002).is_empty,(name,difference.area)
                seam_area=float(difference.area);footprint=boundary_shape
                method='Valid authored mesh boundary; internal projection/precision seams have no inscribed radius >=20 micrometres.'
            regions=[footprint] if footprint.geom_type=='Polygon' else list(footprint.geoms)
            contours=[];grouped=[]
            for polygon in regions:
                rings=[list(polygon.exterior.coords)[:-1]]+[list(r.coords)[:-1] for r in polygon.interiors]
                grouped.append(dict(outer=[list(p) for p in rings[0]],holes=[[list(p) for p in ring] for ring in rings[1:]]))
                for ring in rings:
                    area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(ring,ring[1:]+ring[:1]))/2
                    contours.append(dict(points=[list(p) for p in ring],signedArea=area))
            witnesses=[]
            for solid in selected-set(water_names):
                if solid not in geometry:continue
                samples=sample_under_water(geometry[solid],t,o['bounds_max'][2])
                if samples:witnesses.append(dict(solid=solid,samples=samples))
            waters.append(dict(name=name,unityMin=t.min(axis=(0,1)).tolist(),unityMax=t.max(axis=(0,1)).tolist(),
                authoredProperties=o['properties'],projectedBoundaryLoops=contours,
                projectedRegions=grouped,triangleUnionArea=float(footprint.area),morphTargetBounds=morph_bounds.get(name,[]),
                topologyMethod=method,numericSeamDifferenceArea=seam_area,
                footprintDerivation='Union of all nondegenerate projected GLB triangles with 10-micrometre precision grid; overlapping triangles resolved geometrically. No convex hull, bounding-box filling or topology-validation relaxation.',
                membershipRule='Even-odd containment across all loops. Retain inner loops as holes; never use only outer AABB.',
                submergedUpwardFaceCentroidWitnesses=witnesses,
                scope='Static exported surface footprint with 10-micrometre XZ rounding. Witnesses are offline upward-face centroids under projected water, not proof of reachable or valid support. Confirm frame/rest level and wave envelope before authoring activation height.'))
        reports.append(dict(folder=folder,source=str(path),sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),waters=waters))
    (OUT/'water-footprints-v3.json').write_text(json.dumps(dict(schemaVersion=3,maps=reports),indent=2)+'\n')
    print(json.dumps([dict(folder=m['folder'],waters=[dict(name=w['name'],loops=len(w['projectedBoundaryLoops']),underWaterCandidates=len(w['submergedUpwardFaceCentroidWitnesses'])) for w in m['waters']]) for m in reports]))

if __name__=='__main__':run()
