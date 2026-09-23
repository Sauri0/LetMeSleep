"""Human-only joint topology and cloth weighting; stable 65-bone contract.
v0.3.0: T-shirt, bare arms, dotted pajama trousers (sketch PER-04/UI-06).
Round 2: broader shirt shoulders, slimmer sleeves over thicker forearms,
irregular jittered pajama dots.
Round 3: short square T (hem 86 mm higher, hem as wide as the chest, no waist
taper, collar 30 mm lower), a 30 mm pajama waistband showing ~0.10 m of
trouser hip, straight trouser legs 15% wider and a 65 mm rolled cuff that ends
70 mm above the floor (bare ankle).
Round 4: square T built from rounded-rectangle sections (shoulders 0.43 m,
hem 0.35 m, hem folded in instead of a belt ring) with 6-10 mm facet jitter
and triangulated facets; short wide sleeves ending ~135 mm past the shoulder
with a fused, folded-in cuff; trouser legs that meet under the waistband over
a recessed seat filler (no dark crotch panel); sparser, smaller #D8E0F2 dots
(26 per leg plus 3 per leg on the front of the hips).
Round 5: arms and forearms 30% thicker; shirt shoulder corners 15 mm lower
with a sleeve top that slopes down from the collar (no shoulder-pad peaks);
the T is 12 columns x 7 rings of large planes with a 2-3 mm fold jitter
(PER-04/UI-06 screen 5); 16-18 larger (35-40 mm) irregular 6-7 sided
#C9D5EE dots per leg, including the back of the thighs; a softer knee and a
gradual thigh convergence (no horizontal shading band).
Round 6: close-fitting sleeves (ring radius = arm + 15 mm, 115 mm along the
UpperArm axis, 100% UpperArm except the top ring blended with Chest) under a
shoulder line sloping ~17 deg from the collar, and a slimmer chest, so the
idle arms no longer carry T-shaped sleeve wings; straight 8-sided arm prisms
tapering to the wrist (upper arm radius -10%); 45 mm cream #E6DCC8 dots, 23
per leg including the back of the thighs and the seat.
"""
import math
from mathutils import Vector


def ramp(value, start, end):
    t=max(0.0,min(1.0,(value-start)/(end-start)))
    return t*t*(3-2*t)


def assign(obj, weights):
    groups={name:obj.vertex_groups.new(name=name) for name in sorted({n for row in weights for n in row})}
    for vertex,row in zip(obj.data.vertices,weights):
        total=sum(row.values())
        assert total>0 and len(row)<=4
        for name,value in row.items():
            if value>0:groups[name].add([vertex.index],value/total,'REPLACE')


def wrist_weights(position, side, finger_weights):
    # The welded skin extends under the sleeve. Roll/flexion is distributed
    # over this bridge instead of separating a rigid palm from a rigid cuff.
    hand=ramp(abs(position[0]),.710,.778)
    result={name:value*hand for name,value in finger_weights.items() if value*hand>0}
    if hand<1:result['LowerArm.'+side]=1-hand
    strongest=sorted(result.items(),key=lambda row:row[1],reverse=True)[:4]
    total=sum(value for _,value in strongest)
    return {name:value/total for name,value in strongest}


# Round 6: the sleeve and the bare arm follow UpperArm rigidly from the
# shoulder joint (x=.25) outward; only the sleeve's top ring (inside the
# torso shoulder) blends with Chest. The round-5 wide Chest blend kept the
# sleeve top horizontal when the arm hung down (T-shaped wings). Inboard of
# the joint a vertex that follows the arm rises and swings out; keeping the
# top ring on Chest lets the shoulder fall straight from the torso corner to
# the sleeve's outer top (~17 deg in the idle).
SHOULDER_BLEND=(.212,.250)


def sleeve_weights(position, side):
    x=abs(position[0])
    chest=1-ramp(x,*SHOULDER_BLEND)
    return {name:value for name,value in {
        'Chest':chest,'UpperArm.'+side:1-chest}.items() if value>0}


def arm_weights(position, side):
    # Bare arm skin (T-shirt): same shoulder blend as the sleeve and the
    # same wrist ramp as the welded hand so both surfaces bend together.
    x=abs(position[0])
    chest=1-ramp(x,*SHOULDER_BLEND)
    lower=ramp(x,.458,.582)
    hand=ramp(x,.710,.778)
    return {name:value for name,value in {
        'Chest':chest,
        'UpperArm.'+side:(1-chest)*(1-lower),
        'LowerArm.'+side:(1-chest)*lower*(1-hand),
        'Hand.'+side:(1-chest)*lower*hand}.items() if value>0}


