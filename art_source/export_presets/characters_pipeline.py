"""Original Let me sleep character authoring pipeline, rig contract 0.6.

Run with Blender --background --python this_file -- --root <repository>.
Coordinates in design code are Godot metres (X right, Y up, -Z forward).
Editable Blender meshes/weights, actions and GLB exports are generated together.
No third-party models, textures or motion data are used.
"""
import argparse, json, math, os, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion, kdtree

TAU=math.tau
ROOT=Path(__file__).resolve().parents[2]
MATS={}
BONES={}
RIG=None
OBJECTS=[]

def g(v): return Vector((v[0],-v[2],v[1]))
def p(v): return Vector(v)
def mix(a,b,t): return a+(b-a)*t
def color(h): return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))+(1,)

def mat(name,hexcolor,roughness=.68,metal=0):
    material=bpy.data.materials.new(name)
    material.diffuse_color=color(hexcolor)
    material.use_nodes=True
    bsdf=material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=color(hexcolor)
    bsdf.inputs['Roughness'].default_value=roughness
    bsdf.inputs['Metallic'].default_value=metal
    MATS[name]=material
    return material

class Mesh:
    def __init__(self,name):
        self.name=name; self.v=[]; self.f=[]; self.uv=[]; self.weights=[]; self.mi=[]; self.materials=[]
    def material(self,key):
        if key not in self.materials: self.materials.append(key)
        return self.materials.index(key)
    def vertex(self,point,uv=(0,0),weights=None):
        self.v.append(tuple(g(point)));self.uv.append(uv);self.weights.append(weights or {'root':1})
        return len(self.v)-1
    def face(self,indices,material):
        self.f.append(tuple(indices));self.mi.append(self.material(material))
    def loft(self,centers,radii,material,bone='root',segments=24,weights=None):
        # Elliptical sweeps in the authoring X/Z plane; arbitrary paths use tube.
        start=len(self.v)
        for j,center in enumerate(centers):
            rx,rz=radii[j]
            for i in range(segments+1):
                angle=TAU*i/segments
                q=p(center)+Vector((math.cos(angle)*rx,0,math.sin(angle)*rz))
                self.vertex(q,(i/segments,j/max(1,len(centers)-1)),weights(j,q) if weights else {bone:1})
        for j in range(len(centers)-1):
            for i in range(segments):
                a=start+j*(segments+1)+i;b=a+segments+1
                self.face((a,a+1,b+1,b),material)
        self.face([start+i for i in range(segments-1,-1,-1)],material)
        end=start+(len(centers)-1)*(segments+1)
        self.face([end+i for i in range(segments)],material)
    def ellipsoid(self,center,scale,material,bone='root',segments=24,rings=14,sculpt=None):
        start=len(self.v)
        for j in range(rings+1):
            lat=-math.pi/2+math.pi*j/rings
            for i in range(segments+1):
                a=TAU*i/segments
                local=Vector((math.cos(lat)*math.cos(a),math.sin(lat),math.cos(lat)*math.sin(a)))
                q=Vector((local.x*scale[0],local.y*scale[1],local.z*scale[2]))
                if sculpt:q=sculpt(q,local)
                self.vertex(p(center)+q,(i/segments,j/rings),{bone:1})
        for j in range(rings):
            for i in range(segments):
                a=start+j*(segments+1)+i;b=a+segments+1
                self.face((a,a+1,b+1,b),material)
    def tube(self,points,radii,material,bone='root',segments=10,weights=None):
        points=[p(q) for q in points];start=len(self.v)
        previous_u=None
        for j,center in enumerate(points):
            tangent=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized()
            reference=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((0,0,1))
            u=tangent.cross(reference).normalized() if previous_u is None else (previous_u-tangent*previous_u.dot(tangent)).normalized()
            if u.length<.1:u=tangent.cross(reference).normalized()
            previous_u=u;v=tangent.cross(u).normalized()
            r=radii[j] if isinstance(radii,(list,tuple)) else radii
            for i in range(segments+1):
                a=TAU*i/segments
                q=center+(math.cos(a)*u*r[0]+math.sin(a)*v*r[1]) if isinstance(r,(list,tuple)) else center+(math.cos(a)*u+math.sin(a)*v)*r
                self.vertex(q,(i/segments,j/max(1,len(points)-1)),weights(j,q) if weights else {bone:1})
        for j in range(len(points)-1):
            for i in range(segments):
                a=start+j*(segments+1)+i;b=a+segments+1
                self.face((a,a+1,b+1,b),material)
        self.face([start+i for i in range(segments-1,-1,-1)],material)
        end=start+(len(points)-1)*(segments+1)
        self.face([end+i for i in range(segments)],material)
    def finish(self):
        data=bpy.data.meshes.new(self.name+'_mesh');data.from_pydata(self.v,[],self.f);data.update()
        for key in self.materials:data.materials.append(MATS[key])
        uv=data.uv_layers.new(name='UVMap')
        for poly,index in zip(data.polygons,self.mi):
            poly.material_index=index;poly.use_smooth=True
            for loop in poly.loop_indices:uv.data[loop].uv=self.uv[data.loops[loop].vertex_index]
        obj=bpy.data.objects.new(self.name,data);bpy.context.collection.objects.link(obj)
        groups={key:obj.vertex_groups.new(name=key) for key in sorted({k for w in self.weights for k in w})}
        for i,weights in enumerate(self.weights):
            total=sum(weights.values())
            for key,value in weights.items():groups[key].add([i],value/total,'REPLACE')
        modifier=obj.modifiers.new('Shared deformation rig','ARMATURE');modifier.object=RIG
        obj.parent=RIG
        obj['lms_original_authoring']='characters_pipeline.py'
        obj['lms_rig']='lms06_'+('human' if 'human' in self.name else 'mosquito')
        OBJECTS.append(obj)
        return obj

def curve(points,steps=4):
    pts=[p(points[0])]+[p(q) for q in points]+[p(points[-1])];result=[]
    for i in range(1,len(pts)-2):
        a,b,c,d=pts[i-1:i+3]
        for j in range(steps):
            t=j/steps
            result.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    result.append(p(points[-1]));return result

