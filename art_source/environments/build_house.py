"""Original Let me sleep house library. Run with Blender 4.5.3 --background.

Editable topology, bevelled joinery, lathed ceramics and sewn cushion profiles.
Exports explicit GLB; Godot wrappers retain all catalogue collision envelopes.
"""
from pathlib import Path
import bpy, math, json, random, sys
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'game/assets/art/house'
SOURCE = Path(__file__).resolve().parent / 'house'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
random.seed(61)
bpy.context.preferences.filepaths.save_version=0
PALETTE = {'wood':'A16D43','wood_dark':'654933','sage':'809276','cream':'EDE1C1',
           'ink':'293445','brass':'C69B56','blue':'718CA3','linen':'DBCCA8',
           'coral':'C97D5B','green':'57795C','glass':'20334A','white':'F0EBDB',
           'berry':'976D84','paper':'DFD5B7'}

def linear(value):
    return value/12.92 if value <= .04045 else ((value+.055)/1.055)**2.4

def materials():
    result={}
    for name,hexcolor in PALETTE.items():
        mat=bpy.data.materials.new('House_'+name)
        rgb=tuple(linear(int(hexcolor[i:i+2],16)/255) for i in (0,2,4))
        mat.diffuse_color=(*rgb,1)
        mat.use_nodes=True
        bs=mat.node_tree.nodes.get('Principled BSDF')
        bs.inputs['Base Color'].default_value=(*rgb,1)
        bs.inputs['Roughness'].default_value=.78 if name!='brass' else .38
        bs.inputs['Metallic'].default_value=.5 if name=='brass' else 0
        result[name]=mat
    return result

M={}
def finish(obj,name,material):
    obj.name=name
    obj.data.materials.append(M[material])
    for p in obj.data.polygons: p.use_smooth=True
    return obj

def box(name,p,s,mat='wood',bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p)
    obj=bpy.context.object
    obj.dimensions=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        modifier=obj.modifiers.new('Soft joinery edges','BEVEL')
        modifier.width=min(bevel,min(s)*.43)
        modifier.segments=3
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    finish(obj,name,mat)
    normal=obj.modifiers.new('Weighted panel normals','WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=normal.name)
    return obj

def ellipsoid(name,p,s,mat='cream',segments=20,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=p)
    obj=bpy.context.object
    obj.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat)

def tube(name,points,radius=.018,mat='brass',closed=False):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'
    curve.bevel_depth=radius; curve.bevel_resolution=1; curve.resolution_u=5
    spline=curve.splines.new('BEZIER'); spline.bezier_points.add(len(points)-1)
    for b,p in zip(spline.bezier_points,points):
        b.co=p; b.handle_left_type='AUTO'; b.handle_right_type='AUTO'
    spline.use_cyclic_u=closed
    obj=bpy.data.objects.new(name,curve); bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    finish(obj,name,mat); obj.select_set(False)
    return obj

def lathe(name,p,profile,mat='cream',segments=24):
    vertices=[]; faces=[]
    for z,r in profile:
        for i in range(segments):
            a=math.tau*i/segments
            vertices.append((p[0]+r*math.cos(a),p[1]+r*math.sin(a),p[2]+z))
    for row in range(len(profile)-1):
        for i in range(segments):
            j=(i+1)%segments
            faces.append((row*segments+i,row*segments+j,(row+1)*segments+j,(row+1)*segments+i))
    faces += [tuple(reversed(range(segments))),tuple((len(profile)-1)*segments+i for i in range(segments))]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    return finish(obj,name,mat)

def cushion(name,p,s,mat='blue',piping=True):
    # A continuous superellipse textile mesh with compressed seams and puffed faces.
    vertices=[]; faces=[]; sides=40
    rings=[(-.5,.77),(-.40,.94),(-.22,1.0),(0,1.015),(.22,1.0),(.40,.94),(.5,.77)]
    for z,spread in rings:
        for i in range(sides):
            a=math.tau*i/sides; c=math.cos(a); q=math.sin(a)
            x=math.copysign(abs(c)**.30,c)*s[0]*.5*spread
            y=math.copysign(abs(q)**.30,q)*s[1]*.5*spread
            vertices.append((p[0]+x,p[1]+y,p[2]+z*s[2]))
    for row in range(len(rings)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+i,row*sides+j,(row+1)*sides+j,(row+1)*sides+i))
    faces.extend([tuple(reversed(range(sides))),tuple((len(rings)-1)*sides+i for i in range(sides))])
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); finish(obj,name,mat)
    if piping:
        points=[]
        for i in range(16):
            a=math.tau*i/16; c=math.cos(a); q=math.sin(a)
            points.append((p[0]+math.copysign(abs(c)**.30,c)*s[0]*.503,p[1]+math.copysign(abs(q)**.30,q)*s[1]*.503,p[2]))
        tube(name+'_sewn_edge',points,.005,'linen',True)
    return obj

