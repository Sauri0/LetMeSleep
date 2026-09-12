"""Bounded Blender checks; report only geometry/export facts, never engine proof."""
import bpy
import bmesh
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
contract = json.loads((HERE/'room_contract.json').read_text())
checks = []
def check(name, ok, detail=None):
    checks.append(dict(name=name,passed=bool(ok),detail=detail))
    if not ok:
        print('FAIL',name,detail,flush=True)

def unity(p): return [p.x,p.z,-p.y]
def bbox(ob):
    pts = [unity(ob.matrix_world @ Vector(p)) for p in ob.bound_box]
    return [[min(p[a] for p in pts) for a in range(3)],
            [max(p[a] for p in pts) for a in range(3)]]

def snapshot():
    result={}
    for ob in bpy.context.scene.objects:
        result[ob.name]=dict(type=ob.type,parent=ob.parent.name if ob.parent else None,
            position=unity(ob.matrix_world.translation),
            bbox=bbox(ob) if ob.type=='MESH' else None)
    return result

bpy.ops.wm.open_mainfile(filepath=str(HERE/'room_sample.blend'))
bpy.context.view_layer.update()
source=snapshot()
shell=bpy.data.objects['Architecture_Shell']
bm=bmesh.new(); bm.from_mesh(shell.data)
check('shell manifold boundary',all(e.is_manifold for e in bm.edges),len(bm.edges))
check('shell positive volume',bm.calc_volume(signed=True)>0,bm.calc_volume(signed=True))
check('shell no zero area faces',all(f.calc_area()>1e-8 for f in bm.faces))
bm.free()
check('all object scales unit',all(max(abs(s-1) for s in o.scale)<1e-5 for o in bpy.context.scene.objects))
mesh_count=sum(o.type=='MESH' for o in bpy.context.scene.objects)
triangles=0
for ob in bpy.context.scene.objects:
    if ob.type=='MESH':
        ob.data.calc_loop_triangles()
        triangles+=len(ob.data.loop_triangles)
        check('positive finite bounds '+ob.name,
            all(math.isfinite(x) for bound in bbox(ob) for x in bound) and
            all(bbox(ob)[1][a]>bbox(ob)[0][a] for a in range(3)))

# Compare oriented door-part boxes against static boxes over full swing. Exclude
# hinge barrel hardware intentionally meeting the jamb; no exclusion for leaf,
# raised panels, handles or frame. Separating-axis test in XZ + Y overlap.
hinge=bpy.data.objects['Door_01_Hinge']
static=[]
for c in contract['box_colliders']:
    if c['node']=='Door_01_Hinge': continue
    parent=bpy.data.objects[c['node']]
    origin=unity(parent.matrix_world.translation)
    center=[origin[a]+c['center'][a] for a in range(3)]
    static.append((c['source'],center,c['size']))
def overlap(poly, center, size):
    rect=[(center[0]+sx*size[0]/2,center[2]+sz*size[2]/2) for sx,sz in ((-1,-1),(1,-1),(1,1),(-1,1))]
    axes=[(1,0),(0,1)]
    for i in range(4):
        dx=poly[(i+1)%4][0]-poly[i][0]; dz=poly[(i+1)%4][1]-poly[i][1]
        axes.append((-dz,dx))
    for axis in axes:
        pp=[p[0]*axis[0]+p[1]*axis[1] for p in poly]
        rr=[p[0]*axis[0]+p[1]*axis[1] for p in rect]
        if min(max(pp),max(rr))-max(min(pp),min(rr))<=1e-8: return False
    return True
collisions=[]
for degrees in range(0,-101,-1):
    hinge.rotation_euler.z=math.radians(degrees)
    bpy.context.view_layer.update()
    for ob in hinge.children_recursive:
        if ob.type!='MESH' or ob.name.startswith('Door_Hinge_Barrel'): continue
        local=[Vector(p) for p in ob.bound_box]
        lx,hx=min(p.x for p in local),max(p.x for p in local)
        ly,hy=min(p.y for p in local),max(p.y for p in local)
        poly=[]
        for x,y in ((lx,ly),(hx,ly),(hx,hy),(lx,hy)):
            p=unity(ob.matrix_world@Vector((x,y,0)))
            poly.append((p[0],p[2]))
        bounds=bbox(ob)
        for name,c,s in static:
            if min(bounds[1][1],c[1]+s[1]/2)-max(bounds[0][1],c[1]-s[1]/2)<=1e-7: continue
            if overlap(poly,c,s): collisions.append([degrees,ob.name,name])
