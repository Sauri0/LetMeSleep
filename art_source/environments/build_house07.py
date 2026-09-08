"""Let me sleep 0.7: original room-specific furnishings and architectural joinery.

Blender Z-up metres -> explicit GLB Y-up. No downloaded models/textures.
Door leaf/frame pivot stays at hinge floor; X is nominal 1m portal width.
"""
from pathlib import Path
import sys, math, json, bpy, bmesh
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_house as H

box, tube, ellipsoid, lathe, cushion = H.box, H.tube, H.ellipsoid, H.lathe, H.cushion
SOURCE = H.SOURCE / 'v07'
SOURCE.mkdir(exist_ok=True)

def ring(name,p,radius,minor,mat='brass',axis='Z'):
    bpy.ops.mesh.primitive_torus_add(major_segments=28,minor_segments=8,location=p,major_radius=radius,minor_radius=minor)
    obj=bpy.context.object
    if axis=='Y': obj.rotation_euler[0]=math.pi/2
    if axis=='X': obj.rotation_euler[1]=math.pi/2
    return H.finish(obj,name,mat)

def book(p,scale=1,mat='berry'):
    x,y,z=p
    box('Book pages',(x,y,z+.027*scale),(.23*scale,.31*scale,.045*scale),'paper',.007)
    for dz in (0,.055): box('Book bound cover',(x,y,z+dz*scale),(.25*scale,.33*scale,.012*scale),mat,.008)
    box('Book spine',(x-.123*scale,y,z+.026*scale),(.014*scale,.33*scale,.055*scale),mat,.006)

def bottle(p,mat='green',r=.045,height=.23):
    lathe('Storage bottle',p,[(0,r*.7),(.02,r),(.65*height,r),(.78*height,r*.45),(height,r*.45)],mat,16)
    box('Bottle label',(p[0],p[1]+r+.001,p[2]+height*.4),(r*1.35,.005,height*.3),'paper',.002)

def jar(p,mat='cream'):
    lathe('Kitchen storage jar',p,[(0,.075),(.025,.085),(.19,.085),(.205,.08),(.22,.088)],mat,20)
    lathe('Jar wood lid',(p[0],p[1],p[2]+.22),[(0,.09),(.018,.095),(.026,.065)],'wood',20)

def door_leaf():
    # Coordinates exactly match leaf_box: x[0,1], y thickness±.035, z[.14,2.45].
    box('Continuous door slab',(.5,0,1.295),(1,.05,2.31),'sage',.008)
    for x in (.045,.955): box('Door structural stile',(x,0,1.295),(.09,.07,2.31),'sage',.008)
    for z,h in ((.20,.12),(1.20,.10),(2.395,.11)):
        box('Door structural rail',(.5,0,z),(.84,.07,h),'sage',.006)
    for side in (-1,1):
        for bottom,top in ((.29,1.13),(1.28,2.32)):
            box('Recessed raised panel',(.5,side*.029,(bottom+top)/2),(.69,.014,top-bottom),'cream',.013)
            for x in (.12,.88): box('Panel bead',(x,side*.036,(bottom+top)/2),(.021,.018,top-bottom+.035),'wood',.007)
            for z in (bottom,top): box('Panel bead',(.5,side*.036,z),(.77,.018,.024),'wood',.007)
        ellipsoid('Handle escutcheon',(.88,side*.047,1.09),(.042,.016,.09),'brass',20,12)
        tube('Lever handle',[(.88,side*.06,1.11),(.88,side*.105,1.11),(.76,side*.105,1.11)],.017,'brass')
        for z in (1.035,1.145): ellipsoid('Handle screw',(.88,side*.066,z),(.008,.003,.008),'ink',12,6)
    for z in (.40,1.30,2.17):
        lathe('Hinge barrel',(0,0,z-.055),[(0,.018),(.11,.018)],'brass',12)
        for side in (-1,1): box('Hinge plate',(.026,side*.037,z),(.055,.008,.10),'brass',.004)

