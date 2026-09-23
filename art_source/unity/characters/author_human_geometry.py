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
Round 4 (art-director corrections): the face is a faceted box with rounded
corners (mouth-level width ~75% of the skull, 180 mm flat chin brought 23 mm
forward with a vertical 28 mm front, mouth-to-chin 14 mm shorter); 16x6
faceted eyeballs whose lower third uses Human_EyeWhiteShade, parallel 29 mm
pupils, an 8-10 mm skin lid rim over each eye; a 55x45 mm wedge nose 18 mm
proud; slightly thicker brows; a clean nape edge ~30 mm above the neck, 40%
shorter sideburns and three fringe facets under the band; a puffier crown and
a tail that falls down the left side to a 20% larger pompom at jaw height;
puffy mules with a 25 mm sole, ~112 mm bulbous toe box over ~75% of the foot
and a lower heel.
Round 5 (art-director corrections): round protruding 132 x 148 mm globes
(no lid rim; only the lowest ~14% in a light shade) with 37 mm pupils 4 mm
toward the nose; a hexagonal face whose jaw runs in straight planes from the
full-width cheekbone ring to a 140 mm chin (mouth and chin 20+ mm higher,
mouth-to-chin 55 mm, no horizontal cheek/jaw rings, a flat head bottom and a
rounded occiput under the hair); brows moved above the larger globes; a small
nose; C-cup ears 40% larger and 12 mm further out; neck flared +15% over the
collar; two side fringe facets (no central peak); a 5-ring soft nightcap
with a 30 mm darker band and a 7-section tail.
Round 6 (art-director corrections): the chin drops 45 mm to 1.300 (mouth
stays at 1.40, Socket.Eye at 1.53: mouth-to-chin ~100 mm) and the face is a
rounded box: vertical cheek walls down to a jaw ring at z 1.39, then a
bevel to a flat 150 mm chin (no hexagonal cheekbone peak); the skull is
~15% narrower at the eyes/temples (~302 mm), so the 2 x 132 mm globes cover
~87% of the face and the head is taller than wide. Brows are 3D wedges
(20 -> 16 mm tall, 110 mm long, 8 mm proud) resting on the top of the globes
with the inner end 3.5 mm lower; the side fringe facets are replaced by
30 x 50 mm sideburn blocks in front of the ears; the cap band sits 12 mm
lower, just over the brows. Closed puffy slippers: 20 mm #1E2440 sole,
~+50% instep, ~+11% width, a swollen round toe and a closed heel.
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
# Round 6 (art director): rounded-box face. The chin drops 45 mm (1.345 ->
# 1.300) while the mouth stays at 1.40 (mouth-to-chin ~100 mm, ~0.65 eye
# diameters as in the PER-01 faces). The cheek walls are vertical from the
# temples down to a jaw ring at z 1.39, then a jaw bevel runs to a flat
# 150 mm chin; the round-5 full-width cheekbone ring (hexagon) is gone.
CHIN_Z=1.300
MOUTH_Z=1.400
JAW_Z=1.390
# Head underside (x, y, z) per +X column: the flat chin at the front rises
# under the jaw toward the nape, so the back of the head stays short and the
# neck (narrower than the chin) emerges from it. c7 clears the neck back.
CHIN_HALF=[(0,-.130,1.300),(.036,-.130,1.300),(.075,-.130,1.300),(.100,-.098,1.312),
           (.094,-.040,1.330),(.074,.008,1.345),(.054,.050,1.356),(0,.071,1.360)]
