"""Human-only joint topology and cloth weighting; stable 65-bone contract."""
import math


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
    chest=1-ramp(x,.205,.300)
    hand=.20*ramp(x,.695,.742)
    return {name:value for name,value in {
        'Chest':chest,
        'UpperArm.'+side:(1-chest)*(1-lower),
        'LowerArm.'+side:(1-chest)*lower*(1-hand),
        'Hand.'+side:(1-chest)*lower*hand}.items() if value>0}


def open_cuff(mesh, side, sign, cloth, piping):
    # A thin folded opening, not a capped cylinder. The skin bridge passes
    # through the inner ellipse; only the 2 mm cloth wall closes at the rim.
    sections=[(.695,.044,.034),(.718,.040,.031),(.738,.037,.029),
              (.738,.035,.027),(.716,.038,.029),(.695,.042,.032)]
    count=12
    vertices=[(sign*x,-depth*math.sin(2*math.pi*j/count),1.15+width*math.cos(2*math.pi*j/count))
              for x,width,depth in sections for j in range(count)]
    faces=[(r*count+j,r*count+(j+1)%count,((r+1)%len(sections))*count+(j+1)%count,
            ((r+1)%len(sections))*count+j) for r in range(len(sections)) for j in range(count)]
    cuff=mesh('Cuff.'+side,vertices,faces,cloth)
    assign(cuff,[sleeve_weights(v.co,side) for v in cuff.data.vertices])
    # Narrow stitched piping lies on the outer fabric, with an open center.
    vertices=[(sign*x,-depth*math.sin(2*math.pi*j/count),1.15+width*math.cos(2*math.pi*j/count))
              for x,width,depth in [(.733,.0383,.0303),(.736,.0377,.0297)] for j in range(count)]
    hem=mesh('CuffPiping.'+side,vertices,
             [(j,(j+1)%count,(j+1)%count+count,j+count) for j in range(count)],piping)
    assign(hem,[sleeve_weights(v.co,side) for v in hem.data.vertices])


def clothing(mesh, tube, side, sign, cloth, piping):
    # Support loops straddle the actual elbow at x=.52. Taper continues to
    # the wrist instead of ending at a wide, separately rigid ring.
    sleeve=tube('Sleeve.'+side,
        [(sign*x,0,z) for x,z in [(.185,1.17),(.275,1.17),(.405,1.165),(.458,1.162),
                                  (.490,1.16),(.520,1.16),(.550,1.158),(.582,1.154),
                                  (.645,1.15),(.695,1.15),(.728,1.15)]],
        [.061,.080,.069,.065,.065,.068,.063,.057,.047,.042,.036],
        [.064,.082,.070,.067,.068,.070,.065,.058,.040,.033,.028],
        cloth,sides=12,cap_ends=(False,False))
    for v in sleeve.data.vertices:
        if .465<abs(v.co.x)<.575 and v.co.z<1.14:
            v.co.y-=.004*max(0,1-abs(abs(v.co.x)-.52)/.055)
    assign(sleeve,[sleeve_weights(v.co,side) for v in sleeve.data.vertices])
    open_cuff(mesh,side,sign,cloth,piping)
    # The patella has an explicit anterior plane; the posterior crease is
    # narrower. Rings on either side distribute bending without a pipe kink.
    leg=tube('PajamaLeg.'+side,
        [(sign*.125,y,z) for z,y in [(.13,0),(.20,0),(.32,0),(.375,0),(.410,-.003),
                                    (.440,-.008),(.470,-.003),(.505,0),(.60,0),(.70,0),(.78,0)]],
        [.068,.071,.073,.075,.076,.079,.080,.081,.087,.093,.093],
        [.077,.079,.081,.083,.087,.096,.091,.087,.096,.104,.104],cloth,sides=12)
    weights=[]
    for v in leg.data.vertices:
        upper=ramp(v.co.z,.355,.525)
        hips=ramp(v.co.z,.67,.78)
        weights.append({name:value for name,value in {
            'Hips':hips,'UpperLeg.'+side:upper*(1-hips),
            'LowerLeg.'+side:(1-upper)*(1-hips)}.items() if value>0})
    assign(leg,weights)


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