def door_frame():
    # No threshold. Depth bridges the offset hinge plane and the structural wall.
    for x in (-.041,1.041):
        box('Jamb',(x,0,1.225),(.082,.24,2.45),'wood',.008)
        for side in (-1,1):
            box('Architrave',(x,side*.13,1.255),(.105,.035,2.51),'cream',.012)
            box('Architrave bead',(x,side*.151,1.25),(.025,.016,2.5),'wood',.007)
    box('Lintel',(0.5,0,2.49),(1.164,.24,.08),'wood',.009)
    for side in (-1,1):
        box('Header casing',(.5,side*.13,2.51),(1.19,.035,.12),'cream',.012)
        box('Header crown',(.5,side*.13,2.582),(1.23,.055,.035),'wood',.008)

def molding():
    # Extruded stepped/rounded profile, nominal horizontal X[0,1].
    profile=[(0,0),(.025,0),(.028,.025),(.023,.06),(.030,.09),(.022,.117),(.012,.13),(0,.13)]
    verts=[(x,y,z) for x in (0,1) for y,z in profile]
    n=len(profile); faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    for i in range(n): j=(i+1)%n; faces.append((i,j,j+n,i+n))
    mesh=bpy.data.meshes.new('Moulded profile'); mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Stepped moulding',mesh);bpy.context.collection.objects.link(obj);H.finish(obj,obj.name,'cream')

def pendant():
    lathe('Ceiling rose',(0,0,-.035),[(0,.16),(.02,.17),(.035,.12)],'wood',24)
    lathe('Pendant stem',(0,0,-.19),[(0,.025),(.16,.025)],'brass',16)
    shade=lathe('Pleated ceramic shade',(0,0,-.45),[(0,.25),(.035,.26),(.18,.13),(.21,.12),(.21,.107),(.175,.116),(.029,.245),(0,.236)],'cream',32)
    # This closed profile already forms the ceramic wall; lathe end caps
    # would cover the aperture and hide the actual luminous diffuser.
    topology=bmesh.new();topology.from_mesh(shade.data);topology.faces.ensure_lookup_table()
    bmesh.ops.delete(topology,geom=[topology.faces[-1],topology.faces[-2]],context='FACES_ONLY')
    topology.to_mesh(shade.data);topology.free()
    ellipsoid('Warm enclosed diffuser',(0,0,-.432),(.224,.224,.018),'linen',24,12)
    ring('Shade rolled rim',(0,0,-.45),.244,.008,'brass')

def bath_vanity():
    box('Vanity carcass',(0,0,.39),(1.48,.57,.66),'blue',.045)
    box('Stone top',(0,0,.735),(1.6,.65,.09),'cream',.035)
    for x in (-.37,.37): H.cabinet_door(x,.294,.39,.70,.57,'blue')
    # Rounded bowl has an actual open cavity, rim and drain.
    bowl=lathe('Porcelain basin',(.50,0,.78),[(0,.21),(.025,.25),(.085,.29),(.10,.29),(.095,.263),(.045,.21),(.029,.12)],'white',32)
    bowl.scale.y=.65
    lathe('Drain',(.50,0,.812),[(0,.025),(.004,.025)],'brass',16)
    tube('Swan tap',[(.50,-.22,.78),(.50,-.22,1.04),(.50,-.07,1.05),(.50,-.04,.98)],.019,'brass')
    for x in (-.60,.60): H.leg(x,0,.10)
    bottle((-.55,.02,.78),'green',.04,.23)

def toilet():
    foot=lathe('WC floor plinth',(0,-.10,0),[(0,.16),(.025,.19),(.08,.19),(.22,.14)],'white',24)
    foot.scale.y=1.2
    ellipsoid('Porcelain pedestal',(0,-.05,.20),(.20,.22,.20),'white',24,16)
    bowl=lathe('Open WC bowl',(0,.04,.25),[(0,.16),(.05,.22),(.18,.29),(.22,.30),(.23,.30),(.23,.256),(.15,.22),(.07,.105)],'white',32)
    bowl.scale.y=1.16
    seat=ring('Porcelain seat',(0,.04,.485),.28,.027,'cream');seat.scale.y=1.14
    box('Cistern',(0,-.235,.69),(.52,.22,.49),'white',.07)
    box('Cistern lid',(0,-.235,.944),(.54,.245,.042),'cream',.018)
    lathe('Flush button',(.14,-.235,.968),[(0,.035),(.012,.035)],'brass',16)
    box('Raised seat back',(0,-.242,.52),(.40,.033,.05),'cream',.018)

