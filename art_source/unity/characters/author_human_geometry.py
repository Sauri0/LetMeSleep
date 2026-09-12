"""Authored planes and garment construction for the reference-quality pajama base.
Geometry only; stable existing sockets and rig coordinates are supplied by the caller.
"""
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
        (1.467,.136,.123,.121,.020), (1.490,.145,.125,.124,.059),
        (1.555,.157,.126,.129,.012), (1.625,.151,.123,.124,0),
        (1.652,.144,.116,.114,0), (1.680,.123,.082,.088,0),
        (1.699,.075,.039,.043,0)]
    verts=[];sides=14
    for z,rx,front,back,nose in sections:
        verts += [(x,y,z) for x,y in [
            (-.88*rx,-front),(-.034,-front-nose*.15),(-.009,-front-nose),
            (.009,-front-nose),(.034,-front-nose*.15),(.88*rx,-front),
            (.96*rx,-front*.83),(rx,-front*.62),
            (rx,back*.34),(.62*rx,back),(-.62*rx,back),
            (-rx,back*.34),(-rx,-front*.62),(-.96*rx,-front*.83)]]
    faces=[tuple(reversed(range(sides)))]
    for ring in range(len(sections)-1):
        for j in range(sides):
            # Open the lip interval in the skin; an overlay on solid skin cannot
            # form a mouth when the lower face articulates.
            if sections[ring][0]==1.432 and j in (1,2,3):continue
            a=ring*sides+j;b=ring*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple((len(sections)-1)*sides+j for j in range(sides)))
    head=mesh('HeadAuthoredPlanes',verts,faces,skin)
    upper=head.vertex_groups.new(name='Head');jaw=head.vertex_groups.new(name='Jaw')
    for vertex in head.data.vertices:
        amount=max(0,min(1,(1.457-vertex.co.z)/.055))
        # Jaw motion belongs to the front/lower face; the back of skull stays rigid.
        amount*=max(0,min(1,(.045-vertex.co.y)/.10))
        if amount<1:upper.add([vertex.index],1-amount,'REPLACE')
        if amount>0:jaw.add([vertex.index],amount,'REPLACE')
    tube('Neck',[(0,.012,1.225),(0,.009,1.292),(0,.006,1.373)],
         [.061,.057,.065],[.057,.054,.061],skin,'Neck',8)
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
        ellipsoid('EyeWhite.'+side,(s*.081,-.122,1.558),(.057,.032,.061),white,'Eye.'+side,12,6)
        ellipsoid('Pupil.'+side,(s*.077,-.1545,1.558),(.0185,.0045,.024),dark,'Eye.'+side,12,6)
        # A narrow upper socket edge beds the eyeball into the face without cheek lumps.
        strip('HeadEyeRim.'+side,[(s*.030,-.130,1.594),(s*.057,-.133,1.620),
                                (s*.103,-.127,1.620),(s*.135,-.117,1.591)],.0032,skin,'Head')
        brow=[(s*.030,-.129,1.632),(s*.080,-.129,1.643),(s*.134,-.124,1.633),
              (s*.133,-.124,1.644),(s*.080,-.129,1.656),(s*.030,-.129,1.643)]
        panel(mesh,'Brow.'+side,brow,.007,dark,'Brow.'+side)
    # Recessed oral lining extends behind the true skin opening. Front edge
    # weights match the lip rings exactly to prevent seams in Hit/Faint.
    mouthverts=[(-.034,-.122,1.446),(.034,-.122,1.446),
                (.034,-.119,1.432),(-.034,-.119,1.432),
                (-.032,-.090,1.445),(.032,-.090,1.445),
                (.032,-.090,1.432),(-.032,-.090,1.432)]
    mouth=mesh('MouthCavity',mouthverts,[(0,1,5,4),(1,2,6,5),(2,3,7,6),
                                      (3,0,4,7),(4,5,6,7)],dark)
    oral_groups={name:mouth.vertex_groups.new(name=name) for name in ('Head','Jaw')}
    for vertex in mouth.data.vertices:
        amount=max(0,min(1,(1.457-vertex.co.z)/.055))
        oral_groups['Head'].add([vertex.index],1-amount,'REPLACE')
        oral_groups['Jaw'].add([vertex.index],amount,'REPLACE')
    upper=strip('MouthUpperLip',[(-.035,-.123,1.446),(0,-.125,1.446),(.035,-.123,1.446)],.0025,skin,'Head')
    lower=strip('MouthLowerLip',[(-.033,-.121,1.432),(0,-.124,1.430),(.033,-.121,1.432)],.003,skin,'Jaw')
    for lip in (upper,lower):
        lip.vertex_groups.clear()
        groups={name:lip.vertex_groups.new(name=name) for name in ('Head','Jaw')}
        for vertex in lip.data.vertices:
            amount=max(0,min(1,(1.457-vertex.co.z)/.055))
            groups['Head'].add([vertex.index],1-amount,'REPLACE')
            groups['Jaw'].add([vertex.index],amount,'REPLACE')
    panel(mesh,'MouthTeeth',[(-.024,-.116,1.444),(.024,-.116,1.444),
                           (.024,-.116,1.440),(-.024,-.116,1.440)],.002,white,'Head')
    tube('Nightcap',[(0,.007,1.665),(0,.020,1.735),(.037,.024,1.805),(.105,.019,1.817),(.166,.012,1.754)],
         [.149,.129,.073,.037,.009],[.105,.104,.065,.034,.008],cloth,'Head',12)
    tube('NightcapBand',[(0,.006,1.653),(0,.009,1.680)],[.153,.147],[.108,.104],piping,'Head',12)
    ellipsoid('NightcapPom',(.166,.012,1.748),(.026,.025,.032),white,'Head',10,5)