def leg_weights(position, side):
    z=position[2]
    upper=ramp(z,.355,.525)
    # Native menu audit located back-of-thigh intrusions on the upper ring:
    # retain it with the pelvis so bending the thigh cannot pull that cloth
    # into the cushion.
    hips=ramp(z,.64,.745)
    return {name:value for name,value in {
        'Hips':hips,'UpperLeg.'+side:upper*(1-hips),
        'LowerLeg.'+side:(1-upper)*(1-hips)}.items() if value>0}


def arm_z(x):
    return 1.17-.01*(x-.25)/.27 if x<.52 else 1.16-.01*(x-.52)/.23


def _hash(*values):
    """Deterministic pseudo random in [0,1) (reproducible source generation)."""
    return math.sin(sum(v*k for v,k in zip(values,(12.9898,78.233,37.719,4.581)))*43758.5453)%1


def boxy(a, b, count, z, n=3.2, yc=0.0):
    """Rounded-rectangle (superellipse) section: flat front/back/sides with
    rounded corners, so the T-shirt reads square instead of a tube."""
    pts=[]
    for j in range(count):
        t=2*math.pi*(j+.5)/count
        c=math.cos(t);s=math.sin(t)
        pts.append((a*math.copysign(abs(c)**(2/n),c),yc+b*math.copysign(abs(s)**(2/n),s),z))
    return pts


def loft_rings(mesh, name, rings, material, caps=(True,True)):
    count=len(rings[0]);verts=[p for ring in rings for p in ring];faces=[]
    if caps[0]:faces.append(tuple(reversed(range(count))))
    for r in range(len(rings)-1):
        faces+=[(r*count+j,r*count+(j+1)%count,(r+1)*count+(j+1)%count,(r+1)*count+j) for j in range(count)]
    if caps[1]:faces.append(tuple((len(rings)-1)*count+j for j in range(count)))
    return mesh(name,verts,faces,material)


def facet(obj, amount, axis, movable, seed):
    """Low-poly facets (round 5: 2-3 mm): deterministic displacement of interior
    vertices away from/toward the section axis, then a triangulation so every
    triangle keeps its own flat normal (PER-04 shirt facets). Only vertices
    that `movable(co)` accepts move; skin weights are unaffected."""
    import bmesh
    for vertex in obj.data.vertices:
        co=vertex.co
        if not movable(co):continue
        centre=axis(co);radial=Vector((co.x-centre[0],co.y-centre[1],co.z-centre[2]))
        if radial.length<1e-6:continue
        radial.normalize()
        key=(round(co.x,4),round(co.y,4),round(co.z,4))
        magnitude=amount[0]+(amount[1]-amount[0])*_hash(*key,seed)
        sign=1 if _hash(key[1],key[2],key[0],seed+1)>.45 else -1
        vertex.co=co+radial*magnitude*sign
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm,faces=bm.faces[:],quad_method='ALTERNATE',ngon_method='BEAUTY')
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    return obj


