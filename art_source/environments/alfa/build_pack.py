"""Original alfa environment kit. Blender background, no external providers.
Input dimensions are Godot metres (X right, Y up, front -Z).
Only this alfa source folder and game/assets/art/house/alfa are written.
"""
import bpy, math, json, hashlib, sys, random
from pathlib import Path
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent
ROOT=SOURCE.parents[2]
OUT=ROOT/'game/assets/art/house/alfa'
PALETTE={'wood':'94633F','wood_light':'B48458','wood_dark':'57402F','iron':'393D43','leaf':'355C37','leaf_light':'547D3B','leaf_dark':'234B36','stone':'797F87','stone_light':'969A9D','terracotta':'A35239','soil':'44382D','cream':'C6B797','mail_red':'AD5542'}
SPECS={}
COLLISIONS=[]
M={}
def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def xyz(p):return (p[0],-p[2],p[1])
def material_setup():
 for name,c in PALETTE.items():
  m=bpy.data.materials.new('Alfa_'+name);m.use_nodes=True
  rgba=tuple(linear(int(c[i:i+2],16)/255) for i in (0,2,4))+(1,)
  m.diffuse_color=rgba;n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=rgba;n.inputs['Roughness'].default_value=.90;n.inputs['Metallic'].default_value=0
  M[name]=m

def finish(o,name,mat):
 o.name=name;o.data.materials.append(M[mat])
 for p in o.data.polygons:p.use_smooth=False
 return o

def collider(center,size):
 COLLISIONS.append({'position':[round(center[i]-size[i]/2,6) for i in range(3)],'size':list(size)})
def box(name,p,s,mat='wood',bevel=.008,solid=True):
 bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));o=bpy.context.object;o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Single facet edge','BEVEL');mod.width=min(bevel,min(s)*.18);mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 finish(o,name,mat)
 if solid:collider(p,s)
 return o

def cone(name,p,h,r1,r2,mat='wood',n=8,solid=False):
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=h,location=xyz(p));o=finish(bpy.context.object,name,mat)
 if solid:collider(p,(max(r1,r2)*2,h,max(r1,r2)*2))
 return o

def mesh(name,verts,faces,mat):
 d=bpy.data.meshes.new(name);d.from_pydata([xyz(p) for p in verts],[],faces);d.update();o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);return finish(o,name,mat)
def rock(name,size,mat='stone',solid=True):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1);o=bpy.context.object;rng=random.Random(sum(map(ord,name)))
 for v in o.data.vertices:v.co*=rng.uniform(.88,1.12)
 pts=[v.co.copy() for v in o.data.vertices];lo=[min(p[i] for p in pts) for i in range(3)];hi=[max(p[i] for p in pts) for i in range(3)]
 for v in o.data.vertices:
  v.co.x=(v.co.x-lo[0])/(hi[0]-lo[0])*size[0]-size[0]/2
  v.co.y=(v.co.y-lo[1])/(hi[1]-lo[1])*size[2]-size[2]/2
  v.co.z=(v.co.z-lo[2])/(hi[2]-lo[2])*size[1]
 finish(o,name,mat)
 o.data.materials.append(M['stone_light' if mat=='stone' else 'leaf_light'])
 for i,p in enumerate(o.data.polygons):p.material_index=1 if i%5==0 else 0
 if solid:collider((0,size[1]/2,0),size)
 return o

def support(h,w,d):return [{'center':[0,h,0],'size':[w,d],'edge_margin_m':.06}]
def table():
 for x in [-.59,.59]:
  for z in [-.30,.30]:box('Table leg',(x,.34,z),(.085,.68,.085),'wood_dark')
 for z in [-.30,.30]:box('Apron',(0,.65,z),(1.26,.12,.05))
 for i in range(5):box('Tabletop plank',((i-2)*.30,.72,0),(.298,.08,.85),'wood_light')
 return support(.76,1.38,.73)
def bench():
 for x in [-.66,.66]:
  for z in [-.21,.21]:box('Bench foot',(x,.23,z),(.095,.46,.095),'wood_dark')
 for z in [-.21,0,.21]:box('Seat plank',(0,.475,z),(1.65,.05,.18),'wood_light')
 for x in [-.66,.66]:box('Back upright',(x,.67,.25),(.08,.50,.08),'wood_dark')
 for y in [.64,.83]:box('Backrest plank',(0,y,.25),(1.65,.14,.06))
 return support(.50,1.45,.43)
def chair(stool=False):
 width=.38 if stool else .50;depth=.38 if stool else .52
 for x in [-width*.36,width*.36]:
  for z in [-depth*.36,depth*.36]:box('Leg',(x,.215,z),(.055,.43,.055),'wood_dark',.005)
 box('Seat',(0,.445,0),(width,.03,depth),'wood_light')
 if not stool:
  for x in [-.19,.19]:box('Back post',(x,.70,.22),(.06,.48,.055),'wood_dark')
  for y in [.65,.86]:box('Back slat',(0,y,.22),(.38,.09,.045))
 return support(.46,width-.10,depth-.10)