# Jaw ring on the side/back columns c3..c7 (mirrored): bottom of the vertical
# cheek walls and the rounded occiput above the nape.
JAW_HALF={3:(.137,-.121,1.390),4:(.149,-.036,1.390),5:(.140,.042,1.394),6:(.090,.102,1.400),7:(0,.114,1.404)}
# Round 6: the skull is ~15% narrower at the eyes/temples (c4 .188 -> .151,
# ~302 mm) so the two 132 mm globes cover ~87% of the face width.
HEAD_RINGS=[
    # Cheek ring: c0..c2 share one frontal plane with the chin (flat face).
    ('cheek',1.455,[(0,-.138),(.032,-.138),(.086,-.138),(.137,-.124),(.150,-.034),(.145,.066),(.092,.122),(0,.132)]),
    # Eye line: c1/c2 recessed into a shallow socket behind the globes.
    ('eye',  1.530,[(0,-.137),(.030,-.130),(.088,-.124),(.138,-.113),(.151,-.030),(.146,.078),(.094,.138),(0,.148)]),
    ('brow', 1.600,[(0,-.135),(.036,-.1345),(.088,-.131),(.134,-.112),(.148,-.028),(.143,.082),(.092,.142),(0,.152)]),
    ('band', 1.640,[(0,-.125),(.040,-.1245),(.088,-.118),(.127,-.098),(.140,-.026),(.135,.076),(.088,.131),(0,.142)]),
    ('crown',1.685,[(0,-.097),(.036,-.096),(.074,-.090),(.100,-.072),(.116,-.017),(.111,.058),(.069,.100),(0,.110)]),
    ('top',  1.712,[(0,-.060),(.027,-.059),(.048,-.054),(.065,-.042),(.073,-.006),(.070,.040),(.044,.066),(0,.073)]),
]
HEAD_APEX=(0,.004,1.724)
# Small mouth slit: 40 mm wide, 4 mm open at the centre and 3 mm at the
# corners, which drop 2 mm (worried mouth of PER-01/PER-04).
MOUTH_HALF=.020
LIP_Z={'lower':(MOUTH_Z-.0015,MOUTH_Z-.0037),'upper':(MOUTH_Z+.0025,MOUTH_Z-.0007)}
JAW_WEIGHT={'chin':1.0,'jaw':.30,'mouth':.30,'lower':.70,'upper':0.0}


def mirror(half):
    return list(half)+[(-p[0],)+tuple(p[1:]) for p in reversed(half[1:7])]


def frontal_y(z):
    """The flat front plane of the lower face (cheek c0..c2 down to the chin)."""
    return CHIN_HALF[0][1]+(HEAD_RINGS[0][2][0][1]-CHIN_HALF[0][1])*(z-CHIN_Z)/(HEAD_RINGS[0][1]-CHIN_Z)


# Outline rings used only to aim the hair/cap rays (x, y per column); the
# actual surface is always found on the authored skull triangles.
STRUCTURE=[(CHIN_Z,mirror([(x,y) for x,y,_ in CHIN_HALF])),
           (JAW_Z,mirror([(0,frontal_y(JAW_Z)),(.034,frontal_y(JAW_Z)),(.080,frontal_y(JAW_Z))]
                         +[JAW_HALF[c][:2] for c in range(3,8)]))]
STRUCTURE+=[(z,mirror(half)) for _,z,half in HEAD_RINGS]


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


# Cheek/temple quads above the cheekbone are split into triangles (PER-04
# facets). (lower ring name, first +X column of the quad) -> triangulate.
TRIANGULATED={(ring,column) for ring,columns in [
    ('cheek',range(2,6)),('eye',range(3,6)),('brow',range(3,6))] for column in columns}


