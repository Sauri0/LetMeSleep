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
Round 8 (art director r6 + integration review r2): cone sleeves opening ~20
deg (arm + 20 mm at the shoulder joint to arm + 42 mm at the hem, 125 mm
long, top ring 20 mm lower inside the shoulder); the T-shirt hem 20 mm lower
with 10 mm more flare and Human_ShirtShade on the side planes under the arms
and on the hem; trouser legs 12% wider from the knee down with a 40 mm cuff;
~58 mm 5-7 sided dots that are skipped rather than shrunk into overlaps; the
forearm flattens into the palm and closes inside it (no end cap showing as a
dark crescent at the wrist).
Round 9 (review r8): the forearm tip takes the palm's flat section over a
narrowed palm wrist end (no tab above the wrist seam); low-relief densified
dots with trouser-blue side walls; pajama hips 10 mm narrower per side; a
Human_PantsShade hem; a crew collar that opens with the thicker neck and
rises at the back; the seat filler recessed behind the leg tops.
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


# Bare arm stations (x from the body axis) with the section half-width (bind
# z) and half-depth (bind y); see clothing(). Round 9: the tip takes the
# palm's flat section up to x .695, a little larger than the narrowed wrist
# end of the palm (build_characters.human), and closes inside it.
ARM_XS=[.275,.330,.400,.470,.520,.570,.620,.665,.695,.712,.728]
ARM_WIDTHS=[.0520,.0518,.0512,.0503,.0495,.0478,.0462,.0450,.0445,.0395,.0220]
ARM_DEPTHS=[.0520,.0518,.0512,.0503,.0495,.0470,.0442,.0395,.0285,.0200,.0120]


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


# Round 9 (review r8: at 3.5 mm proud with a light side wall the dots read as
# pebbles glued on and stood out of the leg's silhouette with light/grey
# rims): a low relief, 2.2 mm at the centre and 1.6 mm at the rim. To keep
# the leg's facet creases from poking through at that height the dot is
# densified (every outline edge in DOT_EDGE_SPLITS, two inner rings) and each
# vertex sits on the outer of the two possible triangulations of the leg quad
# under it. The side wall takes the trouser material (blue, tinted with the
# pajama colour), and there is no bottom face (the wall ends inside the leg).
DOT_LIFT_CENTRE,DOT_LIFT_RIM,DOT_WALL_DEPTH=.0022,.0016,.0015
DOT_EDGE_SPLITS=3