def leg(x,y,height,mat='wood_dark',radius=.05):
    lathe('Turned tapered leg',(x,y,0),[(0,radius*.75),(.025,radius),(.07,radius*.8),(height*.8,radius*.9),(height,radius*1.1)],mat,12)

def cabinet_door(x,y,z,w,h,mat='sage'):
    box('Door recessed panel',(x,y,z),(w,.035,h),mat,.02)
    for sx in (-1,1): box('Door stile',(x+sx*(w/2-.025),y+.024,z),(.05,.034,h),mat,.012)
    for sz in (-1,1): box('Door rail',(x,y+.024,z+sz*(h/2-.025)),(w-.07,.034,.05),mat,.012)
    tube('Arched brass pull',[(x+w*.24,y+.055,z-.055),(x+w*.24,y+.095,z),(x+w*.24,y+.055,z+.055)],.012)

def mug(p=(0,0,0),mat='blue'):
    x,y,z=p
    lathe('Cup body',p,[(0,.065),(.015,.078),(.14,.08),(.155,.075),(.15,.064),(.035,.062)],mat)
    tube('Cup handle',[(x+.065,y,z+.12),(x+.12,y,z+.14),(x+.135,y,z+.07),(x+.07,y,z+.04)],.015,mat)
    lathe('Tea',(x,y,z+.13),[(0,.061),(.002,.061)],'wood_dark',20)

def plant(p=(0,0,0)):
    lathe('Ceramic planter',p,[(0,.07),(.025,.085),(.14,.105),(.16,.11),(.17,.11),(.17,.094)],'coral')
    for i in range(7):
        a=math.tau*i/7; x=p[0]+math.cos(a)*.09; y=p[1]+math.sin(a)*.09
        tube('Plant stem',[p,(p[0],p[1],p[2]+.18),(x,y,p[2]+.32)],.006,'green')
        leaf=ellipsoid('Broad leaf',(x,y,p[2]+.32),(.038,.018,.105),'green',12,8)
        leaf.rotation_euler=(math.sin(a)*.65,math.cos(a)*.65,a)

def lamp():
    lathe('Lamp base',(0,0,0),[(0,.16),(.025,.17),(.05,.14),(.065,.10)],'wood',24)
    lathe('Lamp ceramic stem',(0,0,.06),[(0,.06),(.06,.11),(.18,.10),(.27,.045)],'sage')
    lathe('Lampshade',(0,0,.28),[(0,.26),(.025,.26),(.24,.14),(.265,.14),(.265,.13),(.23,.13),(.025,.246),(0,.246)],'cream',32)
    for i in range(16):
        a=math.tau*i/16
        tube('Shade stitched rib',[(.259*math.cos(a),.259*math.sin(a),.305),(.14*math.cos(a),.14*math.sin(a),.535)],.002,'linen')