def dots(mesh, leg, rings_z, side, material, sides=12):
    """Light irregular polka dots (UI-06 screen 5), no texture.

    6-7 sided faceted patches with random rotation and 0.85-1.15 scale, placed
    on a jittered spiral (no column alignment). Every vertex is projected onto
    the actual trouser facets and lifted 2.5 mm, with a mid ring so the patch
    folds over facet creases instead of floating. Each vertex carries the
    trouser weights of its own position so it deforms with the leg.
    """
    points=[Vector(v.co) for v in leg.data.vertices]
    P=lambda i,j:points[i*sides+j%sides]
    ring_centers=[sum((P(i,q) for q in range(sides)),Vector())/sides for i in range(len(rings_z))]

    def surface(angle,z):
        i=max(n for n,value in enumerate(rings_z[:-1]) if value<=z)
        t=(z-rings_z[i])/(rings_z[i+1]-rings_z[i])
        f=(angle/(2*math.pi))%1*sides;j=int(f);u=f-j
        p00,p01,p10,p11=P(i,j),P(i,j+1),P(i+1,j),P(i+1,j+1)
        low=p00.lerp(p01,u);high=p10.lerp(p11,u)
        point=low.lerp(high,t)
        normal=((p01-p00).lerp(p11-p10,t)).cross(high-low).normalized()
        center=ring_centers[i].lerp(ring_centers[i+1],t)
        if normal.dot(point-center)<0:normal=-normal
        return point,normal,(point-center).xy.length

    seed=1.7 if side=='L' else 5.3
    placed=[]
    # Round 5: diameter +60% (35-40 mm, ~22% of the leg width), spread all
    # round the leg. Each row turns by the golden angle so no columns line up.
    # Round 6: diameter +25% (~45 mm), 23 per leg: seven rows up to the hip,
    # plus two dots on the back of the seat (UI-06 screen 5; the back view
    # was empty above mid-thigh).
    rows=[(.180,3,'all'),(.262,4,'all'),(.344,3,'all'),(.426,4,'all'),(.508,3,'all'),(.582,3,'all'),
          (.662,3,'front'),(.716,2,'back')]
    for row,(z0,per_row,zone) in enumerate(rows):
        base=row*2.39996+seed
        for k in range(per_row):
            a0=base+k*2*math.pi/per_row+(_hash(row,k,seed,1)-.5)*.9
            z=z0+(_hash(row,k,seed,2)-.5)*.030
            radius=.0222*(.92+.16*_hash(row,k,seed,3))
            if zone=='back':
                # Tube frame: angle 3pi/2 is +Y (back). These sit on the
                # Hips-weighted leg tops over the seat, which stay above the
                # menu cushion; the mixed hip/thigh band (z .60-.70) behind
                # the thigh does not (it rests on the cushion).
                a0=1.5*math.pi+(k-.5)*.80+(_hash(row,k,seed,1)-.5)*.2
                z=z0+(_hash(row,k,seed,2)-.5)*.008
            point,_,leg_radius=surface(a0,z)
            # Front row near the hip: front/sides only (see above).
            if zone=='front' and point.y>-.01:continue
            for other_a,other_z,other_r,_ in placed:
                arc=((a0-other_a+math.pi)%(2*math.pi)-math.pi)*leg_radius
                distance=math.hypot(arc,z-other_z)
                if distance<radius+other_r+.010:radius=max(.015,distance-other_r-.010)
            placed.append((a0,z,radius,leg_radius))
    verts=[];faces=[]
    for index,(a0,z0,radius,leg_radius) in enumerate(placed):
        count=6+int(_hash(index,seed,7,0)*2)
        spin=_hash(index,seed,8,0)*2*math.pi
        outline=[]
        for q in range(count):
            angle=spin+(q+(_hash(index,q,seed,9)-.5)*.45)*2*math.pi/count
            reach=radius*(.84+.30*_hash(index,q,seed,10))
            outline.append((reach*math.cos(angle),reach*math.sin(angle)))
        local=[(0.0,0.0)]+[(x*.5,y*.5) for x,y in outline]+outline
        base=len(verts)
        for lift in (.0025,-.0020):
            for x,y in local:
                point,normal,_=surface(a0+x/leg_radius,z0+y)
                # Back of the thigh (seated on the menu cushion): flatter dot.
                scale=.5 if normal.y>.5 and z0>.52 else 1.0
                verts.append(tuple(point+normal*lift*scale))
        layer=len(local);mid=1;out=1+count
        for q in range(count):
            n=(q+1)%count
            faces.append((base,base+mid+q,base+mid+n))
            faces.append((base+mid+q,base+out+q,base+out+n,base+mid+n))
            faces.append((base+layer,base+layer+mid+n,base+layer+mid+q))
            faces.append((base+layer+mid+q,base+layer+mid+n,base+layer+out+n,base+layer+out+q))
            faces.append((base+out+q,base+layer+out+q,base+layer+out+n,base+out+n))
    obj=mesh('PajamaDots.'+side,verts,faces,material)
    assign(obj,[leg_weights(v.co,side) for v in obj.data.vertices])
    return obj