def skull(mesh, skin, dark):
    verts=[];jaw=[]

    def add(point, amount):
        verts.append(tuple(point));jaw.append(amount);return len(verts)-1

    # Lower face: head underside ring, the flat-front mouth vertices, the jaw
    # ring and the cheek ring. Every +X face is authored once and mirrored
    # (c -> 14 - c).
    chin={}
    for c,(x,y,z) in enumerate(CHIN_HALF):
        amount=JAW_WEIGHT['chin']*jaw_factor(y)
        chin[c]=add((x,y,z),amount)
        if 0<c<7:chin[14-c]=add((-x,y,z),amount)
    lips={}
    for key in ('lower','upper'):
        centre,corner=LIP_Z[key];amount=JAW_WEIGHT[key]
        lips[(key,0)]=add((0,frontal_y(centre),centre),amount*jaw_factor(frontal_y(centre)))
        lips[(key,1)]=add((MOUTH_HALF,frontal_y(corner),corner),amount*jaw_factor(frontal_y(corner)))
        lips[(key,13)]=add((-MOUTH_HALF,frontal_y(corner),corner),amount*jaw_factor(frontal_y(corner)))
    cheek_half=HEAD_RINGS[0][2]
    mx=(CHIN_HALF[2][0]+cheek_half[2][0])/2
    mouth={2:add((mx,frontal_y(MOUTH_Z),MOUTH_Z),JAW_WEIGHT['mouth']*jaw_factor(frontal_y(MOUTH_Z))),
           12:add((-mx,frontal_y(MOUTH_Z),MOUTH_Z),JAW_WEIGHT['mouth']*jaw_factor(frontal_y(MOUTH_Z)))}
    jawring={}
    for c,(x,y,z) in JAW_HALF.items():
        amount=JAW_WEIGHT['jaw']*jaw_factor(y)
        jawring[c]=add((x,y,z),amount)
        if c<7:jawring[14-c]=add((-x,y,z),amount)
    rings=[];names=[]
    for key,z,half in HEAD_RINGS:
        rings.append([add((x,y,z),0.0) for x,y in mirror(half)]);names.append(key)
    cheek=rings[0]
    K=lambda c:cheek[c%COLS]
    C=lambda c:chin[c%COLS]
    J=lambda c:jawring[c%COLS]
    L=lambda key,c:lips[(key,c)]
    half_faces=[
        (C(0),C(1),L('lower',1),L('lower',0)),                 # flat front under the mouth
        (C(1),C(2),mouth[2],L('lower',1)),
        (L('lower',1),mouth[2],L('upper',1)),                  # mouth corner
        (L('upper',1),mouth[2],K(2),K(1)),                     # flat front over the mouth
        (L('upper',0),L('upper',1),K(1),K(0)),
        # Front corner: chin-corner bevel and the cheek front plane.
        (C(2),C(3),J(3)),(C(2),J(3),mouth[2]),(mouth[2],J(3),K(3)),(mouth[2],K(3),K(2)),
        # Vertical cheek walls (jaw ring -> cheek ring) over the jaw bevel.
        (J(3),J(4),K(4),K(3)),(C(3),C(4),J(4),J(3)),
        (J(4),J(5),K(5),K(4)),(C(4),C(5),J(5),J(4)),
        (J(5),J(6),K(6),K(5)),(C(5),C(6),J(6),J(5)),
        (J(6),J(7),K(7),K(6)),(C(6),C(7),J(7),J(6))]
    flip={chin[c]:chin[(14-c)%COLS] for c in range(COLS)}
    flip.update({cheek[c]:cheek[(14-c)%COLS] for c in range(COLS)})
    flip.update({lips[(k,1)]:lips[(k,13)] for k in ('lower','upper')})
    flip.update({lips[(k,0)]:lips[(k,0)] for k in ('lower','upper')})
    flip.update({mouth[2]:mouth[12]})
    flip.update({jawring[c]:jawring[(14-c)%COLS] for c in jawring})
    # Non-planar underside: a fan around a centre vertex hidden by the neck.
    under=add((0,-.035,1.322),JAW_WEIGHT['chin']*jaw_factor(-.035))
    faces=[(under,chin[(c+1)%COLS],chin[c]) for c in range(COLS)]
    for face in half_faces:
        faces.append(face)
        mirrored_face=tuple(reversed([flip[i] for i in face]))
        if sorted(mirrored_face)!=sorted(face):faces.append(mirrored_face)
    for (name,a),b in zip(zip(names,rings),rings[1:]):
        for j in range(COLS):
            k=(j+1)%COLS
            column=j if j<7 else 13-j
            if (name,column) in TRIANGULATED:
                if j<7:faces+= [(a[j],a[k],b[k]),(a[j],b[k],b[j])]
                else:faces+= [(a[j],a[k],b[j]),(a[k],b[k],b[j])]
            else:faces.append((a[j],a[k],b[k],b[j]))
    apex=add(HEAD_APEX,0.0)
    top=rings[-1]
    faces+=[(top[j],top[(j+1)%COLS],apex) for j in range(COLS)]
    head=mesh('HeadAuthoredPlanes',verts,faces,skin)
    weighted(head,[{'Head':1-amount,'Jaw':amount} for amount in jaw])
    # Dark oral cavity behind the slit, sharing the lip weights.
    outline=[lips[('upper',13)],lips[('upper',0)],lips[('upper',1)],lips[('lower',1)],lips[('lower',0)],lips[('lower',13)]]
    front=[verts[i] for i in outline];amounts=[jaw[i] for i in outline]
    back=[(x*.8,-.109,z) for x,y,z in front]
    n=len(front)
    cavity=mesh('MouthCavity',front+back,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[tuple(range(2*n-1,n-1,-1))],dark)
    weighted(cavity,[{'Head':1-a,'Jaw':a} for a in amounts+amounts])
    return triangles(verts,faces)