def bath_shower():
    # A closed shower screen makes the existing solid furniture volume legible.
    glass=H.M['glass'].copy();glass.name='House_frosted_shower';H.M['shower_glass']=glass
    shader=glass.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(.42,.61,.61,.32)
    shader.inputs['Alpha'].default_value=.32
    shader.inputs['Roughness'].default_value=.3
    glass.diffuse_color=(.42,.61,.61,.32);glass.surface_render_method='DITHERED'
    box('Bath floor plinth',(0,0,.04),(1.57,.61,.08),'blue',.03)
    box('Bath apron',(0,.26,.29),(1.6,.13,.50),'cream',.05)
    box('Bath bottom',(0,0,.18),(1.43,.51,.12),'white',.04)
    for x in (-.746,.746):box('Bath rounded end',(x,0,.31),(.108,.65,.50),'white',.035)
    box('Bath wall rim',(0,-.295,.54),(1.6,.06,.07),'white',.02)
    box('Bath front rim',(0,.29,.54),(1.6,.07,.07),'white',.02)
    for x in (-.765,.765):box('Shower upright',(x,.27,1.335),(.045,.035,1.63),'brass',.012)
    for z in (.555,2.135):box('Sliding shower rail',(0,.27,z),(1.58,.047,.03),'brass',.008)
    for x in (-.374,.374):
        box('Closed frosted screen',(x,.27,1.344),(.74,.009,1.55),'shower_glass',.002)
        box('Screen edge',(x+.36,.275,1.344),(.018,.018,1.55),'brass',.004)
    tube('Shower handle',[(-.04,.31,1.07),(-.04,.335,1.07),(-.04,.335,1.28),(-.04,.31,1.28)],.012,'brass')
    tube('Shower riser',[(.53,-.27,.71),(.53,-.27,1.95),(.53,-.09,2.04)],.014,'brass')
    head=lathe('Shower rose',(.53,-.08,1.998),[(0,.10),(.018,.10),(.03,.045)],'brass',20)
    head.rotation_euler.x=.14
    tube('Mixer',[(.44,-.26,.82),(.62,-.26,.82)],.023,'brass')
    lathe('Bath drain',(-.54,0,.245),[(0,.03),(.004,.03)],'brass',16)

def washer():
    # Twin appliance + folding surface form one collision envelope.
    for x in (-.37,.37):
        box('Enamel washer housing',(x,0,.395),(.72,.65,.79),'cream',.055)
        box('Control fascia',(x,.331,.68),(.67,.022,.12),'blue',.012)
        face=ring('Washer porthole',(x,.35,.35),.235,.025,'brass','Y')
        ellipsoid('Dark washer glass',(x,.343,.35),(.203,.02,.203),'glass',28,16)
        ring('Glass inner rim',(x,.37,.35),.19,.008,'cream','Y')
        for dx in (-.23,-.07,.13): ellipsoid('Control dial',(x+dx,.359,.69),(.027,.019,.027),'cream',12,8)
    box('Laundry folding top',(0,0,.82),(1.50,.70,.06),'wood',.025)
    for i in range(3): cushion('Folded towel',(-.28,0,.875+i*.056),(.43,.38,.052),['blue','cream','sage'][i],False)

def hamper():
    lathe('Woven hamper',(0,0,0),[(0,.26),(.06,.29),(.76,.34),(.79,.35),(.83,.34)],'wood',24)
    for i in range(14):
        a=i*math.tau/14
        tube('Woven upright',[(math.cos(a)*.28,math.sin(a)*.28,.04),(math.cos(a)*.33,math.sin(a)*.33,.76)],.012,'linen')
    for i in range(12): ring('Woven band',(0,0,.08+i*.061),.281+i*.0044,.009,'cream')
    cushion('Laundry linen over rim',(.08,.0,.835),(.46,.42,.14),'blue')
    for side in (-1,1): tube('Basket handle',[(side*.31,-.10,.7),(side*.36,0,.85),(side*.31,.10,.7)],.019,'wood_dark')