def armature(species,bones):
    global RIG,BONES
    BONES=bones
    data=bpy.data.armatures.new('LMS06_'+species+'_Skeleton')
    RIG=bpy.data.objects.new('LMS06_'+species+'_Rig',data);bpy.context.collection.objects.link(RIG)
    bpy.context.view_layer.objects.active=RIG;RIG.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for name,definition in bones.items():
        bone=data.edit_bones.new(name);bone.head=g(definition[0]);bone.tail=g(definition[1])
        if len(definition)>2:bone.parent=data.edit_bones[definition[2]]
        bone.use_deform=True
    bpy.ops.object.mode_set(mode='OBJECT');RIG.show_in_front=True
    RIG['rig_version']='LMS06.1';RIG['units']='metres';RIG['forward']='Godot -Z';RIG['authority']='HumanPose.sample / validated actor state'

def reset(species):
    global OBJECTS,MATS
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for collection in [bpy.data.actions,bpy.data.meshes,bpy.data.armatures,bpy.data.materials]:
        for block in list(collection):collection.remove(block)
    OBJECTS=[];MATS={}
    mat('skin','E3AC83',.72);mat('skin_shadow','C88E6A',.76);mat('lip','AB735A',.72)
    mat('ink','28303A',.6);mat('eye_white','FFF2D4',.35);mat('pupil','1C2831',.24)
    mat('hair','4A3028',.75);mat('primary','7894B4',.83);mat('secondary','D8DFDC',.86)
    mat('cap_cloth','7894B4',.84)
    mat('accent','DAB56B',.7);mat('sole','334557',.8);mat('insect_dark','49362B',.56)
    mat('insect_primary','A47A48',.52);mat('wing','D8E7E8',.34)
    MATS['wing'].diffuse_color=(.70,.83,.86,.63)
    MATS['wing'].node_tree.nodes.get('Principled BSDF').inputs['Alpha'].default_value=.63
    mat('wing_vein','8AAFB9',.6)

def human_bones():
    bones={'root':((0,0,0),(0,.12,0)), 'pelvis':((0,.73,.0),(0,.91,.0),'root'),
           'torso':((0,1.09,0),(0,1.25,0),'root'),'head':((0,1.55,0),(0,1.74,0),'root')}
    for side,s in [(-1,'l'),(1,'r')]:
        hip=(side*.135,.75,-.11);knee=(side*.135,.42,-.11);ankle=(side*.135,.10,0)
        # Natural authoring rest; gameplay still supplies every exact joint.
        shoulder=(side*.28,1.30,0);elbow=(side*.31,1.02,.015);hand=(side*.31,.75,-.015)
        bones.update({'thigh_'+s:(hip,knee,'root'),'shin_'+s:(knee,ankle,'root'),'foot_'+s:(ankle,(ankle[0],.10,-.22),'root'),
                      'upperarm_'+s:(shoulder,elbow,'root'),'forearm_'+s:(elbow,hand,'root'),'hand_'+s:(hand,p(hand)+(p(hand)-p(elbow)).normalized()*.10,'root')})
        align=Vector((0,-1,0)).rotation_difference((p(hand)-p(elbow)).normalized())
        for finger in range(4):
            x=hand[0]+side*(-.041+finger*.027)
            base=(x,hand[1]-.023,hand[2]);joint=(x,hand[1]-.060,hand[2]-.012);tip=(x,hand[1]-.092+(0.008 if finger in [0,3] else 0),hand[2]-.026)
            base=p(hand)+align@(p(base)-p(hand));joint=p(hand)+align@(p(joint)-p(hand));tip=p(hand)+align@(p(tip)-p(hand))
            bones['finger%d_a_%s'%(finger,s)]=(base,joint,'hand_'+s)
            bones['finger%d_b_%s'%(finger,s)]=(joint,tip,'finger%d_a_%s'%(finger,s))
    return bones