def collar_and_pocket(mesh,strip,cloth,piping):
    for side,s in [('L',1),('R',-1)]:
        points=[(s*.059,-.064,1.258),(s*.103,-.086,1.231),
                (s*.097,-.145,1.190),(s*.013,-.151,1.169),(s*.044,-.140,1.218)]
        panel(mesh,'Collar.'+side,points,.005,cloth,'Chest')
        strip('CollarEdge.'+side,[points[i] for i in [0,1,2,3]],.0025,piping,'Chest')
    pocket=[(.063,-.136,1.082),(.132,-.124,1.082),(.130,-.126,1.016),
            (.096,-.140,1.009),(.064,-.140,1.016)]
    panel(mesh,'PocketPatch',pocket,.004,cloth,'Chest')
    strip('PocketPiping',[pocket[0],pocket[1]],.0027,piping,'Chest')


def slipper(side,s,mesh,tube,strip,skin,cloth,piping,sole):
    outline=[(-.057,.075),(.057,.075),(.084,.035),(.087,-.170),
             (.060,-.225),(-.060,-.225),(-.087,-.170),(-.084,.035)]
    center=s*.125
    soleverts=[(center+x*scale,y*scale,z) for scale,z in [(.94,0),(1,.012),(1,.026)] for x,y in outline]
    faces=[tuple(reversed(range(8))),tuple(range(16,24))]
    faces += [(r*8+i,r*8+(i+1)%8,(r+1)*8+(i+1)%8,(r+1)*8+i) for r in range(2) for i in range(8)]
    mesh('SlipperSole.'+side,soleverts,faces,sole,'Foot.'+side)
    opening=[(-.031,.064),(.031,.064),(.049,.027),(.049,-.014),
             (.031,-.030),(-.031,-.030),(-.049,-.014),(-.049,.027)]
    heights=[.065,.065,.091,.083,.064,.064,.083,.091]
    verts=[(center+x,y,.025) for x,y in outline]
    verts += [(center+x*.98,y*.98,z) for (x,y),z in zip(outline,heights)]
    verts += [(center+x,y,.093) for x,y in opening]
    verts += [(center+x*.91,y,.073) for x,y in opening]
    faces=[tuple(reversed(range(8))),tuple(range(24,32))]
    for lower,upper in [(0,8),(8,16),(16,24)]:
        faces += [(lower+i,lower+(i+1)%8,upper+(i+1)%8,upper+i) for i in range(8)]
    mesh('SlipperUpper.'+side,verts,faces,cloth,'Foot.'+side)
    rimverts=[(center+x*factor,.017+(y-.017)*factor,z)
              for factor,z in [(1.07,.091),(1.07,.096),(.95,.096),(.95,.091)] for x,y in opening]
    rimfaces=[(r*8+i,r*8+(i+1)%8,((r+1)%4)*8+(i+1)%8,((r+1)%4)*8+i)
              for r in range(4) for i in range(8)]
    mesh('SlipperRim.'+side,rimverts,rimfaces,piping,'Foot.'+side)
    tube('Ankle.'+side,[(center,.014,.079),(center,.008,.145)],[.046,.047],[.043,.045],skin,'Foot.'+side,8)