def television():
    H.build('dresser')
    # Fixed envelope has screen on stand, knobs, speaker slots and cable.
    for x in (-.31,.31): box('TV foot',(x,0,1.39),(.25,.35,.045),'ink',.017)
    box('Television rounded frame',(0,-.035,1.75),(1.10,.20,.65),'wood_dark',.065)
    box('Rounded dark TV screen',(-.065,.074,1.76),(.83,.02,.49),'glass',.06)
    for z in (1.60,1.72,1.84): ellipsoid('TV dial',(.44,.10,z),(.022,.017,.022),'brass',12,8)
    for x in (-.25,.25): tube('TV aerial',[(x,-.03,2.08),(x*1.9,-.03,2.36)],.008,'brass')

def piano():
    box('Piano plinth',(0,0,.10),(1.46,.55,.20),'wood_dark',.055)
    box('Piano upright',(0,-.14,.79),(1.4,.35,1.25),'wood',.045)
    for x in (-.68,.68): box('Piano cheek',(x,.04,.70),(.09,.56,1.19),'wood_dark',.04)
    box('Keyboard bed',(0,.21,.75),(1.31,.38,.10),'wood_dark',.035)
    for key in range(23):
        x=-.59+key*.052
        box('Ivory key',(x,.30,.822),(.048,.26,.026),'cream',.002)
        if key%7 not in (2,6): box('Ebony key',(x+.025,.215,.852),(.027,.12,.028),'ink',.003)
    for x in (-.13,0,.13): tube('Brass pedal',[(x,0,.16),(x,.37,.12),(x,.4,.10)],.018,'brass')
    box('Music rest',(0,.077,1.13),(.61,.038,.31),'wood_dark',.018)
    for x in (-.155,.155):
        page=box('Sheet music',(x,.101,1.15),(.285,.006,.27),'paper',.002)
        for row in range(4): box('Music staff',(x,.106,1.22-row*.045),(.235,.003,.005),'ink',0)

def sewing_table():
    H.build('desk')
    box('Sewing machine foot',(0,.02,.76),(.57,.33,.06),'ink',.035)
    box('Sewing machine column',(.19,-.01,.94),(.14,.20,.34),'sage',.06)
    box('Sewing machine arm',(-.04,-.01,1.09),(.52,.21,.13),'sage',.052)
    box('Sewing needle head',(-.27,-.01,1.005),(.115,.18,.22),'sage',.03)
    tube('Needle',[(-.27,.01,.93),(-.27,.01,.80)],.004,'brass')
    ring('Hand wheel',(.27,-.01,1.04),.096,.022,'wood_dark','X')
    lathe('Thread spool',(.13,-.01,1.18),[(0,.027),(.085,.027)],'berry',16)
    for x in (-.55,.50): lathe('Cloth roll',(x,-.05,.75),[(0,.065),(.33,.065)],'linen',16)
    box('Fabric on table',(-.22,.15,.756),(.44,.36,.006),'berry',.001)

def pantry_shelf():
    for x in (-.52,.52): box('Pantry side',(x,0,.69),(.085,.52,1.38),'wood',.025)
    box('Pantry back',(0,-.23,.69),(1.08,.045,1.38),'wood_dark',.014)
    for row in range(4):
        z=.055+row*.38
        box('Deep pantry shelf',(0,0,z),(1.1,.52,.055),'wood',.02)
        for i in range(4):
            p=(-.39+i*.26,.04,z+.035)
            if row%2: bottle(p,['green','berry','coral','blue'][i],.054,.27)
            else: jar(p,['cream','sage','linen','coral'][i])