def fence(post=False):
 if post:
  box('Square post',(0,.50,0),(.16,1,.16));cone('Pyramid cap',(0,1.04,0),.08,.113137,0,'wood_light',4).rotation_euler.z=math.pi/4;collider((0,1.04,0),(.16,.08,.16))
 else:
  for x in [-.94,.94]:box('Fence post',(x,.50,0),(.12,1,.14),'wood_dark')
  for y in [.30,.76]:box('Horizontal rail',(0,y,.022),(1.88,.12,.065))
  for i in range(8):box('Picket',((i-3.5)*.22,.50,-.035),(.09,.80,.05),'wood_light')
 return []
def stone():
 n=7;verts=[]
 for y in [0,.035]:
  for i in range(n):
   a=i*math.tau/n;verts.append((math.cos(a)*.275,y,math.sin(a)*.21))
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh('Path stone',verts,faces,'stone');return []
def planter():
 cone('Terracotta pot',(0,.16,0),.32,.17,.23,'terracotta',10,True)
 cone('Pot rim',(0,.34,0),.08,.23,.23,'terracotta',10,True)
 cone('Soil',(0,.375,0),.012,.202,.202,'soil',10)
 for i in range(7):
  a=i*math.tau/7;h=.35+.17*(i%3)/2
  base=(math.cos(a)*.025,.38,math.sin(a)*.025);tip=(math.cos(a)*.18,.38+h,math.sin(a)*.18)
  mid=(math.cos(a)*.12,.38+h*.55,math.sin(a)*.12);side=(-math.sin(a)*.065,0,math.cos(a)*.065)
  mesh('Thick leaf',[base,(mid[0]+side[0],mid[1],mid[2]+side[2]),tip,(mid[0]-side[0],mid[1],mid[2]-side[2]),(mid[0],mid[1]-.025,mid[2])],[(0,1,2),(0,2,3),(0,4,1),(1,4,2),(2,4,3),(3,4,0)],'leaf_light' if i%2 else 'leaf')
 return []
def pine():
 cone('Trunk',(0,.60,0),1.2,.12,.085,'wood_dark',7,True)
 for i,(base,h,r) in enumerate([(.85,1.40,.9),(1.55,1.25,.71),(2.25,1.15,.48)]):
  cone('Faceted canopy',(0,base+h/2,0),h,r,.025,['leaf_dark','leaf','leaf_light'][i],8)
  collider((0,base+h/2,0),(r*2,h,r*2))
 return []
def bush():rock('Low shrub',(1.05,.75,.85),'leaf',False);return []
def grass():
 for i in range(9):
  a=i*2.399;x=math.cos(a)*(.10+.025*i);z=math.sin(a)*(.10+.021*i);h=.16+.07*(i%3);w=.035
  mesh('Grass blade',[(x-w,0,z),(x+w,0,z),(x+math.cos(a)*.08,h,z+math.sin(a)*.08),(x,.04,z+.014)],[(0,1,2),(0,2,3),(1,3,2),(0,3,1)],'leaf_light' if i%2 else 'leaf')
 return []
def mailbox():
 box('Mailbox post',(0,.41,.08),(.09,.82,.09),'wood_dark')
 box('Mailbox body',(0,1.,0),(.40,.32,.50),'mail_red',.045)
 box('Mail slot',(0,1.04,-.254),(.23,.025,.008),'iron',.002,False)
 box('Flag',(.211,1.13,.04),(.015,.14,.10),'wood_light',.002,False)
 return []
def crate():
 box('Crate inner',(0,.275,0),(.50,.55,.50),'wood_dark')
 for i in range(3):
  for z in [-.255,.255]:box('Crate plank',((i-1)*.175,.275,z),(.168,.51,.04),'wood_light')
 for x in [-.255,.255]:box('Crate side',(x,.275,0),(.04,.51,.50))
 box('Lid',(0,.535,0),(.55,.03,.55),'wood_light')
 return support(.55,.43,.43)
def barrel():
 rings=[(0,.23),(.08,.27),(.25,.29),(.60,.29),(.76,.26),(.82,.23)];n=10;verts=[]
 for y,r in rings:
  for i in range(n):a=i*math.tau/n;verts.append((math.cos(a)*r,y,math.sin(a)*r))
 faces=[tuple(range(n-1,-1,-1)),tuple(range((len(rings)-1)*n,len(rings)*n))]
 for j in range(len(rings)-1):
  for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 mesh('Barrel staves',verts,faces,'wood')
 for y,r in [(.12,.279),(.66,.282)]:cone('Iron hoop',(0,y,0),.035,r,r,'iron',10)
 collider((0,.41,0),(.58,.82,.58));return support(.82,.28,.28)
def logs():
 for x,y in [(-.20,.13),(.20,.13),(0,.36)]:
  o=cone('Cut log',(x,y,0),.9,.12,.105,'wood',9);o.rotation_euler[1]=math.pi/2
  # cylinder longitudinal X, stacked profiles on Y/Z
  o.location=xyz((0,y,x))
 bpy.context.view_layer.update();items=[o for o in bpy.context.scene.objects if o.type=='MESH'];bottom=min((o.matrix_world@v.co).z for o in items for v in o.data.vertices)
 for o in items:o.location.z-=bottom
 collider((0,.24,0),(.9,.48,.64));return []