def human():
    reset('human');armature('human',human_bones())
    # Face planes and adult jaw: narrower than the previous sphere, stronger nose.
    mesh=Mesh('human_head')
    def skull(q,v):
        q.x*=.80+.20*min(1,max(0,(v.y+.85)/1.1))
        if v.z<0:q.z-=.008*max(0,1-abs(v.y+.42)*2)
        return q
    mesh.ellipsoid((0,1.55,0),(.169,.222,.151),'skin','head',32,22,skull)
    mesh.loft([(0,1.31,.015),(0,1.40,.014),(0,1.43,.01)],[(.092,.084),(.085,.077),(.082,.07)],'skin','head')
    mesh.ellipsoid((0,1.537,-.172),(.060,.050,.074),'skin','head',24,14)
    mesh.ellipsoid((0,1.517,-.212),(.052,.028,.032),'skin_shadow','head',20,10)
    for side in [-1,1]:
        mesh.ellipsoid((side*.169,1.535,-.004),(.035,.064,.028),'skin','head',20,12)
        mesh.ellipsoid((side*.183,1.537,-.024),(.019,.039,.006),'skin_shadow','head',16,10)
        mesh.ellipsoid((side*.103,1.49,-.117),(.034,.031,.020),'skin','head',20,12)
        mesh.ellipsoid((side*.023,1.514,-.238),(.008,.004,.004),'lip','head',12,6)
    mesh.finish()
    mesh=Mesh('human_core')
    # Skin sleeves and articulated hands, weighted to the same game arm rig.
    for side,s in [(-1,'l'),(1,'r')]:
        a=p(BONES['forearm_'+s][0]);b=p(BONES['forearm_'+s][1]);direction=(b-a).normalized()
        points=[a-direction*.025,a,a.lerp(b,.35),a.lerp(b,.75),b-direction*.015,b+direction*.022,b+direction*.045,b+direction*.060]
        mesh.tube(points,[(.060,.060),(.078,.078),(.080,.080),(.062,.060),(.042,.034),(.058,.032),(.056,.027),(.041,.020)],'skin','forearm_'+s,24,
                  lambda j,q,s=s:{'forearm_'+s:1-min(1,max(0,(j-3)/2)),'hand_'+s:min(1,max(0,(j-3)/2))})
        hand=p(BONES['hand_'+s][0])
        palm_start=len(mesh.v)
        thumb=curve([hand+Vector((-side*.038,-.012,0)),hand+Vector((-side*.068,-.038,-.006)),hand+Vector((-side*.058,-.069,-.021))],4)
        mesh.tube(thumb,[.022*(1-.36*i/(len(thumb)-1)) for i in range(len(thumb))],'skin','hand_'+s,10)
        align=Vector((0,-1,0)).rotation_difference(direction)
        for index in range(palm_start,len(mesh.v)):
            point=mesh.v[index];q=Vector((point[0],point[2],-point[1]))
            mesh.v[index]=tuple(g(hand+align@(q-hand)))
        for finger in range(4):
            ba='finger%d_a_%s'%(finger,s);bb='finger%d_b_%s'%(finger,s)
            points=curve([BONES[ba][0],BONES[ba][1],BONES[bb][1]],4)
            mesh.tube(points,[.0145*(1-.28*i/(len(points)-1)) for i in range(len(points))],'skin',ba,10,
                      lambda j,q,ba=ba,bb=bb,n=len(points):{ba:1-j/(n-1),bb:j/(n-1)})
            mesh.ellipsoid(BONES[bb][1],(.011,.013,.011),'skin',bb,12,8)
    mesh.finish()
    # Three compatible garment families. Source contains all; runtime shows one.
    for style in range(3):
        mesh=Mesh('human_outfit_%d'%style)
        trim=Mesh('human_outfit_%d_trim'%style)
        centers=[];radii=[]
        for i in range(19):
            y=.79+.635*i/18
            distance=max(0,abs(y-1.09)-.10)
            radius=math.sqrt(max(.000025,.24*.24-distance*distance))
            if y<.92:radius=max(radius,.18)
            centers.append((0,y,0));radii.append((radius,radius))
        mesh.loft(centers,radii,'primary','torso',32)
        mesh.ellipsoid((0,.75,.025),(.20,.12,.18),'primary','pelvis',24,14)
        for side,s in [(-1,'l'),(1,'r')]:
            hip=p(BONES['thigh_'+s][0]);knee=p(BONES['shin_'+s][0]);ankle=p(BONES['shin_'+s][1])
            points=[hip+Vector((0,.09,0)),hip,hip.lerp(knee,.3),hip.lerp(knee,.75),knee,knee.lerp(ankle,.25),knee.lerp(ankle,.65),ankle,ankle+Vector((0,-.025,0))]
            mesh.tube(points,[.075,.105,.105,.105,.105,.105,.105,.101,.086],'primary','thigh_'+s,28,
                      lambda j,q,s=s:{'thigh_'+s:1-min(1,max(0,(j-3)/2)),'shin_'+s:min(1,max(0,(j-3)/2))})
            a=p(BONES['upperarm_'+s][0]);b=p(BONES['upperarm_'+s][1]);d=(b-a).normalized()
            mesh.ellipsoid(a+Vector((0,-.020,0)),(.087,.079,.087),'primary','upperarm_'+s,24,16)
            bridge=[Vector((side*.13,1.30,0)),Vector((side*.22,1.285,0)),a]
            mesh.tube(bridge,[.078,.077,.078],'primary','torso',24,
                      lambda j,q,s=s:{'torso':1-j/2,'upperarm_'+s:j/2})
            points=[a-d*.022,a,a.lerp(b,.35),a.lerp(b,.82),b+d*.014]
            mesh.tube(points,[.054,.082,.087,.090,.080],'primary','upperarm_'+s,24)
            # Rolled sleeve cuff has a clear seam but leaves the forearm target skin exposed.
            trim.tube([b-d*.024,b-d*.016,b+d*.010],[.106,.109,.087],'secondary','upperarm_'+s,24)
            # Swept fabric collar, not a block on the first-person abdomen.
            collar=curve([(side*.078,1.395,-.088),(side*.113,1.345,-.123),(side*.068,1.307,-.171)],4)
            trim.tube(collar,[.030,.030,.028,.027,.027,.025,.023,.02,.012],'secondary','torso',8)
        seam=curve([(0,1.30,-.201),(0,1.15,-.243),(0,.99,-.243),(0,.80,-.162)],5)
        trim.tube(seam,.005,'secondary','torso',8)
        for y,z in [(1.255,-.231),(1.105,-.246),(.947,-.238),(.835,-.185)]:
            trim.ellipsoid((0,y,z),(.011,.011,.005),'accent','torso',12,8)
        if style==2:
            # Vest lapels and angled pocket retain the underlying attacking surface.
            for side in [-1,1]:
                trim.tube(curve([(side*.10,1.355,-.13),(side*.062,1.21,-.234),(side*.035,1.08,-.246)],4),.012,'accent','torso',10)
                trim.tube(curve([(side*.095,.955,-.216),(side*.18,.979,-.163)],5),.009,'accent','torso',8)
        mesh.finish();trim.finish()
    for expression in range(3): human_face(expression)
    for style in range(3):
        human_hair(style)
        human_hair(style,True)
    for style in range(3): human_shoes(style)
    human_accessories()
    create_actions('human')
    finish('human')