# ---------------------------------------------------------------- eyes
EYE_SEGMENTS=16
# Round 5: ring latitudes (degrees from the top). The bottom cap starts at
# 136 deg (z < -0.72 r), i.e. only the lowest ~14% of the globe takes the
# light Human_EyeWhiteShade (#E2E4EC); the rest is a clean white sphere.
EYE_LATITUDES=(0,30,60,90,115,136,180)
# Round 5: pupil 0.28 of the eye width (37 mm), vertically centred and 4 mm
# toward the nose (parallel gaze, no cross-eye).
PUPIL_HALF=(.0185,.0185)
PUPIL_INSET=.004


def pupil_direction(side_sign):
    return Vector((-side_sign*PUPIL_INSET/EYE_RADIUS,-1,0)).normalized()


def faceted_ball(mesh, name, center, radii, material, bone, segments, latitudes, shade=None):
    """Low-poly vertical-axis ball, 16 columns: ~8 large facet columns visible
    from the front. A meridian column is centred on the front (-Y) so the
    pupil sits in the middle of a facet column. Only the bottom cap takes the
    optional shade material (URP ignores vertex colours)."""
    lats=[math.radians(v) for v in latitudes]
    verts=[(center[0],center[1],center[2]+radii[2])]
    offset=math.pi/segments
    # Vertices on the circumscribed circle: the facet planes (not the corners)
    # sit at the radius, so the front view shows the full 2r = 132 mm width.
    wide=1/math.cos(math.pi/segments)
    for lat in lats[1:-1]:
        for j in range(segments):
            a=-math.pi/2+offset+2*math.pi*j/segments
            verts.append((center[0]+wide*radii[0]*math.sin(lat)*math.cos(a),center[1]+wide*radii[1]*math.sin(lat)*math.sin(a),
                          center[2]+radii[2]*math.cos(lat)))
    verts.append((center[0],center[1],center[2]-radii[2]))
    bottom=len(verts)-1;rings=len(lats)-1
    ring=lambda i,j:1+(i-1)*segments+j%segments
    faces=[];lower=[]
    for j in range(segments):faces.append((0,ring(1,j+1),ring(1,j)))
    for i in range(1,rings-1):
        for j in range(segments):
            faces.append((ring(i,j),ring(i,j+1),ring(i+1,j+1),ring(i+1,j)))
    for j in range(segments):
        faces.append((ring(rings-1,j),ring(rings-1,j+1),bottom));lower.append(len(faces)-1)
    obj=mesh(name,verts,faces,material,bone)
    if shade is not None:
        obj.data.materials.append(shade)
        for index in lower:obj.data.polygons[index].material_index=1
    return obj


def ball_triangles(obj):
    vs=[Vector(v.co) for v in obj.data.vertices]
    return [(vs[p.vertices[0]],vs[p.vertices[i]],vs[p.vertices[i+1]]) for p in obj.data.polygons for i in range(1,len(p.vertices)-1)]


def pupil(mesh, name, center, side_sign, material, ball):
    """Flat faceted pupil decal (37 mm): every vertex is ray-projected onto the
    actual eyeball facets and lifted, with an inner ring so it follows the
    facet creases instead of cutting through them."""
    r=EYE_RADIUS;c=Vector(center);tris=ball_triangles(ball)
    d=pupil_direction(side_sign)
    u=Vector((1,0,0));u=(u-d*u.dot(d)).normalized();v=d.cross(u).normalized()
    if v.z<0:v=-v
    def on_ball(direction,lift):
        direction=direction.normalized();hit=None
        for a,b,t in tris:
            distance=ray_triangle(c,direction,a,b,t)
            if distance is not None and (hit is None or distance>hit):hit=distance
        return tuple(c+direction*((hit or r)+lift))
    count=12;front=[];back=[]
    for lift,out in ((.0018,front),(.0004,back)):
        out.append(on_ball(d,lift+.0004))
        for fraction in (.5,1.0):
            for k in range(count):
                a=2*math.pi*k/count
                direction=d+u*math.tan(math.asin(PUPIL_HALF[0]*fraction/r))*math.cos(a)+v*math.tan(math.asin(PUPIL_HALF[1]*fraction/r))*math.sin(a)
                out.append(on_ball(direction,lift))
    verts=front+back;n=len(front);faces=[]
    for base,flip in ((0,False),(n,True)):
        tri=[(base,base+1+k,base+1+(k+1)%count) for k in range(count)]
        quad=[(base+1+k,base+1+count+k,base+1+count+(k+1)%count,base+1+(k+1)%count) for k in range(count)]
        for f in tri+quad:faces.append(tuple(reversed(f)) if flip else f)
    for k in range(count):
        a=1+count+k;b=1+count+(k+1)%count
        faces.append((a,a+n,b+n,b))
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