def clothing(mesh, tube, side, sign, m):
    # Short T-shirt sleeve (15% slimmer than round 1) with a slightly belled
    # opening; the thicker bare arm runs through it.
    # Round 4 sleeve: ends ~135 mm past the 0.43 m shoulder (x=.352) with a
    # wide opening (~1.8x the bare arm) whose hem is folded into the sleeve
    # itself (inner lip + closing ring), not a separate bracelet band.
    count=12
    def section(x,a,b,dz=0.0,n=2.2):
        z0=arm_z(x)+dz;pts=[]
        for j in range(count):
            t=2*math.pi*(j+.5)/count;c=math.cos(t);s=math.sin(t)
            pts.append((sign*x,a*math.copysign(abs(c)**(2/n),c),z0+b*math.copysign(abs(s)**(2/n),s)))
        return pts
    # Round 6 (art director): a close-fitting short sleeve around the upper
    # arm. The top ring sits inside the torso shoulder (under its ~17 deg
    # slope) and blends with Chest; from the shoulder joint the ring radius is
    # the arm radius + 15 mm (.052 + .015) and the hem is 115 mm along the
    # UpperArm axis (x .250 -> .365), folded in by a short inner lip.
    outer=[(.190,.082,.046,-.010),(.222,.072,.061,-.003),(.250,.067,.067,0),(.290,.067,.067,0),
           (.330,.0665,.0665,0),(.365,.066,.066,0)]
    rings=[section(*row) for row in outer]+[section(.360,.059,.059),section(.346,.057,.057)]
    if sign<0:rings=[list(reversed(r)) for r in rings]
    sleeve=loft_rings(mesh,'Sleeve.'+side,rings,m['shirt'])
    assign(sleeve,[sleeve_weights(v.co,side) for v in sleeve.data.vertices])
    facet(sleeve,(.0015,.0025),lambda co:(co.x,0,arm_z(abs(co.x))),lambda co:.200<abs(co.x)<.355,7.3+sign)
    # Bare arm. Round 6: straight 8-sided prisms, the upper arm radius -10%
    # (58 -> 52 mm) with no biceps/forearm bulge, tapering evenly from the
    # elbow to the wrist, whose end enters the (longer) palm.
    xs=[.275,.330,.400,.470,.520,.570,.620,.665,.695,.715]
    arm=tube('Arm.'+side,[(sign*x,0,arm_z(x)) for x in xs],
             [.0520,.0518,.0512,.0503,.0495,.0478,.0458,.0432,.0400,.0370],
             [.0520,.0518,.0512,.0503,.0495,.0470,.0442,.0408,.0362,.0322],m['skin'],sides=8)
    assign(arm,[arm_weights(v.co,side) for v in arm.data.vertices])
    # Dotted pajama trousers (UI-06 screen 5): straight legs 15% wider than
    # round 2 (depth unchanged so the seated thigh keeps clear of the cushion),
    # a patella plane, and a thick rolled cuff 70 mm above the floor.
    # Legs converge slightly under the T-shirt: centre x moves toward the seat.
    # Round 4: the leg tops widen inward and meet under the waistband, so the
    # hips read as one pajama surface with a front seam instead of a separate
    # crotch panel.
    # Round 5: softer patella and a gradual convergence from the knee up, so
    # the thigh has no horizontal shading band.
    rings=[(.100,0,.122),(.200,0,.122),(.32,0,.122),(.375,0,.121),(.410,-.002,.120),(.440,-.004,.119),
           (.470,-.002,.117),(.505,0,.114),(.60,0,.106),(.70,0,.096),(.78,0,.091)]
    leg=tube('PajamaLeg.'+side,[(sign*x,y,z) for z,y,x in rings],
        [.080,.080,.080,.080,.081,.082,.082,.081,.081,.084,.087],
        [.076,.076,.077,.078,.080,.084,.082,.080,.078,.075,.074],m['pajamas'],sides=12)
    assign(leg,[leg_weights(v.co,side) for v in leg.data.vertices])
    dots(mesh,leg,[z for z,y,x in rings],side,m['dots'])
    # Rolled cuff: 65 mm tall, 9 mm proud of the leg, bottom edge at 70 mm.
    cuff=tube('TrouserCuff.'+side,[(sign*.122,0,z) for z in [.070,.075,.130,.135]],
              [.084,.089,.089,.085],[.080,.085,.085,.081],m['pajamas'],'LowerLeg.'+side,12)
    return cuff


def hip_block(mesh, m):
    """Round 4 pajama seat: a boxy filler recessed behind the leg tops (which
    now meet at the centre), flush with their backs, so the crotch no longer
    shows a dark recessed panel. It follows Hips like the round-3 seat."""
    count=16
    sections=[(.672,.030,.040,.010),(.688,.052,.050,.012),(.702,.110,.058,.012),
              (.716,.150,.062,.012),(.745,.160,.064,.012),(.775,.160,.064,.012)]
    block=loft_rings(mesh,'TrouserSeat',[boxy(a,b,count,z,3.0,yc) for z,a,b,yc in sections],m['pajamas'])
    assign(block,[{'Hips':1.0} for _ in block.data.vertices])
    return block