def build(asset):
    if asset in ('sofa','armchair'):
        width=2.4 if asset=='sofa' else 1.15; count=3 if asset=='sofa' else 1
        for x in (-width*.41,width*.41):
            for y in (-.34,.34): leg(x,y,.22)
        box('Upholstered foundation',(0,0,.30),(width,.91,.27),'blue',.1)
        for x in (-width*.46,width*.46):
            cushion('Rolled arm',(x,0,.59),(.25,.98,.47),'blue')
        for i in range(count):
            x=(i-(count-1)/2)*(width-.35)/count
            cushion('Seat cushion',(x,.10,.52),((width-.38)/count,.68,.21),'blue')
            cushion('Back cushion',(x,-.32,.75),((width-.32)/count,.25,.53),'blue')
        cushion('Accent pillow',(width*.24,-.10,.72),(.35,.17,.37),'cream')
        ellipsoid('Pillow tuft',(width*.24,-.006,.72),(.024,.016,.024),'brass',12,6)
    elif asset=='bed':
        for x in (-.80,.80):
            for y in (-.47,.47): leg(x,y,.20)
        box('Bed wooden frame',(0,0,.20),(1.98,1.17,.19),'wood',.07)
        cushion('Mattress',(0,0,.36),(1.94,1.13,.22),'linen')
        # Horizontal bed in the catalogue: head is at the left short end.
        box('Curved headboard',(-.91,0,.46),(.13,1.18,.58),'wood',.065)
        box('Inset padded headboard',(-.825,0,.52),(.035,1.0,.27),'sage',.02)
        cushion('Draped duvet',(.30,0,.485),(1.25,1.12,.15),'sage')
        cushion('Folded blanket edge',(-.30,0,.525),(.16,1.1,.10),'cream')
        for y in (-.29,.29): cushion('Sleeping pillow',(-.58,y,.50),(.40,.48,.15),'cream')
        for y in (-.32,0,.32): tube('Duvet seam',[(-.15,y,.556),(.32,y,.568),(.79,y,.556)],.004,'linen')
    elif asset in ('table','desk','nightstand'):
        width=1.45 if asset!='nightstand' else .60; depth=.85 if asset!='nightstand' else .52; height=.72 if asset!='nightstand' else .59
        for x in (-width*.40,width*.40):
            for y in (-depth*.36,depth*.36): leg(x,y,height-.10,radius=.038)
        box('Apron',(0,0,height-.17),(width*.84,depth*.76,.15),'wood_dark')
        box('Rounded tabletop',(0,0,height-.045),(width,depth,.09),'wood',.04)
        if asset=='desk':
            box('Drawer',(width*.27,.06,height-.21),(.40,depth*.65,.20),'sage')
            tube('Drawer handle',[(width*.20,depth*.395,height-.21),(width*.27,depth*.43,height-.21),(width*.34,depth*.395,height-.21)],.012)
        if asset=='nightstand':
            box('Lower shelf',(0,0,.18),(width*.79,depth*.70,.055),'wood')
    elif asset in ('wardrobe','dresser','bookcase'):
        height=1.35; width=1.10; depth=.48
        for x in (-.42,.42):
            for y in (-.15,.15): leg(x,y,.12,radius=.037)
        box('Carcass back',(0,-.21,.72),(width,.07,1.16),'wood_dark')
        for x in (-.51,.51): box('Carcass side',(x,0,.72),(.09,depth,1.16),'sage')
        for z in (.15,1.30): box('Moulded plinth',(0,0,z),(width+.02,depth+.04,.10),'sage',.03)
        if asset=='bookcase':
            for row in range(3):
                z=.18+row*.35
                box('Shelf',(0,0,z),(1.0,.44,.045),'wood')
                for i in range(5):
                    x=-.39+i*.15
                    book=box('Book spine',(x,.07,z+.15),(.07+.012*(i%2),.26,.22+.035*(i%3)),['coral','blue','cream','berry','sage'][i],.01)
                    if i==4: book.rotation_euler[1]=-.14
        elif asset=='dresser':
            for row in range(3):
                z=.35+row*.34
                box('Drawer front',(0,.247,z),(.94,.055,.29),'sage',.02)
                for x in (-.25,.25): ellipsoid('Drawer brass knob',(x,.295,z),(.025,.023,.025),'brass',12,8)
        else:
            for x in (-.255,.255): cabinet_door(x,.235,.73,.49,1.08)
    elif asset in ('sink','stove','fridge'):
        if asset=='fridge':
            box('Rounded fridge body',(0,0,.73),(.72,.65,1.46),'cream',.075)
            for z,h in [(.45,.73),(1.14,.56)]:
                box('Enamel door',(0,.345,z),(.68,.065,h),'cream',.04)
                tube('Fridge handle',[(-.23,.405,z-.13),(-.23,.445,z),(-.23,.405,z+.13)],.02,'brass')
            box('Pinned note',(.12,.387,1.18),(.16,.006,.19),'paper',.004)
            ellipsoid('Magnet',(.12,.399,1.23),(.028,.009,.028),'coral',12,8)
            return
        width=1.70; height=.88
        box('Kitchen kickplate',(0,0,.09),(1.57,.51,.18),'wood_dark',.015)
        box('Base cabinetry',(0,0,.44),(width,.61,.68),'sage')
        box('Thick rounded worktop',(0,0,.815),(width+.02,.67,.09),'cream',.035)
        if asset=='sink':
            for x in (-.42,.42): cabinet_door(x,.32,.45,.79,.57)
            box('Sink dark recess',(-.24,0,.866),(.68,.46,.008),'glass',.035)
            for x in (-.60,.12): box('Sink rim side',(x,0,.875),(.025,.48,.025),'brass',.01)
            for y in (-.24,.24): box('Sink rim edge',(-.24,y,.875),(.73,.025,.025),'brass',.01)
            tube('Curved kitchen faucet',[(-.24,-.22,.87),(-.24,-.22,1.03),(-.24,-.08,1.10),(-.24,.02,1.02)],.023)
            mug((.43,.07,.87),'blue')
            box('Folded towel',(.55,-.11,.89),(.30,.23,.04),'linen',.015)
        else:
            box('Oven door',(-.40,.326,.43),(.70,.045,.54),'ink',.035)
            box('Oven glass',(-.40,.355,.40),(.54,.013,.35),'glass',.02)
            tube('Oven handle',[(-.67,.375,.66),(-.40,.42,.66),(-.13,.375,.66)],.018)
            cabinet_door(.43,.32,.45,.70,.57)
            for x in (-.58,-.25):
                for y in (-.15,.15):
                    lathe('Burner',(x,y,.868),[(0,.10),(.022,.10),(.022,.065),(.03,.065)],'ink',16)
                    for a in (0,math.pi/2):
                        dx=.10*math.cos(a);dy=.10*math.sin(a)
                        tube('Hob crossbar',[(x-dx,y-dy,.91),(x+dx,y+dy,.91)],.01,'ink')
            for x in (-.62,-.39,-.16): ellipsoid('Oven dial',(x,.359,.748),(.027,.018,.027),'brass',12,8)
            lathe('Stockpot',(.45,-.03,.87),[(0,.16),(.035,.17),(.17,.17),(.18,.18),(.20,.16)],'coral')
            lathe('Lid',(.45,-.03,1.06),[(0,.17),(.035,.14),(.043,.03)],'brass')
            ellipsoid('Lid grip',(.45,-.03,1.13),(.036,.036,.024),'ink')
    elif asset=='lamp': lamp()
    elif asset=='plant': plant()
    elif asset=='mug': mug()
    elif asset=='swatter':
        tube('Curved swatter handle',[(0,0,0),(0,0,.26),(0,.005,.43)],.014,'coral')
        points=[(-.10,0,.43),(-.13,0,.53),(-.10,0,.67),(.10,0,.67),(.13,0,.53),(.10,0,.43)]
        tube('Swatter rim',points,.017,'sage',True)
        for x in (-.075,-.025,.025,.075): tube('Swatter mesh',[(x,0,.445),(x,0,.66)],.003,'sage')
        for z in (.47,.51,.55,.59,.63): tube('Swatter weave',[(-.115,0,z),(.115,0,z)],.003,'sage')
        box('Grip',(0,0,.07),(.044,.027,.13),'coral',.012)
    elif asset=='newspaper':
        lathe('Rolled news',(0,0,0),[(0,.029),(.28,.040),(.34,.05),(.34,.032),(.29,.029)],'paper',20)
        for z in (.08,.11,.14,.22):
            tube('Printed column',[(-.022,-.029,z),(0,-.040,z+.006),(.025,-.030,z+.003)],.002,'ink')