def human_face(expression):
    mesh=Mesh('human_face_%d'%expression)
    blink_vertices={}
    drowsy_vertices={}
    for side in [-1,1]:
        eye_start=len(mesh.v)
        mesh.ellipsoid((side*.068,1.596,-.142),(.051,.043,.029),'eye_white','head',24,16)
        gaze=-.006 if expression==1 else .005
        mesh.ellipsoid((side*.068+gaze,1.59,-.168),(.018,.023,.011),'pupil','head',20,12)
        mesh.ellipsoid((side*.065+gaze,1.601,-.179),(.0055,.007,.002),'eye_white','head',12,8)
        for index in range(eye_start,len(mesh.v)):
            value=Vector(mesh.v[index]);value.z=1.579+(value.z-1.579)*.025;blink_vertices[index]=value
        # Upper eyelid is sculpted skin; the sleeping style has a lowered arc.
        lid=curve([(side*.020,1.602 if expression==1 else 1.616,-.156),(side*.066,1.612 if expression==1 else 1.637,-.173),(side*.115,1.606 if expression==1 else 1.615,-.148)],5)
        lid_start=len(mesh.v)
        mesh.tube(lid,.011 if expression==1 else .006,'skin','head',10)
        for index in range(lid_start,len(mesh.v)):
            value=Vector(mesh.v[index]);value.z-=.024 if expression==1 else .037;blink_vertices[index]=value
        brow=curve([(side*.022,1.656 if expression!=2 else 1.646,-.136),(side*.064,1.67,-.135),(side*.112,1.650 if expression!=2 else 1.675,-.119)],5)
        brow_start=len(mesh.v)
        mesh.tube(brow,[.005+math.sin(math.pi*i/(len(brow)-1))*.009 for i in range(len(brow))],'hair','head',10)
        for index in range(brow_start,len(mesh.v)):
            value=Vector(mesh.v[index]);value.z+=.013*(1-min(1,abs(value.x)/.13));drowsy_vertices[index]=value
        bag=curve([(side*.025,1.566,-.154),(side*.065,1.557,-.161),(side*.105,1.568,-.14)],4)
        mesh.tube(bag,.003,'skin_shadow','head',8)
    mouth=curve([(-.060,1.459,-.132),(-.026,1.449 if expression!=2 else 1.462,-.149),(.016,1.451,-.151),(.061,1.466,-.129)],5)
    mesh.tube(mouth,.0045,'lip','head',8)
    if expression==2:mesh.ellipsoid((0,1.45,-.146),(.024,.009,.005),'ink','head',20,10)
    obj=mesh.finish()
    obj.shape_key_add(name='Basis')
    blink=obj.shape_key_add(name='Blink')
    for index,value in blink_vertices.items():blink.data[index].co=value
    drowsy=obj.shape_key_add(name='DrowsyBrow')
    for index,value in drowsy_vertices.items():drowsy.data[index].co=value

def human_hair(style,capped=False):
    mesh=Mesh('human_hair_%d'%style+('_capped' if capped else ''))
    if capped:
        # A separate fitted haircut under hats: no flattened poles or pointed
        # tufts penetrating the brim. Rounded fringes retain the three styles.
        mesh.loft([(0,1.683,.010),(0,1.710,.015),(0,1.738,.015)],[(.152,.150),(.170,.151),(.169,.145)],'hair','head',40)
        for index in range(41):
            x,y,z=mesh.v[index]
            frequency=10 if style==2 else 3 if style==1 else 4
            mesh.v[index]=(x,y,z+.004*(1+math.sin(index/40*TAU*frequency+style)))
        for side in [-1,1]:
            mesh.ellipsoid((side*.145,1.603,.035),(.028,.091,.048),'hair','head',20,12)
        mesh.finish()
        return
    mesh.ellipsoid((0,1.70,.020),(.171,.098,.143),'hair','head',28,16,
        lambda q,v:Vector((q.x,q.y*(.75 if v.z<-.2 else 1),q.z)))
    count=4 if style<2 else 9
    for i in range(count):
        angle=-math.pi*.86+math.pi*1.72*i/max(1,count-1)
        x=math.sin(angle)*.128;z=-.092+math.cos(angle)*.042
        height=1.755+(.052 if style==1 else .015)*math.sin(i*.8)
        if style==2:
            mesh.ellipsoid((x,height,z),(.05,.048,.055),'hair','head',16,10)
        else:
            path=curve([(x*.85,1.73,z+.02),(x+.025,1.721 if style==0 else height,z-.016),(x+.047,1.67 if style==0 else height+.010,z-.038)],5)
            mesh.tube(path,[.038*(1-i/(len(path)-1))+.002 for i in range(len(path))],'hair','head',12)
    for side in [-1,1]:mesh.ellipsoid((side*.145,1.604,.039),(.035,.115,.054),'hair','head',20,12)
    mesh.finish()

def human_shoes(style):
    mesh=Mesh('human_footwear_%d'%style)
    for side,s in [(-1,'l'),(1,'r')]:
        x=side*.135;bone='foot_'+s
        # Closed sculpted slipper with flat sole; footprint stays in the .105 capsule.
        mesh.loft([(x,.009,-.09),(x,.027,-.09),(x,.065,-.095),(x,.116,-.11),(x,.17,-.095)],
                  [(.084,.186),(.098,.201),(.101,.20),(.087,.176),(.052,.096)],'accent',bone,28)
        mesh.loft([(x,.006,-.09),(x,.02,-.09),(x,.033,-.09)],[(.080,.179),(.100,.199),(.096,.197)],'sole',bone,28)
        rim=[(x+math.cos(TAU*i/48)*.100,.035,-.09+math.sin(TAU*i/48)*.198) for i in range(49)]
        mesh.tube(rim,.004,'secondary',bone,8)
        mesh.tube(curve([(x-.059,.061,.079),(x,.089,.100),(x+.059,.061,.079)],5),.004,'secondary',bone,8)
        if style==1:
            for sign in [-1,1]:
                path=curve([(x-sign*.079,.070,-.19),(x,.126,-.12),(x+sign*.074,.107,-.046)],5)
                mesh.tube(path,.018,'secondary',bone,10)
        elif style==2:
            for i in range(3):
                path=curve([(x-.06,.117,-.18+i*.036),(x,.150,-.17+i*.036),(x+.06,.117,-.18+i*.036)],4)
                mesh.tube(path,.008,'secondary',bone,8)
        else:
            for sign in [-1,1]:
                path=curve([(x+sign*.019,.115,-.252),(x+sign*.029,.111,-.258),(x+sign*.041,.119,-.252)],4)
                mesh.tube(path,.0035,'sole',bone,8)
    mesh.finish()

def human_accessories():
    cap=Mesh('human_accessory_1')
    cap.ellipsoid((0,1.745,.014),(.183,.107,.166),'primary','head',28,16)
    cap.ellipsoid((0,1.742,-.167),(.180,.014,.093),'secondary','head',28,10)
    cap.finish()
    glasses=Mesh('human_accessory_2')
    for side in [-1,1]:
        pts=[(side*.068+math.cos(TAU*i/40)*.057,1.596+math.sin(TAU*i/40)*.050,-.181) for i in range(41)]
        glasses.tube(pts,.006,'ink','head',8)
        glasses.tube([(side*.118,1.608,-.173),(side*.173,1.610,-.042)],.004,'ink','head',8)
    glasses.tube([(-.01,1.600,-.18),(0,1.611,-.182),(.01,1.600,-.18)],.005,'ink','head',8);glasses.finish()
    cap=Mesh('human_accessory_3')
    path=curve([(0,1.73,.015),(0,1.80,.02),(-.075,1.84,.025),(-.17,1.78,.016),(-.218,1.69,.008)],6)
    cap.tube(path,[.18*pow(1-i/(len(path)-1),.95)+.006 for i in range(len(path))],'cap_cloth','head',32)
    brim=[(math.cos(TAU*i/48)*.182,1.747,math.sin(TAU*i/48)*.159+.015) for i in range(49)]
    cap.tube(brim,.018,'secondary','head',10)
    cap.ellipsoid((-.219,1.665,.004),(.034,.039,.034),'secondary','head',20,14)
    for i in range(6):
        a=TAU*i/6;cap.ellipsoid((-.219+math.cos(a)*.026,1.665+math.sin(a)*.026,.002),(.014,.014,.017),'secondary','head',12,8)
    cap.finish()