# Round 6 brows: 3D wedges 110 mm long, 20 mm tall at the inner end tapering
# to 16 mm, 8 mm proud with bevelled top/bottom facets (the front face is
# 6 mm shorter than the base), resting on the top of the globes (eye top
# z=1.604) with the inner end 3.5 mm lower. They stay under the lowered cap
# band and above z=1.60, clear of the frontal closed-lid rays (z<=1.564).
BROW_SPAN=(.014,.124)
BROW_BOTTOM=1.5995
BROW_RELIEF=.008


def brows(mesh, tris, dark):
    columns=[BROW_SPAN[0]+(BROW_SPAN[1]-BROW_SPAN[0])*i/6 for i in range(7)]
    for side,s in [('L',1),('R',-1)]:
        outer=[];inner=[]
        for t in (0,1):
            front_row=[];back_row=[]
            for x in columns:
                u=(x-columns[0])/(columns[-1]-columns[0])
                bottom=BROW_BOTTOM+.0035*u+.0015*math.sin(math.pi*u)
                height=.020-.004*u
                z=bottom+height*t
                zf=bottom+.0028+(height-.0060)*t
                back=front_y(tris,s*x,z)+.0006
                front=front_y(tris,s*x,zf)+.0006-BROW_RELIEF
                front_row.append((s*x,front,zf));back_row.append((s*x,back,z))
            outer.append(front_row);inner.append(back_row)
        slab(mesh,'Brow.'+side,outer,inner,dark,'Brow.'+side)


def nose(mesh, tris, skin):
    """Round 5 small wedge nose (42 x 44 mm, 14 mm proud at an 18 mm tip)
    between the lower halves of the globes, well above the raised mouth.
    The ridge runs from the flush bridge down to the tip, so the side facets
    face up/outward and stay lit; only the underside reads as a shadow."""
    f=lambda x,z:front_y(tris,x,z)
    on=lambda x,z,out:(x,f(x,z)-out,z)
    points=[on(-.006,1.478,-.003),on(.006,1.478,-.003),on(0,1.476,.002),
            on(-.009,1.4385,.014),on(.009,1.4385,.014),
            on(-.021,1.4400,-.003),on(.021,1.4400,-.003),
            on(-.010,1.4345,-.003),on(.010,1.4345,-.003)]
    faces=[(1,2,0),(2,3,4),(0,5,3),(0,3,2),(1,2,4),(1,4,6),
           (3,5,7),(3,7,8),(3,8,4),(4,8,6),(0,1,6,8,7,5)]
    mesh('HeadNose',points,faces,skin,'Head')


def eyes(mesh, tris, white, dark, skin, shade=None):
    for side,s in [('L',1),('R',-1)]:
        cx,cy,cz=EYE_CENTERS[side];r=EYE_RADIUS
        ball=faceted_ball(mesh,'EyeWhite.'+side,(cx,cy,cz),(r,r,r*EYE_HEIGHT_FACTOR),white,'Head',EYE_SEGMENTS,EYE_LATITUDES,shade)
        pupil(mesh,'Pupil.'+side,(cx,cy,cz),s,dark,ball)
    brows(mesh,tris,dark)
    nose(mesh,tris,skin)


# Round 5 ears: 40% larger and 12 mm further out, built as a low-poly "C"
# cup (outer rim, sunken bowl, back shell) so the 3/4 view shows the PER-04 C.
# Round 6: moved in with the narrower skull (side wall x ~.148 at the ear);
# the rim stands ~20 mm off the head.
EAR_CENTER=(.156,.030,1.492)
EAR_SIZE=(.038,.050)          # half depth (y) and half height (z) of the rim
EAR_OUT=.012                  # rim distance outside the centre plane