def export_asset(asset):
    global M
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    M=materials()
    if asset=='curtain':
        vertices=[]; faces=[]; columns=24; rows=20
        for row in range(rows+1):
            t=row/rows; z=t*1.58
            width=.20*(.66+.34*abs(t-.48)*2)
            for col in range(columns+1):
                u=col/columns
                vertices.append(((u-.5)*width*2,.035*math.cos(u*math.tau*5)*(1-.25*math.sin(t*math.pi)),z+.022*math.sin(u*math.pi)))
        for row in range(rows):
            for col in range(columns):
                a=row*(columns+1)+col
                faces.append((a,a+1,a+columns+2,a+columns+1))
        mesh=bpy.data.meshes.new('Woven curtain'); mesh.from_pydata(vertices,[],faces); mesh.update()
        obj=bpy.data.objects.new('Gathered cloth',mesh); bpy.context.collection.objects.link(obj)
        finish(obj,'Gathered cloth','blue')
        bpy.context.view_layer.objects.active=obj
        solid=obj.modifiers.new('Hem thickness','SOLIDIFY'); solid.thickness=.006
        bpy.ops.object.modifier_apply(modifier=solid.name)
        tube('Tie cord',[(-.15,.01,.76),(0,.055,.73),(.15,.01,.76)],.009,'brass')
    else:
        build(asset)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes: obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]; bpy.ops.object.join()
    obj=bpy.context.object; obj.name='House_'+asset
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Explicit UVs make sources ready for a later authored atlas/lightmap pass.
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.01)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.data.calc_loop_triangles()
    coords=[obj.matrix_world @ Vector(v) for v in obj.bound_box]
    lower=[min(p[i] for p in coords) for i in range(3)]; upper=[max(p[i] for p in coords) for i in range(3)]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(asset+'.blend')),compress=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(asset+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_cameras=False,export_lights=False)
    return {'asset':asset,'triangles':len(obj.data.loop_triangles),'materials':len(obj.data.materials),'bounds_blender':{'min':lower,'max':upper},'glb_bytes':(OUT/(asset+'.glb')).stat().st_size}

ASSETS=['sofa','armchair','bed','table','desk','nightstand','wardrobe','dresser','bookcase','sink','stove','fridge','lamp','plant','mug','swatter','newspaper','curtain']
if __name__ == '__main__':
    report=[export_asset(asset) for asset in ASSETS]
    (SOURCE/'manifest.json').write_text(json.dumps({'blender':bpy.app.version_string,'original_art':True,'unit':'metre','assets':report},indent=2),encoding='utf-8')
    print('HOUSE_EXPORT_COMPLETE',json.dumps(report))