def mosquito():
    reset('mosquito')
    # Authoring units deliberately match the existing mosquito's .35 visual scale.
    bones={'root':((0,0,0),(0,.08,0)), 'thorax':((0,0,0),(0,.08,0),'root'),
           'head':((0,.018,-.091),(0,.10,-.091),'root'),'abdomen':((0,-.008,.080),(0,-.020,.235),'root'),
           'wing_l':((-.033,.047,.045),(-.23,.06,.10),'root'),'wing_r':((.033,.047,.045),(.23,.06,.10),'root')}
    bones['proboscis']=((0,-.013,-.135),(0,-.043,-.264),'head')
    for side,s in [(-1,'l'),(1,'r')]:
        for i,z in enumerate([-.035,.035,.102]):
            a=(side*.048,-.02,z);b=(side*(.118+abs(i-1)*.014),-.083,z+.014);c=(side*(.143+abs(i-1)*.013),-.148,z-.009)
            bones['leg%d_a_%s'%(i,s)]=(a,b,'root');bones['leg%d_b_%s'%(i,s)]=(b,c,'leg%d_a_%s'%(i,s))
        bones['antenna_'+s]=((side*.033,.073,-.105),(side*.065,.177,-.108),'head')
    armature('mosquito',bones)
    mesh=Mesh('mosquito_core')
    mesh.ellipsoid((0,0,.032),(.066,.065,.09),'insect_dark','thorax',28,18)
    mesh.ellipsoid((0,.015,-.089),(.076,.072,.064),'insect_primary','head',28,18)
    mesh.tube(curve([(0,-.013,-.135),(0,-.025,-.19),(0,-.043,-.264)],6),[.012*(1-i/12)+.001 for i in range(13)],'insect_dark','proboscis',12)
    mesh.finish()
    for style in range(3):
        mesh=Mesh('mosquito_outfit_%d'%style)
        for i in range(5):
            t=i/4;scale=(.051*(1-t*.63),.046*(1-t*.68),.041)
            mesh.ellipsoid((0,-.006-t*.027,.105+t*.121),scale,'insect_primary' if (i%2==0 or style==2) else 'insect_dark','abdomen',24,14)
        if style==1:
            for side in [-1,1]:
                for i in range(3):mesh.ellipsoid((side*(.045-i*.008),.001,.105+i*.037),(.009,.020,.011),'accent','abdomen',14,8)
        mesh.finish()
    for expression in range(3):mosquito_face(expression)
    for style in range(3):mosquito_antennae(style)
    for style in range(3):mosquito_legs(style)
    for side,s in [(-1,'l'),(1,'r')]:
        mesh=Mesh('mosquito_wing_'+s)
        # Thin tapered leaf surface, with original radial veins, no solid ellipsoid.
        start=len(mesh.v);radial=40
        center=Vector((side*.145,.062,.067))
        mesh.vertex(center,(.5,.5),{'wing_'+s:1})
        for i in range(radial):
            a=TAU*i/radial;q=center+Vector((side*math.cos(a)*.134,math.sin(a)*.006,math.sin(a)*.064))
            mesh.vertex(q,((math.cos(a)+1)/2,(math.sin(a)+1)/2),{'wing_'+s:1})
        for i in range(radial):mesh.face((start,start+1+i,start+1+(i+1)%radial),'wing')
        rim=[center+Vector((side*math.cos(TAU*i/radial)*.134,math.sin(TAU*i/radial)*.006,math.sin(TAU*i/radial)*.064)) for i in range(radial+1)]
        mesh.tube(rim,.0011,'wing_vein','wing_'+s,6)
        for i in range(5):
            z=-.04+i*.023
            mesh.tube(curve([(side*.037,.060,.045),(side*.12,.064,.058+z*.4),(side*.23,.064,.067+z)],4),.0015,'wing_vein','wing_'+s,6)
        mesh.finish()
    bow=Mesh('mosquito_accessory_1')
    for side in [-1,1]:bow.ellipsoid((side*.043,.088,-.008),(.047,.027,.016),'accent','head',20,12)
    bow.ellipsoid((0,.088,-.009),(.018,.020,.019),'primary','head',16,10);bow.finish()
    glasses=Mesh('mosquito_accessory_2')
    for side in [-1,1]:
        pts=[(side*.044+math.cos(TAU*i/32)*.045,.033+math.sin(TAU*i/32)*.048,-.151) for i in range(33)]
        glasses.tube(pts,.004,'accent','head',8)
    glasses.tube([(-.006,.038,-.150),(0,.045,-.151),(.006,.038,-.150)],.003,'accent','head',8);glasses.finish()
    create_actions('mosquito');finish('mosquito')

def mosquito_face(expression):
    mesh=Mesh('mosquito_face_%d'%expression)
    blink_vertices={}
    for side in [-1,1]:
        eye_start=len(mesh.v)
        mesh.ellipsoid((side*.043,.033,-.129),(.043,.047,.026),'eye_white','head',24,16)
        mesh.ellipsoid((side*.040,.029,-.153),(.016,.023,.008),'pupil','head',20,12)
        mesh.ellipsoid((side*.037,.039,-.160),(.0045,.006,.002),'eye_white','head',12,8)
        for index in range(eye_start,len(mesh.v)):
            value=Vector(mesh.v[index]);value.z=.012+(value.z-.012)*.035;blink_vertices[index]=value
        brow=curve([(side*.009,.087,-.131),(side*.040,.097+(.018 if expression==1 else 0),-.126),(side*.078,.083,-.118)],5)
        mesh.tube(brow,[.003+math.sin(math.pi*i/(len(brow)-1))*.004 for i in range(len(brow))],'insect_dark','head',8)
        if expression==2:
            lid=curve([(side*.004,.043,-.153),(side*.042,.047,-.159),(side*.082,.041,-.137)],4)
            mesh.tube(lid,.008,'insect_primary','head',8)
    mesh.ellipsoid((0,-.028,-.131),(.034,.018,.011),'ink','head',20,12)
    mesh.ellipsoid((.009,-.023,-.140),(.020,.005,.004),'eye_white','head',16,8)
    obj=mesh.finish();obj.shape_key_add(name='Basis');blink=obj.shape_key_add(name='Blink')
    for index,value in blink_vertices.items():blink.data[index].co=value

