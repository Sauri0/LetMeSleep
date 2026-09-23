"""Human-only joint topology and cloth weighting; stable 65-bone contract.
v0.3.0: T-shirt, bare arms, dotted pajama trousers (sketch PER-04/UI-06).
Round 2: broader shirt shoulders, slimmer sleeves over thicker forearms,
irregular jittered pajama dots.
Round 3: short square T (hem 86 mm higher, hem as wide as the chest, no waist
taper, collar 30 mm lower), a 30 mm pajama waistband showing ~0.10 m of
trouser hip, straight trouser legs 15% wider and a 65 mm rolled cuff that ends
70 mm above the floor (bare ankle).
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


def sleeve_weights(position, side):
    x=abs(position[0])
    lower=ramp(x,.458,.582)
    # v0.3.0 short sleeve: the shoulder blend spans several rings so lowering
    # the arm does not concentrate the stretch on one edge over the shoulder.
    chest=1-ramp(x,.160,.330)
    hand=.20*ramp(x,.695,.742)
    return {name:value for name,value in {
        'Chest':chest,
        'UpperArm.'+side:(1-chest)*(1-lower),
        'LowerArm.'+side:(1-chest)*lower*(1-hand),
        'Hand.'+side:(1-chest)*lower*hand}.items() if value>0}


def arm_weights(position, side):
    # Bare arm skin (T-shirt): same shoulder/elbow blend as the sleeve and the
    # same wrist ramp as the welded hand so both surfaces bend together.
    x=abs(position[0])
    chest=1-ramp(x,.160,.330)
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


def dots(mesh, leg, rings_z, side, material, sides=12):
    """Light irregular polka dots (UI-06 screen 5), no texture.

    5-7 sided faceted patches with random rotation and 0.7-1.3 scale, placed
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
    for row,z0 in enumerate([.188,.258,.328,.398,.468,.538,.608,.676]):
        base=row*2.39996+seed
        for k in range(5):
            a0=base+k*2*math.pi/5+(_hash(row,k,seed,1)-.5)*.45
            z=z0+(_hash(row,k,seed,2)-.5)*.020
            radius=.0195*(.72+.56*_hash(row,k,seed,3))
            point,_,leg_radius=surface(a0,z)
            # Top row: front/sides only; the back of the thigh sits on the menu cushion.
            if z0>.64 and point.y>-.01:continue
            for other_a,other_z,other_r,_ in placed:
                arc=((a0-other_a+math.pi)%(2*math.pi)-math.pi)*leg_radius
                distance=math.hypot(arc,z-other_z)
                if distance<radius+other_r+.006:radius=max(.012,distance-other_r-.006)
            placed.append((a0,z,radius,leg_radius))
    verts=[];faces=[]
    for index,(a0,z0,radius,leg_radius) in enumerate(placed):
        count=5+int(_hash(index,seed,7,0)*3)
        spin=_hash(index,seed,8,0)*2*math.pi
        outline=[]
        for q in range(count):
            angle=spin+(q+(_hash(index,q,seed,9)-.5)*.45)*2*math.pi/count
            reach=radius*(.80+.34*_hash(index,q,seed,10))
            outline.append((reach*math.cos(angle),reach*math.sin(angle)))
        local=[(0.0,0.0)]+[(x*.5,y*.5) for x,y in outline]+outline
        base=len(verts)
        for lift in (.0025,-.0020):
            for x,y in local:
                point,normal,_=surface(a0+x/leg_radius,z0+y)
                verts.append(tuple(point+normal*lift))
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
    xs=[.120,.170,.210,.250,.290,.340,.380,.400]
    sleeve=tube('Sleeve.'+side,[(sign*x,0,arm_z(x)+.004) for x in xs],
                [.064,.068,.069,.068,.066,.064,.066,.070],[.066,.070,.071,.070,.068,.066,.068,.072],m['shirt'],sides=12)
    assign(sleeve,[sleeve_weights(v.co,side) for v in sleeve.data.vertices])
    # Folded hem band at the belled sleeve opening.
    count=12;z0=arm_z(.395)+.004
    loops=[[(sign*x,-.071*k*math.sin(2*math.pi*j/count),z0+.070*k*math.cos(2*math.pi*j/count)) for j in range(count)]
           for x,k in [(.380,1.0),(.382,1.08),(.406,1.08),(.406,.97)]]
    hem=mesh('SleeveHem.'+side,[p for loop in loops for p in loop],
             [(r*count+j,r*count+(j+1)%count,((r+1)%4)*count+(j+1)%count,((r+1)%4)*count+j)
              for r in range(4) for j in range(count)],m['shirt'])
    assign(hem,[sleeve_weights(v.co,side) for v in hem.data.vertices])
    # Bare arm, forearm ~25% thicker than round 1 (cartoon PER-04 forearms).
    xs=[.260,.330,.380,.440,.500,.545,.590,.640,.685,.712]
    arm=tube('Arm.'+side,[(sign*x,0,arm_z(x)) for x in xs],
             [.047,.046,.045,.044,.042,.043,.044,.041,.036,.037],
             [.048,.047,.046,.045,.043,.044,.044,.039,.032,.033],m['skin'],sides=10)
    assign(arm,[arm_weights(v.co,side) for v in arm.data.vertices])
    # Dotted pajama trousers (UI-06 screen 5): straight legs 15% wider than
    # round 2 (depth unchanged so the seated thigh keeps clear of the cushion),
    # a patella plane, and a thick rolled cuff 70 mm above the floor.
    # Legs converge slightly under the T-shirt: centre x moves toward the seat.
    rings=[(.100,0,.122),(.200,0,.122),(.32,0,.122),(.375,0,.122),(.410,-.003,.122),(.440,-.008,.122),
           (.470,-.003,.121),(.505,0,.118),(.60,0,.112),(.70,0,.106),(.78,0,.104)]
    leg=tube('PajamaLeg.'+side,[(sign*x,y,z) for z,y,x in rings],
        [.080,.080,.080,.080,.081,.082,.082,.081,.078,.074,.072],
        [.076,.076,.077,.078,.082,.090,.085,.081,.078,.072,.072],m['pajamas'],sides=12)
    assign(leg,[leg_weights(v.co,side) for v in leg.data.vertices])
    dots(mesh,leg,[z for z,y,x in rings],side,m['dots'])
    # Rolled cuff: 65 mm tall, 9 mm proud of the leg, bottom edge at 70 mm.
    cuff=tube('TrouserCuff.'+side,[(sign*.122,0,z) for z in [.070,.075,.130,.135]],
              [.084,.089,.089,.085],[.080,.085,.085,.081],m['pajamas'],'LowerLeg.'+side,12)
    return cuff


