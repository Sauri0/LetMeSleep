"""Read-only central trim identification and projected depth against actual cloth triangles."""
import hashlib
import argparse
import importlib.util
import json
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--current-label',default='fit4')
parser.add_argument('--output',default='work/garment09-continuous-seam-audit.json')
args=parser.parse_args()
spec=importlib.util.spec_from_file_location('glb_reader',ROOT/'work/hair08-inspect.py')
reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)

def unpack(glb,name,material=None):
    mesh=next(m for m in glb.doc['meshes'] if m['name'].removesuffix('_mesh')==name)
    points=[];faces=[]
    for primitive in mesh['primitives']:
        if material and glb.doc['materials'][primitive['material']]['name']!=material:continue
        start=len(points)
        points.extend(glb.accessor(primitive['attributes']['POSITION']))
        idx=np.array(glb.accessor(primitive['indices']),dtype=int).reshape(-1,3)+start
        faces.extend(idx.tolist())
    return np.array(points),np.array(faces)

def components(points,faces):
    parent=list(range(len(points)))
    def root(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    def join(a,b):parent[root(a)]=root(b)
    welded={}
    for i,p in enumerate(points):
        key=tuple(np.round(p,7))
        if key in welded:join(i,welded[key])
        else:welded[key]=i
    for a,b,c in faces:join(a,b);join(b,c)
    out={}
    for i in range(len(points)):out.setdefault(root(i),[]).append(i)
    return list(out.values())

def depth_at_xy(points,triangles):
    """Exact barycentric frontmost triangle at each trim vertex x/y; no bounds-only inference."""
    a,b,c=triangles[:,0],triangles[:,1],triangles[:,2]
    v=b-a;w=c-a
    denominator=v[:,0]*w[:,1]-v[:,1]*w[:,0]
    valid=np.abs(denominator)>1e-12
    result=[]
    for point in points:
        q=point-a
        u=np.divide(q[:,0]*w[:,1]-q[:,1]*w[:,0],denominator,out=np.zeros_like(denominator),where=valid)
        t=np.divide(v[:,0]*q[:,1]-v[:,1]*q[:,0],denominator,out=np.zeros_like(denominator),where=valid)
        inside=valid&(u>=-1e-8)&(t>=-1e-8)&(u+t<=1+1e-8)
        if inside.any():
            z=(a[:,2]+u*v[:,2]+t*w[:,2])[inside].min()
            result.append({'point':point.tolist(),'cloth_front_z':float(z),'outward_gap_m':float(z-point[2])})
    return result

report={'scope':'Bind mesh only, actual GLB triangle depth; does not claim posed trim coverage.',
        'source_recipe':'characters_pipeline.py:248-249, central secondary tube radius .005 m, y1.30 to .80.',
        'sources':[]}
for label,path in [('before',ROOT/'outputs/0.9-facial-motion-witnesses/source-before/game/assets/art/characters/human/human_lms06.glb'),
                   (args.current_label,ROOT/'game/assets/art/characters/human/human_lms06.glb')]:
    glb=reader.GLB(path);row={'label':label,'path':str(path),'sha256':hashlib.sha256(glb.raw).hexdigest(),'outfits':[]}
    for outfit in range(3):
        name=f'human_outfit_{outfit}'
        trim,faces=unpack(glb,name+'_trim','secondary')
        candidates=[]
        for component in components(trim,faces):
            p=trim[component];lo=p.min(axis=0);hi=p.max(axis=0)
            if hi[0]-lo[0]<.02 and hi[1]-lo[1]>.30 and abs(float(p[:,0].mean()))<.01:candidates.append(component)
        assert len(candidates)==1,(label,outfit,len(candidates))
        seam=trim[candidates[0]]
        cloth,cf=unpack(glb,name)
        measured=depth_at_xy(seam,cloth[cf])
        gaps=np.array([v['outward_gap_m'] for v in measured])
        mask=set(candidates[0]);samples=[]
        for face in faces:
            if not all(int(i) in mask for i in face):continue
            a,b,c=trim[face]
            normal=np.cross(b-a,c-a);length=np.linalg.norm(normal)
            if length<1e-10 or normal[2]/length>-.2:continue
            for u,v in [(0.25,0.25),(.5,.25),(.25,.5),(1/3,1/3),(.5,0),(0,.5),(.5,.5)]:
                samples.append(a*(1-u-v)+b*u+c*v)
        interior=depth_at_xy(samples,cloth[cf])
        ig=np.array([v['outward_gap_m'] for v in interior])
        row['outfits'].append({'mesh':name+'_trim','material':'secondary','seam_vertices':len(seam),
            'bbox_min':seam.min(axis=0).tolist(),'bbox_max':seam.max(axis=0).tolist(),
            'rays_hitting_cloth':len(measured),'front_of_cloth_vertices':int((gaps>1e-5).sum()),
            'behind_cloth_vertices':int((gaps< -1e-5).sum()),'gap_min_m':float(gaps.min()),'gap_max_m':float(gaps.max()),
            'deepest':measured[int(gaps.argmin())],'most_exposed':measured[int(gaps.argmax())],
            'front_triangle_samples':len(interior),'front_samples_buried':int((ig< -1e-5).sum()),
            'front_sample_gap_min_m':float(ig.min()),'front_sample_gap_max_m':float(ig.max()),
            'front_deepest':interior[int(ig.argmin())]})
    report['sources'].append(row)
(ROOT/args.output).write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report,indent=2))
