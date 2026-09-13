"""Geometry-only audit of extracted FBX path and recorded native contact."""
import collections, json, math, pathlib, sys

def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def add(a,b):return tuple(x+y for x,y in zip(a,b))
def mul(a,t):return tuple(x*t for x in a)
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def norm(v):return math.sqrt(dot(v,v))
def unit(v):return mul(v,1/norm(v))
def closest(p,a,b,c):
    ab,ac,ap=sub(b,a),sub(c,a),sub(p,a);d1,d2=dot(ab,ap),dot(ac,ap)
    if d1<=0 and d2<=0:return a
    bp=sub(p,b);d3,d4=dot(ab,bp),dot(ac,bp)
    if d3>=0 and d4<=d3:return b
    vc=d1*d4-d3*d2
    if vc<=0 and d1>=0 and d3<=0:return add(a,mul(ab,d1/(d1-d3)))
    cp=sub(p,c);d5,d6=dot(ab,cp),dot(ac,cp)
    if d6>=0 and d5<=d6:return c
    vb=d5*d2-d1*d6
    if vb<=0 and d2>=0 and d6<=0:return add(a,mul(ac,d2/(d2-d6)))
    va=d3*d6-d5*d4
    if va<=0 and d4-d3>=0 and d5-d6>=0:return add(b,mul(sub(c,b),(d4-d3)/((d4-d3)+(d5-d6))))
    denom=1/(va+vb+vc);return add(a,add(mul(ab,vb*denom),mul(ac,vc*denom)))

def main():
    source=pathlib.Path(sys.argv[1]);data=json.loads(source.read_text());raw=data['vertices']
    # Source control points are Blender Z-up; measured native bounds validate X/Z/Y.
    # Native FBX transform introduces several micrometres of rotation error; do not
    # treat sub-.01mm conclusions as precise PhysX reconstruction.
    points=[(v[0],v[2],v[1]) for v in raw];faces=[(p[0],p[2],p[1]) for p in data['polygons']]
    unique={};remap=[]
    for v in points:
        if v not in unique:unique[v]=len(unique)
        remap.append(unique[v])
    verts=list(unique);triangles=[tuple(remap[i] for i in t) for t in faces]
    normals=[];areas=[];edges=collections.defaultdict(list);duplicates=collections.Counter(tuple(sorted(t)) for t in triangles)
    for i,t in enumerate(triangles):
        a,b,c=[verts[k] for k in t];n=cross(sub(b,a),sub(c,a));areas.append(norm(n)/2);normals.append(unit(n) if norm(n)>0 else (0,0,0))
        for j in range(3):edges[tuple(sorted((t[j],t[(j+1)%3])))].append(i)
    boundary=[e for e,faces_ in edges.items() if len(faces_)==1]
    interior=[(e,fs) for e,fs in edges.items() if len(fs)==2]
    angle=lambda a,b:math.degrees(math.acos(max(-1,min(1,dot(a,b)))))
    pos=(.0000258263917,2.11988282,-24.4289646);center=add(pos,(0,.25,0))
    nearby=[]
    for i,t in enumerate(triangles):
        q=closest(center,*[verts[k] for k in t]);d=norm(sub(center,q))
        if d<.8:nearby.append({'triangle':i,'vertices':[verts[k] for k in t],'normal':normals[i],'area':areas[i],'closestPoint':q,'sphereCenterDistance':d,'signedRadiusMinusDistance':.25-d})
    nearby.sort(key=lambda x:x['sphereCenterDistance'])
    local_edges=[]
    for edge,fs in interior:
        a,b=[verts[k] for k in edge]
        if max(a[2],b[2])>-25 and min(a[2],b[2])<-24 and min(abs(a[0]),abs(b[0]))<.01:
            local_edges.append({'endpoints':[a,b],'faces':fs,'angleDegrees':angle(normals[fs[0]],normals[fs[1]])})
    # Each quad cell must tile the unique X/Z grid exactly once (two triangles).
    xs=sorted(set(v[0] for v in verts));zs=sorted(set(v[2] for v in verts))
    cells=collections.Counter()
    for t in triangles:
        vv=[verts[k] for k in t];cx=sum(v[0] for v in vv)/3;cz=sum(v[2] for v in vv)/3
        ix=next(i for i in range(len(xs)-1) if xs[i]<cx<xs[i+1]);iz=next(i for i in range(len(zs)-1) if zs[i]<cz<zs[i+1]);cells[ix,iz]+=1
    report={'source':str(source),'sourceFbxSha256':data['sourceSha256'],'method':'Read-only binary FBX; source XYZ -> Unity XZY with reversed winding, native bounds checked. No Unity/PhysX executed.',
        'vertexCount':len(points),'uniqueExactPositions':len(verts),'triangles':len(triangles),'degenerateTriangles':sum(a<1e-10 for a in areas),'duplicateTriangles':sum(n-1 for n in duplicates.values()),'nonmanifoldEdges':sum(len(v)>2 for v in edges.values()),'boundaryEdges':len(boundary),'interiorEdges':len(interior),'gridDimensions':[len(xs),len(zs)],'gridCells':len(cells),'trianglesPerGridCell':dict(collections.Counter(cells.values())),
        'minTriangleArea':min(areas),'minNormalUp':min(n[1] for n in normals),'maxSlopeDegrees':max(angle(n,(0,1,0)) for n in normals),'maxAdjacentNormalAngleDegrees':max(angle(normals[fs[0]],normals[fs[1]]) for _,fs in interior),'zeroThickness':True,
        'recordedFootPosition':pos,'lowerSphereCenter':center,'nearestTriangles':nearby[:8],'nearbyInteriorEdges':local_edges}
    band=[v for v in verts if -25.386<=v[2]<=-23.869]
    mx,my,mz=[sum(v[k] for v in band)/len(band) for k in range(3)]
    a=sum((v[0]-mx)*(v[1]-my) for v in band)/sum((v[0]-mx)**2 for v in band)
    b=sum((v[2]-mz)*(v[1]-my) for v in band)/sum((v[2]-mz)**2 for v in band)
    residual=[v[1]-(my+a*(v[0]-mx)+b*(v[2]-mz)) for v in band]
    report['convexCandidate']={'bandZ':[min(v[2] for v in band),max(v[2] for v in band)],'topVertices':len(band),'thicknessDown':.04,'supportPlane':[a,b,my-a*mx-b*mz+max(residual)],'upperBoundHullTopRaiseMeters':max(residual)-min(residual),'note':'Convex hull of unchanged band top vertices and copies 0.04m below. Hull top is between original triangulated top and this supporting plane; bound applies across every original triangle. Native cooking not tested.'}
    path=source.parent/'south-arrival-geometry-audit.json';path.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ['nearestTriangles','nearbyInteriorEdges']},indent=2));print('Nearest:',json.dumps(nearby[:2],indent=2));print('Report:',path)

if __name__=='__main__':main()