def mosquito_antennae(style):
    mesh=Mesh('mosquito_hair_%d'%style)
    for side,s in [(-1,'l'),(1,'r')]:
        path=curve([(side*.032,.073,-.106),(side*.047,.132,-.112),(side*(.077 if style==1 else .059),.178,-.097)],6)
        mesh.tube(path,[.0045*(1-.35*i/(len(path)-1)) for i in range(len(path))],'insect_dark','antenna_'+s,8)
        if style==2:
            for i in range(3,11,2):
                q=path[i]
                for sign in [-1,1]:mesh.tube([q,q+Vector((side*sign*.020,.01,0))],[.002,.0007],'insect_dark','antenna_'+s,6)
    mesh.finish()

def mosquito_legs(style):
    mesh=Mesh('mosquito_footwear_%d'%style)
    for side,s in [(-1,'l'),(1,'r')]:
        for i in range(3):
            for part in ['a','b']:
                bone='leg%d_%s_%s'%(i,part,s);a=p(BONES[bone][0]);b=p(BONES[bone][1])
                mesh.tube([a,a.lerp(b,.3),b],[.006,.0055,.0035],'insect_dark',bone,10)
                if part=='b':
                    mesh.ellipsoid(a,(.0063,.0063,.0063),'insect_dark',bone,12,8)
                if style==1:
                    for t in [.3,.6]:mesh.ellipsoid(a.lerp(b,t),(.0065,.0065,.0065),'accent',bone,12,8)
                elif style==2 and part=='b':
                    q=a.lerp(b,.70);mesh.ellipsoid(q,(.008,.012,.008),'primary',bone,14,10)
            bone='leg%d_b_%s'%(i,s);b=p(BONES[bone][1])
            mesh.tube([b,b+Vector((side*.009,0,-.016))],[.005,.002],'insect_dark',bone,8)
    mesh.finish()

def create_actions(species):
    # Editable reference clips; runtime uses the identical authoritative bones.
    pose_file=ROOT/'art_source/characters/human/authoritative_pose_clips.json'
    if species=='human' and pose_file.exists():
        create_authoritative_human_actions(json.loads(pose_file.read_text(encoding='utf8')))
        return
    mosquito_file=ROOT/'art_source/characters/mosquito/authoritative_pose_clips.json'
    if species=='mosquito' and mosquito_file.exists():
        create_authoritative_mosquito_actions(json.loads(mosquito_file.read_text(encoding='utf8')))
        return
    names=['idle','walk','run','crouch','jump','inspect','clap','tool_hold','task'] if species=='human' else ['hover','accelerate','brake','perch','focus','bite','detach','stunned','recover','rescue']
    RIG.animation_data_create()
    for label in names:
        action=bpy.data.actions.new(species+'_'+label);RIG.animation_data.action=action
        duration=24 if label not in ['clap','jump','recover'] else 12
        for frame in range(1,duration+2,3):
            phase=(frame-1)/duration*TAU
            for pose_bone in RIG.pose.bones:
                pose_bone.rotation_mode='XYZ';pose_bone.rotation_euler=(0,0,0);pose_bone.location=(0,0,0)
                name=pose_bone.name
                if species=='human':
                    if label in ['walk','run'] and name.startswith(('thigh_','shin_')):
                        pose_bone.rotation_euler.x=math.sin(phase+(math.pi if name.endswith('_r') else 0))*(.16 if label=='walk' else .24)
                    if name=='head' and label in ['idle','inspect']:pose_bone.rotation_euler.x=math.sin(phase)*.035+(.18 if label=='inspect' else 0)
                    if label=='clap' and name.startswith('hand_'):pose_bone.rotation_euler.y=math.sin(phase*.5)*(.3 if name.endswith('_l') else -.3)
                    if name.startswith('finger') and label in ['clap','tool_hold','task']:pose_bone.rotation_euler.x=.28+math.sin(phase)*.07
                else:
                    if name.startswith('wing_'):pose_bone.rotation_euler.y=math.sin(phase*2)*(.65 if label not in ['stunned','perch','bite'] else .035)*(1 if name.endswith('_l') else -1)
                    if name=='thorax' and label in ['accelerate','brake']:pose_bone.rotation_euler.x=.18 if label=='accelerate' else -.10
                    if name=='root' and label=='stunned':pose_bone.rotation_euler.y=1.25+math.sin(phase)*.025
                    if name.startswith('leg') and label in ['stunned','recover']:
                        pose_bone.rotation_euler.z=(.65 if label=='stunned' else .65*(1-(frame-1)/duration))*(1 if name.endswith('_l') else -1)
                    if name=='proboscis' and label in ['bite','focus']:pose_bone.rotation_euler.x=-.82 if label=='bite' else -.35
                pose_bone.keyframe_insert('rotation_euler',frame=frame,group=name)
                pose_bone.keyframe_insert('location',frame=frame,group=name)
        track=RIG.animation_data.nla_tracks.new();track.name=species+'_'+label
        track.strips.new(action.name,1,action);track.mute=True
    RIG.animation_data.action=None
    for bone in RIG.pose.bones:bone.matrix_basis=Matrix.Identity(4)
    bpy.context.scene.frame_set(1)

