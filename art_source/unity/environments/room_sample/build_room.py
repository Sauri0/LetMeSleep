"""Original Let me sleep room sample. Run with Blender --background --factory-startup.

All authored dimensions below are Unity-space metres (X right, Y up, Z forward).
No rendering, external dependencies, downloaded meshes, or Unity scene edits.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT_NAME = 'LMS_RoomSample_01'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.materials):
    bpy.data.materials.remove(data)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

def vec(p):
    return Vector((p[0], -p[2], p[1]))

def empty(name, pos=(0, 0, 0), parent=None):
    ob = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(ob)
    ob.parent = parent
    ob.location = vec(pos)
    ob.empty_display_size = .12
    ob.empty_display_type = 'ARROWS'
    return ob

root = empty(ROOT_NAME)
root['units'] = 'metres'
root['source_axes'] = 'Blender X right, Z up, -Y forward; FBX -Z forward / Y up'
root['contract_version'] = 1

palette = {
    'Plaster_Warm': (.68, .61, .48, 1),
    'Ceiling_Cream': (.80, .76, .65, 1),
    'Wood_Honey': (.36, .19, .073, 1),
    'Wood_Edge': (.21, .095, .034, 1),
    'Floor_Oak': (.29, .155, .069, 1),
    'Textile_Navy': (.065, .16, .29, 1),
    'Textile_Blue': (.18, .34, .48, 1),
    'Linen': (.86, .80, .64, 1),
    'Iron': (.048, .062, .068, 1),
    'Glass_Blue_Opaque': (.30, .47, .53, 1),
}
mats = {}
for name, color in palette.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = .78 if name != 'Iron' else .48
    bsdf.inputs['Metallic'].default_value = .55 if name == 'Iron' else 0
    mats[name] = mat

colliders = []
def box(name, center, size, material, parent=root, bevel=0, collision=False):
    bpy.ops.mesh.primitive_cube_add(size=1)
    ob = bpy.context.object
    ob.name = name
    ob.parent = parent
    ob.location = vec(center)
    ob.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(mats[material])
    if bevel:
        mod = ob.modifiers.new('Small manufactured edge', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.affect = 'EDGES'
        bpy.ops.object.modifier_apply(modifier=mod.name)
        ob.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=ob.modifiers[-1].name)
    if collision:
        colliders.append(dict(node=parent.name, center=list(center), size=list(size), source=name))
    return ob

# The shell is a boolean union on an exact orthogonal grid. Emit ONLY boundary
# faces: wall corners, floor/wall and ceiling/wall junctions have no duplicate
# coplanar faces and no overlapping solid volumes. No numerical CSG dependency.
solids = [
    ('Floor', (-.18, -.18, -.18), (4.98, 0, 4.58)),
    ('Ceiling', (-.18, 2.8, -.18), (4.98, 3.0, 4.58)),
    ('West', (-.18, 0, -.18), (0, 2.8, 4.58)),
    ('East', (4.8, 0, -.18), (4.98, 2.8, 4.58)),
    ('SouthLeft', (0, 0, -.18), (.50, 2.8, 0)),
    ('SouthRight', (1.74, 0, -.18), (4.8, 2.8, 0)),
    ('DoorLintel', (.50, 2.26, -.18), (1.74, 2.8, 0)),
    ('NorthLeft', (0, 0, 4.4), (3.0, 2.8, 4.58)),
    ('NorthRight', (4.2, 0, 4.4), (4.8, 2.8, 4.58)),
    ('WindowBelow', (3.0, 0, 4.4), (4.2, 1.15, 4.58)),
    ('WindowAbove', (3.0, 2.25, 4.4), (4.2, 2.8, 4.58)),
]
grid = [sorted({v[a] for _, lo, hi in solids for v in (lo, hi)}) for a in range(3)]
occupied = set()
for i in range(len(grid[0])-1):
    for j in range(len(grid[1])-1):
        for k in range(len(grid[2])-1):
            p = [(grid[a][n]+grid[a][n+1])/2 for a,n in enumerate((i,j,k))]
            if any(all(lo[a] < p[a] < hi[a] for a in range(3)) for _,lo,hi in solids):
                occupied.add((i,j,k))
vertices, faces, face_mats, lookup = [], [], [], {}
for cell in sorted(occupied):
    for axis in range(3):
        other = [a for a in range(3) if a != axis]
        for sign in (-1, 1):
            neighbor = list(cell)
            neighbor[axis] += sign
            if tuple(neighbor) in occupied:
                continue
            face = []
            for a,b in ((0,0),(1,0),(1,1),(0,1)):
                idx = list(cell)
                idx[axis] += int(sign > 0)
                idx[other[0]] += a
                idx[other[1]] += b
                key = tuple(idx)
                if key not in lookup:
                    lookup[key] = len(vertices)
                    vertices.append(tuple(vec([grid[d][idx[d]] for d in range(3)])))
                face.append(lookup[key])
            faces.append(face)
            y = (grid[1][cell[1]]+grid[1][cell[1]+1])/2
            face_mats.append(0 if y < 0 else 2 if y >= 2.8 else 1)
mesh = bpy.data.meshes.new('Shell_WeldedBoundary')
mesh.from_pydata(vertices, [], faces)
mesh.update()
shell = bpy.data.objects.new('Architecture_Shell', mesh)
bpy.context.collection.objects.link(shell)
shell.parent = root
for name in ('Floor_Oak','Plaster_Warm','Ceiling_Cream'):
    mesh.materials.append(mats[name])
for poly, material in zip(mesh.polygons, face_mats):
    poly.material_index = material
bm = bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bmesh.ops.dissolve_limit(bm, angle_limit=.001, use_dissolve_boundaries=False,
                        verts=list(bm.verts), edges=list(bm.edges), delimit={'MATERIAL'})
bm.to_mesh(mesh)
bm.free()
for name, lo, hi in solids:
    colliders.append(dict(node=root.name, source=name,
        center=[(lo[a]+hi[a])/2 for a in range(3)], size=[hi[a]-lo[a] for a in range(3)]))

# Frame ends at the opening; it does not cover another visible wall face.
box('Door_Jamb_L', (.535,1.10,-.08), (.07,2.2,.20), 'Wood_Edge', collision=True)
box('Door_Jamb_R', (1.705,1.10,-.08), (.07,2.2,.20), 'Wood_Edge', collision=True)
box('Door_Frame_Header', (1.12,2.23,-.08), (1.24,.06,.20), 'Wood_Edge', collision=True)
door = empty('Door_01', parent=root)
hinge = empty('Door_01_Hinge', (.585,0,.045), door)
hinge['closed_yaw_degrees'] = 0.0
hinge['open_yaw_degrees'] = -100.0
hinge['axis_unity'] = '+Y'
hinge['network_state_owner'] = 'host; integrate through Director contract'
box('Door_01_Leaf', (.535,1.1025,0), (1.07,2.185,.04), 'Wood_Honey', hinge, .008, True)
# Raised panels have visible thickness; surfaces do not coincide with leaf face.
for z in (-.026,.026):
    for y,h in ((.61,.83),(1.60,.75)):
        box('Door_Panel', (.535,y,z), (.85,h,.018), 'Wood_Edge', hinge, .006)
    box('Door_Handle_Base', (.945,1.035,z*1.9), (.075,.16,.035), 'Iron', hinge, .006)
    box('Door_Handle_Lever', (.900,1.055,z*3), (.17,.027,.027), 'Iron', hinge, .005)
for y in (.25,1.85):
    box('Door_Hinge_Barrel', (0,y,-.015), (.028,.12,.065), 'Iron', hinge, .004)
handle = empty('Socket_Door_Use', (.92,1.06,.105), hinge)

# One ordinary bed, nightstand, desk and chair, deliberately laid out by use.
bed = empty('Furniture_Bed', (3.5,0,3.1), root)
box('Bed_Base', (0,.30,0), (1.60,.19,2.20), 'Wood_Edge', bed, .018, True)
for x in (-.70,.70):
    for z in (-.99,.99):
        box('Bed_Leg', (x,.115,z), (.12,.23,.12), 'Wood_Honey', bed, .008, True)
box('Bed_Mattress', (0,.49,0), (1.50,.19,2.10), 'Linen', bed, .04, True)
box('Bed_Quilt', (0,.615,-.28), (1.52,.07,1.55), 'Textile_Navy', bed, .025, True)
box('Bed_Fold', (0,.665,.39), (1.49,.035,.19), 'Textile_Blue', bed, .012)
for x in (-.38,.38):
    box('Bed_Pillow', (x,.635,.76), (.62,.15,.39), 'Linen', bed, .065)
box('Bed_Headboard', (0,.74,1.05), (1.64,.72,.10), 'Wood_Honey', bed, .015, True)
for x in (-.78,.78):
    box('Bed_Head_Post', (x,.65,1.055), (.105,1.30,.12), 'Wood_Edge', bed, .008)

night = empty('Furniture_Nightstand', (2.225,0,3.85), root)
box('Nightstand_Case', (0,.325,0), (.55,.55,.50), 'Wood_Honey', night, .012, True)
box('Nightstand_Top', (0,.625,0), (.59,.05,.54), 'Wood_Edge', night, .01, True)
for y in (.25,.47):
    box('Nightstand_Drawer', (0,y,-.262), (.46,.18,.025), 'Wood_Edge', night, .004)
    box('Nightstand_Knob', (0,y,-.29), (.055,.035,.04), 'Iron', night, .008)

desk = empty('Furniture_Desk', (.84,0,3.95), root)
box('Desk_Top', (0,.765,0), (1.32,.06,.64), 'Wood_Honey', desk, .014, True)
for x in (-.56,.56):
    for z in (-.24,.24):
        box('Desk_Leg', (x,.3675,z), (.085,.735,.085), 'Wood_Edge', desk, .006, True)
box('Desk_Rear_Rail', (0,.63,.245), (1.05,.12,.055), 'Wood_Edge', desk, .005, True)
chair = empty('Furniture_Chair', (.85,0,2.98), root)
box('Chair_Seat', (0,.445,0), (.49,.065,.49), 'Wood_Honey', chair, .012, True)
for x in (-.19,.19):
    for z in (-.19,.19):
        box('Chair_Leg', (x,.205,z), (.065,.41,.065), 'Wood_Edge', chair, .006, True)
for x in (-.19,.19):
    box('Chair_Back_Post', (x,.70,-.215), (.065,.50,.065), 'Wood_Edge', chair, .006, True)
box('Chair_Back', (0,.865,-.215), (.40,.16,.055), 'Wood_Honey', chair, .01, True)

# Closed window: opaque material placeholder; no alpha sorting claim.
for x in (3.025,4.175):
    box('Window_Jamb', (x,1.70,4.485), (.05,1.10,.17), 'Wood_Edge', collision=True)
for y in (1.175,2.225):
    box('Window_Rail', (3.6,y,4.485), (1.10,.05,.17), 'Wood_Edge', collision=True)
box('Window_Pane', (3.6,1.70,4.49), (1.10,1.0,.018), 'Glass_Blue_Opaque', collision=True)
box('Window_Mullion', (3.6,1.70,4.46), (.045,1.0,.045), 'Wood_Honey')
box('Window_Sill', (3.6,1.125,4.38), (1.32,.05,.32), 'Wood_Honey', bevel=.008, collision=True)

sockets = []
def socket(name, kind, position, facing, stand, enabled=False):
    ob = empty(name, position, root)
    ob.rotation_euler.z = math.atan2(facing[0], facing[2])
    ob['kind'] = kind
    ob['enabled_in_alpha'] = enabled
    sockets.append(dict(id=name, node=name, kind=kind, position=position,
        outward_normal=facing, standing_position=stand,
        clearance_radius=.4, enabled_in_alpha=enabled))
socket('Socket_Pickup_Desk', 'pickup_surface', [.84,.795,3.95], [0,0,-1], [1.65,0,3.2], True)
socket('Socket_Pickup_Nightstand', 'pickup_surface', [2.225,.65,3.85], [0,0,-1], [2.225,0,3.1], True)
socket('Socket_Task_Bed', 'future_task', [2.69,.60,2.55], [-1,0,0], [2.20,0,2.55])

contract = dict(schema_version=1, asset=ROOT_NAME, units='metres', axes='Unity +Y up, +Z forward',
    interior_size=[4.8,2.8,4.4], exterior_min=[-.18,-.18,-.18], exterior_max=[4.98,3.0,4.58],
    architecture='single welded boundary of union; no internal contact faces',
    door=dict(root='Door_01', pivot='Door_01_Hinge', pivot_position=[.585,0,.045],
        closed_yaw=0, open_yaw=-100, axis=[0,1,0], leaf_size=[1.07,2.185,.04],
        leaf_center_local=[.535,1.1025,0], clear_opening=dict(x=[.57,1.67],y=[0,2.20],z=0),
        use_socket='Socket_Door_Use', sweep_radius=1.09,
        sweep_clearance_reserve=dict(min=[.36,0,.02],max=[1.69,2.20,1.15]),
        collision_rule='host checks sweep/occupancy; never close through actor; physics belongs to integration'),
    furniture_footprints=[
        dict(id='Furniture_Bed',min=[2.68,2.0],max=[4.32,4.215]),
        dict(id='Furniture_Nightstand',min=[1.93,3.56],max=[2.52,4.12]),
        dict(id='Furniture_Desk',min=[.18,3.63],max=[1.50,4.27]),
        dict(id='Furniture_Chair',min=[.605,2.7325],max=[1.095,3.225])],
    routes=[dict(id='entry_to_center', points=[[1.12,0,-.4],[1.12,0,.48],[1.96,0,1.50]]),
        dict(id='bed_left', points=[[2.12,0,1.5],[2.12,0,2.55],[2.12,0,3.08]])],
    actor_contract=dict(human_radius=.25,human_height=1.72,human_eye_height=1.53,
        crouched_height=1.0,mosquito_radius=.055,main_circulation_width=1.8,stair_width=1.6),
    main_clear_area=dict(min=[1.76,0,.1],max=[3.56,2.8,1.9]),
    sockets=sockets, box_colliders=colliders,
    export=dict(forward='-Z',up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        animation=False,add_leaf_bones=False),
    limitations=['Not Unity-import validated', 'No engine lighting/render/playtest or WAN evidence',
        'Controller traversal and physical door interaction require Unity integration',
        'Pickup sockets reserve geometry only; no extra alpha inventory or Tasks gameplay'])
(HERE/'room_contract.json').write_text(json.dumps(contract,indent=2)+'\n',encoding='utf-8')

# Keep rest pose closed. Runtime animates the actual hinge, including handles.
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = root
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'room_sample.blend'))
bpy.ops.export_scene.fbx(filepath=str(HERE/'room_sample.fbx'), use_selection=True,
    object_types={'MESH','EMPTY'}, global_scale=1, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, bake_anim=False, add_leaf_bones=False,
    path_mode='AUTO', use_mesh_modifiers=True)
# Separate door export for the runtime prefab. Its root remains in room local
# coordinates; the documented hinge placement is preserved rather than baked.
for ob in bpy.context.scene.objects:
    ob.select_set(ob == door or ob in door.children_recursive)
bpy.ops.export_scene.fbx(filepath=str(HERE/'door_01.fbx'), use_selection=True,
    object_types={'MESH','EMPTY'}, global_scale=1, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, bake_anim=False, add_leaf_bones=False)
for ob in bpy.context.scene.objects:
    ob.select_set(ob != door and ob not in door.children_recursive)
bpy.ops.export_scene.fbx(filepath=str(HERE/'room_furnished_without_door.fbx'), use_selection=True,
    object_types={'MESH','EMPTY'}, global_scale=1, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, bake_anim=False, add_leaf_bones=False)
print('LMS_ROOM_GENERATION_COMPLETE', flush=True)
