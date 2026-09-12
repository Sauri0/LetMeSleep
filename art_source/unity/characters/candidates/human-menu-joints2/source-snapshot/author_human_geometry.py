"""Authored planes and garment construction for the reference-quality pajama base.
Geometry only; stable existing sockets and rig coordinates are supplied by the caller.
"""
import math

def panel(mesh, name, points, depth, material, bone):
    count=len(points)
    verts=list(points)+[(x,y+depth,z) for x,y,z in points]
    faces=[tuple(range(count)),tuple(reversed(range(count,2*count)))]
    faces += [(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    return mesh(name,verts,faces,material,bone)


def head_and_cap(c,mesh,tube,ellipsoid,strip,skin,cloth,piping,white,dark):
    # Cross sections have a broad facial plane, cheek bevel, flat temple and occiput.
    # The central columns explicitly form the nose bridge/tip/base in the same skin.
    sections=[
        (1.356,.062,.086,.055,0), (1.380,.085,.103,.079,0),
        (1.425,.110,.117,.105,0), (1.432,.115,.119,.108,0),
        (1.446,.122,.122,.114,0), (1.450,.124,.121,.116,0),
        (1.467,.136,.123,.121,.015), (1.480,.140,.124,.123,.048),
        (1.490,.145,.125,.124,.049), (1.501,.147,.125,.125,.047),
        (1.555,.157,.126,.129,.012), (1.625,.151,.123,.124,0),
        (1.652,.144,.116,.114,0), (1.680,.123,.082,.088,0),
        (1.699,.075,.039,.043,0)]
    verts=[];sides=14
    for z,rx,front,back,nose in sections:
        # Mouth/nose support loops must not introduce horizontal contour steps
        # across the entire cheek, temple and occiput. Interpolate those loops
        # on the same broad plane between deliberate jaw/cheek landmarks.
        if 1.425<z<1.555:
            a,b=((1.425,.110,.117,.105),(1.490,.145,.125,.124)) if z<1.490 else ((1.490,.145,.125,.124),(1.555,.157,.126,.129))
            t=(z-a[0])/(b[0]-a[0])
            rx,front,back=[a[i]+(b[i]-a[i])*t for i in range(1,4)]
        center_width=.009+.003*min(1,nose/.049)
        ring=[
            (-.88*rx,-front),(-.034,-front),(-center_width,-front-nose),
            (center_width,-front-nose),(.034,-front),(.88*rx,-front),
            (.96*rx,-front*.83),(rx,-front*.62),
            (rx,back*.34),(.62*rx,back),(-.62*rx,back),
            (-rx,back*.34),(-rx,-front*.62),(-.96*rx,-front*.83)]
        for j,(x,y) in enumerate(ring):
            local_z=z
            if z in (1.432,1.446) and j in (1,4):x=(-1 if j==1 else 1)*.043
            if z==1.432 and j in (1,4):local_z=1.443
            if z==1.432 and j in (2,3):local_z=1.438
            if z==1.446 and j in (1,4):local_z=1.445
            verts.append((x,y,local_z))
    faces=[tuple(reversed(range(sides)))]
    for ring in range(len(sections)-1):
        for j in (0,1,2,3,4,5,6,12,13):
            # Open the lip interval in the skin; an overlay on solid skin cannot
            # form a mouth when the lower face articulates.
            if sections[ring][0]==1.432 and j in (1,2,3):continue
            a=ring*sides+j;b=ring*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    # Mouth support loops stop at the temples. Broad posterior patches retain
    # the shared boundary vertices, so there are no T-junctions at the seam.
    # Keep the crown support sections beneath the unchanged nightcap. Removing
    # them changed its contact silhouette in reference10 (HR10-R1). Dense mouth
    # support still stops at the temples; the lower occiput remains simplified.
    posterior=[i for i,section in enumerate(sections) if section[0] in (1.356,1.425,1.490,1.555,1.625,1.652,1.680,1.699)]
    for low,high in zip(posterior,posterior[1:]):
        for j in range(7,12):
            face=[low*sides+j,low*sides+j+1]
            face.extend(r*sides+j+1 for r in (range(low+1,high+1) if j==11 else [high]))
            face.append(high*sides+j)
            if j==7:face.extend(r*sides+j for r in range(high-1,low,-1))
            faces.append(tuple(face))
    faces.append(tuple((len(sections)-1)*sides+j for j in range(sides)))
    used=sorted({index for face in faces for index in face})
    remap={old:new for new,old in enumerate(used)}
    verts=[verts[index] for index in used]
    faces=[tuple(remap[index] for index in face) for face in faces]
    head=mesh('HeadAuthoredPlanes',verts,faces,skin)
    upper=head.vertex_groups.new(name='Head');jaw=head.vertex_groups.new(name='Jaw')
    for vertex in head.data.vertices:
        amount=max(0,min(1,(1.457-vertex.co.z)/.055))
        # Jaw motion belongs to the front/lower face; the back of skull stays rigid.
        amount*=max(0,min(1,(-.025-vertex.co.y)/.075))
        if amount<1:upper.add([vertex.index],1-amount,'REPLACE')
        if amount>0:jaw.add([vertex.index],amount,'REPLACE')
    tube('Neck',[(0,.012,1.225),(0,.009,1.285),(0,.008,1.322),(0,.006,1.373)],
         [.074,.055,.049,.065],[.063,.050,.047,.061],skin,'Neck',8)
    for side,s in [('L',1),('R',-1)]:
        # Ear rim and recessed concha are connected planes, not a cheek sphere.
        contour=[(-.018,-.038),(.005,-.047),(.025,-.029),(.031,.005),
                 (.022,.036),(.002,.047),(-.017,.032),(-.022,.001)]
        earverts=[(s*(.150+u),-.008,1.520+v) for u,v in contour]
        earverts += [(s*(.153+u*.57),-.022,1.520+v*.60) for u,v in contour]
        earverts += [(s*.157,-.010,1.521),(s*.150,.017,1.520)]
        earfaces=[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
        earfaces += [(8+i,8+(i+1)%8,16) for i in range(8)]
        earfaces += [((i+1)%8,i,17) for i in range(8)]
        mesh('Ear.'+side,earverts,earfaces,skin,'Head')
        ellipsoid('EyeWhite.'+side,(s*.081,-.108,1.558),(.054,.047,.057),white,'Head',16,8)
        ellipsoid('Pupil.'+side,(s*.077,-.155,1.558),(.0175,.0035,.023),dark,'Eye.'+side,12,6)
        # Orbital planes meet the buried sphere along a full skin rim. The
        # outer loop sinks into the facial plane instead of floating as trim.
        orbit=[];count=12
        for j in range(count):
            a=2*math.pi*j/count
            x=s*(.081+.062*math.cos(a));z=1.558+.069*math.sin(a)
            y=-.125+.01*max(0,(abs(x)-.120)/.04)
            orbit.append((x,y,z))
        for j in range(count):
            a=2*math.pi*j/count
            orbit.append((s*(.081+.047*math.cos(a)),-.132,1.558+.0495*math.sin(a)))
        mesh('HeadEyeSocket.'+side,orbit,[(j,(j+1)%count,(j+1)%count+count,j+count) for j in range(count)],skin,'Head')
        brow=[(s*.030,-.129,1.632),(s*.080,-.129,1.643),(s*.134,-.124,1.633),
              (s*.133,-.124,1.644),(s*.080,-.129,1.656),(s*.030,-.129,1.643)]
        panel(mesh,'Brow.'+side,brow,.007,dark,'Brow.'+side)
    # Recessed oral lining extends behind the true skin opening. Front edge
    # weights match the lip rings exactly to prevent seams in Hit/Faint.
    upper_front=.117+.008*(1.446-1.425)/.065
    lower_front=.117+.008*(1.432-1.425)/.065
    opening=[(-.043,-upper_front,1.445),(-.009,-upper_front,1.446),
             (.009,-upper_front,1.446),(.043,-upper_front,1.445),
             (.043,-lower_front,1.443),(.009,-lower_front,1.438),
             (-.009,-lower_front,1.438),(-.043,-lower_front,1.443)]
    mouthverts=opening+[(x,-.090,z) for x,y,z in opening]
    mouth=mesh('MouthCavity',mouthverts,[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]+[tuple(range(8,16))],dark)
    oral_groups={name:mouth.vertex_groups.new(name=name) for name in ('Head','Jaw')}
    for vertex in mouth.data.vertices:
        amount=max(0,min(1,(1.457-vertex.co.z)/.055))
        oral_groups['Head'].add([vertex.index],1-amount,'REPLACE')
        oral_groups['Jaw'].add([vertex.index],amount,'REPLACE')
    # The skin boundary itself is the lip. Separate tubes/teeth bars made the
    # neutral expression read as an assembly of pale pieces in reference9.
    tube('Nightcap',[(0,.007,1.665),(0,.020,1.735),(.037,.024,1.805),(.105,.019,1.817),(.166,.012,1.754)],
         [.149,.129,.073,.037,.009],[.105,.104,.065,.034,.008],cloth,'Head',12)
    tube('NightcapBand',[(0,.006,1.653),(0,.009,1.680)],[.153,.147],[.108,.104],piping,'Head',12)
    ellipsoid('NightcapPom',(.166,.012,1.748),(.026,.025,.032),white,'Head',10,5)


def collar_and_pocket(mesh,strip,cloth,piping):
    for side,s in [('L',1),('R',-1)]:
        points=[(s*.058,-.063,1.284),(s*.103,-.086,1.247),
                (s*.097,-.145,1.202),(s*.013,-.151,1.185),(s*.044,-.140,1.238)]
        panel(mesh,'Collar.'+side,points,.005,cloth,'Chest')
        strip('CollarEdge.'+side,[points[i] for i in [0,1,2,3]],.0025,piping,'Chest')
    pocket=[(.063,-.136,1.082),(.132,-.124,1.082),(.130,-.126,1.016),
            (.096,-.140,1.009),(.064,-.140,1.016)]
    panel(mesh,'PocketPatch',pocket,.004,cloth,'Chest')
    strip('PocketPiping',[pocket[0],pocket[1]],.0027,piping,'Chest')


def slipper(side,s,mesh,tube,strip,skin,cloth,piping,sole):
    outline=[(-.035,.068),(.035,.068),(.055,.040),(.074,-.050),
             (.081,-.128),(.063,-.187),(.025,-.218),(-.025,-.218),
             (-.063,-.187),(-.081,-.128),(-.074,-.050),(-.055,.040)]
    count=len(outline)
    center=s*.125
    soleverts=[(center+x*scale,y*scale,z) for scale,z in [(.94,0),(1,.010),(.98,.022)] for x,y in outline]
    faces=[tuple(reversed(range(count))),tuple(range(2*count,3*count))]
    faces += [(r*count+i,r*count+(i+1)%count,(r+1)*count+(i+1)%count,(r+1)*count+i) for r in range(2) for i in range(count)]
    mesh('SlipperSole.'+side,soleverts,faces,sole,'Foot.'+side)
    opening=[(-.026,.052),(.026,.052),(.041,.025),(.047,-.014),
             (.039,-.037),(.024,-.049),(.010,-.054),(-.010,-.054),
             (-.024,-.049),(-.039,-.037),(-.047,-.014),(-.041,.025)]
    heights=[.062,.062,.067,.074,.065,.051,.045,.045,.051,.065,.074,.067]
    verts=[(center+x*.98,y*.98,.021) for x,y in outline]
    verts += [(center+x*.98,y*.98,z) for (x,y),z in zip(outline,heights)]
    verts += [(center+(x+ox)*.5,(y+oy)*.5,(z+.093)*.5+(.014 if y<-.05 else .004))
              for (x,y),(ox,oy),z in zip(outline,opening,heights)]
    verts += [(center+x,y,.093) for x,y in opening]
    verts += [(center+x*.91,y,.061) for x,y in opening]
    faces=[tuple(reversed(range(count))),tuple(range(4*count,5*count))]
    for ring in range(4):
        lower=ring*count;upper=lower+count
        faces += [(lower+i,lower+(i+1)%count,upper+(i+1)%count,upper+i) for i in range(count)]
    mesh('SlipperUpper.'+side,verts,faces,cloth,'Foot.'+side)
    rimverts=[(center+x*factor,.017+(y-.017)*factor,z)
              for factor,z in [(1.07,.091),(1.07,.096),(.95,.096),(.95,.091)] for x,y in opening]
    rimfaces=[(r*count+i,r*count+(i+1)%count,((r+1)%4)*count+(i+1)%count,((r+1)%4)*count+i)
              for r in range(4) for i in range(count)]
    mesh('SlipperRim.'+side,rimverts,rimfaces,piping,'Foot.'+side)
    tube('Ankle.'+side,[(center,.014,.079),(center,.008,.145)],[.046,.047],[.043,.045],skin,'Foot.'+side,8)
