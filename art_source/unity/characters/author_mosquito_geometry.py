"""Mosquito geometry/rig owned by Modelador de Mosquitos.
Explicit helper injection keeps this module independent of export/orchestration.
Initial extraction preserves the previous generator statements unchanged.
"""


def create_mosquito(*, Character, material, tube, ellipsoid, strip, mesh):
    c=Character('Mosquito')
    shell=material('Mosquito_Shell',(.46,.12,.10)); belly=material('Mosquito_Abdomen',(.70,.24,.14))
    dark=material('Mosquito_Legs',(.095,.055,.075)); eye=material('Mosquito_EyeWhite',(.94,.91,.79))
    pupil=material('Mosquito_Expression',(.035,.027,.05)); wing=material('Mosquito_Wing',(.60,.76,.84,.72),.4)
    vein=material('Mosquito_WingVein',(.30,.43,.54),.85)
    def p(x,y,z): return (x,y,z-.105)
    c.bone('Root',(0,0,0),(0,0,.03),deform=False)
    # Raise the same character's body over articulated legs without moving actor Root
    # or the gameplay mouth point. The proboscis now descends from head to that point.
    c.bone('Thorax',p(0,.015,.160),p(0,-.035,.160),'Root')
    c.bone('Head',p(0,-.045,.160),p(0,-.085,.160),'Thorax')
    c.bone('Abdomen01',p(0,.035,.158),p(0,.130,.135),'Thorax')
    c.bone('Abdomen02',p(0,.130,.135),p(0,.230,.079),'Abdomen01')
    c.bone('Proboscis',p(0,-.092,.151),p(0,-.19,.105),'Head')
    c.bone('Socket.Mouth',p(0,-.19,.105),p(0,-.20,.105),'Proboscis',False)
    c.bone('Socket.Back',p(0,.015,.190),p(0,.015,.205),'Thorax',False)
    c.bone('Socket.CameraTarget',(0,0,0),(0,-.02,0),'Thorax',False)
    c.bone('Socket.AimForward',(0,-.08,0),(0,-.12,0),'Head',False)
    # Gameplay holds Root 57 mm off the support. With Unity scale .5 this is
    # source Z=-.114; preserve the 55 mm collision sphere and 95 mm mouth reach.
    c.bone('Socket.GroundContact',p(0,0,-.009),p(0,-.02,-.009),'Root',False)
    ellipsoid('Thorax',p(0,0,.160),(.032,.047,.030),shell,'Thorax')
    ellipsoid('Head',p(0,-.068,.160),(.036,.034,.035),shell,'Head')
    abdomen=tube('Abdomen',[p(0,y,z) for y,z in [(.03,.158),(.073,.153),(.124,.138),(.168,.115),(.205,.091),(.235,.074)]],
                   [.022,.029,.025,.019,.010,.0025],[.022,.027,.024,.017,.009,.0025],belly)
    a=abdomen.vertex_groups.new(name='Abdomen01'); b=abdomen.vertex_groups.new(name='Abdomen02')
    for v in abdomen.data.vertices:
        w=max(0,min(1,(v.co.y-.108)/.047))
        if w<1: a.add([v.index],1-w,'REPLACE')
        if w>0: b.add([v.index],w,'REPLACE')
    tube('Proboscis',[p(0,-.092,.151),p(0,-.139,.128),p(0,-.19,.105)],[.006,.0035,.0014],[.005,.0035,.0014],shell,'Proboscis',8)
    for side,s in [('L',1),('R',-1)]:
        ellipsoid('Eye.'+side,p(s*.022,-.097,.174),(.023,.016,.027),eye,'Head',12,6)
        ellipsoid('Pupil.'+side,p(s*.018,-.112,.173),(.008,.004,.012),pupil,'Head',12,6)
        strip('Brow.'+side,[p(s*.004,-.107,.200),p(s*.023,-.108,.204),p(s*.040,-.093,.196)],.0035,dark,'Head')
        strip('Antenna.'+side,[p(s*.013,-.076,.195),p(s*.026,-.08,.221),p(s*.043,-.086,.226)],.002,dark,'Head')
        c.bone('Wing.'+side,p(s*.017,.0,.187),p(s*.22,.065,.218),'Thorax')
        c.bone('Socket.WingRoot.'+side,p(s*.017,0,.187),p(s*.017,-.02,.187),'Thorax',False)
        verts=[p(s*x,y,z) for x,y,z in [(.017,0,.187),(.118,.018,.224),(.24,.100,.245),(.150,.110,.231),(.055,.044,.200)]]
        # A shallow central ridge creates actual low-poly facets instead of a flat n-gon.
        verts.append(p(s*.113,.060,.225))
        # Thin solid double-sided geometry keeps FBX silhouette independent of culling settings.
        verts += [(x,y,z-.0006) for x,y,z in verts]
        faces=[(i,(i+1)%5,5) for i in range(5)]
        faces += [(i+6,11,(i+1)%5+6) for i in range(5)]
        faces += [(i,i+6,(i+1)%5+6,(i+1)%5) for i in range(5)]
        mesh('WingMembrane.'+side,verts,faces,wing,'Wing.'+side)
        strip('WingLeadingEdge.'+side,[verts[i] for i in [0,1,2]],.0012,vein,'Wing.'+side)
        strip('WingVein.'+side,[verts[i] for i in [0,5,2]],.0008,vein,'Wing.'+side)
        for i,(y,dy) in enumerate([(-.033,-.052),(.006,.012),(.042,.067)],1):
            pts=[p(s*.024,y,.155),p(s*.085,y+dy*.4,.094),p(s*.095,y+dy,-.0026),p(s*.107,y+dy+.006,-.0076)]
            parent='Thorax'
            for j in range(3):
                name=f'Leg{i}{j+1:02d}.{side}'
                c.bone(name,pts[j],pts[j+1],parent); parent=name
                tube('Limb_'+name,[pts[j],pts[j+1]],[.0032-j*.00065,.0028-j*.00065],[.0032-j*.00065,.0028-j*.00065],shell if j==0 else dark,name,6)
                if j<2:ellipsoid('LegJoint_'+name,pts[j+1],(.0036,.0036,.0036),dark,name,8,4)
    c.bind()
    mouth=c.rig.data.bones['Socket.Mouth'].head_local
    c.contact={'gameplay_tip_rest_unity_m':[mouth.x*.5,mouth.z*.5,-mouth.y*.5],
               'gameplay_collision_radius_m':.055,'gameplay_surface_root_offset_m':.057,
               'ground_contact_rest_unity_m':[0,-.057,0],
               'surface_rotation_contract':'local +Y outward normal, forward projected tangent; runtime validation belongs to W1/W2'}
    return c