def pantry_crates():
    for x in (-.85,0,.85):
        for y in (-.29,.29):
            box('Produce crate bottom',(x,y,.07),(.79,.53,.11),'wood',.015)
            for side in (-1,1):
                for z in (.16,.31,.46): box('Open crate slat',(x,y+side*.255,z),(.80,.042,.115),'wood',.013)
            for side in (-1,1): box('Crate end',(x+side*.38,y,.30),(.045,.5,.45),'wood',.015)
            for i in range(6): ellipsoid('Stored vegetables',(x-.25+(i%3)*.25,y-.13+(i//3)*.25,.39),(.10,.09,.095),'coral' if x<0 else 'green',12,8)

def dining_set():
    for x in (-1.10,1.10):
        for y in (-.28,.28): H.leg(x,y,.71,radius=.044)
    box('Dining apron',(0,0,.61),(2.55,.63,.14),'wood_dark')
    box('Dining oval-edged top',(0,0,.76),(3.0,.93,.09),'wood',.055)
    for x in (-.94,-.31,.31,.94):
        for y in (-.26,.26):
            lathe('Dinner plate',(x,y,.81),[(0,.13),(.013,.135),(.022,.115)],'cream',24)
            tube('Fork',[(x+.16,y-.07,.824),(x+.16,y+.07,.824)],.006,'brass')
    H.plant((0,0,.81))

def game_table():
    H.build('table')
    box('Game board',(0,0,.74),(.60,.60,.035),'wood_dark',.012)
    for x in range(8):
        for y in range(8):
            if (x+y)%2: box('Checker square',(-.2625+x*.075,-.2625+y*.075,.760),(.074,.074,.006),'cream',0)
    for side in (-1,1):
        for x in range(4): lathe('Game piece',(-.225+x*.15,side*.19,.765),[(0,.025),(.012,.028),(.035,.018),(.043,.025)],'coral' if side<0 else 'ink',12)
    book((.46,-.12,.725),.6,'berry')

def tea_service():
    box('Tea tray',(0,0,.017),(.64,.40,.035),'wood',.025)
    for x in (-.29,.29): tube('Tray raised grip',[(x,-.10,.025),(x,-.10,.065),(x,.10,.065),(x,.10,.025)],.012,'brass')
    lathe('Tea pot',(-.13,0,.04),[(0,.065),(.025,.10),(.13,.095),(.17,.062)],'cream',24)
    lathe('Tea pot lid',(-.13,0,.21),[(0,.064),(.018,.068),(.031,.02)],'sage',20)
    tube('Tea spout',[(-.22,0,.09),(-.28,0,.17),(-.30,0,.21)],.024,'cream')
    H.mug((.16,0,.04),'blue')

def bedside_set():
    H.lamp(); book((.25,.02,0),.72,'berry')
    box('Alarm clock body',(-.28,.02,.095),(.20,.08,.17),'coral',.04)
    ring('Clock rim',(-.28,.064,.10),.066,.007,'brass','Y')
    ellipsoid('Clock face',(-.28,.065,.10),(.062,.004,.062),'cream',20,12)
    tube('Clock hands',[(-.28,.07,.15),(-.28,.07,.10),(-.24,.07,.10)],.004,'ink')

def radio():
    box('Radio moulded casing',(0,0,.16),(.49,.23,.30),'blue',.045)
    for x in (-.14,.14):
        ellipsoid('Radio speaker grille',(x,.119,.16),(.086,.005,.086),'ink',20,12)
        ring('Speaker brass surround',(x,.128,.16),.085,.006,'brass','Y')
        for line in range(6):box('Speaker grille slot',(x,.135,.105+line*.02),(.12,.003,.004),'wood_dark',0)
    tube('Carry handle',[(-.15,0,.315),(-.15,0,.39),(.15,0,.39),(.15,0,.315)],.013,'wood_dark')
    tube('Telescopic aerial',[(.19,-.07,.31),(.14,-.07,.58)],.006,'brass')
    for x in (-.17,.17):lathe('Radio control knob',(x,.01,.31),[(0,.023),(.025,.023)],'brass',12)

def wall_shelf():
    box('Floating shelf',(0,0,0),(.95,.23,.055),'wood',.018)
    for x in (-.31,.31): tube('Shelf bracket',[(x,-.075,-.19),(x,-.075,-.018),(x,.10,-.018)],.018,'brass')
    book((-.19,0,.033),.67,'coral');book((-.18,0,.076),.7,'blue');H.plant((.28,0,.03))

def wall_books():
    for x in (-.62,.62): box('Shelf upright',(x,-.07,0),(.075,.23,.91),'wood',.015)
    for row in range(3):
        z=-.43+row*.35
        box('Library wall shelf',(0,0,z),(1.35,.33,.052),'wood',.020)
        for i in range(8):
            x=-.52+i*.148
            box('Bound book spine',(x,.015,z+.15),(.08+(i%2)*.018,.24,.24+(i%3)*.021),['sage','blue','coral','cream','berry'][i%5],.009)
            for dz in (-.065,.066):box('Gold book title band',(x,.141,z+.15+dz),(.068,.004,.008),'brass',.001)

def bath_mirror():
    ring('Oval mirror frame',(0,0,.16),.36,.028,'brass','Y').scale.z=1.20
    ellipsoid('Polished mirror',(0,.009,.16),(.33,.01,.40),'glass',32,20)
    tube('Mirror glint',[(-.17,.022,.36),(.07,.022,.12)],.009,'blue')
    tube('Towel rail',[(-.51,0,-.40),(-.51,.17,-.40),(.51,.17,-.40),(.51,0,-.40)],.018,'brass')
    cushion('Hanging folded towel',(.14,.165,-.58),(.34,.026,.42),'cream')
    for z in (-.73,-.69):box('Towel hem',(.14,.186,z),(.32,.006,.018),'blue',.002)

def kitchen_rack():
    box('Utensil wall board',(0,-.045,.19),(1.2,.035,.67),'wood',.035)
    tube('Kitchen hanging rail',[(-.54,0,.42),(.54,0,.42)],.02,'brass')
    for i,x in enumerate((-.39,0,.37)):
        tube('Hanging utensil stem',[(x,.01,.39),(x,.01,.06)],.012,'brass')
        if i==0:
            lathe('Copper saucepan',(x,0,-.055),[(0,.09),(.02,.13),(.06,.135)],'brass',24).rotation_euler[0]=math.pi/2
            ring('Pan lip',(x,.025,-.055),.133,.008,'wood_dark','Y')
        else: ellipsoid('Spoon or spatula',(x,.01,-.045),(.048 if i==1 else .071,.012,.087),'cream',16,10)
    box('Spice ledge',(0,.065,-.28),(1.24,.22,.05),'wood',.016)
    for i,x in enumerate((-.44,-.21,.02,.25,.47)):
        bottle((x,.06,-.25),['berry','green','coral','blue','cream'][i],.045,.17)

def thread_rack():
    box('Thread organizer backing',(0,-.045,0),(1.25,.04,.82),'wood',.018)
    for row in range(3):
        for col in range(7):
            x=-.51+col*.17;z=-.29+row*.28
            tube('Thread peg',[(x,0,z),(x,.12,z)],.009,'wood_dark')
            spool=lathe('Colored thread spool',(x,.045,z),[(0,.044),(.02,.037),(.10,.037),(.12,.044)],['coral','sage','cream','berry','blue'][col%5],16)
            spool.rotation_euler[0]=math.pi/2
    tube('Scissors one',[(-.30,.11,-.48),(-.15,.11,-.62),(-.05,.11,-.76)],.010,'brass')
    tube('Scissors two',[(-.06,.11,-.48),(-.17,.11,-.62),(-.28,.11,-.76)],.010,'brass')
    for x in (-.30,-.06):ring('Scissors grip',(x,.11,-.44),.049,.009,'brass','Y')

def wall_guitar():
    # Contiguous figure-eight soundbox built from a lofted outline.
    outline=[(-.02,-.50),(-.18,-.48),(-.28,-.34),(-.22,-.20),(-.16,-.08),(-.20,.04),(-.11,.15),(0,.18),(.11,.15),(.20,.04),(.16,-.08),(.22,-.20),(.28,-.34),(.18,-.48)]
    n=len(outline);verts=[(x,y,z) for y in (-.04,.065) for x,z in outline]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    for i in range(n):j=(i+1)%n;faces.append((i,j,j+n,i+n))
    mesh=bpy.data.meshes.new('Guitar carved soundbox');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Guitar body',mesh);bpy.context.collection.objects.link(obj);H.finish(obj,obj.name,'wood')
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    mod=obj.modifiers.new('Rounded guitar edges','BEVEL');mod.width=.025;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    box('Guitar neck',(0,0,.45),(.073,.055,.66),'wood_dark',.014)
    box('Guitar headstock',(0,0,.83),(.12,.057,.18),'wood',.025)
    ring('Guitar sound hole',(0,.07,-.06),.070,.007,'brass','Y')
    ellipsoid('Dark sound hole',(0,.069,-.06),(.065,.002,.065),'ink',20,12)
    box('Guitar bridge',(0,.084,-.30),(.14,.027,.04),'wood_dark',.01)
    for x in (-.022,-.013,-.004,.004,.013,.022):tube('Guitar string',[(x,.10,-.31),(x,.04,.80)],.0008,'brass')
    for z in (.76,.82,.88):
        for x in (-.082,.082):ellipsoid('Tuning key',(x,0,z),(.019,.015,.022),'cream',12,8)

def entry_rack():
    box('Coat rack carved rail',(0,-.06,.27),(1.28,.07,.17),'wood',.025)
    for x in (-.46,0,.46):tube('Coat hook',[(x,-.015,.28),(x,.12,.17),(x,.15,.27)],.019,'brass')
    cushion('Hanging scarf',(-.43,.16,-.15),(.20,.035,.70),'berry')
    for z in (-.48,-.44):box('Scarf woven stripe',(-.43,.186,z),(.195,.005,.026),'cream',.002)
    tube('Key loop',[(.45,.15,.18),(.41,.16,.13),(.45,.16,.08),(.50,.16,.13),(.45,.15,.18)],.008,'brass')
    for x in (.42,.49):tube('House key',[(x,.16,.10),(x,.16,-.06),(x+.04,.16,-.06)],.008,'brass')
    box('Letter shelf',(0,.10,-.62),(1.32,.28,.06),'wood',.022)
    for x in (-.30,0,.28):box('Mail envelope',(x,.07,-.56),(.24,.13,.012),'paper',.004)

def wall_art():
    box('Picture frame backing',(0,0,0),(1.06,.045,.73),'wood_dark',.025)
    box('Linen picture mat',(0,.03,0),(.99,.015,.66),'linen',.005)
    box('Painting sky',(0,.04,0),(.89,.008,.56),'blue',.002)
    # Layered hills, moon and small house, an original relief still life.
    ellipsoid('Painted moon',(-.24,.047,.15),(.09,.003,.09),'cream',20,12)
    ellipsoid('Painted hill',(.20,.047,-.19),(.25,.004,.09),'sage',20,12)
    box('Painted house',(-.10,.05,-.15),(.18,.009,.14),'coral',.002)
    tube('Painted roof',[(-.23,.06,-.06),(-.10,.06,.06),(.03,.06,-.06)],.016,'wood_dark')
    box('Painted window',(-.10,.061,-.13),(.04,.003,.042),'cream',0)

def window_frame():
    for side in (-1,1):
        box('Window carved jamb',(side*.785,0,0),(.095,.17,1.54),'wood',.018)
        box('Window inner jamb',(side*.72,.035,0),(.045,.10,1.42),'cream',.010)
    for z in (-.74,.74): box('Window cross casing',(0,0,z),(1.65,.17,.08),'wood',.018)
    box('Deep window sill',(0,.06,-.80),(1.78,.31,.08),'cream',.025)
    box('Window mullion',(0,.03,0),(.045,.09,1.44),'cream',.012)
    box('Window transom',(0,.03,.02),(1.45,.09,.045),'cream',.012)
    for side in (-1,1): tube('Window latch',[(side*.12,.095,-.04),(side*.12,.14,-.04),(side*.045,.14,-.04)],.012,'brass')

def railing():
    # Nominal one-metre modular section; no hidden collision in exported mesh.
    for x in (0,1):
        lathe('Turned baluster',(x,0,0),[(0,.035),(.11,.035),(.16,.020),(.34,.020),(.42,.042),(.49,.023),(.82,.024),(.87,.035),(.93,.035)],'wood',16)
    box('Rail lower stretcher',(.5,0,.09),(1,.045,.055),'wood',.016)
    box('Rounded handrail',(.5,0,.99),(1.08,.095,.085),'wood_dark',.035)

def rail_post():
    lathe('Turned balustrade post',(0,0,0),[(0,.035),(.11,.035),(.16,.020),(.34,.020),(.42,.035),(.49,.023),(.82,.024),(.89,.035),(.96,.035)],'wood',16)

def rail_bar():
    box('Carved continuous handrail',(.5,0,0),(1,.095,.085),'wood_dark',.032)

def landscape():
    # Distant silhouettes composed as complete tapered trees, not gameplay cover.
    for i in range(7):
        x=(i-3)*3.5; y=.8*math.sin(i*2); h=2.8+(i%3)*.6
        lathe('Night tree trunk',(x,y,0),[(0,.19),(h,.075)],'wood_dark',12)
        for j in range(5):
            a=j*math.tau/5
            ellipsoid('Night tree crown',(x+math.cos(a)*.64,y+math.sin(a)*.55,h-.15+j*.11),(1.0,.80,1.20),'green',14,10)
    for i in range(4):
        x=(i-1.5)*5.4
        box('Distant house',(x,5.0,.95),(3.8,3.0,1.9),'ink',.08)
        verts=[(x-2.05,3.4,1.9),(x+2.05,3.4,1.9),(x+2.05,6.6,1.9),(x-2.05,6.6,1.9),(x,3.4,3.15),(x,6.6,3.15)]
        faces=[(0,1,4),(3,5,2),(0,4,5,3),(1,2,5,4),(0,3,2,1)]
        mesh=bpy.data.meshes.new('Pitched roof');mesh.from_pydata(verts,[],faces);mesh.update()
        obj=bpy.data.objects.new('Night roof',mesh);bpy.context.collection.objects.link(obj);H.finish(obj,obj.name,'wood_dark')
        for dx in (-.9,.9): box('Distant warm window',(x+dx,3.489,1.1),(.45,.01,.65),'brass',.005)

BUILDERS={'door_leaf':door_leaf,'door_frame':door_frame,'molding':molding,'pendant':pendant,
          'bath_vanity':bath_vanity,'toilet':toilet,'washer':washer,'hamper':hamper,
          'television':television,'piano':piano,'sewing_table':sewing_table,'pantry_shelf':pantry_shelf,
          'pantry_crates':pantry_crates,'dining_set':dining_set,'game_table':game_table,
          'tea_service':tea_service,'bedside_set':bedside_set,'wall_shelf':wall_shelf,
          'wall_books':wall_books,'bath_mirror':bath_mirror,'kitchen_rack':kitchen_rack,
          'thread_rack':thread_rack,'wall_guitar':wall_guitar,'entry_rack':entry_rack,'wall_art':wall_art,
          'window_frame':window_frame,'railing':railing,'night_landscape':landscape}
BUILDERS.update({'rail_post':rail_post,'rail_bar':rail_bar,'radio':radio,'bath_shower':bath_shower})

def export(name):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    H.M=H.materials(); BUILDERS[name]()
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join()
    obj=bpy.context.object;obj.name='House07_'+name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001);bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.01)
    bpy.ops.object.mode_set(mode='OBJECT');obj.data.calc_loop_triangles()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
    bpy.ops.export_scene.gltf(filepath=str(H.OUT/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_cameras=False,export_lights=False)
    coords=[obj.matrix_world@Vector(v) for v in obj.bound_box]
    return {'asset':name,'triangles':len(obj.data.loop_triangles),'bounds_blender':{'min':[min(p[i] for p in coords) for i in range(3)],'max':[max(p[i] for p in coords) for i in range(3)]},'bytes':(H.OUT/(name+'.glb')).stat().st_size}

if __name__=='__main__':
    wanted=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(BUILDERS)
    report=[export(name) for name in wanted]
    if len(wanted)!=len(BUILDERS) and (SOURCE/'manifest.json').exists():
        saved={a['asset']:a for a in json.loads((SOURCE/'manifest.json').read_text(encoding='utf-8'))['assets']}
        saved.update({a['asset']:a for a in report})
        report=[saved[name] for name in BUILDERS if name in saved]
    (SOURCE/'manifest.json').write_text(json.dumps({'blender':bpy.app.version_string,'original_art':True,'units':'metre','assets':report},indent=2),encoding='utf-8')
    print('HOUSE07_EXPORT_PASS',len(report))
