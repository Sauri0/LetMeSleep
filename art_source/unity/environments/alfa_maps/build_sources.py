"""Fixed alpha house/patio, lobby and small furnishing kit. Blender CPU; no render."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
PLAN=json.loads((HERE.parent/'room_sample/house_layout_plan.json').read_text(encoding='utf-8-sig'))
PALETTE={
 'Plaster_Warm':(.68,.61,.48,1),'Ceiling_Cream':(.80,.76,.65,1),
 'Wood_Honey':(.36,.19,.073,1),'Wood_Edge':(.21,.095,.034,1),'Floor_Oak':(.29,.155,.069,1),
 'Textile_Navy':(.065,.16,.29,1),'Textile_Blue':(.18,.34,.48,1),'Linen':(.86,.80,.64,1),
 'Iron':(.048,.062,.068,1),'Glass_Blue_Opaque':(.30,.47,.53,1),
 'Roof_Terracotta':(.45,.15,.075,1),'Grass':(.17,.28,.085,1),
 'Pine':(.075,.19,.07,1),'Stone':(.33,.36,.36,1),'Tile':(.60,.66,.64,1),
 'Porcelain':(.82,.83,.77,1), 'Textile_Rust':(.43,.14,.085,1)}
DATA={}
def v(p): return Vector((p[0],-p[2],p[1]))
def reset(name):
 bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
 global mats, root, colliders, mesh_colliders
 mats={};colliders=[];mesh_colliders=[]
 for n,c in PALETTE.items():
  m=bpy.data.materials.get(n) or bpy.data.materials.new(n)
  m.diffuse_color=c;m.use_nodes=True
  b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=c
  b.inputs['Roughness'].default_value=.78;b.inputs['Metallic'].default_value=.45 if n=='Iron' else 0
  mats[n]=m
 root=empty(name)
def empty(name,pos=(0,0,0),parent=None):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;o.location=v(pos);return o
def cube(name,c,s,mat='Wood_Honey',parent=None,bevel=0,collision=True,surface='Wood'):
 bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;o.name=name;o.parent=parent or root;o.location=v(c);o.scale=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mats[mat])
 if bevel:
  mod=o.modifiers.new('Manufactured edge','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 if collision:colliders.append(dict(node=o.parent.name,source=o.name,center=c,size=s,surface=surface))
 return o
def mesh(name,vertices,faces,mat='Wood_Honey',parent=None,collision=True,surface='Wood'):
 m=bpy.data.meshes.new(name+'_Mesh');m.from_pydata([v(p) for p in vertices],[],faces);m.materials.append(mats[mat]);m.update()
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);o.parent=parent or root
 if collision:mesh_colliders.append(dict(node=o.name,surface=surface))
 return o
def beam(name,a,b,r=.035,mat='Wood_Edge'):
 delta=v(b)-v(a);bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=r,depth=delta.length)
 o=bpy.context.object;o.name=name;o.parent=root;o.location=(v(a)+v(b))/2;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();o.data.materials.append(mats[mat]);mesh_colliders.append(dict(node=o.name,surface='Wood'));return o
def difference(box,cut):
 lo,hi=box;cl,ch=cut
 if any(min(hi[a],ch[a])<=max(lo[a],cl[a]) for a in range(3)):return [box]
 out=[];l=list(lo);h=list(hi)
 for a in range(3):
  if l[a]<cl[a]:q=h.copy();q[a]=cl[a];out.append((l.copy(),q));l[a]=cl[a]
  if h[a]>ch[a]:q=l.copy();q[a]=ch[a];out.append((q,h.copy()));h[a]=ch[a]
 return out
def shell(name,solids,cuts,floor_mats=True):
 # Orthogonal union boundary removes hidden coplanar junction faces.
 grid=[sorted(set(p[a] for pair in solids+cuts for p in pair)) for a in range(3)]
 grid[2]=sorted(set(grid[2]+[18.2])) if floor_mats else grid[2]
 occupied=set()
 for i in range(len(grid[0])-1):
  for j in range(len(grid[1])-1):
   for k in range(len(grid[2])-1):
    idx=(i,j,k);p=[(grid[a][idx[a]]+grid[a][idx[a]+1])/2 for a in range(3)]
    inside=lambda box:all(box[0][a]<p[a]<box[1][a] for a in range(3))
    if any(inside(s) for s in solids) and not any(inside(c) for c in cuts):occupied.add(idx)
 verts=[];faces=[];indices=[];lookup={}
 material_names=['Plaster_Warm','Floor_Oak','Ceiling_Cream','Grass','Stone','Tile']
 for cell in sorted(occupied):
  for a in range(3):
   others=[n for n in range(3) if n!=a]
   for sign in (-1,1):
    nei=list(cell);nei[a]+=sign
    if tuple(nei) in occupied:continue
    face=[];points=[]
    for b,c in ((0,0),(1,0),(1,1),(0,1)):
     ix=list(cell);ix[a]+=int(sign>0);ix[others[0]]+=b;ix[others[1]]+=c;key=tuple(ix)
     p=tuple(grid[d][ix[d]] for d in range(3));points.append(p)
     if key not in lookup:lookup[key]=len(verts);verts.append(p)
     face.append(lookup[key])
    faces.append(face);center=[sum(p[d] for p in points)/4 for d in range(3)];mat=0
    if a==1:
     if sign<0:mat=2
     elif abs(center[1])<.001 or abs(center[1]-3)<.001:
      x,y,z=center;mat=1
      if floor_mats and (x<0 or x>12.8 or z<0 or z>11.4):mat=4 if 5.16<=x<=6.96 else 3
      if floor_mats and 7.14<=x<=12.62 and 6.74<=z<=11.22:mat=5
    indices.append(mat)
 o=mesh(name,verts,faces,collision=False)
 o.data.materials.clear()
 for n in material_names:o.data.materials.append(mats[n])
 for p,m in zip(o.data.polygons,indices):p.material_index=m
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.dissolve_limit(bm,angle_limit=.001,use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),delimit={'MATERIAL'});bm.to_mesh(o.data);bm.free()
 for n,s in enumerate(solids):
  parts=[s]
  for c in cuts:parts=[p for part in parts for p in difference(part,c)]
  for j,(lo,hi) in enumerate(parts):
   colliders.append(dict(node=root.name,source=f'{name}_Solid_{n}_{j}',center=[(lo[a]+hi[a])/2 for a in range(3)],size=[hi[a]-lo[a] for a in range(3)],surface='Wood' if hi[1] in (0,3) else 'Stone'))
 return o
def export(name,extra=None):
 bpy.context.view_layer.update();renderers=[];points=[];triangles=0
 for o in bpy.context.scene.objects:
  if o.type!='MESH':continue
  o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
  renderers.append(dict(node=o.name,parent=o.parent.name,materials=[m.name for m in o.data.materials]))
  for p in o.bound_box:
   q=o.matrix_world@Vector(p);points.append((q.x,q.z,-q.y))
 entry=dict(root=root.name,units='metres',axes='authored Unity +Y/+Z; FBX needs proven Unity Z correction',
   min=[min(p[a] for p in points) for a in range(3)],max=[max(p[a] for p in points) for a in range(3)],
   renderers=renderers,box_colliders=colliders,mesh_colliders=mesh_colliders,triangles=triangles,
   materials=[dict(name=n,color=list(c)) for n,c in PALETTE.items()],
   roots=[dict(name=o.name,position=[o.location.x,o.location.z,-o.location.y]) for o in root.children if o.type=='EMPTY'])
 if extra:entry.update(extra)
 DATA[name]=entry
 bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=root
 bpy.ops.wm.save_as_mainfile(filepath=str(HERE/(name+'.blend')))
 bpy.ops.export_scene.fbx(filepath=str(HERE/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=False,add_leaf_bones=False)

reset('HouseAlfaSource')
solids=[((-.5,-.18,-2),(13.3,0,19.4)),((0,5.8,0),(12.8,6,11.4)),((0,2.8,0),(12.8,3,11.4)),
 ((0,0,0),(.18,5.8,11.4)),((12.62,0,0),(12.8,5.8,11.4)),((.18,0,0),(12.62,5.8,.18)),((.18,0,11.22),(12.62,5.8,11.4)),
 ((4.98,0,.18),(5.16,5.8,11.22)),((6.96,0,.18),(7.14,5.8,11.22)),((.18,0,4.58),(4.98,5.8,4.76)),
 ((7.14,0,4.58),(12.62,5.8,4.76)),((7.14,0,6.56),(12.62,5.8,6.74)),((9.50,3,6.74),(9.68,5.8,11.22)),
 ((1.30,0,7.38),(1.48,5.8,11.22)),((.18,0,7.38),(1.30,5.8,7.56))]
cuts=[((1.48,2.79,7.38),(4.98,3.01,11.22))]
for portal in PLAN['portals']:
 x,y,z=portal['center'];floor=y-1.1;w=portal['width']+(.14 if portal['door'] else 0);h=2.26 if portal['door'] else 2.2
 if portal['normal'][0]:cuts.append(((x-.091,floor,z-w/2),(x+.091,floor+h,z+w/2)))
 else:cuts.append(((x-w/2,floor,z-.091),(x+w/2,floor+h,z+.091)))
 if portal['door']:
  # All actual leaves use the proven 1.10 m clear door; jambs fill the structural aperture.
  for s in (-1,1):cube(portal['id']+'_Jamb_'+str(s),(x+s*.585,floor+1.1,z),(.07,2.2,.20),'Wood_Edge')
  cube(portal['id']+'_Header',(x,floor+2.23,z),(1.24,.06,.20),'Wood_Edge')
windows=[]
for floor in (0,3):
 for x in (1.38,10.6):windows.append((x,floor+1.70,.09,'z'))
 windows.append((12.71,floor+1.70,8.8,'x'))
windows.append((10.4,1.70,11.31,'z'))
windows.append((8.25,4.70,11.31,'z'))
for i,(x,y,z,axis) in enumerate(windows):
 s=(.182,1.1,1.2) if axis=='x' else (1.2,1.1,.182)
 cuts.append((tuple(c-d/2 for c,d in zip((x,y,z),s)),tuple(c+d/2 for c,d in zip((x,y,z),s))))
 cube(f'Window_{i}_Pane',(x,y,z),(.018,1,1.1) if axis=='x' else (1.1,1,.018),'Glass_Blue_Opaque',surface='Stone')
 for a in (-1,1):
  c=(x,y,z+a*.575) if axis=='x' else (x+a*.575,y,z)
  cube(f'Window_{i}_Jamb_{a}',c,(.20,1.1,.05) if axis=='x' else (.05,1.1,.20),'Wood_Edge')
  cube(f'Window_{i}_Rail_{a}',(x,y+a*.525,z),(.20,.05,1.1) if axis=='x' else (1.1,.05,.20),'Wood_Edge')
shell('HouseShell',solids,cuts)
# Closed roof volume: continuous folded section, no overlapping roof seam faces.
section=[(-.25,6),(6.4,8.2),(13.05,6),(13.05,5.84),(6.4,8.04),(-.25,5.84)]
verts=[(x,y,z) for z in (-.25,11.65) for x,y in section]
faces=[tuple(range(5,-1,-1)),tuple(range(6,12))]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)]
mesh('Roof_Continuous',verts,faces,'Roof_Terracotta',surface='Stone')
for i in range(8):
 height=(i+1)/6;cube(f'Stair_Lower_{i}',(4.18,height/2,7.38+.28*(i+.5)),(1.6,height,.28),'Wood_Honey')
 height=(i+1)/6;cube(f'Stair_Upper_{i}',(2.28,1.5+height/2,9.62-.28*(i+.5)),(1.6,height,.28),'Wood_Honey')
cube('Stair_MidLanding',(3.23,1.41,10.42),(3.5,.18,1.6),'Wood_Honey')
for x,y0,y1 in ((3.32,1.0,2.5),(3.14,4.0,2.5)):
 beam('Stair_Handrail',(x,y0,7.38),(x,y1,9.62))
 for k in (0,.5,1):
  y=y0+(y1-y0)*k;z=7.38+2.24*k;beam('Stair_Baluster',(x,y-1,z),(x,y,z),.025)
beam('Stair_Upper_Guard',(3.38,4.05,7.34),(4.98,4.05,7.34))
for x in (3.40,4.90):beam('Stair_Upper_Guard_Post',(x,3,7.34),(x,4.05,7.34))
# Patio boundary and porch columns, outside circulation.
for x in (0,12.8):
 for z in (12,14,16,18,19.3):cube('Fence_Post',(x,.70,z),(.12,1.4,.12),'Wood_Edge')
 for y in (.45,1.1):cube('Fence_Rail',(x,y,15.4),(.08,.12,8),'Wood_Honey')
for x in (2,4,6,8,10,12):cube('Fence_Back_Post',(x,.7,19.3),(.12,1.4,.12),'Wood_Edge')
for y in (.45,1.1):cube('Fence_Back_Rail',(6.4,y,19.3),(12.8,.12,.08),'Wood_Honey')
export('house_alfa_static',dict(windows=[dict(center=[x,y,z],axis=a,sealed=True) for x,y,z,a in windows]))

reset('LobbyAlfaSource')
solids=[((-5.18,-.18,-4.18),(5.18,0,4.18)),((-5.18,3.2,-4.18),(5.18,3.4,4.18)),
 ((-5.18,0,-4.18),(-5,3.2,4.18)),((5,0,-4.18),(5.18,3.2,4.18)),((-5,0,-4.18),(5,3.2,-4)),((-5,0,4),(5,3.2,4.18))]
shell('LobbyShell',solids,[],False)
# Keep the whole 1.80 m perimeter route clear; seating would narrow the corners.
# Wall-mounted decoration has thickness; no controls or borrowed branding baked into art.
for x in (-2.6,0,2.6):cube('Lobby_Wall_Panel',(x,1.85,3.96),(1.5,1.0,.055),'Textile_Blue',collision=False)
export('lobby_alfa_static')

reset('FurnitureKitAlfa')
def prop(name,index):return empty('Kit_'+name,(index*4,0,0),root)
def part(p,name,c,s,mat='Wood_Honey',bevel=.012):
 surface='Metal' if mat=='Iron' else 'Carpet' if mat.startswith('Textile') else 'Tile' if mat in ('Tile','Porcelain','Glass_Blue_Opaque','Stone') else 'Wood'
 return cube(p.name+'_'+name,c,s,mat,p,bevel,surface=surface)
p=prop('Table',0);part(p,'Top',(0,.77,0),(1.8,.08,.9))
for x in (-.77,.77):
 for z in (-.32,.32):part(p,'Leg',(x,.365,z),(.10,.73,.10),'Wood_Edge',.006)
p=prop('Sofa',1);part(p,'Base',(0,.30,0),(2.1,.25,.85),'Wood_Edge');part(p,'Seat',(0,.49,-.04),(1.82,.17,.72),'Textile_Rust',.045);part(p,'Back',(0,.85,.35),(2.1,.65,.17),'Textile_Rust',.045)
for x in (-.97,.97):part(p,'Arm',(x,.61,-.015),(.20,.40,.85),'Textile_Rust',.035)
p=prop('Counter',2);part(p,'Body',(0,.43,0),(1.50,.80,.62));part(p,'Top',(0,.855,0),(1.56,.05,.67),'Tile')
for x in (-.37,.37):part(p,'Front',(x,.45,-.323),(.69,.66,.022),'Wood_Edge',.004)
p=prop('Stove',3);part(p,'Body',(0,.43,0),(.65,.86,.62),'Iron');part(p,'Oven',(0,.42,-.325),(.51,.43,.025),'Glass_Blue_Opaque')
for x in (-.16,.16):
 for z in (-.15,.15):part(p,'Burner',(x,.872,z),(.22,.015,.22),'Iron',.02)
p=prop('Fridge',4);part(p,'Case',(0,.88,0),(.70,1.76,.68),'Porcelain',.02)
for y,h in ((.68,1.18),(1.52,.41)):
 part(p,'Door',(0,y,-.358),(.65,h,.04),'Tile');part(p,'Handle',(.23,y,-.407),(.035,.28,.035),'Iron',.006)
p=prop('Vanity',5);part(p,'Cabinet',(0,.40,0),(.8,.75,.54));part(p,'Basin',(0,.81,0),(.85,.11,.60),'Porcelain',.04);part(p,'Basin_Depression',(0,.87,-.015),(.51,.006,.30),'Stone',.04);part(p,'Tap',(0,.965,.20),(.04,.20,.035),'Iron',.006)
p=prop('Toilet',6);part(p,'Base',(0,.20,0),(.31,.40,.45),'Porcelain',.07);part(p,'Bowl',(0,.43,-.10),(.43,.16,.59),'Porcelain',.07);part(p,'SeatInset',(0,.515,-.11),(.27,.014,.39),'Stone',.06);part(p,'Tank',(0,.64,.24),(.46,.65,.20),'Porcelain',.04)
p=prop('Washer',7);part(p,'Body',(0,.44,0),(.68,.88,.65),'Porcelain');part(p,'Door',(0,.44,-.339),(.46,.46,.035),'Glass_Blue_Opaque',.15);part(p,'Panel',(0,.78,-.341),(.56,.09,.03),'Iron',.01)
p=prop('Shelf',8)
for x in (-.56,.56):part(p,'Side',(x,.85,0),(.08,1.7,.35),'Wood_Edge')
for y in (.08,.58,1.08,1.65):part(p,'Board',(0,y,0),(1.04,.06,.35))
p=prop('PatioBench',9);part(p,'Seat',(0,.49,0),(1.8,.10,.48));part(p,'Back',(0,.82,.22),(1.8,.60,.065))
for x in (-.71,.71):part(p,'Leg',(x,.22,0),(.10,.44,.40),'Wood_Edge')
p=prop('Pine',10);part(p,'Trunk',(0,.75,0),(.22,1.5,.22),'Wood_Edge',.02)
for y,r,h in ((1.15,.95,1.3),(1.90,.72,1.25),(2.50,.48,1.1)):
 bpy.ops.mesh.primitive_cone_add(vertices=7,radius1=r,radius2=.035,depth=h);o=bpy.context.object;o.name='Kit_Pine_Foliage';o.parent=p;o.location=v((0,y,0));o.data.materials.append(mats['Pine'])
# Foliage visual only; trunk collider defines tree support. No invisible canopy box.
export('furniture_kit_alfa')
(HERE/'source_manifest.json').write_text(json.dumps(DATA,indent=2)+'\n',encoding='utf-8')
print('LMS_ALFA_SOURCES_COMPLETE',json.dumps({k:dict(meshes=len(a['renderers']),triangles=a['triangles'],boxes=len(a['box_colliders'])) for k,a in DATA.items()}),flush=True)