def porch():box('Porch post',(0,1.30,0),(.18,2.60,.18),'wood_dark');return []
def shutter():
 for x in [-.19,.19]:box('Shutter stile',(x,.60,-.035),(.06,1.20,.07),'wood_dark')
 for i in range(6):box('Inset board',((i-2.5)*.052,.60,-.03),(.050,1.10,.04))
 for y in [.18,1.02]:box('Shutter strap',(0,y,-.073),(.40,.035,.012),'iron',.002,False)
 return []
def canopy():
 # Wedge roof, anchored on wall back plane Z=0. Fascia faces -Z.
 verts=[(-.8,0,-.6),(.8,0,-.6),(-.8,.20,0),(.8,.20,0),(-.8,.08,-.6),(.8,.08,-.6),(-.8,.28,0),(.8,.28,0)]
 mesh('Canopy roof',verts,[(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5),(4,5,7,6),(0,2,3,1)],'terracotta')
 collider((0,.14,-.3),(1.6,.28,.6));return []
def chimney():
 box('Chimney shaft',(0,.49,0),(.47,.98,.44),'stone')
 box('Chimney shoulder',(0,.97,0),(.58,.08,.53),'stone_light')
 for x in [-.245,.245]:box('Cap support',(x,1.035,0),(.06,.13,.44),'stone')
 box('Stone cap',(0,1.075,0),(.65,.05,.60),'stone_light')
 box('Flue darkness',(0,1.013,0),(.33,.005,.30),'iron',0,False)
 return []
BUILDERS={'bench':bench,'patio_table':table,'chair':chair,'stool':lambda:chair(True),'fence_panel':fence,'fence_post':lambda:fence(True),'path_stone':stone,'planter':planter,'pine':pine,'bush':bush,'rock_small':lambda:rock('Small rock',(.65,.45,.50)),'rock_large':lambda:rock('Large rock',(1.05,.85,.85)),'grass_patch':grass,'mailbox':mailbox,'crate':crate,'barrel':barrel,'log_stack':logs,'porch_post':porch,'shutter':shutter,'canopy':canopy,'chimney':chimney}

def bounds(meshes):
 pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 godot=[(p.x,p.z,-p.y) for p in pts];lo=[min(p[i] for p in godot) for i in range(3)];hi=[max(p[i] for p in godot) for i in range(3)]
 return {'position':[round(v,6) for v in lo],'size':[round(hi[i]-lo[i],6) for i in range(3)]}
def export(name):
 COLLISIONS.clear();M.clear();bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);material_setup()
 surfaces=BUILDERS[name]();surfaces=surfaces if isinstance(surfaces,list) else []
 bpy.context.view_layer.update();meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];measured=bounds(meshes)
 bpy.context.scene['asset_id']='alfa_'+name;bpy.context.scene['units']='metres';bpy.context.scene['reference']='work/references094/environment.png'
 source=SOURCE/(name+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
 bpy.ops.object.select_all(action='DESELECT')
 for o in meshes:o.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();o=bpy.context.object;o.name='Alfa_'+name
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 o.data.calc_loop_triangles();path=OUT/(name+'.glb')
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_cameras=False,export_lights=False,export_texcoords=False)
 return {'id':'alfa_'+name,'path':'res://assets/art/house/alfa/'+name+'.glb','source':str(source.relative_to(ROOT)).replace('\\','/'),'source_kind':'original_alfa','pivot':'back_base_center' if name in ['shutter','canopy'] else 'base_center','placement_kind':'wall' if name in ['shutter','canopy'] else 'ground','front':'-Z','visual_bounds':measured,'placement_bounds':measured,'collision_boxes':list(COLLISIONS),'support_surfaces':surfaces,'triangles':len(o.data.loop_triangles),'material_slots':len(o.data.materials),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'bytes':path.stat().st_size}
if __name__=='__main__':
 SOURCE.mkdir(exist_ok=True);OUT.mkdir(parents=True,exist_ok=True);bpy.context.preferences.filepaths.save_version=0
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(BUILDERS)
 entries={}
 manifest=OUT/'manifest.json'
 if manifest.exists():entries={a['id']:a for a in json.loads(manifest.read_text())['assets']}
 for name in names:
  assert name in BUILDERS,name;row=export(name);entries[row['id']]=row;print('ALFA_ASSET',name,row['triangles'],row['visual_bounds'],flush=True)
 package={'schema':'lms.alfa.assets/1','units':'metre','authoring':'Original project meshes, deterministic local Blender construction; no external assets/providers','license':'Project-owned original art; repository terms apply','reference':'work/references094/environment.png','blender':bpy.app.version_string,'assets':list(entries.values())}
 manifest.write_text(json.dumps(package,indent=2));(SOURCE/'manifest.json').write_text(json.dumps(package,indent=2))
 print('ALFA_EXPORT_COMPLETE',len(names),flush=True)
