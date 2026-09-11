"""Selected-production eyelids: a moving shell around an invariant eye.

The old Blink key scaled the white and pupil to 2.5% height. These selected
assets keep their ocular components, replacing only the old tubular lid/crease
surfaces with upper/lower lids. Eight angular intervals avoid the chord through
the eye produced by a single linear open/closed morph.
"""
import json, math
import bpy
from mathutils import Vector

ARC_STEPS=8

def globe_components(mesh):
    """Tangent globes share a vertex; only a shared EDGE joins their faces."""
    faces=[p for p in mesh.polygons if mesh.materials[p.material_index].name=='eye_white']
    edges={};neighbors={p.index:set() for p in faces};lookup={p.index:p for p in faces}
    for p in faces:
        ids=list(p.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):edges.setdefault(tuple(sorted((a,b))),[]).append(p.index)
    for owners in edges.values():
        for index in owners:neighbors[index].update(owners)
    pending=set(neighbors);components=[]
    while pending:
        stack=[pending.pop()];part=set(stack)
        while stack:
            for index in neighbors[stack.pop()]:
                if index in pending:pending.remove(index);part.add(index);stack.append(index)
        points={i for index in part for i in lookup[index].vertices}
        extent=[max(mesh.vertices[i].co[a] for i in points)-min(mesh.vertices[i].co[a] for i in points) for a in range(3)]
        if min(extent)>.02:components.append((points,[list(lookup[i].vertices) for i in part]))
    assert len(components)==2,('Two full ocular volumes required',mesh.name,len(components))
    return components

def _components(faces):
    neighbors={}
    for face in faces:
        for i in face:neighbors.setdefault(i,set()).update(face)
    remaining=set(neighbors);result=[]
    while remaining:
        stack=[remaining.pop()];part=set(stack)
        while stack:
            for i in neighbors[stack.pop()]:
                if i in remaining:remaining.remove(i);part.add(i);stack.append(i)
        result.append(part)
    return sorted(result,key=len,reverse=True)

