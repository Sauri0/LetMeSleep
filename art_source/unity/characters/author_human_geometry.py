"""v0.3.0 human base drawn from the user sketches (PER-01, PER-04 BASE CHARACTER, UI-06).

Round 3 (art-director corrections): the head is re-authored as large low-poly
planes, 14 radial columns x ~8 visible rings with triangulated cheek/jaw
diagonals (8-10 facets across the face, not thin strips), a cheekbone ridge
just under the eyes and clean V planes down to a 136 mm flat chin. The eyes
sit 11 mm deeper in a shallow socket; the pupil is a lens on the globe
(~33 mm), brows are thin arched bars (inner end 4 mm higher: worried, not
angry) lying on the forehead, the nose is a small 40 mm wedge and the mouth a
48 mm slit whose corners drop 3 mm. The nightcap has a 30 mm rolled band that
stands 10 mm off the head and a thick soft tail that falls back behind the
left ear to a bright pompom. Slippers are open-back mules (upper over the
front 60% of the foot, 16 mm sole) with bare heels and ankles.
Geometry and skin weights only; stable rig/socket coordinates are supplied by
the caller (eyes stay centred on Socket.Eye z=1.53 for Unity).
"""
import math
import bmesh
from mathutils import Vector
from author_human_facial import EYE_CENTERS, EYE_RADIUS, EYE_HEIGHT_FACTOR


def clamp(value):
    return max(0.0, min(1.0, value))


def ring_solid(mesh, name, loops, material, bone=None):
    """Close consecutive equal-length loops into a torus-like solid (hems, bands)."""
    count=len(loops[0]);verts=[p for loop in loops for p in loop];n=len(loops)
    faces=[(r*count+i,r*count+(i+1)%count,((r+1)%n)*count+(i+1)%count,((r+1)%n)*count+i)
           for r in range(n) for i in range(count)]
    return mesh(name,verts,faces,material,bone)


def weighted(obj, rows):
    """Explicit per-vertex {bone: weight} rows (normalised, <=4 influences)."""
    groups={}
    for vertex,row in zip(obj.data.vertices,rows):
        total=sum(row.values())
        for name,value in row.items():
            if value<=0:continue
            if name not in groups:groups[name]=obj.vertex_groups.new(name=name)
            groups[name].add([vertex.index],value/total,'REPLACE')
    return obj


# ---------------------------------------------------------------- head shape
# 14 radial columns: c0 front centre, c1..c6 around +X, c7 back centre and
# c8..c13 mirrored on -X. Each ring lists the +X half (x, y); -Y is the front.
COLS=14
HEAD_RINGS=[
    ('chin', 1.300,[(0,-.101),(.030,-.100),(.056,-.093),(.068,-.074),(.067,-.050),(.054,-.036),(.030,-.029),(0,-.027)]),
    ('jaw',  1.335,[(0,-.117),(.030,-.1165),(.064,-.111),(.088,-.092),(.096,-.049),(.086,-.002),(.050,.016),(0,.019)]),
    ('mouth',1.380,[(0,-.1275),(.024,-.1268),(.074,-.1235),(.116,-.103),(.130,-.044),(.118,.032),(.072,.064),(0,.070)]),
    ('lip',  1.418,[(0,-.1330),(.030,-.1325),(.082,-.1310),(.136,-.112),(.158,-.041),(.146,.052),(.088,.100),(0,.106)]),
    # Cheekbone ridge just under the eyes: ~+8 mm outward (V start) and a few
    # mm forward; the planes below stay near-frontal so the face stays lit.
    ('cheek',1.455,[(0,-.135),(.032,-.1355),(.086,-.1385),(.148,-.127),(.188,-.036),(.172,.066),(.102,.122),(0,.132)]),
    # Eye line: c1/c2 recessed into a shallow socket behind the globes.
    ('eye',  1.530,[(0,-.137),(.030,-.130),(.090,-.126),(.150,-.123),(.188,-.032),(.177,.078),(.106,.140),(0,.150)]),
    ('brow', 1.600,[(0,-.135),(.036,-.1345),(.090,-.1325),(.146,-.117),(.181,-.030),(.170,.082),(.102,.144),(0,.153)]),
    ('band', 1.640,[(0,-.125),(.040,-.1245),(.094,-.119),(.140,-.101),(.168,-.026),(.160,.076),(.096,.132),(0,.143)]),
    ('crown',1.685,[(0,-.097),(.040,-.096),(.084,-.090),(.114,-.072),(.134,-.017),(.128,.058),(.078,.100),(0,.110)]),
    ('top',  1.712,[(0,-.060),(.030,-.059),(.055,-.054),(.074,-.042),(.084,-.006),(.080,.040),(.050,.066),(0,.073)]),
]
HEAD_APEX=(0,.004,1.724)
# Mouth slit: 48 mm wide, 4 mm open at the centre and 3 mm at the corners,
# which drop 3 mm (worried mouth of PER-01/PER-04).
LIPS={'lower':[(0,-.1273,1.3785),(.024,-.1266,1.3758)],
      'upper':[(0,-.1277,1.3825),(.024,-.1269,1.3788)]}
