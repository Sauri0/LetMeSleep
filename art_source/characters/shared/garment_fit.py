"""Local selected-A garment fitting; no head/hand/bone/collision changes."""
import json
import math
import bpy
from mathutils import Vector


def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)


def repair_human_garments():
    records=[]
    for obj in bpy.data.objects:
        if obj.type!='MESH' or not obj.name.startswith('human_outfit_'):
            continue
        if obj.get('garment_fit09'):
            records.append(json.loads(obj['garment_fit09']))
            continue
        mesh=obj.data
        assert mesh.shape_keys is None
        original=[v.co.copy() for v in mesh.vertices]
        groups={group.index:group.name for group in obj.vertex_groups}
        old_weights=[{groups[g.group]:g.weight for g in v.groups} for v in mesh.vertices]
        replacements=[]
        moved=[]
        for vertex,q0,weights in zip(mesh.vertices,original,old_weights):
            q=Vector((q0.x,q0.z,-q0.y))
            radial=math.hypot(q.x,q.z)
            # The former domed shirt cap rose to 1.425m and the trim to1.395.
            # Lower only its central neckline into the real neck stem. Outer
            # shoulders, lower chest, sleeves, cuffs and all body dimensions
            # stay outside this smooth region.
            # Includes 8mm local allowance for the measured high-breath crouch
            # witnesses 54/63/72; no change to the external shoulder contour.
            amount=.133*(1-smooth(.10,.25,radial))*smooth(1.18,1.31,q.y)
            if not obj.name.endswith('_trim'):
                # The old spherical sleeve cap extended 79mm above the arm's
                # origin and became a tall peak when that bone stretched.
                # Tailor its upper half; keep the sleeve wall and cuff intact.
                amount+=.036*smooth(.17,.27,abs(q.x))*smooth(1.27,1.375,q.y)
            if amount>0:
                vertex.co.z-=amount
                moved.append(amount)
            current=dict(weights)
            if not obj.name.endswith('_trim') and q.y>1.03 and abs(q.x)>.10:
                # Replace the old hard y1.17/z-.17 decision with one continuous
                # field across the entire shoulder seam, including its front.
                side='l' if q.x<0 else 'r'
                influence=sum(weights.get(name,0) for name in ['torso','upperarm_'+side])
                if influence>.95:
                    blend=smooth(.13,.30,abs(q.x))
                    strength=smooth(1.03,1.16,q.y)
                    arm=(1-strength)*weights.get('upperarm_'+side,0)+strength*blend
                    current={'torso':1-arm,'upperarm_'+side:arm}
            replacements.append(current)
        if not obj.name.endswith('_trim'):
            # Smooth neighboring bind weights, preserving the field's pinned
            # torso/sleeve boundaries and avoiding discontinuities at clipping
            # boundaries of the old nearest-neighbour transfer.
            neighbors=[set() for _ in mesh.vertices]
            for edge in mesh.edges:
                a,b=edge.vertices;neighbors[a].add(b);neighbors[b].add(a)
            positions=[vertex.co.copy() for vertex in mesh.vertices]
            tailored=[point.copy() for point in positions]
            for _ in range(8):
                result=[]
                for i,(q0,point) in enumerate(zip(original,positions)):
                    q=Vector((q0.x,q0.z,-q0.y))
                    strength=smooth(1.10,1.23,q.y)*(1-smooth(.33,.39,abs(q.x)))*.45
                    if strength>0 and neighbors[i]:
                        average=sum((positions[j] for j in neighbors[i]),Vector())/len(neighbors[i])
                        candidate=point.lerp(average,strength)
                        delta=candidate-tailored[i]
                        if delta.length>.020:
                            candidate=tailored[i]+delta.normalized()*.020
                        result.append(candidate)
                    else:
                        result.append(point.copy())
                positions=result
            for vertex,point in zip(mesh.vertices,positions):
                vertex.co=point
            for _ in range(3):
                updated=[]
                for i,(q0,current) in enumerate(zip(original,replacements)):
                    q=Vector((q0.x,q0.z,-q0.y))
                    strength=smooth(1.03,1.17,q.y)*smooth(.10,.15,abs(q.x))*(1-smooth(.30,.36,abs(q.x)))*.35
                    if strength<=0 or not neighbors[i]:
                        updated.append(current);continue
                    mean={}
                    for j in neighbors[i]:
                        for key,weight in replacements[j].items():
                            mean[key]=mean.get(key,0)+weight/len(neighbors[i])
                    names=set(current)|set(mean)
                    updated.append({name:(1-strength)*current.get(name,0)+strength*mean.get(name,0) for name in names})
                replacements=updated
        changed=0
        for vertex,before,weights in zip(mesh.vertices,old_weights,replacements):
            weights={k:v for k,v in weights.items() if v>1e-8}
            total=sum(weights.values());weights={k:v/total for k,v in weights.items()}
            if any(abs(before.get(k,0)-weights.get(k,0))>1e-7 for k in set(before)|set(weights)):
                changed+=1
                for group in list(vertex.groups):
                    obj.vertex_groups[group.group].remove([vertex.index])
                for name,value in weights.items():
                    if name not in obj.vertex_groups:obj.vertex_groups.new(name=name)
                    obj.vertex_groups[name].add([vertex.index],value,'REPLACE')
        uv_adjusted=0
        if not obj.name.endswith('_trim'):
            # Keep the authored cylindrical cloth field on the tailored
            # surface. Leaving UVs at their pre-relaxation positions makes
            # straight stripe isolines zigzag across the moved triangles.
            # Apply only the displacement delta, retaining the original seams
            # and untouched lower body/sleeve coordinates.
            uv=mesh.uv_layers.active
            assert uv is not None
            for loop in mesh.loops:
                index=loop.vertex_index
                before=original[index]
                point=mesh.vertices[index].co
                delta=math.atan2(point.y,point.x)-math.atan2(before.y,before.x)
                delta=(delta+math.pi)%(2*math.pi)-math.pi
                dv=(point.z-before.z)*.8
                if abs(delta)>1e-10 or abs(dv)>1e-10:
                    uv.data[loop.index].uv.x+=delta/(2*math.pi)
                    uv.data[loop.index].uv.y+=dv
                    uv_adjusted+=1
        mesh.update()
        for polygon in mesh.polygons:polygon.use_smooth=True
        record={'mesh':obj.name,'moved_vertices':len(moved),'max_neckline_lowering_m':max(moved,default=0),
                'reweighted_vertices':changed,'vertices':len(mesh.vertices),'topology_unchanged':True,
                'method':'Tailored neckline and sleeve-cap height; bounded 20mm upper-garment relaxation; continuous shoulder binding field',
                'upper_surface_relaxation_iterations':8 if not obj.name.endswith('_trim') else 0,
                'upper_surface_relaxation_limit_m':.020 if not obj.name.endswith('_trim') else 0,
                'cloth_uv_loops_reparameterized':uv_adjusted}
        obj['garment_fit09']=json.dumps(record)
        records.append(record)
    return records
