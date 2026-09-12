"""Deterministic editable mesh recipe. Stdlib only; Unity coordinates in metres.

Run with Python, then import generated_exterior.json in Unity through
AlfaQualityExterior.Build. Blender reconstruction is a separate slotted operation.
"""
import hashlib
import json
import math
from pathlib import Path
import random

HERE = Path(__file__).resolve().parent
TAU = math.tau
PALETTE = {
    'Bark': (.22, .145, .095), 'BarkLight': (.32, .225, .14),
    'NeedleDeep': (.105, .20, .16), 'Needle': (.20, .32, .245),
    'NeedleTip': (.29, .40, .28), 'Grass': (.25, .34, .19),
    'GrassTip': (.39, .44, .26), 'Leaf': (.26, .37, .25),
    'Stone': (.37, .40, .39), 'StoneLight': (.46, .47, .42),
    'Earth': (.265, .235, .18), 'SoilEdge': (.20, .18, .145),
    'Masonry': (.49, .46, .39), 'Timber': (.34, .23, .145),
}


def add(a, b): return tuple(x + y for x, y in zip(a, b))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def mul(a, s): return tuple(x * s for x in a)
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def dot(a, b): return sum(x*y for x, y in zip(a, b))
def unit(a): return mul(a, 1/math.sqrt(dot(a, a)))
def vec(p): return dict(zip(('x', 'y', 'z'), (round(x, 6) for x in p)))


class Mesh:
    def __init__(self, name):
        self.name, self.vertices, self.groups = name, [], {}

    def face(self, points, material, outward=None, double=False):
        points = list(points)
        if outward is not None and dot(cross(sub(points[1], points[0]), sub(points[2], points[0])), outward) < 0:
            points.reverse()
        offset = len(self.vertices)
        self.vertices.extend(points)
        triangles = self.groups.setdefault(material, [])
        for i in range(1, len(points)-1):
            triangles.extend((offset, offset+i, offset+i+1))
        # Explicit backface triangles for leaves; no globally double-sided material.
        if double:
            self.face(points[::-1], material)

    def rings(self, rings, material):
        n = len(rings[0])
        for low, high in zip(rings, rings[1:]):
            center = mul(tuple(sum(v[a] for v in low+high) for a in range(3)), 1/(2*n))
            for i in range(n):
                j = (i+1) % n
                points = (low[i], low[j], high[j], high[i])
                outward = sub(mul(add(low[i], low[j]), .5), center)
                # Radial direction makes trunk, stone and crown facets consistently outward.
                self.face(points, material, (outward[0], 0, outward[2]))
        self.face(rings[0], material, (0, -1, 0))
        self.face(rings[-1], material, (0, 1, 0))

    def data(self):
        return dict(name=self.name, vertices=[vec(v) for v in self.vertices],
                    submeshes=[dict(material=m, triangles=t) for m, t in self.groups.items()])


def ring(center, rx, rz, n=9, phase=0, irregular=None):
    return [add(center, (math.cos(i*TAU/n+phase)*rx*(irregular[i] if irregular else 1), 0,
                         math.sin(i*TAU/n+phase)*rz*(irregular[i] if irregular else 1))) for i in range(n)]


def rock(mesh, center, scale, seed, material='Stone'):
    rng = random.Random(seed)
    n = 7 + seed % 3
    irregular = [rng.uniform(.77, 1.15) for _ in range(n)]
    rx, height, rz = scale
    rings = []
    for y, radius, shift in ((0, .72, 0), (.22, 1, -.07), (.76, .79, .12), (1, .39, .17)):
        rings.append(ring(add(center, (shift*rx, y*height, shift*rz)), rx*radius, rz*radius,
                          n, .17, irregular))
    mesh.rings(rings, material)


def stem(mesh, start, end, radius, material='Bark', sides=7):
    axis = unit(sub(end, start))
    tangent = unit(cross(axis, (0, 0, 1) if abs(axis[2]) < .9 else (1, 0, 0)))
    bitangent = cross(axis, tangent)
    rings = [[add(c, add(mul(tangent, math.cos(i*TAU/sides)*r), mul(bitangent, math.sin(i*TAU/sides)*r)))
              for i in range(sides)] for c, r in ((start, radius), (end, radius*.35))]
    for i in range(sides):
        j = (i+1) % sides
        mesh.face((rings[0][i], rings[0][j], rings[1][j], rings[1][i]), material,
                  sub(rings[0][i], start))
    mesh.face(rings[0], material, mul(axis, -1))
    mesh.face(rings[1], material, axis)