def ears(mesh, skin):
    n=10
    for side,s in [('L',1),('R',-1)]:
        cx,cy,cz=EAR_CENTER;verts=[]
        def loop(x,ry,rz,dy=0.0,dz=0.0):
            # Slightly egg-shaped (wider on top) and tilted back 10 deg.
            out=[]
            for k in range(n):
                a=2*math.pi*k/n
                y=ry*math.cos(a);z=rz*math.sin(a)*(1.08 if math.sin(a)>0 else .92)
                y,z=y*math.cos(.17)+z*math.sin(.17),-y*math.sin(.17)+z*math.cos(.17)
                out.append((s*x,cy+dy+y,cz+dz+z))
            return out
        rim=loop(cx+EAR_OUT,*EAR_SIZE)
        lip=loop(cx+EAR_OUT-.002,EAR_SIZE[0]*.62,EAR_SIZE[1]*.66,-.004,.002)
        bowl=(s*(cx-.004),cy-.006,cz+.002)
        back=loop(cx-.026,EAR_SIZE[0]*.80,EAR_SIZE[1]*.84,.004,0)
        verts=rim+lip+back+[bowl];b=len(verts)-1
        faces=[]
        for k in range(n):
            q=(k+1)%n
            faces.append((k,q,n+q,n+k))                 # rim top, rolling into the bowl
            faces.append((n+k,n+q,b))                   # sunken bowl
            faces.append((q,k,2*n+k,2*n+q))             # back shell toward the skull
        faces.append(tuple(range(3*n-1,2*n-1,-1)))      # closed inside the head
        if s<0:faces=[tuple(reversed(f)) for f in faces]
        mesh('Ear.'+side,verts,faces,skin,'Head')


# Hair bottom edge per fractional ring column on the +X half (mirrored by
# column -> 14 - column): temple, sideburn in front of the ear, the line over
# the ear and faceted locks at the nape. The top row hides under the cap band.
HAIR_TOP=1.634
# Round 6: the hair starts behind the temple corner (no strands on the
# front of the side walls). Columns 3.64 -> 4.00 are a 30 mm wide sideburn
# block in front of the ear whose flat bottom sits 50 mm under the lowered
# cap band (z 1.574) and stands 14 mm off the skin; then the line over the
# ear and a clean nape ~20 mm above the jaw ring. Every integer skull column
# is sampled so the slab never cuts a skull crease.
HAIR_BOTTOM={3.64:1.574,3.82:1.574,4.00:1.574,4.25:1.556,4.60:1.551,5.00:1.536,
             5.30:1.505,5.65:1.470,6.00:1.446,6.35:1.432,6.70:1.426,7.00:1.424}
HAIR_ROWS=[(0.0,.0090),(.17,.0100),(.34,.0100),(.51,.0100),(.68,.0100),(.84,.0098),(1.0,.0080)]
SIDEBURN_COLUMNS=(3.6,4.05)
SIDEBURN_THICKNESS=1.4


def hair(mesh, tris, hair_mat):
    """Dark brown hair under the cap: block sideburns, above the ears, clean nape.
    Round 6: the thin fringe facets at the temples (seen edge-on as dark
    lines) are removed."""
    solid=[tuple(Vector(p) for p in t) for t in tris]
    columns=sorted(set(HAIR_BOTTOM)|{round(14-c,3) for c in HAIR_BOTTOM})
    bottom=lambda c:HAIR_BOTTOM[c] if c in HAIR_BOTTOM else HAIR_BOTTOM[round(14-c,3)]
    thick=lambda c:SIDEBURN_THICKNESS if SIDEBURN_COLUMNS[0]<=min(c,14-c)<=SIDEBURN_COLUMNS[1] else 1.0
    outer=[];inner=[]
    for t,distance in HAIR_ROWS:
        front_row=[];back_row=[]
        for c in columns:
            z=HAIR_TOP+(bottom(c)-HAIR_TOP)*t
            front_row.append(radial_surface(solid,z,c,distance*thick(c)));back_row.append(radial_surface(solid,z,c,-.002))
        outer.append(front_row);inner.append(back_row)
    slab(mesh,'HeadHair',outer,inner,hair_mat,'Head')