def create_authoritative_mosquito_actions(payload):
    """State transitions sampled from the real runtime deformation skeleton."""
    RIG.animation_data_create();bpy.context.scene.render.fps=int(payload['fps'])
    conversion=Matrix(((1,0,0),(0,0,-1),(0,1,0)))
    for label,frames in payload['clips'].items():
        action=bpy.data.actions.new('mosquito_'+label);RIG.animation_data.action=action
        for sample in frames:
            for bone in RIG.pose.bones:bone.matrix_basis=Matrix.Identity(4)
            for name,pose in sample['bones'].items():
                values=pose['basis'];basis=Matrix((values[0:3],values[3:6],values[6:9])).transposed()
                matrix=(conversion@basis@conversion.inverted()).to_4x4();matrix.translation=g(pose['p'])
                bone=RIG.pose.bones[name];bone.matrix=matrix
                bpy.context.view_layer.update()
            for bone in RIG.pose.bones:
                bone.rotation_mode='QUATERNION'
                for key in ['location','rotation_quaternion','scale']:bone.keyframe_insert(key,frame=sample['frame'],group=bone.name)
        track=RIG.animation_data.nla_tracks.new();track.name=action.name
        track.strips.new(action.name,1,action);track.mute=True
    RIG.animation_data.action=None
    for bone in RIG.pose.bones:bone.matrix_basis=Matrix.Identity(4)
    bpy.context.scene.frame_set(1)

def create_authoritative_human_actions(payload):
    """Editable clip curves sampled from the exact shared Godot pose contract."""
    RIG.animation_data_create();bpy.context.scene.render.fps=int(payload['fps'])
    def bone_transform(name,start,end,orientation=None):
        rest=RIG.data.bones[name].matrix_local.copy()
        old=g(BONES[name][1])-g(BONES[name][0]);direction=g(end)-g(start)
        change=old.normalized().rotation_difference(direction.normalized()).to_matrix()
        basis=change@rest.to_3x3()
        if orientation:basis=orientation@basis
        basis.col[1]*=direction.length/old.length
        matrix=basis.to_4x4();matrix.translation=g(start)
        RIG.pose.bones[name].matrix=matrix
    for label,frames in payload['clips'].items():
        action=bpy.data.actions.new('human_'+label);RIG.animation_data.action=action
        for sample in frames:
            for bone in RIG.pose.bones:bone.matrix_basis=Matrix.Identity(4)
            rise=Vector((0,sample.get('root_y',0),0))
            def point(key):return p(sample[key])+rise
            bone_transform('root',rise,rise+Vector((0,.12,0)))
            # Matrix setters must see this frame's parent before deriving local
            # channels, otherwise jump rise is applied a second time on export.
            bpy.context.view_layer.update()
            for name,length in [('pelvis',.18),('torso',.16*sample['torso_height']/.68),('head',.19)]:
                orientation=None
                if name=='head' or (name=='torso' and 'torso_basis' in sample):
                    q=sample[name+'_basis'];orientation=Quaternion((q[3],q[0],-q[2],q[1])).to_matrix()
                bone_transform(name,point(name),point(name)+Vector((0,length,0)),orientation)
            for side in ['l','r']:
                for name,start,end in [('thigh','hip','knee'),('shin','knee','ankle'),('upperarm','shoulder','elbow'),('forearm','elbow','hand')]:
                    bone_transform(name+'_'+side,point(start+'_'+side),point(end+'_'+side))
                bone_transform('foot_'+side,point('ankle_'+side),point('ankle_'+side)+p(sample.get('foot_direction_'+side,[0,0,-1]))*.22)
                hand=point('hand_'+side);direction=(hand-point('elbow_'+side)).normalized()
                bone_transform('hand_'+side,hand,hand+direction*.10)
            for bone in RIG.pose.bones:
                bone.rotation_mode='QUATERNION'
                if bone.name.startswith('finger') and bone.name.endswith('_r') and sample['tool']!='hands':
                    bone.rotation_quaternion=Quaternion((1,0,0),.55)
                for key in ['location','rotation_quaternion','scale']:bone.keyframe_insert(key,frame=sample['frame'],group=bone.name)
        track=RIG.animation_data.nla_tracks.new();track.name=action.name
        track.strips.new(action.name,1,action);track.mute=True
    RIG.animation_data.action=None
    for bone in RIG.pose.bones:bone.matrix_basis=Matrix.Identity(4)
    bpy.context.scene.frame_set(1)