def pine(variant):
    mesh = Mesh('Pine_%02d' % variant)
    rng = random.Random(410 + variant)
    # Fits original .22 m trunk support and ~3.05 m crown height.
    stem(mesh, (0, 0, 0), (.018, 2.91, -.012), .105)
    for tier, (y, radius) in enumerate(((.92, .84), (1.45, .72), (1.97, .55), (2.45, .35))):
        count = 5 if tier < 3 else 4
        for branch in range(count):
            angle = branch*TAU/count + tier*.9 + variant*.43
            extent = radius*rng.uniform(.73, 1.12)
            tip = (math.cos(angle)*extent, y+.13, math.sin(angle)*extent)
            stem(mesh, (0, y-.09, 0), tip, .027 if tier < 2 else .018, 'BarkLight', 5)
            center = mul(tip, .61)
            center = (center[0], y+.09, center[2])
            n = 7
            irregular = [rng.uniform(.80, 1.12) for _ in range(n)]
            width = radius*.55
            # Branch masses overlap loosely; not stacked full-cone discs.
            mesh.rings([ring(add(center, (0, -.14, 0)), width*.66, width*.68, n, angle, irregular),
                        ring(center, width, width*.81, n, angle, irregular),
                        ring(add(center, (-.05, .24, .03)), width*.61, width*.48, n, angle, irregular),
                        ring(add(center, (-.02, .48, 0)), .016, .016, n, angle)],
                       ('NeedleDeep', 'Needle', 'NeedleTip')[(branch+tier+variant) % 3])
    return mesh


def grass(variant):
    mesh, rng = Mesh('Grass_%02d' % variant), random.Random(90+variant)
    for i in range(11+variant*2):
        angle = rng.uniform(0, TAU)
        base = (rng.uniform(-.11, .11), 0, rng.uniform(-.1, .1))
        height, bend, width = rng.uniform(.14, .33), rng.uniform(.05, .15), rng.uniform(.013, .028)
        side = (math.cos(angle)*width, 0, math.sin(angle)*width)
        direction = (-math.sin(angle), 0, math.cos(angle))
        mid = add(base, add(mul(direction, bend*.32), (0, height*.64, 0)))
        tip = add(base, add(mul(direction, bend), (0, height, 0)))
        ridge = add(mid, mul(direction, .009))
        mat = 'GrassTip' if i % 4 == 0 else 'Grass'
        mesh.face((sub(base, side), add(base, side), ridge), mat, double=True)
        mesh.face((add(base, side), add(mid, mul(side, .60)), ridge), mat, double=True)
        mesh.face((sub(base, side), ridge, sub(mid, mul(side, .60))), mat, double=True)
        mesh.face((sub(mid, mul(side, .60)), ridge, tip), mat, double=True)
        mesh.face((ridge, add(mid, mul(side, .60)), tip), mat, double=True)
    return mesh


def herb(variant):
    mesh, rng = Mesh('Herb_%02d' % variant), random.Random(270+variant)
    for i in range(9):
        angle = i*2.399 + variant
        base = (rng.uniform(-.13, .13), 0, rng.uniform(-.13, .13))
        height = rng.uniform(.19, .43)
        top = add(base, (math.cos(angle)*.13, height, math.sin(angle)*.13))
        stem(mesh, base, top, .009, 'Leaf', 5)
        for level in (.5, .8, 1):
            origin = add(base, mul(sub(top, base), level))
            direction = (math.cos(angle+level*4), .27, math.sin(angle+level*4))
            tip = add(origin, mul(direction, .18))
            mid = add(origin, mul(direction, .09))
            side = (-direction[2]*.047, -.011, direction[0]*.047)
            ridge = add(mid, (0, .019, 0))
            mesh.face((origin, sub(mid, side), tip, ridge), 'Leaf', double=True)
            mesh.face((origin, ridge, tip, add(mid, side)), 'NeedleTip', double=True)
    return mesh


