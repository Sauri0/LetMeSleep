"""Selected-A glasses: closed nasal arcs and ear-supported curved temples.

Only human_accessory_2 is replaced. Work in Godot metres to make the measured
support contract explicit; object transforms, the head rig and other parts stay
untouched. Detailed samples belong in an external audit, not GLB custom extras.
"""
import math,json
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def godot(q):return Vector((q.x,q.z,-q.y))
def blender(q):return Vector((q.x,-q.z,q.y))
def geometry(obj):
    obj.data.calc_loop_triangles()
    p=[godot(v.co) for v in obj.data.vertices]
    t=[tuple(f.vertices) for f in obj.data.loop_triangles]
    return p,t,BVHTree.FromPolygons(p,t,all_triangles=True)

def catmull(points,steps=8):
    result=[]
    for i in range(len(points)-1):
        a=points[max(0,i-1)];b=points[i];c=points[i+1];d=points[min(len(points)-1,i+2)]
        for j in range(steps):
            t=j/steps
            result.append((2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)*.5)
    return result+[points[-1].copy()]

def repair_human_glasses():
    obj=bpy.data.objects['human_accessory_2']
    if obj.get('glasses_fit09'):return json.loads(obj['glasses_fit09']),{}
    head=geometry(bpy.data.objects['human_head'])[2]
    hair=[geometry(bpy.data.objects['human_hair_%d'%i])[2] for i in range(3)]
    vertices=[];faces=[];parts=[];centerlines={};support=[]

    def tube(label,path,radius,closed=False):
        start=len(vertices);first_face=len(faces);sides=10
        for i,q in enumerate(path):
            tangent=(path[(i+1)%len(path)]-path[(i-1)%len(path)] if closed else path[min(i+1,len(path)-1)]-path[max(i-1,0)]).normalized()
            axis=Vector((0,0,1))
            if abs(axis.dot(tangent))>.93:axis=Vector((0,1,0))
            n=(axis-tangent*axis.dot(tangent)).normalized();b=tangent.cross(n).normalized()
            for j in range(sides):
                theta=math.tau*j/sides;vertices.append(q+radius*(n*math.cos(theta)+b*math.sin(theta)))
        for i in range(len(path) if closed else len(path)-1):
            nxt=(i+1)%len(path)
            for j in range(sides):
                k=(j+1)%sides
                faces.append((start+i*sides+j,start+i*sides+k,start+nxt*sides+k,start+nxt*sides+j))
        if not closed:
            for i,reverse in [(0,True),(len(path)-1,False)]:
                middle=len(vertices);vertices.append(path[i])
                for j in range(sides):
                    a=start+i*sides+j;b=start+i*sides+(j+1)%sides
                    faces.append((middle,b,a) if reverse else (middle,a,b))
        centerlines[label]=[list(p) for p in path]
        parts.append({'name':label,'vertex_start':start,'vertex_count':len(vertices)-start,'polygon_start':first_face,'polygon_count':len(faces)-first_face,'radius_m':radius,'closed_path':closed})

    # Keep the upper/external circle and move only the lower medial arc toward
    # the cheek when needed. Depth-only conformance produced a nasal basket;
    # limiting that protrusion requires a local XY change, measured per point.
    ring_changes=[]
    for side in [-1,1]:
        path=[];depth=[];count=96;radius=.006
        def front_limit(p):
            limit=-.17776
            for dx,dy in [(0,0)]+[(math.cos(math.tau*k/12)*radius,math.sin(math.tau*k/12)*radius) for k in range(12)]:
                hit,_,_,_=head.ray_cast(Vector((p.x+dx,p.y+dy,-1)),Vector((0,0,1)),2)
                if hit is not None:limit=min(limit,hit.z-radius-.0015)
            return limit
        offsets=[];original=[];eligible=[]
        for i in range(count):
            t=math.tau*i/count
            p=Vector((side*.07344+math.cos(t)*.06156,1.59324+math.sin(t)*.047,-.17776))
            original.append(p.copy());offset=0.0
            eligible.append(side*p.x<.07344 and p.y<1.604)
            if eligible[-1]:
                while front_limit(p)<-.19676 and offset<.050:
                    p.x+=side*.0005;offset+=.0005
            path.append(p);offsets.append(offset)
        for _ in range(4):offsets=[max(offsets[i],(offsets[(i-1)%count]+offsets[i]*2+offsets[(i+1)%count])/4) if eligible[i] else 0 for i in range(count)]
        for i,p in enumerate(path):
            p.x=original[i].x+side*offsets[i]
            depth.append(front_limit(p))
        # Smooth, conservative depth envelope. Never changes any X/Y centre.
        for _ in range(5):depth=[min(depth[i],(depth[(i-1)%count]+depth[i]*2+depth[(i+1)%count])/4) for i in range(count)]
        for p,z in zip(path,depth):p.z=z
        ring_changes.append({'side':side,'maximum_lateral_shift_m':max(offsets),'maximum_forward_shift_m':max(-.17776-z for z in depth)})
        tube('ring_left' if side<0 else 'ring_right',path,radius,True)

        # The front hinge is on the outer upper ring. A real hook follows the
        # ear's upper/back surface instead of ending in empty air beside it.
        angle=math.pi-.23 if side<0 else .23
        hinge=path[round((angle%math.tau)/math.tau*count)%count].copy()
        anchors=[hinge,Vector((side*.170,1.616,-.105)),Vector((side*.192,1.607,-.038)),
                 Vector((side*.190,1.599,-.004)),Vector((side*.200,1.586,.008)),Vector((side*.204,1.561,.024))]
        curve=catmull(anchors,7);r=.004
        for i,p in enumerate(curve):
            # Last hook span lies just above the actual ear. Other spans only
            # move when the head or a compatible hair style would cut them.
            if i>=21:
                hit,normal,tri,_=head.find_nearest(p)
                p=hit+normal*(r+.0004)
                support.append({'part':'temple_left' if side<0 else 'temple_right','sample':i,'head_triangle':tri,'point':list(hit),'normal':list(normal)})
            for _ in range(8):
                for tree in [head]+hair:
                    hit,normal,_,_=tree.find_nearest(p)
                    signed=(p-hit).dot(normal)
                    if signed<r+.0004:p+=normal*(r+.0004-signed)
            curve[i]=p
        tube('temple_left' if side<0 else 'temple_right',curve,r)

    bridge=catmull([Vector((-.0108,1.597,-.17776)),Vector((0,1.60734,-.17968)),Vector((.0108,1.597,-.17776))],6)
    for p in bridge:
        for dx in [-.005,0,.005]:
            hit,_,_,_=head.ray_cast(Vector((p.x+dx,p.y,-1)),Vector((0,0,1)),2)
            if hit is not None:p.z=min(p.z,hit.z-.0065)
    tube('bridge',bridge,.005)
    old=obj.data;old_name=old.name;mesh=bpy.data.meshes.new(old_name+'_supported')
    mesh.from_pydata([blender(p) for p in vertices],[],faces)
    for material in old.materials:mesh.materials.append(material)
    obj.data=mesh
    if old.users==0:bpy.data.meshes.remove(old)
    mesh.name=old_name
    # Vertex group names and the existing armature modifier stay on the node.
    for group in obj.vertex_groups:group.remove(list(range(len(mesh.vertices))))
    if 'head' not in obj.vertex_groups:obj.vertex_groups.new(name='head')
    obj.vertex_groups['head'].add(list(range(len(mesh.vertices))),1,'REPLACE')
    for face in mesh.polygons:face.use_smooth=True
    mesh.update()
    record={'version':2,'mesh':obj.name,'method':'Closed rings with local lower-medial lateral clearance and limited depth, unchanged upper/external arc; curved ear hooks supported on actual head triangles',
            'coordinate_system':'Godot metres X/Y/-Z front','parts':parts,'vertices':len(vertices),
            'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),'head_weight':1.0,
            'ear_support_region':{'abs_x':[.170,.230],'y':[1.540,1.615],'z':[-.030,.045]},
            'expected_root_gap_m':.0004,'other_parts_unchanged':True,'ring_changes':ring_changes}
    obj['glasses_fit09']=json.dumps(record,separators=(',',':'))
    return record,{'centerlines':centerlines,'ear_support':support}