def dots(mesh, leg, rings_z, side, material, sides=12, wall_material=None):
    """Light irregular polka dots (UI-06 screen 5), no texture.

    5-7 sided faceted patches with random rotation and 0.85-1.15 scale, placed
    on a jittered spiral (no column alignment). Every vertex is projected onto
    the actual trouser facets and lifted (round 9: 1.6-2.2 mm), with densified
    rings so the patch folds over facet creases instead of floating. Each
    vertex carries the trouser weights of its own position so it deforms with
    the leg.
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
        # Round 9: the leg quads are not planar; the exporter/importer may
        # split either diagonal. Sit on the outer of the two triangulations.
        a=p00+(p01-p00)*u+(p11-p01)*t if u>=t else p00+(p11-p10)*u+(p10-p00)*t
        b=p00+(p01-p00)*u+(p10-p00)*t if u+t<=1 else p11+(p10-p11)*(1-u)+(p01-p11)*(1-t)
        point=point+normal*max((a-point).dot(normal),(b-point).dot(normal))
        return point,normal,(point-center).xy.length

    seed=1.7 if side=='L' else 5.3
    placed=[]
    # Round 5: diameter +60% (35-40 mm, ~22% of the leg width), spread all
    # round the leg. Each row turns by the golden angle so no columns line up.
    # Round 6: diameter +25% (~45 mm), 23 per leg: seven rows up to the hip,
    # plus two dots on the back of the seat (UI-06 screen 5; the back view
    # was empty above mid-thigh).
    # Round 8 (art director r6): ~58 mm dots (radius .0285) with 5-7 sides; the
    # lowest row sits 15 mm higher so no dot runs under the rolled cuff, and a
    # dot that cannot keep 10 mm from its neighbours at >= 72% of its size is
    # left out instead of shrunk into an overlapping sliver.
    rows=[(.178,3,'all'),(.262,4,'all'),(.350,3,'all'),(.428,4,'all'),(.508,3,'all'),(.584,3,'all'),
          (.662,3,'front'),(.716,2,'back')]
    for row,(z0,per_row,zone) in enumerate(rows):
        base=row*2.39996+seed
        for k in range(per_row):
            a0=base+k*2*math.pi/per_row+(_hash(row,k,seed,1)-.5)*.9
            z=z0+(_hash(row,k,seed,2)-.5)*.024
            radius=.0285*(.92+.16*_hash(row,k,seed,3))
            if zone=='back':
                # Tube frame: angle 3pi/2 is +Y (back). These sit on the
                # Hips-weighted leg tops over the seat, which stay above the
                # menu cushion; the mixed hip/thigh band (z .60-.70) behind
                # the thigh does not (it rests on the cushion).
                # Round 9: the pair turns 0.15 rad toward the outside of the
                # hip (smaller tube angles on the left leg, larger on the
                # right), so the crotch-side dot no longer wraps into the V
                # between the legs where the seat filler shows.
                a0=1.5*math.pi+(k-.5)*.80+(-.15 if side=='L' else .15)+(_hash(row,k,seed,1)-.5)*.2
                z=z0+(_hash(row,k,seed,2)-.5)*.008
                # Round 8: the seat dots keep the round-6 size (~45 mm): a
                # 58 mm dot reaches down into the mixed hip/thigh band.
                radius=.0222*(.92+.16*_hash(row,k,seed,3))
            point,_,leg_radius=surface(a0,z)
            # Front row near the hip: front/sides only (see above).
            # Round 8: the larger dots wrap further round the thigh, so the
            # near-hip row keeps its centres on the front half (y < -.035).
            if zone=='front' and point.y>-.035:continue
            wanted=radius
            for other_a,other_z,other_r,_ in placed:
                arc=((a0-other_a+math.pi)%(2*math.pi)-math.pi)*leg_radius
                distance=math.hypot(arc,z-other_z)
                if distance<radius+other_r+.010:radius=distance-other_r-.010
            if radius<.72*wanted:continue
            placed.append((a0,z,radius,leg_radius))
    verts=[];faces=[];walls=[]
    for index,(a0,z0,radius,leg_radius) in enumerate(placed):
        count=5+min(2,int(_hash(index,seed,7,0)*3))  # round 8: 5-7 sides
        spin=_hash(index,seed,8,0)*2*math.pi
        corners=[]
        for q in range(count):
            angle=spin+(q+(_hash(index,q,seed,9)-.5)*.45)*2*math.pi/count
            reach=radius*(.84+.30*_hash(index,q,seed,10))
            corners.append((reach*math.cos(angle),reach*math.sin(angle)))
        # Round 9: straight outline edges split in DOT_EDGE_SPLITS (the polygon
        # keeps its 5-7 corners), two inner rings at 1/3 and 2/3.
        outline=[(ax+(bx-ax)*k/DOT_EDGE_SPLITS,ay+(by-ay)*k/DOT_EDGE_SPLITS)
                 for (ax,ay),(bx,by) in zip(corners,corners[1:]+corners[:1]) for k in range(DOT_EDGE_SPLITS)]
        n_out=len(outline)
        rings=[[(x*f,y*f) for x,y in outline] for f in (1/3,2/3,1.0)]
        # Back of the thigh (seated on the menu cushion): flatter dot (~1.3 mm
        # proud at the centre, as before).
        _,centre_normal,_=surface(a0,z0)
        scale=(.36*.0035/DOT_LIFT_CENTRE) if centre_normal.y>.5 and z0>.52 else 1.0
        def put(x,y,lift):
            point,normal,_=surface(a0+x/leg_radius,z0+y)
            verts.append(tuple(point+normal*lift*scale));return len(verts)-1
        centre=put(0.0,0.0,DOT_LIFT_CENTRE)
        lifts=(DOT_LIFT_CENTRE,DOT_LIFT_CENTRE+(DOT_LIFT_RIM-DOT_LIFT_CENTRE)*.5,DOT_LIFT_RIM)
        ids=[[put(x,y,lift) for x,y in ring] for ring,lift in zip(rings,lifts)]
        bottom=[put(x,y,-DOT_WALL_DEPTH) for x,y in rings[-1]]
        for q in range(n_out):
            n=(q+1)%n_out
            faces.append((centre,ids[0][q],ids[0][n]))
            for inner,outer in ((ids[0],ids[1]),(ids[1],ids[2])):
                faces.append((inner[q],outer[q],outer[n],inner[n]))
            faces.append((ids[2][q],bottom[q],bottom[n],ids[2][n]))
            walls.append(len(faces)-1)
    obj=mesh('PajamaDots.'+side,verts,faces,material)
    if wall_material is not None:
        obj.data.materials.append(wall_material)
        for index in walls:obj.data.polygons[index].material_index=1
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
    # Round 8 (art director r6): the close sleeve read as a square shoulder pad.
    # Now a cone that opens ~20 deg: the top ring sits 20 mm lower inside the
    # torso shoulder, the ring radius grows from arm + 20 mm at the shoulder
    # joint to arm + 42 mm at the hem, 125 mm along UpperArm (x .250 -> .375).
    outer=[(.190,.080,.040,-.020),(.222,.072,.054,-.010),(.250,.072,.062,-.004),(.290,.077,.070,-.002),
           (.330,.084,.079,-.001),(.375,.093,.090,0)]
    rings=[section(*row) for row in outer]+[section(.370,.086,.083),section(.356,.082,.079)]
    if sign<0:rings=[list(reversed(r)) for r in rings]
    sleeve=loft_rings(mesh,'Sleeve.'+side,rings,m['shirt'])
    assign(sleeve,[sleeve_weights(v.co,side) for v in sleeve.data.vertices])
    facet(sleeve,(.0015,.0025),lambda co:(co.x,0,arm_z(abs(co.x))),lambda co:.200<abs(co.x)<.365,7.3+sign)
    # Bare arm. Round 6: straight 8-sided prisms, the upper arm radius -10%
    # (58 -> 52 mm) with no biceps/forearm bulge, tapering evenly from the
    # elbow to the wrist, whose end enters the (longer) palm.
    # Round 8 (review r2, wrist seam): the flat end cap of the forearm stood
    # ~6 mm proud of the thinner welded palm (HandSkin is its own mesh) and
    # showed as a dark crescent at the wrist. The last stations now flatten
    # into the palm and close to a small tip inside it, so only the plain
    # intersection of two lit surfaces remains.
    # Round 9 (review r8: a flat tab of the palm stood above the zigzag seam on
    # the thumb side, and thin seam lines showed in profile): the palm (.0425
    # wide, ~.025 deep at its wrist end) was wider than the round forearm tip,
    # so its flat edges emerged ~20 mm before its faces did. The forearm tip now
    # takes the palm's flat section up to x .695 over a narrowed palm wrist end
    # and then closes inside it: the palm emerges all round within x .70-.712,
    # a single ring-like seam (no tab, no sliver).
    arm=tube('Arm.'+side,[(sign*x,0,arm_z(x)) for x in ARM_XS],ARM_WIDTHS,ARM_DEPTHS,m['skin'],sides=8)
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
    # Round 9 (art director r6 / review r8): the pajama hips lose 10 mm per
    # side (the leg tops keep meeting at the centre: centre and half-width
    # each -5 mm from z .60 up), keeping the gap to the hanging hands.
    rings=[(.100,0,.122),(.200,0,.122),(.32,0,.122),(.375,0,.121),(.410,-.002,.120),(.440,-.004,.119),
           (.470,-.002,.117),(.505,0,.114),(.60,0,.101),(.70,0,.091),(.78,0,.086)]
    # Round 8 (art director r6, UI-06 screen 5): 12% wider from the knee down
    # (width only: the seated thigh depth over the menu cushion is unchanged).
    leg=tube('PajamaLeg.'+side,[(sign*x,y,z) for z,y,x in rings],
        [.0896,.0896,.0896,.0896,.0907,.0918,.0918,.0875,.076,.079,.082],
        [.076,.076,.077,.078,.080,.084,.082,.080,.078,.075,.074],m['pajamas'],sides=12)
    assign(leg,[leg_weights(v.co,side) for v in leg.data.vertices])
    dots(mesh,leg,[z for z,y,x in rings],side,m['dots'],wall_material=m['pajamas'])
    # Rolled cuff: 9 mm proud of the leg, bottom edge at 70 mm. Round 8: a
    # 40 mm hem (was 65) on the 12% wider leg.
    # Round 9 (art director r6, UI-06 screen 5): the hem is the darker
    # Human_PantsShade #23407E (tinted with the pajama colour in Unity).
    cuff=tube('TrouserCuff.'+side,[(sign*.122,0,z) for z in [.070,.075,.105,.110]],
              [.094,.100,.100,.095],[.080,.085,.085,.081],m.get('pantsshade',m['pajamas']),'LowerLeg.'+side,12)
    return cuff


def hip_block(mesh, m):
    """Round 4 pajama seat: a boxy filler recessed behind the leg tops (which
    now meet at the centre), flush with their backs, so the crotch no longer
    shows a dark recessed panel. It follows Hips like the round-3 seat."""
    count=16
    # Round 9: its back (y .074-.076) stood 1-2 mm behind the leg tops' backs
    # (.074-.075) and hid the inner seat dots near the crotch; it is recessed
    # 4 mm behind them now (back at y .070).
    sections=[(.672,.030,.040,.010),(.688,.052,.050,.012),(.702,.110,.058,.012),
              (.716,.150,.058,.012),(.745,.160,.058,.012),(.775,.160,.058,.012)]
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
    # Round 8 (art director r6): the hem hangs 20 mm lower with 10 mm more
    # flare (a loose pajama T), and Human_ShirtShade #C4A983 covers the side
    # planes under the arms and the hem band: the faceted folds of PER-04.
    sections=[(.792,.180,.093),(.905,.182,.100),(1.035,.192,.112),(1.150,.200,.115),
              (1.188,.200,.112),(1.205,.197,.108),(1.2167,.188,.104),(1.2234,.170,.100),
              (1.2326,.140,.099),(1.243,.108,.097)]
    count=12
    rings=[boxy(.176,.088,count,.786)]+[boxy(.186,.098,count,.756)]
    rings+=[boxy(a,b,count,z,3.2 if z<1.20 else 2.6) for z,a,b in sections]
    shirt=loft_rings(mesh,'Shirt',rings,m['shirt'])
    def body_weights(position):
        z=position[2]
        wh=1-max(0,min(1,(z-.79)/.15));wc=max(0,min(1,(z-1.02)/.13))
        return {name:value for name,value in {'Hips':wh,'Spine':1-wh-wc,'Chest':wc}.items() if value>0}
    assign(shirt,[body_weights(v.co) for v in shirt.data.vertices])
    facet(shirt,(.002,.003),lambda co:(0,0,co.z),lambda co:.85<co.z<1.19,3.1)
    if 'shirtshade' in m:
        shirt.data.materials.append(m['shirtshade'])
        for polygon in shirt.data.polygons:
            z=polygon.center.z;n=polygon.normal
            side=abs(n.x)>.80 and .86<z<1.13
            hem=z<.800
            if side or hem:polygon.material_index=1
    # Elastic pajama waistband under the hem: flat front almost flush with the
    # hem so it is not a dark shadowed belt (<=5% darker than the trousers).
    count=16
    # Round 9: 10 mm narrower per side with the pajama hips.
    waist=loft_rings(mesh,'Waistband',[boxy(.160,.074,count,.748,3.0,.004),boxy(.168,.082,count,.751,3.0,.003),
                                        boxy(.168,.084,count,.782,3.0,.003)],m['pajamas'])
    assign(waist,[{'Hips':1.0} for _ in waist.data.vertices])
    count=14
    ellipse=lambda rx,ry,z,yc=.010:[(rx*math.cos(2*math.pi*j/count),yc+ry*math.sin(2*math.pi*j/count),z) for j in range(count)]
    # Round 5: wider crew collar around the thicker neck.
    # Round 9: the neck is ~20% thicker and 5 mm further back; the collar
    # opens with it (still inside the shirt's neck ring, x .106).
    collar=[ellipse(.106,.096,1.231,.014),ellipse(.104,.094,1.252,.014),ellipse(.095,.085,1.252,.014),ellipse(.097,.087,1.231,.014)]
    # The crew neck rises toward the back (+14 mm at the nape), which also
    # shortens the neck seen from the side and behind.
    for loop in collar[1:3]:
        loop[:]=[(x,y,z+.014*max(0.0,(y-.014)/.094)) for x,y,z in loop]
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