def soil(name, rx, rz, seed):
    mesh = Mesh(name)
    rng = random.Random(seed)
    irr = [rng.uniform(.88, 1.08) for _ in range(17)]
    # Tops 6–18 mm above existing collider floor; soft irregular visual edge.
    mesh.rings([ring((0, .003, 0), rx, rz, 17, irregular=irr),
                ring((0, .012, 0), rx*.93, rz*.94, 17, irregular=irr),
                ring((0, .018, 0), rx*.66, rz*.64, 17, irregular=irr)], 'Earth')
    return mesh


def chamfer_box(name, sx, sy, sz, bevel=.014, material='Timber'):
    mesh = Mesh(name)
    # Octagonal section plus inset upper/lower ring: bevels on all twelve edges.
    contour = [(-sx/2+bevel,-sz/2),(sx/2-bevel,-sz/2),(sx/2,-sz/2+bevel),
               (sx/2,sz/2-bevel),(sx/2-bevel,sz/2),(-sx/2+bevel,sz/2),
               (-sx/2,sz/2-bevel),(-sx/2,-sz/2+bevel)]
    rings = [[(x*scale, y, z*scale) for x,z in contour]
             for y,scale in ((0,.93),(bevel,1),(sy-bevel,1),(sy,.93))]
    mesh.rings(rings, material)
    return mesh