JAW_WEIGHT={'chin':1.0,'jaw':1.0,'mouth':.30,'lower':.70,'upper':0.0}


def mirror(half):
    return list(half)+[(-p[0],)+tuple(p[1:]) for p in reversed(half[1:7])]


STRUCTURE=[(z,mirror(half)) for _,z,half in HEAD_RINGS]


def ring_at(z):
    """Head outline (14 x,y points) at height z, interpolated between rings."""
    if z<=STRUCTURE[0][0]:return STRUCTURE[0][1]
    for (za,a),(zb,b) in zip(STRUCTURE,STRUCTURE[1:]):
        if za<=z<=zb:
            t=(z-za)/(zb-za)
            return [(p[0]+(q[0]-p[0])*t,p[1]+(q[1]-p[1])*t) for p,q in zip(a,b)]
    return STRUCTURE[-1][1]


def offset_ring(points, distance):
    """Push a closed x,y outline outward along its averaged edge normals."""
    out=[]
    for i,p in enumerate(points):
        a=points[i-1];b=points[(i+1)%len(points)]
        tx,ty=b[0]-a[0],b[1]-a[1];length=math.hypot(tx,ty)
        out.append((p[0]+distance*ty/length,p[1]-distance*tx/length))
    return out


def ring_point(z, column, distance=0.0):
    """Point on the (offset) head outline at a fractional column index."""
    points=offset_ring(ring_at(z),distance)
    i=int(math.floor(column))%COLS;j=(i+1)%COLS;f=column-math.floor(column)
    return (points[i][0]+(points[j][0]-points[i][0])*f,points[i][1]+(points[j][1]-points[i][1])*f,z)


def ray_triangle(origin, direction, a, b, c):
    """Double-sided Moller-Trumbore; distance along the ray or None."""
    e1=b-a;e2=c-a;h=direction.cross(e2);det=e1.dot(h)
    if abs(det)<1e-12:return None
    f=1/det;s=origin-a;u=f*s.dot(h)
    if u<-1e-9 or u>1+1e-9:return None
    q=s.cross(e1);v=f*direction.dot(q)
    if v<-1e-9 or u+v>1+1e-9:return None
    t=f*e2.dot(q)
    return t if t>0 else None


def radial_surface(tris, z, column, distance=0.0):
    """Point `distance` outside the actual (triangulated) skull facets on the
    horizontal ray from the head axis through an outline column at height z.
    Hair, band and crown hug the real planes instead of an interpolated ring."""
    ring=ring_at(z);yc=sum(p[1] for p in ring)/len(ring);target=ring_point(z,column)
    origin=Vector((0,yc,z));direction=Vector((target[0],target[1]-yc,0)).normalized()
    far=None
    for a,b,c in tris:
        if max(a.z,b.z,c.z)<z-1e-9 or min(a.z,b.z,c.z)>z+1e-9:continue
        t=ray_triangle(origin,direction,a,b,c)
        if t is not None and (far is None or t>far):far=t
    if far is None:far=(Vector(target)-origin).length
    p=origin+direction*(far+distance)
    return (p.x,p.y,z)


def jaw_factor(y):
    # Back of the skull and the neck side of the jaw stay on Head.
    return clamp((.03-y)/.09)