check('door 101 angle samples no static penetration',not collisions,collisions[:20])
hinge.rotation_euler.z=0
bpy.context.view_layer.update()
check('hinge position preserved',max(abs(a-b) for a,b in zip(unity(hinge.matrix_world.translation),contract['door']['pivot_position']))<1e-6)

# Actor footprint tests against furniture; standing points are floor anchors.
for socket in contract['sockets']:
    p=socket['standing_position']; radius=socket['clearance_radius']
    hits=[]
    for f in contract['furniture_footprints']:
        closest=[max(f['min'][a],min(p[(0,2)[a]],f['max'][a])) for a in range(2)]
        d=math.hypot(p[0]-closest[0],p[2]-closest[1])
        if d<radius-1e-6: hits.append(f['id'])
    check('socket standing clearance '+socket['id'],not hits,hits)
area=contract['main_clear_area']
hits=[]
for f in contract['furniture_footprints']:
    if all(min(area['max'][(0,2)[a]],f['max'][a])-max(area['min'][(0,2)[a]],f['min'][a])>0 for a in range(2)):
        hits.append(f['id'])
check('main 1.8m square free of furniture',not hits,hits)

roundtrips=[]
for filename in ('room_sample.fbx','door_01.fbx','room_furnished_without_door.fbx'):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(HERE/filename),use_anim=False)
    bpy.context.view_layer.update()
    imported=snapshot()
    expected=set(source)
    door_names={n for n in source if n=='Door_01' or n.startswith('Door_') or n=='Socket_Door_Use'}
    if filename=='door_01.fbx': expected=door_names
    elif filename=='room_furnished_without_door.fbx': expected-=door_names
    # Door_Jamb/Frame are architectural, not descendants of Door_01.
    if filename=='door_01.fbx':
        expected={n for n in expected if not n.startswith(('Door_Jamb','Door_Frame'))}
    elif filename=='room_furnished_without_door.fbx':
        expected|={n for n in source if n.startswith(('Door_Jamb','Door_Frame'))}
    check(filename+' object set',set(imported)==expected,dict(missing=sorted(expected-set(imported)),extra=sorted(set(imported)-expected)))
    max_error=0
    hierarchy_errors=[]
    for n in expected & set(imported):
        old,new=source[n],imported[n]
        wanted_parent=old['parent'] if old['parent'] in expected else None
        if new['parent']!=wanted_parent: hierarchy_errors.append(n)
        pairs=list(zip(old['position'],new['position']))
        if old['bbox'] and new['bbox']:
            pairs+=list(zip(sum(old['bbox'],[]),sum(new['bbox'],[])))
        max_error=max(max_error,max(abs(a-b) for a,b in pairs))
    check(filename+' hierarchy',not hierarchy_errors,hierarchy_errors)
    check(filename+' bounds and origins within 0.1mm',max_error<.0001,max_error)
    roundtrips.append(dict(file=filename,objects=len(imported),max_error_metres=max_error))

files={p.name:dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
       for p in HERE.iterdir() if p.suffix in ('.blend','.fbx')}
report=dict(blender=bpy.app.version_string,scope='CPU source geometry and Blender FBX roundtrip only; Unity not tested',
    meshes=mesh_count,triangles=triangles,passed=sum(c['passed'] for c in checks),
    failed=sum(not c['passed'] for c in checks),checks=checks,roundtrips=roundtrips,files=files)
(HERE/'validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('LMS_ROOM_VALIDATION',json.dumps({k:report[k] for k in ('meshes','triangles','passed','failed')}),flush=True)
if report['failed']: raise RuntimeError('Room validation failed; inspect validation.json')
