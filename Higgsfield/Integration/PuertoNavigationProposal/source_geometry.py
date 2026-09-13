"""Read-only Puerto GLB triangles in Unity XZY, cross-checked with the Blender audit."""
import hashlib,json,struct
from pathlib import Path
import numpy as np

SOURCE=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/05-pueblo')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

class Geometry:
    def __init__(self):
        self.audit=json.loads((SOURCE/'scene-audit.json').read_text())
        self.objects={o['name']:o for o in self.audit['objects']}
        self.recipe=json.loads((SOURCE/'UnityRecipeGpu/hf-puerto-del-faro-v1.recipe.json').read_text())
        assert self.recipe['sourceSha256']==sha(SOURCE/'HF_MAP_05_pueblo_UNITY.fbx')=='aa4bb192353bfbebe5f3d48e4dd6950245b854f397b95a7fb7d7ac85a2a834a4'
        data=(SOURCE/'HF_MAP_05_pueblo.glb').read_bytes();assert data[:4]==b'glTF'
        n,t=struct.unpack_from('<II',data,12);assert t==0x4e4f534a
        doc=json.loads(data[20:20+n]);pos=20+n;nb,t=struct.unpack_from('<II',data,pos);assert t==0x004e4942
        binary=data[pos+8:pos+8+nb]
        def accessor(i):
            a=doc['accessors'][i];assert 'sparse' not in a
            b=doc['bufferViews'][a['bufferView']];assert b.get('buffer',0)==0
            dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']])
            width={'VEC3':3,'SCALAR':1}[a['type']];offset=b.get('byteOffset',0)+a.get('byteOffset',0)
            return np.ndarray((a['count'],width),dtype=dt,buffer=binary,offset=offset,strides=(b.get('byteStride',dt.itemsize*width),dt.itemsize)).copy()
        self.meshes={};self.polygons={};deviations=[]
        def visit(index,parent):
            o=doc['nodes'][index]
            if 'matrix' in o:local=np.array(o['matrix']).reshape(4,4).T
            else:
                x,y,z,w=o.get('rotation',[0,0,0,1]);local=np.eye(4)
                local[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(o.get('scale',[1,1,1]))
                local[:3,3]=o.get('translation',[0,0,0])
            world=parent@local
            if 'mesh' in o:
                name=o['name'];parts=[]
                for p in doc['meshes'][o['mesh']]['primitives']:
                    assert p.get('mode',4)==4
                    v=accessor(p['attributes']['POSITION']);v=v@world[:3,:3].T+world[:3,3];v[:,2]*=-1
                    ids=accessor(p['indices']).reshape(-1,3);parts.append(v[ids])
                tris=np.concatenate(parts);self.meshes[name]=tris
                a=self.objects[name]
                # Audit transforms local bounding-box corners; rotated rocks have a
                # larger enclosing AABB than their actual transformed vertices.
                delta=max(0,np.max(np.array(a['bounds_min'])[[0,2,1]]-tris.min(axis=(0,1))),np.max(tris.max(axis=(0,1))-np.array(a['bounds_max'])[[0,2,1]]))
                # Animated ocean can be exported at evaluated phase; it is non-solid.
                if a['properties'].get('collision_role')=='static_solid':deviations.append((name,float(delta)))
            for child in o.get('children',[]):visit(child,world)
        for i in doc['scenes'][doc.get('scene',0)]['nodes']:visit(i,np.eye(4))
        assert len(self.meshes)==730 and len(deviations)==458
        assert max(d for n,d in deviations)<.0001,max(deviations,key=lambda x:x[1])
        self.max_bounds_error=max(d for n,d in deviations)
        self.solid={n:t for n,t in self.meshes.items() if self.objects[n]['properties'].get('collision_role')=='static_solid'}
        self.names=list(self.solid);self.tris=np.concatenate(list(self.solid.values()))
        self.ids=np.concatenate([np.full(len(t),i) for i,t in enumerate(self.solid.values())])
        self.lo=self.tris.min(axis=1);self.hi=self.tris.max(axis=1)

    def floor(self,x,z,ceiling=20,names=None,eligible=False):
        mask=(self.lo[:,0]<=x+1e-7)&(self.hi[:,0]>=x-1e-7)&(self.lo[:,2]<=z+1e-7)&(self.hi[:,2]>=z-1e-7)&(self.lo[:,1]<ceiling)
        ix=np.where(mask)[0];t=self.tris[ix];a=t[:,0];b=t[:,1]-a;c=t[:,2]-a
        det=b[:,0]*c[:,2]-b[:,2]*c[:,0];good=np.abs(det)>1e-10
        u=np.divide((x-a[:,0])*c[:,2]-(z-a[:,2])*c[:,0],det,out=np.zeros(len(t)),where=good)
        v=np.divide(b[:,0]*(z-a[:,2])-b[:,2]*(x-a[:,0]),det,out=np.zeros(len(t)),where=good)
        y=a[:,1]+u*b[:,1]+v*c[:,1]
        normal=np.cross(b,c);up=np.divide(-normal[:,1],np.linalg.norm(normal,axis=1),out=np.zeros(len(t)),where=np.linalg.norm(normal,axis=1)>1e-10)
        good&=(u>=-1e-7)&(v>=-1e-7)&(u+v<=1+1e-7)&(y<ceiling)&(up>(.55 if eligible else 0))
        rows=[(float(y[j]),self.names[self.ids[ix[j]]]) for j in np.where(good)[0] if (names is None or self.names[self.ids[ix[j]]] in names) and (not eligible or is_support(self.names[self.ids[ix[j]]]))]
        return max(rows) if rows else None

    def segment(self,start,end,radius=.055):
        """Conservative sampled sphere sweep over triangle surfaces; not engine physics."""
        start=np.array(start);end=np.array(end);low=np.minimum(start,end)-radius;high=np.maximum(start,end)+radius
        ix=np.where(np.all(self.lo<=high,axis=1)&np.all(self.hi>=low,axis=1))[0]
        if not len(ix):return None
        tris=self.tris[ix];best=(float('inf'),None)
        # A 1cm sample spacing has <=5mm center error; report margin accordingly.
        for p in np.linspace(start,end,max(2,int(np.linalg.norm(end-start)/.01)+2)):
            distances=point_triangles(p,tris);j=int(np.argmin(distances))
            if distances[j]<best[0]:best=(float(distances[j]),self.names[self.ids[ix[j]]])
        return dict(minDistance=best[0],nearest=best[1],sampleSpacingMax=.01,clear=best[0]>=radius+.005)

def is_support(name):
    return name in ['Terrain_Playable_110x85','Plaza_ContinuousPaving','Workshop_QuaysidePorch_Support'] or any(s in name for s in ['Floor','Foundation','Stair','LandConnector','Deck','Threshold'])

def point_triangles(p,t):
    a=t[:,0];b=t[:,1];c=t[:,2];ab=b-a;ac=c-a;ap=p-a;n=np.cross(ab,ac);nn=np.sum(n*n,axis=1)
    dist=np.divide(np.sum(ap*n,axis=1)**2,nn,out=np.full(len(t),np.inf),where=nn>1e-16)
    d00=np.sum(ab*ab,axis=1);d01=np.sum(ab*ac,axis=1);d11=np.sum(ac*ac,axis=1);d20=np.sum(ap*ab,axis=1);d21=np.sum(ap*ac,axis=1);den=d00*d11-d01*d01
    v=np.divide(d11*d20-d01*d21,den,out=np.zeros(len(t)),where=np.abs(den)>1e-16);w=np.divide(d00*d21-d01*d20,den,out=np.zeros(len(t)),where=np.abs(den)>1e-16)
    dist[(v<0)|(w<0)|(v+w>1)|(np.abs(den)<1e-16)]=np.inf
    for u,vv in [(a,b),(b,c),(c,a)]:
        edge=vv-u;dd=np.sum(edge*edge,axis=1);f=np.clip(np.divide(np.sum((p-u)*edge,axis=1),dd,out=np.zeros(len(t)),where=dd>1e-16),0,1)
        dist=np.minimum(dist,np.sum((p-u-f[:,None]*edge)**2,axis=1))
    return np.sqrt(np.maximum(0,dist))

if __name__=='__main__':
    g=Geometry();print(json.dumps(dict(meshes=len(g.meshes),solids=len(g.solid),maxBoundsError=g.max_bounds_error,plaza=g.floor(-3,0,5),cottage=g.floor(-14,-3,5),pier=g.floor(-14,-25,3))))