def triangles(verts, faces):
    return [(verts[f[0]],verts[f[k]],verts[f[k+1]]) for f in faces for k in range(1,len(f)-1)]


def front_y(tris, x, z):
    """Front-most (most -Y) surface point of the authored skull at (x, z)."""
    best=None
    for a,b,c in tris:
        d=(b[0]-a[0])*(c[2]-a[2])-(c[0]-a[0])*(b[2]-a[2])
        if abs(d)<1e-12:continue
        u=((x-a[0])*(c[2]-a[2])-(c[0]-a[0])*(z-a[2]))/d
        v=((b[0]-a[0])*(z-a[2])-(x-a[0])*(b[2]-a[2]))/d
        if u<-1e-9 or v<-1e-9 or u+v>1+1e-9:continue
        y=a[1]+u*(b[1]-a[1])+v*(c[1]-a[1])
        if best is None or y<best:best=y
    return best


# Cheek/jaw/temple quads are split into triangles (V planes, PER-04 facets).
# (lower ring name, first +X column of the quad) -> triangulate.
TRIANGULATED={(ring,column) for ring,columns in [
    ('chin',range(1,6)),('jaw',range(2,6)),('upper',range(2,6)),('lip',range(2,6)),
    ('cheek',range(2,6)),('eye',range(3,6)),('brow',range(3,6))] for column in columns}


def skull(mesh, skin, dark):
    verts=[];jaw=[]

    def add(point, amount):
        verts.append(tuple(point));jaw.append(amount);return len(verts)-1

    rings=[];names=[]
    for key,z,half in HEAD_RINGS:
        if key=='mouth':
            shared=[None,None]+[add((x,y,z),JAW_WEIGHT['mouth']*jaw_factor(y)) for x,y in half[2:]]
            shared+=[add((-x,y,z),JAW_WEIGHT['mouth']*jaw_factor(y)) for x,y in reversed(half[2:7])]
            for lip in ['lower','upper']:
                (x0,y0,z0),(x1,y1,z1)=LIPS[lip];amount=JAW_WEIGHT[lip]
                ring=list(shared)
                ring[0]=add((x0,y0,z0),amount*jaw_factor(y0))
                ring[1]=add((x1,y1,z1),amount*jaw_factor(y1))
                ring.append(add((-x1,y1,z1),amount*jaw_factor(y1)))
                rings.append(ring);names.append(lip)
            continue
        amount=JAW_WEIGHT.get(key,0.0)
        rings.append([add((x,y,z),amount*jaw_factor(y)) for x,y in mirror(half)]);names.append(key)
    apex=add(HEAD_APEX,0.0)
    faces=[tuple(reversed(rings[0]))]
    for (name,a),b in zip(zip(names,rings),rings[1:]):
        for j in range(COLS):
            k=(j+1)%COLS
            if name=='lower' and j in (13,0):continue   # real mouth opening
            quad=(a[j],a[k],b[k],b[j])
            if a[j]==b[j] and a[k]==b[k]:continue
            if a[j]==b[j]:faces.append((a[j],a[k],b[k]));continue
            if a[k]==b[k]:faces.append((a[j],a[k],b[j]));continue
            column=j if j<7 else 13-j
            if (name,column) in TRIANGULATED:
                if j<7:faces+= [(a[j],a[k],b[k]),(a[j],b[k],b[j])]
                else:faces+= [(a[j],a[k],b[j]),(a[k],b[k],b[j])]
            else:faces.append(quad)
    top=rings[-1]
    faces+=[(top[j],top[(j+1)%COLS],apex) for j in range(COLS)]
    head=mesh('HeadAuthoredPlanes',verts,faces,skin)
    weighted(head,[{'Head':1-amount,'Jaw':amount} for amount in jaw])
    # Dark oral cavity behind the slit, sharing the lip weights.
    lower,upper=rings[names.index('lower')],rings[names.index('upper')]
    outline=[upper[13],upper[0],upper[1],lower[1],lower[0],lower[13]]
    front=[verts[i] for i in outline];amounts=[jaw[i] for i in outline]
    back=[(x*.8,-.109,z) for x,y,z in front]
    n=len(front)
    cavity=mesh('MouthCavity',front+back,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[tuple(range(2*n-1,n-1,-1))],dark)
    weighted(cavity,[{'Head':1-a,'Jaw':a} for a in amounts+amounts])
    return triangles(verts,faces)


