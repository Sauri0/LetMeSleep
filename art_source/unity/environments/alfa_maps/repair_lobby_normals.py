"""Bounded source repair matching build_sources' explicit cavity winding.

Updates only the existing lobby blend/FBX; no geometry, material or collider change.
"""
import bpy, bmesh
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'lobby_alfa_static.blend'))
obj=bpy.data.objects['LobbyShell']
before=[tuple(v.co) for v in obj.data.vertices]
bm=bmesh.new();bm.from_mesh(obj.data)
planes=[(1,0,(0,1,0)),(1,3.2,(0,-1,0)),(0,-5,(1,0,0)),(0,5,(-1,0,0)),(2,-4,(0,0,1)),(2,4,(0,0,-1))]
reverse=[];seen=set()
for face in bm.faces:
    points=[Vector((v.co.x,v.co.z,-v.co.y)) for v in face.verts]
    for index,(axis,coordinate,expected) in enumerate(planes):
        if all(abs(p[axis]-coordinate)<.0001 for p in points):
            center=sum(points,Vector())/len(points)
            if not (-5.0001<=center.x<=5.0001 and -.0001<=center.y<=3.2001 and -4.0001<=center.z<=4.0001):
                continue
            seen.add(index)
            normal=Vector((face.normal.x,face.normal.z,-face.normal.y))
            if normal.dot(Vector(expected))<0:reverse.append(face)
assert len(seen)==6, 'All six interior cavity planes required'
bmesh.ops.reverse_faces(bm,faces=reverse);bm.normal_update();bm.to_mesh(obj.data);bm.free()
assert before==[tuple(v.co) for v in obj.data.vertices], 'Repair must not move geometry'
bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=bpy.data.objects['LobbyAlfaSource']
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'lobby_alfa_static.blend'))
bpy.ops.export_scene.fbx(filepath=str(HERE/'lobby_alfa_static.fbx'),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=False,bake_anim=False,add_leaf_bones=False)
print('LMS_LOBBY_REPAIR reversed_faces='+str(len(reverse))+'; vertex positions unchanged',flush=True)