def rebuild_eyelids(obj,species,variant):
    old=obj.data;human=species=='human'
    old_keys={key.name:[v.co.copy() for v in key.data] for key in old.shape_keys.key_blocks}
    base=old_keys['Basis']
    removed_materials={'skin','skin_shadow'} if human else {'insect_primary'}
    removed=[p for p in old.polygons if old.materials[p.material_index].name in removed_materials]
    kept=[p for p in old.polygons if p not in removed]
    indices=sorted({i for p in kept for i in p.vertices});remap={a:b for b,a in enumerate(indices)}
    points=[base[i].copy() for i in indices]
    uv_points=[(0.0,0.0) for _ in indices]
    if old.uv_layers.active:
        for p in kept:
            for loop in p.loop_indices:uv_points[remap[old.loops[loop].vertex_index]]=tuple(old.uv_layers.active.data[loop].uv)
    faces=[[remap[i] for i in p.vertices] for p in kept]
    materials=[p.material_index for p in kept]
    keys={name:[values[i].copy() for i in indices] for name,values in old_keys.items()}
    weights=[[ (obj.vertex_groups[w.group].name,w.weight) for w in old.vertices[i].groups] for i in indices]
    eye_indices={i for p in kept if old.materials[p.material_index].name in {'eye_white','pupil'} for i in p.vertices}
    # Includes moving highlights as well as the two complete white globes.
    # Gaze remains unchanged; only the destructive inherited Blink is removed.
    for blink in ['BlinkL','BlinkR']:
        for i in eye_indices:keys[blink][remap[i]]=base[i].copy()
        for step in range(1,ARC_STEPS):keys[blink+'Arc'+str(step)]=[q.copy() for q in points]
    globes=[indices for indices,_ in globe_components(old)]
    lid_material=next(i for i,m in enumerate(old.materials) if m.name==('skin' if human else 'insect_primary'))
    ocular=[]
    angular=40;rows=12
    for component in sorted(globes,key=lambda c:sum(base[i].x for i in c)/len(c)):
        lo=Vector(tuple(min(base[i][axis] for i in component) for axis in range(3)))
        hi=Vector(tuple(max(base[i][axis] for i in component) for axis in range(3)))
        center=(lo+hi)*.5;radii=(hi-lo)*.5
        blink='BlinkL' if center.x<0 else 'BlinkR'
        # Blender +Y is facial forward, Z is up. The pupil is a real convex
        # component in front of the white; the lid envelope includes that tip.
        side_indices=[i for i in eye_indices if (base[i].x<0)==(center.x<0)]
        front=max(base[i].y for i in side_indices)
        pupil_depth=max(0,front-center.y-radii.y)
        # Existing mosquito IDs 0/1 used identical lids and differed only in
        # pupil position. Alertas now has its own open silhouette; the closed
        # envelope, eye volumes and all eight blink intervals remain identical.
        top_angle=([.78,1.10,1.30] if human else [.77,.42,1.12])[variant]
        bottom_angle=([2.52,2.52,2.36] if human else [2.52,2.70,2.52])[variant]
        def shell(theta,phi):
            direction=Vector((math.sin(phi)*math.cos(theta),math.sin(phi)*math.sin(theta),math.cos(phi)))
            depth=radii.y+pupil_depth+.001 if direction.y>0 else radii.y
            q=center+Vector((radii.x*direction.x,depth*direction.y,radii.z*direction.z))
            normal=Vector((direction.x/radii.x,direction.y/radii.y,direction.z/radii.z)).normalized()
            # A smooth front bulge covers the actual pupil/highlight, with a
            # sub-millimetre radial reserve for interpolation between knots.
            return q+normal*(.0010 if human else .0008)
        for upper in [True,False]:
            first=len(points)
            for row in range(rows+1):
                t=row/rows
                for column in range(angular):
                    theta=2*math.pi*column/angular
                    corner=abs(math.cos(theta))**1.3
                    opened=top_angle+(math.pi*.5-top_angle)*corner if upper else bottom_angle-(bottom_angle-1.65)*corner
                    closed=1.78-.07*corner
                    def position(amount):
                        boundary=opened+(closed-opened)*amount
                        # Tiny nonzero pole rings avoid collapsed triangles.
                        phi=.012+(boundary-.012)*t if upper else boundary+(math.pi-.012-boundary)*t
                        return shell(theta,phi)
                    neutral=position(0);shut=position(1)
                    points.append(neutral);uv_points.append((column/angular,t));weights.append([('head',1.0)])
                    for name,values in keys.items():
                        if name==blink:values.append(shut)
                        elif name.startswith(blink+'Arc'):
                            knot=int(name.split('Arc')[1])/ARC_STEPS
                            values.append(neutral+position(knot)-neutral.lerp(shut,knot))
                        else:values.append(neutral.copy())
            for row in range(rows):
                for column in range(angular):
                    a=first+row*angular+column;b=first+row*angular+(column+1)%angular
                    # This order points out of the ellipsoid in Blender axes.
                    faces.append([a,a+angular,b+angular,b]);materials.append(lid_material)
            pole=len(points);pole_point=shell(0,0 if upper else math.pi)
            points.append(pole_point);uv_points.append((.5,0 if upper else 1));weights.append([('head',1.0)])
            for values in keys.values():values.append(pole_point.copy())
            ring=first if upper else first+rows*angular
            for column in range(angular):
                a=ring+column;b=ring+(column+1)%angular
                faces.append([pole,a,b] if upper else [pole,b,a]);materials.append(lid_material)
        ocular.append({'side':blink[-1],'center_blender_m':list(center),'radii_blender_m':list(radii),
                       'white_and_pupil_blink_max_delta_m':0.0,'pupil_front_allowance_m':pupil_depth})
    mesh=bpy.data.meshes.new(old.name+'_volume_lids')
    mesh.from_pydata(points,[],faces)
    for material in old.materials:mesh.materials.append(material)
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:uv.data[loop.index].uv=uv_points[loop.vertex_index]
    for polygon,material in zip(mesh.polygons,materials):polygon.material_index=material;polygon.use_smooth=True
    obj.data=mesh
    for group in sorted({name for influences in weights for name,_ in influences}):
        if obj.vertex_groups.get(group) is None:obj.vertex_groups.new(name=group)
    for i,influences in enumerate(weights):
        for group,weight in influences:obj.vertex_groups[group].add([i],weight,'REPLACE')
    for name,values in keys.items():
        key=obj.shape_key_add(name=name)
        if name in ['GazeX','GazeY']:key.slider_min=-1
        for v,q in zip(key.data,values):v.co=q
    mesh.update()
    obj['eyelid_replacement']=json.dumps({'original_vertices':len(old.vertices),'original_polygons':len(old.polygons),
        'retained_source_vertex_indices':indices,'removed_tubular_lid_polygons':[p.index for p in removed],
        'retained_source_polygon_indices':[p.index for p in kept],
        'new_lid_vertices':len(points)-len(indices),'new_lid_polygons':len(faces)-len(kept),
        'ocular_components':ocular,'angular_intervals':ARC_STEPS,'public_controls_unchanged':True,
        'surface':'Upper/lower ocular-envelope shell; eyeball and pupil volume invariant under Blink',
        'rest_aperture091':{'upper_angle':top_angle,'lower_angle':bottom_angle,'variant':variant}})
    return obj