def torso(mesh, tube, m):
    """Cream T-shirt (PER-04 BASE CHARACTER): a short square T whose hem is
    as wide as the chest (no waist taper, no flare), ending ~0.10 m above the
    crotch over a visible pajama waistband; low crew collar 30 mm lower so the
    neck shows, and flat shoulders into the short sleeves."""
    sections=[(.776,.186,.112),(.800,.186,.112),(.880,.185,.111),(.960,.186,.111),
              (1.040,.189,.113),(1.110,.195,.115),(1.170,.199,.115),(1.198,.193,.110),
              (1.214,.165,.099),(1.226,.116,.085),(1.232,.093,.078)]
    shirt=tube('Shirt',[(0,0,z) for z,_,_ in sections],[w for _,w,_ in sections],
               [d for _,_,d in sections],m['shirt'])
    def body_weights(position):
        z=position[2]
        wh=1-max(0,min(1,(z-.79)/.15));wc=max(0,min(1,(z-1.02)/.13))
        return {name:value for name,value in {'Hips':wh,'Spine':1-wh-wc,'Chest':wc}.items() if value>0}
    assign(shirt,[body_weights(v.co) for v in shirt.data.vertices])
    count=12
    loop=lambda k,z:[(.186*k*math.cos(2*math.pi*j/count),.112*k*math.sin(2*math.pi*j/count),z) for j in range(count)]
    hem_loops=[loop(.97,.770),loop(1.035,.772),loop(1.035,.794),loop(.97,.794)]
    hem=mesh('ShirtHem',[p for l in hem_loops for p in l],
             [(r*count+j,r*count+(j+1)%count,((r+1)%4)*count+(j+1)%count,((r+1)%4)*count+j)
              for r in range(4) for j in range(count)],m['shirt'])
    assign(hem,[body_weights(v.co) for v in hem.data.vertices])
    # Elastic pajama waistband just under the hem (30 mm, 8 mm proud of the hips).
    band=lambda k,z:[(.179*k*math.cos(2*math.pi*j/count),.090*k*math.sin(2*math.pi*j/count),z) for j in range(count)]
    waist=mesh('Waistband',[p for l in [band(.99,.740),band(1.035,.743),band(1.035,.767),band(.99,.772)] for p in l],
               [(r*count+j,r*count+(j+1)%count,((r+1)%4)*count+(j+1)%count,((r+1)%4)*count+j)
                for r in range(4) for j in range(count)],m['pajamas'])
    assign(waist,[{'Hips':1.0} for _ in waist.data.vertices])
    count=14
    ellipse=lambda rx,ry,z:[(rx*math.cos(2*math.pi*j/count),.010+ry*math.sin(2*math.pi*j/count),z) for j in range(count)]
    collar=[ellipse(.087,.077,1.218),ellipse(.085,.075,1.234),ellipse(.072,.067,1.234),ellipse(.074,.068,1.218)]
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