# ---------------------------------------------------------------- eyes
EYE_SEGMENTS,EYE_RINGS=12,8
# Pupil lens: ~33 x 34 mm (0.28 of the eye width), a touch toward the nose.
PUPIL_HALF=(.0165,.0172)


def faceted_ball(mesh, name, center, radii, material, bone, segments, rings):
    """Low-poly UV ball whose pole faces the front (-Y): clean faceted silhouette."""
    bm=bmesh.new()
    bmesh.ops.create_uvsphere(bm,u_segments=segments,v_segments=rings,radius=1)
    bm.verts.index_update()
    verts=[(center[0]+v.co.x*radii[0],center[1]-v.co.z*radii[1],center[2]+v.co.y*radii[2]) for v in bm.verts]
    faces=[tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return mesh(name,verts,faces,material,bone)


def pupil(mesh, name, center, side_sign, material):
    r=EYE_RADIUS;c=Vector(center)
    d=Vector((-side_sign*.07,-1,.03)).normalized()
    u=Vector((1,0,0));u=(u-d*u.dot(d)).normalized();v=d.cross(u).normalized()
    if v.z<0:v=-v
    ring=[]
    for k in range(10):
        a=2*math.pi*k/10
        direction=(d+u*math.tan(math.asin(PUPIL_HALF[0]/r))*math.cos(a)+v*math.tan(math.asin(PUPIL_HALF[1]/r))*math.sin(a)).normalized()
        ring.append(tuple(c+direction*(r+.0016)))
    verts=ring+[tuple(c+d*(r+.0030)),tuple(c+d*(r+.0006))]
    faces=[(k,(k+1)%10,10) for k in range(10)]+[((k+1)%10,k,11) for k in range(10)]
    return mesh(name,verts,faces,material,'Eye.'+('L' if side_sign>0 else 'R'))


def slab(mesh, name, outer, inner, material, bone):
    """Closed shell between two equal grids (rows x cols): hair patches, brows."""
    rows=len(outer);cols=len(outer[0])
    verts=[p for row in outer for p in row]+[p for row in inner for p in row]
    base=rows*cols;faces=[]
    for r in range(rows-1):
        for c in range(cols-1):
            a=r*cols+c
            faces.append((a,a+1,a+cols+1,a+cols))
            faces.append((base+a+cols,base+a+cols+1,base+a+1,base+a))
    for c in range(cols-1):
        faces.append((c+1,c,base+c,base+c+1))
        top=(rows-1)*cols+c
        faces.append((top,top+1,base+top+1,base+top))
    for r in range(rows-1):
        a=r*cols
        faces.append((a+cols,a,base+a,base+a+cols))
        b=r*cols+cols-1
        faces.append((b,b+cols,base+b+cols,base+b))
    return mesh(name,verts,faces,material,bone)


def brows(mesh, tris, dark):
    # Arched bars lying on the forehead (<=4 mm proud): inner end 4 mm higher
    # than the outer end (worried/goofy, PER-01/PER-04), 15 -> 10 mm tall
    # (-35%), bottom ~5 mm above the eyeball so skin shows in between.
    # Everything stays above Z=1.59, clear of the frontal closed-lid rays.
    columns=[.022,.038,.054,.070,.086,.102,.118]
    for side,s in [('L',1),('R',-1)]:
        outer=[];inner=[]
        for t in (0,.5,1):
            front_row=[];back_row=[]
            for x in columns:
                u=(x-columns[0])/(columns[-1]-columns[0])
                middle=1.5995+.004*(1-u)+.003*math.sin(math.pi*u)
                height=.0150-.0050*u
                z=middle-height/2+height*t
                back=front_y(tris,s*x,z)+.0006
                front_row.append((s*x,back-.0038,z));back_row.append((s*x,back,z))
            outer.append(front_row);inner.append(back_row)
        slab(mesh,'Brow.'+side,outer,inner,dark,'Brow.'+side)


def nose(mesh, tris, skin):
    """Small triangular wedge under/between the eyes: 40 mm tall, 10 mm bridge,
    24 mm base, 12 mm proud at the tip (reads as a bump under the eye in profile)."""
    f=lambda x,z:front_y(tris,x,z)
    points=[(-.005,f(-.005,1.487)+.002,1.487),(.005,f(.005,1.487)+.002,1.487),
            (0,f(0,1.488)-.0015,1.488),(0,f(0,1.452)-.012,1.452),
            (-.011,f(-.011,1.448)-.0075,1.448),(.011,f(.011,1.448)-.0075,1.448),
            (-.012,f(-.012,1.4475)+.002,1.4475),(.012,f(.012,1.4475)+.002,1.4475)]
    faces=[(0,2,1),(2,3,5),(2,5,7),(2,7,1),(2,4,3),(2,6,4),(2,0,6),
           (3,4,6,7,5),(0,1,7,6)]
    mesh('HeadNose',points,faces,skin,'Head')


def eyes(mesh, tris, white, dark, skin):
    for side,s in [('L',1),('R',-1)]:
        cx,cy,cz=EYE_CENTERS[side];r=EYE_RADIUS
        faceted_ball(mesh,'EyeWhite.'+side,(cx,cy,cz),(r,r,r*EYE_HEIGHT_FACTOR),white,'Head',EYE_SEGMENTS,EYE_RINGS)
        pupil(mesh,'Pupil.'+side,(cx,cy,cz),s,dark)
    brows(mesh,tris,dark)
    nose(mesh,tris,skin)


def ears(ellipsoid, skin):
    for side,s in [('L',1),('R',-1)]:
        ellipsoid('Ear.'+side,(s*.184,.028,1.492),(.017,.027,.036),skin,'Head',8,5)


# Hair bottom edge per fractional ring column on the +X half (mirrored by
# column -> 14 - column): temple, sideburn in front of the ear, the line over
# the ear and faceted locks at the nape. The top row hides under the cap band.
HAIR_TOP=1.632
HAIR_BOTTOM={3.25:1.612,3.55:1.558,3.85:1.498,4.10:1.512,4.30:1.536,4.60:1.536,4.90:1.522,
             5.20:1.476,5.60:1.448,6.00:1.430,6.40:1.418,6.70:1.424,7.00:1.412}
HAIR_ROWS=[(0.0,.0090),(.22,.0095),(.45,.0095),(.70,.0088),(1.0,.0040)]


def hair(mesh, tris, hair_mat):
    """Dark brown hair under the cap: temples, sideburns, above the ears, nape locks."""
    solid=[tuple(Vector(p) for p in t) for t in tris]
    columns=sorted(set(HAIR_BOTTOM)|{round(14-c,3) for c in HAIR_BOTTOM})
    bottom=lambda c:HAIR_BOTTOM[c] if c in HAIR_BOTTOM else HAIR_BOTTOM[round(14-c,3)]
    outer=[];inner=[]
    for t,distance in HAIR_ROWS:
        front_row=[];back_row=[]
        for c in columns:
            z=HAIR_TOP+(bottom(c)-HAIR_TOP)*t
            front_row.append(radial_surface(solid,z,c,distance));back_row.append(radial_surface(solid,z,c,-.002))
        outer.append(front_row);inner.append(back_row)
    slab(mesh,'HeadHair',outer,inner,hair_mat,'Head')


# ---------------------------------------------------------------- nightcap
# Rolled band: (outward offset from the skull, z); 30 mm tall, 10-11 mm proud.
BAND_PROFILE=[(-.002,1.622),(.006,1.6225),(.0100,1.628),(.0112,1.637),(.0100,1.646),(.006,1.6515),(-.002,1.652)]
CROWN=[(1.646,.0100),(1.670,.0120),(1.694,.0125),(1.712,.0120)]
CROWN_APEX=(0,.012,1.739)
# Soft tail: spine points and radii (base ~0.085 m, +30%). Its start is buried
# in the crown; it swells out of the back-left of the crown (max ~1.762 m),
# flops back and hangs behind the left ear (UI-06 sleeping human), clear of the
# head/hair/band; from the front only the swell and the pompom show.
CAP_TAIL=[((0.020,.000,1.640),.085),((0.032,.038,1.668),.088),((0.050,.080,1.680),.082),
          ((0.072,.120,1.674),.074),((0.094,.158,1.654),.065),((0.114,.194,1.624),.057),
          ((0.132,.214,1.590),.050),((0.146,.222,1.558),.043),((0.156,.222,1.530),.036),
          ((0.163,.216,1.508),.030),((0.167,.209,1.491),.024),((0.169,.204,1.478),.018)]
POMPOM_CENTER=(.170,.200,1.465)
POMPOM_RADIUS=.052


def loft(mesh, name, spine, material, bone, sides=10):
    """Tube along a spine with rotation-minimising frames (no twist or folds)."""
    points=[Vector(p) for p,_ in spine];radii=[r for _,r in spine]
    tangents=[(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized() for i in range(len(points))]
    normal=Vector((1,0,0));normal=(normal-tangents[0]*normal.dot(tangents[0])).normalized()
    verts=[]
    for point,radius,tangent in zip(points,radii,tangents):
        normal=(normal-tangent*normal.dot(tangent)).normalized();binormal=tangent.cross(normal)
        verts+=[tuple(point+radius*(math.cos(2*math.pi*j/sides)*normal+math.sin(2*math.pi*j/sides)*binormal))
                for j in range(sides)]
    faces=[tuple(reversed(range(sides)))]
    for i in range(len(points)-1):
        faces+=[(i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j) for j in range(sides)]
    faces.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    return mesh(name,verts,faces,material,bone)


def pompom(mesh, material):
    """Fluffy faceted ball (jittered icosphere) instead of a regular UV 'disco ball'."""
    bm=bmesh.new()
    bmesh.ops.create_icosphere(bm,subdivisions=1,radius=1)
    bm.verts.index_update()
    c=Vector(POMPOM_CENTER);verts=[]
    for v in bm.verts:
        n=v.co.normalized()
        jitter=1+.07*math.sin(12.9898*n.x+78.233*n.y+37.719*n.z)
        verts.append(tuple(c+n*POMPOM_RADIUS*jitter))
    faces=[tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    mesh('NightcapPom',verts,faces,material,'Head')


def nightcap(mesh, tris, m):
    solid=[tuple(Vector(p) for p in t) for t in tris]
    loops=[[radial_surface(solid,z,j,d) for j in range(COLS)] for d,z in BAND_PROFILE]
    ring_solid(mesh,'NightcapBand',loops,m['band'],'Head')
    rings=[[radial_surface(solid,z,j,d) for j in range(COLS)] for z,d in CROWN]
    verts=[p for ring in rings for p in ring]+[CROWN_APEX];apex=len(verts)-1
    faces=[]
    for r in range(len(rings)-1):
        faces+=[(r*COLS+j,r*COLS+(j+1)%COLS,(r+1)*COLS+(j+1)%COLS,(r+1)*COLS+j) for j in range(COLS)]
    last=(len(rings)-1)*COLS
    faces+=[(last+j,last+(j+1)%COLS,apex) for j in range(COLS)]
    mesh('NightcapCrown',verts,faces,m['cap'],'Head')
    loft(mesh,'NightcapTail',CAP_TAIL,m['cap'],'Head',10)
    pompom(mesh,m['trim'])


def head_and_cap(c, mesh, tube, ellipsoid, m):
    tris=skull(mesh,m['skin'],m['dark'])
    eyes(mesh,tris,m['white'],m['dark'],m['skin'])
    ears(ellipsoid,m['skin'])
    hair(mesh,tris,m['hair'])
    # Neck ~38% of the head width; 66 mm visible between the lowered collar and the chin.
    tube('Neck',[(0,.012,1.200),(0,.010,1.275),(0,.008,1.335),(0,.008,1.400)],
         [.072,.066,.064,.066],[.064,.060,.058,.060],m['skin'],'Neck',10)
    nightcap(mesh,tris,m)


# ---------------------------------------------------------------- slippers
# Open-back mule: 16 mm sole (#2A3050), 0.265 m long x 0.20 m wide (1.25x the
# trouser leg), a domed upper over the front 60% of the foot that stands 80 mm
# at the toe box, and a bare heel/instep behind it.
SOLE_HALF=[(0,-.205),(.040,-.200),(.071,-.184),(.092,-.160),(.100,-.128),(.097,-.090),
           (.088,-.050),(.080,-.012),(.076,.022),(.066,.046),(.042,.060),(0,.064)]
UPPER=[(-.207,.030),(-.196,.056),(-.172,.074),(-.140,.081),(-.105,.078),(-.072,.068),(-.046,.058)]


def sole_outline():
    return SOLE_HALF+[(-x,y) for x,y in reversed(SOLE_HALF[1:-1])]


def sole_half_width(y):
    pts=SOLE_HALF
    for (x0,y0),(x1,y1) in zip(pts,pts[1:]):
        if min(y0,y1)<=y<=max(y0,y1) and y0!=y1:
            return x0+(x1-x0)*(y-y0)/(y1-y0)
    return 0.0


def ankle_weights(z, side):
    lower=clamp((z-.100)/.035)
    return {'Foot.'+side:1-lower,'LowerLeg.'+side:lower}


def slipper(side, s, mesh, tube, m):
    center=s*.125
    outline=sole_outline();count=len(outline)
    loops=[(.97,0),(1.0,.005),(1.0,.0145),(.965,.0165)]
    soleverts=[(center+x*k,(y+.070)*k-.070,z) for k,z in loops for x,y in outline]
    faces=[tuple(reversed(range(count))),tuple(range(3*count,4*count))]
    faces+=[(r*count+i,r*count+(i+1)%count,(r+1)*count+(i+1)%count,(r+1)*count+i) for r in range(3) for i in range(count)]
    mesh('SlipperSole.'+side,soleverts,faces,m['sole'],'Foot.'+side)
    # Domed upper: puffy arches from sole edge to sole edge, toe to throat.
    arch=9;verts=[];base=.0150
    for y,h in UPPER:
        w=max(sole_half_width(y)*1.015,.030)
        for k in range(arch):
            a=math.pi*k/(arch-1);c=math.cos(a);sn=math.sin(a)
            # puffy: the arch swells ~7% past the sole edge just above the base
            x=w*(1+.07*math.sin(min(math.pi,2.2*sn)))*c;z=base+(h-base)*sn**.85
            verts.append((center+x,y,z))
    rows=len(UPPER)
    toe=len(verts);verts.append((center,-.2105,.024))
    faces=[]
    for r in range(rows-1):
        faces+=[(r*arch+k,r*arch+k+1,(r+1)*arch+k+1,(r+1)*arch+k) for k in range(arch-1)]
    faces+=[(k+1,k,toe) for k in range(arch-1)]
    faces.append(tuple((rows-1)*arch+k for k in range(arch)))           # throat
    faces.append(tuple([toe]+[r*arch for r in range(rows)]+[(rows-1-r)*arch+arch-1 for r in range(rows)]))  # base
    mesh('SlipperUpper.'+side,verts,faces,m['slipper'],'Foot.'+side)
    # Bare heel and instep block on the sole, entering the upper.
    sections=[(.061,.035,.020,.0185),(.052,.040,.033,.0245),(.026,.046,.040,.0295),(-.020,.047,.042,.0305),(-.085,.043,.042,.0265)]
    foot=[]
    for y,zc,hw,hh in sections:
        foot+= [(center+hw*math.cos(2*math.pi*j/12),y,zc+hh*math.sin(2*math.pi*j/12)) for j in range(12)]
    faces=[tuple(reversed(range(12))),tuple(range(48,60))]
    faces+=[(r*12+j,r*12+(j+1)%12,(r+1)*12+(j+1)%12,(r+1)*12+j) for r in range(4) for j in range(12)]
    mesh('FootSkin.'+side,foot,faces,m['skin'],'Foot.'+side)
    ankle=tube('Ankle.'+side,[(center,.010,.030),(center,.009,.080),(center,.008,.140)],
               [.041,.041,.042],[.038,.038,.040],m['skin'],None,8)
    weighted(ankle,[ankle_weights(v.co.z,side) for v in ankle.data.vertices])