def build():
    meshes = [pine(i) for i in range(3)] + [grass(i) for i in range(4)] + [herb(i) for i in range(2)]
    for i in range(4):
        mesh = Mesh('Rock_%02d' % i)
        rock(mesh, (0, 0, 0), (.25+i*.045, .14+i*.032, .20+i*.025), 34+i,
             'StoneLight' if i % 2 else 'Stone')
        meshes.append(mesh)
    meshes.extend((soil('Soil_Wide', 1.30, .48, 71), soil('Soil_Round', .72, .64, 82),
                   chamfer_box('FoundationStone', .44, .19, .045, .012, 'Masonry'),
                   chamfer_box('WindowSill', 1.36, .085, .20, .015),
                   chamfer_box('WindowApron', 1.20, .12, .04, .008),
                   chamfer_box('FenceCap', .15, .07, .15, .025)))
    instances = []
    def place(name, mesh, pos, yaw=0, scale=(1,1,1), zone='Garden'):
        instances.append(dict(name=name, mesh=mesh, position=vec(pos), yaw=yaw, scale=vec(scale), zone=zone))

    # Composition islands. Front entry x4.8..7.3 and patio centre x4.8..7.3 stay empty.
    islands = [('LivingBed',1.85,-.88,'Soil_Wide'), ('DiningBed',10.5,-.80,'Soil_Wide'),
               ('PatioWestNear',.92,12.9,'Soil_Round'), ('PatioWestBack',1.32,18.48,'Soil_Round'),
               ('PatioEastBack',11.63,18.12,'Soil_Round'), ('PatioEastNear',11.86,13.25,'Soil_Round')]
    for k, (label,x,z,patch) in enumerate(islands):
        place(label+'_Earth', patch, (x,0,z))
        rng = random.Random(720+k)
        rx = 1.10 if patch == 'Soil_Wide' else .52
        for i in range(9):
            angle = i*2.399
            radius = rng.uniform(.40, .9)
            dx, dz = math.cos(angle)*rx*radius, math.sin(angle)*.36*radius
            kind = 'Herb_%02d' % (i%2) if i in (1,4,7) else 'Grass_%02d' % (i%4)
            place(label+'_Plant_%02d'%i, kind, (x+dx,.018,z+dz), rng.uniform(0,360),
                  (1,rng.uniform(.73,1.06),1))
        for i in range(3):
            # Low edge stones stay within planted island, never a path obstacle.
            place(label+'_Stone_%d'%i, 'Rock_%02d'%((i+k)%4),
                  (x+(-.7+i*.65)*rx,.005,z+.20+(i%2)*.05), 31+i*63,
                  (.66,.62,.66))

    # Trees inherit original trunk collision and world position. New foliage is visual only.
    place('Patio_Pine_W_Replacement','Pine_00',(2,0,16.5),285,zone='Replacement')
    place('Patio_Pine_E_Replacement','Pine_01',(10.8,0,18.3),137,zone='Replacement')

    # Exterior stone course, interrupted well clear of front/rear doors.
    for label,z in (('Front',-.020),('Back',11.420)):
        for row in range(2):
            for index in range(28):
                x=.23+index*.455+(row%2)*.21
                if 4.85 < x < 7.22 or x > 12.55: continue
                place('Foundation_%s_%d_%02d'%(label,row,index),'FoundationStone',(x,.016+row*.198,z))
    for floor in (0,3):
        for label,x in (('Living',1.38),('Dining',10.6)):
            place('Sill_%s_%d'%(label,floor),'WindowSill',(x,floor+1.105,-.075),zone='Facade')
            place('Apron_%s_%d'%(label,floor),'WindowApron',(x,floor+.984,-.018),zone='Facade')
    for x in (0,12.8):
        for z in (12,14,16,18,19.3):
            place('FenceCap_%s_%s'%(x,z),'FenceCap',(x,1.4,z),zone='Facade')
    for x in (2,4,6,8,10,12):
        place('FenceBackCap_%d'%x,'FenceCap',(x,1.4,19.3),zone='Facade')

    # Director-approved nonplayable depth, all actual meshes, beyond z=-2.2 boundary.
    # Camera ray through Living window heads left as it travels toward -Z.
    for i,(x,z,s) in enumerate(((-4.8,-5.5,1.72),(2.7,-6.8,1.51),(-8.1,-11,2.32),
                               (-.8,-12.6,2.07),(5.8,-13.4,2.43),(-12.4,-18.8,2.61),
                               (-4.9,-20,2.75),(3.2,-21,2.63),(11.7,-19,2.45))):
        place('Depth_Pine_%02d'%i,'Pine_%02d'%(i%3),(x,-.10,z),i*47,(s,s,s),zone='BeyondBoundary')
    ridge = Mesh('DistantGround')
    # Irregular low relief, away from house. Closed volume, no vertical backdrop plane.
    outline = [(-17,-.14,-2.23),(17,-.14,-2.23),(20,.17,-12),(18,.72,-26),
               (7,1.28,-29),(-5,.98,-28),(-18,.42,-24),(-20,.12,-10)]
    center = (0,.10,-13)
    for i in range(len(outline)):
        a,b=outline[i],outline[(i+1)%len(outline)]
        ridge.face((center,a,b),'Grass',(0,1,0))
        ridge.face((a,(a[0],-.85,a[2]),(b[0],-.85,b[2]),b),'SoilEdge',
                   (a[0]+b[0],0,a[2]+b[2]+26))
    ridge.face([(p[0],-.85,p[2]) for p in outline],'SoilEdge',(0,-1,0))
    meshes.append(ridge)
    place('Depth_Ground','DistantGround',(0,0,0),zone='BeyondBoundary')
    for i,(x,z) in enumerate(((-2.7,-4.2),(-6.3,-8.4),(1.2,-8.7),(4.5,-15.1),(-9.8,-16))):
        place('Depth_Rock_%02d'%i,'Rock_%02d'%(i%4),(x,-.08,z),i*41,(2.3,2.6,2.1),zone='BeyondBoundary')

    # Ground contact follows the actual piecewise-planar relief at every remote root.
    for instance in instances:
        if instance['zone']!='BeyondBoundary' or instance['mesh']=='DistantGround': continue
        x,z=instance['position']['x'],instance['position']['z']
        heights=[]
        for i in range(len(outline)):
            a,b,c=center,outline[i],outline[(i+1)%len(outline)]
            denominator=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
            u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/denominator
            v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/denominator
            if u>=-1e-6 and v>=-1e-6 and u+v<=1.000001:
                heights.append(u*a[1]+v*b[1]+(1-u-v)*c[1])
        assert heights, ('Remote instance outside its terrain',instance['name'])
        instance['position']['y']=round(max(heights)-.025,6)

    result=dict(schemaVersion=1, mapId='house-patio-v1', units='metres', axes='Unity +Y up +Z forward',
                materials=[dict(name=n,color=dict(r=c[0],g=c[1],b=c[2],a=1)) for n,c in PALETTE.items()],
                meshes=[m.data() for m in meshes], instances=instances,
                replaceRendererRoots=['Patio_Pine_W','Patio_Pine_E'])
    path=HERE/'generated_exterior.json'
    report=validate(result)
    path.write_text(json.dumps(result,separators=(',',':'))+'\n',encoding='utf-8')
    report['sourceSha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    report['geometrySha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    (HERE/'geometry_validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))


def validate(data):
    names=[m['name'] for m in data['meshes']]
    assert len(set(names))==len(names)
    assert len(set(i['name'] for i in data['instances']))==len(data['instances'])
    triangles=0
    for mesh in data['meshes']:
        vertices=[tuple(v[k] for k in ('x','y','z')) for v in mesh['vertices']]
        assert all(math.isfinite(c) for v in vertices for c in v)
        for submesh in mesh['submeshes']:
            indices=submesh['triangles']
            assert len(indices)%3==0
            triangles+=len(indices)//3
            for i in range(0,len(indices),3):
                a,b,c=(vertices[j] for j in indices[i:i+3])
                normal=cross(sub(b,a),sub(c,a))
                assert dot(normal,normal)>1e-16, (mesh['name'],i,'degenerate')
    assert all(i['mesh'] in names for i in data['instances'])
    # Geometry clearance, beyond the unchanged physics contract. Both authored patio
    # mosquito spawns are sphere centres, not feet. Check the actual new triangles.
    mesh_lookup={m['name']:m for m in data['meshes']}
    clearances=[]
    for spawn in ((2,1.8,17),(10,1.8,17)):
        nearest=999
        for instance in data['instances']:
            p,s=instance['position'],instance['scale']
            if abs(p['x']-spawn[0])>3 or abs(p['z']-spawn[2])>3: continue
            a=math.radians(instance['yaw']);co,si=math.cos(a),math.sin(a)
            points=[]
            for v in mesh_lookup[instance['mesh']]['vertices']:
                x,y,z=(v[k]*s[k] for k in ('x','y','z'))
                points.append((p['x']+x*co+z*si,p['y']+y,p['z']-x*si+z*co))
            for group in mesh_lookup[instance['mesh']]['submeshes']:
                ids=group['triangles']
                for i in range(0,len(ids),3):
                    nearest=min(nearest,point_triangle_distance(spawn,*(points[j] for j in ids[i:i+3])))
        clearances.append(round(nearest,6))
        assert nearest>.075, ('patio mosquito spawn intersects exterior art',spawn,nearest)
    return dict(status='PASS_OFFLINE_GEOMETRY_ONLY',uniqueMeshes=len(names),
                instances=len(data['instances']),uniqueMeshTriangles=triangles,
                patioMosquitoSpawnMinimumDistance=clearances,
                checks=['finite coordinates','unique identifiers','nondegenerate indexed triangles','instance mesh references'],
                pending=['Unity compile/import','native living and patio views','collider/ID invariance in Unity',
                         'human/mosquito traversal','Blender editable source reconstruction','visual approval'])


def point_triangle_distance(p,a,b,c):
    ab,ac,ap=sub(b,a),sub(c,a),sub(p,a)
    normal=unit(cross(ab,ac))
    projection=sub(p,mul(normal,dot(ap,normal)))
    q=sub(projection,a)
    aa,bb,cc=dot(ab,ab),dot(ac,ac),dot(ab,ac)
    denominator=aa*bb-cc*cc
    u=(dot(q,ab)*bb-dot(q,ac)*cc)/denominator
    v=(dot(q,ac)*aa-dot(q,ab)*cc)/denominator
    if u>=0 and v>=0 and u+v<=1: return abs(dot(ap,normal))
    closest=999
    for start,end in ((a,b),(b,c),(c,a)):
        edge=sub(end,start)
        t=max(0,min(1,dot(sub(p,start),edge)/dot(edge,edge)))
        delta=sub(p,add(start,mul(edge,t)))
        closest=min(closest,math.sqrt(dot(delta,delta)))
    return closest


if __name__=='__main__': build()