# ---------------------------------------------------------------- nightcap
# Round 5: a 30 mm band (bottom lip, two front facets, top lip) raised 12 mm
# above the larger eyes/brows, 11 mm proud, darker than the cap body.
# Round 6: the band is 12 mm lower (bottom 1.622), almost touching the brows
# (UI-06 screen 4).
BAND_PROFILE=[(-.002,1.622),(.009,1.6235),(.0125,1.637),(.009,1.6505),(-.002,1.652)]
# Round 5: soft low-poly fabric, 5 horizontal rings from band to apex
# (UI-06 screen 1) instead of 12+ ribbed rings. Round 6: a little puffier
# over the narrower skull.
CROWN=[(1.646,.0135),(1.674,.0290),(1.699,.0400)]
CROWN_DOME=[(1.729,.82),(1.751,.48)]
CROWN_APEX_Z=1.764
# Soft tail (UI-06 screens 1/4): flops over the top-left of the crown and
# falls down the LEFT side of the head, outside the ear, to a pompom at jaw
# height beside the face. Round 5: 7 sections (large soft facets). Round 6:
# ~35 mm closer to the narrower head, still ~20 mm clear of the ear rim.
CAP_TAIL=[((0.020,.022,1.672),.078),((0.100,.038,1.686),.072),((0.166,.058,1.654),.063),
          ((0.211,.074,1.594),.053),((0.232,.086,1.506),.041),((0.226,.094,1.420),.027),
          ((0.213,.096,1.372),.015)]
POMPOM_CENTER=(.207,.097,1.338)
POMPOM_RADIUS=.0624


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
    # Soft dome above the skull top: shrink the last ring toward its centre.
    last=rings[-1];cx=sum(p[0] for p in last)/COLS;cy=sum(p[1] for p in last)/COLS
    for z,scale in CROWN_DOME:
        rings.append([(cx+(p[0]-cx)*scale,cy+(p[1]-cy)*scale,z) for p in last])
    verts=[p for ring in rings for p in ring]+[(cx,cy,CROWN_APEX_Z)];apex=len(verts)-1
    faces=[]
    for r in range(len(rings)-1):
        faces+=[(r*COLS+j,r*COLS+(j+1)%COLS,(r+1)*COLS+(j+1)%COLS,(r+1)*COLS+j) for j in range(COLS)]
    last=(len(rings)-1)*COLS
    faces+=[(last+j,last+(j+1)%COLS,apex) for j in range(COLS)]
    mesh('NightcapCrown',verts,faces,m['cap'],'Head')
    loft(mesh,'NightcapTail',CAP_TAIL,m['cap'],'Head',8)
    pompom(mesh,m['trim'])


def head_and_cap(c, mesh, tube, ellipsoid, m):
    tris=skull(mesh,m['skin'],m['dark'])
    eyes(mesh,tris,m['white'],m['dark'],m['skin'],m.get('eyeshade'))
    ears(mesh,m['skin'])
    hair(mesh,tris,m['hair'])
    # Round 5 neck: +15% wider where it shows over the collar (trapezius
    # flare). Round 6: the lower chin leaves ~50 mm of neck in front; its top
    # tapers to ~90% of the 150 mm chin so the jaw overhangs it.
    tube('Neck',[(0,.012,1.200),(0,.010,1.270),(0,.008,1.335),(0,.010,1.420)],
         [.084,.074,.061,.056],[.068,.063,.056,.056],m['skin'],'Neck',10)
    nightcap(mesh,tris,m)


# ---------------------------------------------------------------- slippers
# Round 4 open-back mule: 25 mm sole (#1E2240), 0.29 m long x 0.224 m wide
# (centred 5 mm outboard so the pair keeps a gap), and a puffy #3B4A9A upper
# that covers the front ~74% of the foot with a bulbous toe box standing
# ~112 mm, then slopes down to a low throat under the trouser cuff. The bare
# heel block is 10 mm lower than round 3.
# Round 6 (art director): thick, cushy closed slippers. 20 mm #1E2440 sole
# 0.304 m long; the upper covers the whole foot including a closed heel
# counter (no bare heel from behind), instep ~+50% (~85 mm over the sole),
# a swollen round toe box ~137 mm high, ~+15% width (14% lateral / 6%
# medial puff past the sole edge). Centred 22 mm outboard of the foot bone
# so the wider pair keeps a ~30 mm gap. The trouser cuff (bottom z .070)
# enters the dome, so no ankle skin shows.
SLIPPER_OUTBOARD=.022
SOLE_HALF=[(0,-.236),(.054,-.231),(.094,-.213),(.120,-.186),(.131,-.150),(.129,-.104),
           (.117,-.058),(.105,-.014),(.099,.022),(.086,.047),(.056,.062),(0,.068)]