def torso(mesh, tube, m):
    """Cream T-shirt (PER-04 BASE CHARACTER): a short square T whose hem is
    as wide as the chest (no waist taper, no flare), ending ~0.10 m above the
    crotch over a visible pajama waistband; low crew collar 30 mm lower so the
    neck shows, and flat shoulders into the short sleeves."""
    # Round 4: square T. Rounded-rectangle sections, shoulders 0.43 m wide
    # tapering to a 0.35 m hem; the hem is folded into the body (inner lip)
    # instead of a separate belt-like ring, and interior vertices carry a
    # 6-10 mm facet jitter.
    # Round 5: 12 columns x 7 rings of large planes (4-6 at the front), the
    # shoulder corners 15 mm lower so the shoulders slope into the sleeves,
    # and a 2-3 mm fold jitter instead of the crumpled 6-10 mm one.
    # Round 6: slimmer chest (.212 -> .200) and a straight shoulder line
    # falling ~17 deg from the collar (x .106, z 1.243) to the shoulder corner
    # (x .198, z 1.200), where the close-fitting sleeve takes over.
    sections=[(.792,.177,.090),(.905,.182,.100),(1.035,.192,.112),(1.150,.200,.115),
              (1.188,.200,.112),(1.205,.197,.108),(1.2167,.188,.104),(1.2234,.170,.100),
              (1.2326,.140,.095),(1.243,.106,.089)]
    count=12
    rings=[boxy(.168,.080,count,.806)]+[boxy(.176,.088,count,.776)]
    rings+=[boxy(a,b,count,z,3.2 if z<1.20 else 2.6) for z,a,b in sections]
    shirt=loft_rings(mesh,'Shirt',rings,m['shirt'])
    def body_weights(position):
        z=position[2]
        wh=1-max(0,min(1,(z-.79)/.15));wc=max(0,min(1,(z-1.02)/.13))
        return {name:value for name,value in {'Hips':wh,'Spine':1-wh-wc,'Chest':wc}.items() if value>0}
    assign(shirt,[body_weights(v.co) for v in shirt.data.vertices])
    facet(shirt,(.002,.003),lambda co:(0,0,co.z),lambda co:.85<co.z<1.19,3.1)
    # Elastic pajama waistband under the hem: flat front almost flush with the
    # hem so it is not a dark shadowed belt (<=5% darker than the trousers).
    count=16
    waist=loft_rings(mesh,'Waistband',[boxy(.170,.074,count,.748,3.0,.004),boxy(.178,.082,count,.751,3.0,.003),
                                        boxy(.178,.084,count,.782,3.0,.003)],m['pajamas'])
    assign(waist,[{'Hips':1.0} for _ in waist.data.vertices])
    count=14
    ellipse=lambda rx,ry,z:[(rx*math.cos(2*math.pi*j/count),.010+ry*math.sin(2*math.pi*j/count),z) for j in range(count)]
    # Round 5: wider crew collar around the thicker neck.
    collar=[ellipse(.097,.086,1.231),ellipse(.094,.083,1.250),ellipse(.083,.075,1.250),ellipse(.085,.076,1.231)]
    obj=mesh('ShirtCollar',[p for loop in collar for p in loop],
             [(r*count+j,r*count+(j+1)%count,((r+1)%4)*count+(j+1)%count,((r+1)%4)*count+j)
              for r in range(4) for j in range(count)],m['shirt'])
    assign(obj,[{'Chest':1.0} for _ in obj.data.vertices])
    return shirt


def align_menu_grip(p, side, tilt):
    """Follow the forearm, keeping the handle as upright as that bend allows."""
    from mathutils import Vector,Matrix
    lower=p.rig.pose.bones['LowerArm.'+side]
    forward=lower.tail-lower.head
    forward.normalize()
    vertical=Vector((0,0,1 if side=='R' else -1))
    vertical-=forward*vertical.dot(forward)
    assert vertical.length>.1,'Unstable vertical forearm needs a different elbow target'
    vertical.normalize()
    normal=vertical.cross(forward)
    rotation=Matrix((vertical,forward,normal)).transposed().to_4x4()
    rotation=Matrix.Rotation(tilt,4,'X')@rotation
    # Distribute pronation through the lower arm as well as the wrist skin.
    # Lower-arm direction and endpoint are preserved by this roll change.
    origin=lower.head.copy();direction=lower.tail-origin
    p.point(lower.name,origin,direction,rotation.to_3x3()@Vector((0,0,1)))
    hand=p.rig.pose.bones['Hand.'+side]
    rotation.translation=lower.tail
    hand.matrix=rotation;p.update()