def finish(species):
    output=ROOT/'game/assets/art/characters'/species;source=ROOT/'art_source/characters'/species
    output.mkdir(parents=True,exist_ok=True);source.mkdir(parents=True,exist_ok=True)
    # Clean manifold duplicate poles/seams and recalculate outward normals.
    for obj in OBJECTS:
        garment=obj.name.startswith('human_outfit_') and not obj.name.endswith('_trim')
        if garment or obj.name=='human_core':
            # Weld shoulder/hip seams into one deforming garment. Retain original
            # surface weights, UVs and material assignments through nearest samples.
            old=obj.data
            tree=kdtree.KDTree(len(old.vertices))
            weights=[];uvs=[(0,0)]*len(old.vertices);indices=[0]*len(old.vertices)
            group_names={group.index:group.name for group in obj.vertex_groups}
            for vertex in old.vertices:
                tree.insert(vertex.co,vertex.index)
                weights.append({group_names[item.group]:item.weight for item in vertex.groups})
            tree.balance()
            for poly in old.polygons:
                for loop in poly.loop_indices:
                    index=old.loops[loop].vertex_index
                    uvs[index]=tuple(old.uv_layers.active.data[loop].uv);indices[index]=poly.material_index
            bpy.context.view_layer.objects.active=obj
            remesh=obj.modifiers.new('Continuous tailored garment' if garment else 'Integrated palm and fingers','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.010 if garment else .0035
            remesh.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=remesh.name)
            smooth=obj.modifiers.new('Surface relaxation','SMOOTH');smooth.factor=.45 if garment else .28;smooth.iterations=2
            bpy.ops.object.modifier_apply(modifier=smooth.name)
            decimate=obj.modifiers.new('Character surface budget','DECIMATE');decimate.ratio=.15 if garment else .09
            bpy.ops.object.modifier_apply(modifier=decimate.name)
            for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
            groups={name:obj.vertex_groups.new(name=name) for name in set(group_names.values())}
            nearest=[]
            for vertex in obj.data.vertices:
                samples=tree.find_n(vertex.co,4);combined={};total=0
                for _,index,distance in samples:
                    factor=1/max(distance,.0001)**2;total+=factor
                    for key,weight in weights[index].items():combined[key]=combined.get(key,0)+weight*factor
                q=Vector((vertex.co.x,vertex.co.z,-vertex.co.y))
                if garment and abs(q.x)>.13 and q.y>1.17 and q.z>-.17:
                    blend=min(1,max(0,(abs(q.x)-.15)/.12));blend=blend*blend*(3-2*blend)
                    combined={'torso':1-blend,'upperarm_'+('l' if q.x<0 else 'r'):blend}
                elif not garment:
                    side='l' if q.x<0 else 'r';a=p(BONES['forearm_'+side][0]);b=p(BONES['forearm_'+side][1])
                    along=(q-a).dot(b-a)/(b-a).length_squared
                    if along<1.04:
                        blend=min(1,max(0,(along-.82)/.22));blend=blend*blend*(3-2*blend)
                        combined={'forearm_'+side:1-blend,'hand_'+side:blend}
                    elif 'forearm_'+side in combined:
                        # The tube extends beyond the wrist. Never revert those
                        # terminal vertices to a stretched forearm after the
                        # interpolation interval: it folds a false wrist seam.
                        combined['hand_'+side]=combined.get('hand_'+side,0)+combined.pop('forearm_'+side)
                top=sorted(combined.items(),key=lambda item:-item[1])[:4];normalizer=sum(w for _,w in top)
                for key,weight in top:groups[key].add([vertex.index],weight/normalizer,'REPLACE')
                nearest.append(samples[0][1])
            for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
            uv=obj.data.uv_layers.new(name='UVMap');uv.active_render=True
            for poly in obj.data.polygons:
                poly.material_index=indices[nearest[poly.vertices[0]]]
                coordinates=[]
                for loop in poly.loop_indices:
                    vertex=obj.data.vertices[obj.data.loops[loop].vertex_index]
                    # One continuous authoring cylinder avoids UV seams inferred
                    # from blended skin weights at the shoulder and pelvis.
                    coordinates.append([math.atan2(vertex.co.y,vertex.co.x)/TAU,vertex.co.z*.8])
                # Unwrap the cylindrical seam per triangle instead of interpolating
                # a full revolution across its edge. Grain follows each garment panel.
                if max(q[0] for q in coordinates)-min(q[0] for q in coordinates)>.5:
                    for q in coordinates:
                        if q[0]<0:q[0]+=1
                for loop,coordinate in zip(poly.loop_indices,coordinates):uv.data[loop].uv=coordinate
        if obj.name=='human_head':
            bpy.context.view_layer.objects.active=obj
            remesh=obj.modifiers.new('Continuous caricature sculpt','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.007
            remesh.use_smooth_shade=True
            bpy.ops.object.modifier_apply(modifier=remesh.name)
            smooth=obj.modifiers.new('Polished face planes','SMOOTH');smooth.factor=.65;smooth.iterations=4
            bpy.ops.object.modifier_apply(modifier=smooth.name)
            decimate=obj.modifiers.new('Character surface budget','DECIMATE');decimate.ratio=.18
            bpy.ops.object.modifier_apply(modifier=decimate.name)
            for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
            group=obj.vertex_groups.new(name='head');group.add(list(range(len(obj.data.vertices))),1.0,'REPLACE')
            for poly in obj.data.polygons:poly.material_index=0
        # BMesh carries shape-key layers while welding duplicate UV poles.
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(obj.data);bm.free()
        for poly in obj.data.polygons:poly.use_smooth=True
    bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=32
    bpy.context.scene.world.color=(.16,.20,.25)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1.0
    # Default visible set in .blend; GLB exports all variants for runtime selection.
    defaults={'face':1 if species=='human' else 0,'hair':0,'outfit':0,'footwear':0,'accessory':3 if species=='human' else 0}
    for obj in OBJECTS:
        pieces=obj.name.split('_')
        if len(pieces)>=3 and pieces[1] in defaults:
            obj.hide_render=int(pieces[2])!=defaults[pieces[1]]
            if species=='human' and pieces[1]=='hair':
                obj.hide_render=obj.hide_render or not obj.name.endswith('_capped')
        obj.hide_set(obj.hide_render)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(species+'_lms06.blend')))
    for obj in OBJECTS:obj.hide_render=False;obj.hide_set(False)
    bpy.ops.object.select_all(action='DESELECT');RIG.select_set(True)
    for obj in OBJECTS:obj.select_set(True)
    bpy.context.view_layer.objects.active=RIG
    bpy.ops.export_scene.gltf(filepath=str(output/(species+'_lms06.glb')),export_format='GLB',use_selection=True,
        export_yup=True,export_apply=False,export_animations=True,export_nla_strips=True,
        export_skins=True,export_all_influences=False,export_def_bones=True,export_extras=True)
    manifest={'id':'lms06_'+species,'rig_version':'LMS06.1','blender':bpy.app.version_string,'units':'metres','forward':'-Z',
              'source':str(source.relative_to(ROOT)/(species+'_lms06.blend')),'glb':str(output.relative_to(ROOT)/(species+'_lms06.glb')),
              'original':True,'authoring':'art_source/export_presets/characters_pipeline.py','license':'Project-original; no third-party source assets',
              'bones':{k:{'from':list(v[0]),'to':list(v[1]),'parent':v[2] if len(v)>2 else ''} for k,v in BONES.items()},
              'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(f.vertices)-2 for f in o.data.polygons)} for o in OBJECTS],
              'default':defaults,'actions':[a.name for a in bpy.data.actions if a.name.startswith(species+'_')]}
    (source/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    (output/'rig_contract.json').write_text(json.dumps({'id':manifest['id'],'rig_version':manifest['rig_version'],'bones':manifest['bones']},indent=2),encoding='utf8')
    print('LMS_CHARACTER_EXPORTED',species,sum(m['triangles'] for m in manifest['meshes']),'all-variant triangles')

if __name__=='__main__':
    bpy.context.preferences.filepaths.save_version=0
    parser=argparse.ArgumentParser();parser.add_argument('--root');parser.add_argument('--species',choices=['human','mosquito','both'],default='both')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.root:ROOT=Path(args.root).resolve()
    if args.species in ['human','both']:human()
    if args.species in ['mosquito','both']:mosquito()