UPPER=[(-.233,.062),(-.223,.094),(-.207,.117),(-.185,.132),(-.156,.138),(-.122,.135),
       (-.088,.126),(-.054,.116),(-.020,.107),(.012,.100),(.036,.090),(.052,.074),(.063,.052)]
UPPER_PUFF=(.14,.06)          # lateral, medial swell past the sole edge
SOLE_TOP=.020


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
    center=s*(.125+SLIPPER_OUTBOARD)
    outline=sole_outline();count=len(outline)
    loops=[(.97,0),(1.0,.005),(1.0,SOLE_TOP-.003),(.965,SOLE_TOP)]
    soleverts=[(center+x*k,(y+.070)*k-.070,z) for k,z in loops for x,y in outline]
    faces=[tuple(reversed(range(count))),tuple(range(3*count,4*count))]
    faces+=[(r*count+i,r*count+(i+1)%count,(r+1)*count+(i+1)%count,(r+1)*count+i) for r in range(3) for i in range(count)]
    mesh('SlipperSole.'+side,soleverts,faces,m['sole'],'Foot.'+side)
    # Closed domed upper: puffy arches from sole edge to sole edge, from the
    # swollen toe cap over the instep and ankle to a closed heel cap.
    arch=9;verts=[];base=SOLE_TOP-.0015
    for y,h in UPPER:
        w=max(sole_half_width(y)*1.015,.030)
        for k in range(arch):
            a=math.pi*k/(arch-1);c=math.cos(a);sn=math.sin(a)
            puff=UPPER_PUFF[0] if c*s>0 else UPPER_PUFF[1]
            # puffy: the arch swells past the sole edge just above the base
            x=w*(1+puff*math.sin(min(math.pi,2.2*sn)))*c;z=base+(h-base)*sn**.80
            verts.append((center+x,y,z))
    rows=len(UPPER)
    # Toe/heel caps at the base height keep the base polygon flat (a raised
    # cap tilted it into a dark notch visible from the front).
    toe=len(verts);verts.append((center,UPPER[0][0]-.008,base))
    heel=len(verts);verts.append((center,UPPER[-1][0]+.007,base))
    faces=[]
    for r in range(rows-1):
        faces+=[(r*arch+k,r*arch+k+1,(r+1)*arch+k+1,(r+1)*arch+k) for k in range(arch-1)]
    faces+=[(k+1,k,toe) for k in range(arch-1)]
    last=(rows-1)*arch
    faces+=[(last+k,last+k+1,heel) for k in range(arch-1)]              # closed heel counter
    faces.append(tuple([toe]+[r*arch for r in range(rows)]+[heel]+[(rows-1-r)*arch+arch-1 for r in range(rows)]))  # base
    mesh('SlipperUpper.'+side,verts,faces,m['slipper'],'Foot.'+side)
    # Bare heel and instep block on the sole, entering the upper; round 4 heel
    # ~10 mm lower and rounder. Foot skin and ankle stay on the leg axis.
    leg=s*.125
    sections=[(.061,.031,.020,.012),(.052,.034,.032,.018),(.026,.040,.040,.024),(-.020,.045,.042,.027),(-.085,.043,.042,.024)]
    foot=[]
    for y,zc,hw,hh in sections:
        foot+= [(leg+hw*math.cos(2*math.pi*j/12),y,zc+hh*math.sin(2*math.pi*j/12)) for j in range(12)]
    faces=[tuple(reversed(range(12))),tuple(range(48,60))]
    faces+=[(r*12+j,r*12+(j+1)%12,(r+1)*12+(j+1)%12,(r+1)*12+j) for r in range(4) for j in range(12)]
    mesh('FootSkin.'+side,foot,faces,m['skin'],'Foot.'+side)
    ankle=tube('Ankle.'+side,[(leg,.010,.030),(leg,.009,.080),(leg,.008,.140)],
               [.041,.041,.042],[.038,.038,.040],m['skin'],None,8)
    weighted(ankle,[ankle_weights(v.co.z,side) for v in ankle.data.vertices])
