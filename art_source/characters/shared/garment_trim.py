"""Conform only the authored central piping to the selected-A shirt surface."""
import json
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def central_component(obj):
    mesh=obj.data
    eligible=set()
    neighbors={}
    for face in mesh.polygons:
        if obj.material_slots[face.material_index].material.name!='secondary':continue
        ids=list(face.vertices);eligible.update(ids)
        for a,b in zip(ids,ids[1:]+ids[:1]):
            neighbors.setdefault(a,set()).add(b);neighbors.setdefault(b,set()).add(a)
    components=[]
    while eligible:
        stack=[eligible.pop()];group=[]
        while stack:
            i=stack.pop();group.append(i)
            for j in neighbors.get(i,()):
                if j in eligible:eligible.remove(j);stack.append(j)
        points=[mesh.vertices[i].co for i in group]
        if max(p.x for p in points)-min(p.x for p in points)<.020 and max(p.z for p in points)-min(p.z for p in points)>.30:
            components.append(sorted(group))
    assert len(components)==1,(obj.name,'central piping component',len(components))
    return components[0]


def barycentric(p,a,b,c):
    v0=b-a;v1=c-a;v2=p-a
    d00=v0.dot(v0);d01=v0.dot(v1);d11=v1.dot(v1)
    d20=v2.dot(v0);d21=v2.dot(v1)
    denominator=d00*d11-d01*d01
    assert abs(denominator)>1e-16
    v=(d11*d20-d01*d21)/denominator
    w=(d00*d21-d01*d20)/denominator
    return (1-v-w,v,w)


def conform_human_central_trim():
    records=[]
    for style in range(3):
        trim=bpy.data.objects['human_outfit_%d_trim'%style]
        if trim.get('garment_trim09'):
            records.append(json.loads(trim['garment_trim09']));continue
        cloth=bpy.data.objects['human_outfit_%d'%style]
        component=central_component(trim)
        original_component_size=len(component)
        # A coarse longitudinal chord can cut through the curved shirt even
        # when every old ring vertex is supported. Refine this component only.
        bm=bmesh.new();bm.from_mesh(trim.data);bm.verts.ensure_lookup_table()
        selected=set(component)
        edges=[edge for edge in bm.edges if all(v.index in selected for v in edge.verts)]
        bmesh.ops.subdivide_edges(bm,edges=edges,cuts=2,use_grid_fill=True)
        bm.normal_update();bm.to_mesh(trim.data);bm.free();trim.data.update()
        component=central_component(trim)
        cloth.data.calc_loop_triangles()
        space=trim.matrix_world.inverted()@cloth.matrix_world
        points=[space@v.co for v in cloth.data.vertices]
        faces=[tuple(t.vertices) for t in cloth.data.loop_triangles]
        tree=BVHTree.FromPolygons(points,faces,all_triangles=True)
        old={v.index:(v.co.copy(),v.normal.copy()) for v in trim.data.vertices}
        moved=[];bindings=[]
        names={g.index:g.name for g in cloth.vertex_groups}
        for index in component:
            vertex=trim.data.vertices[index]
            position,normal=old[index]
            p=position.copy()
            hit,_,triangle,_=tree.ray_cast(Vector((p.x,2.0,p.z)),Vector((0,-1,0)),4.0)
            # The tapered top may extend a few mm beyond the lowered neckline.
            # End it on the actual shirt, never support it on empty space.
            lowering=0.0
            while hit is None and lowering<.020:
                lowering+=.0005;p.z=position.z-lowering
                hit,_,triangle,_=tree.ray_cast(Vector((p.x,2.0,p.z)),Vector((0,-1,0)),4.0)
            assert hit is not None,(trim.name,index,'no chest support within20mm')
            # Preserve a closed, shallow round cross-section. Rear roots embed
            # at most0.2mm; the exposed ridge rises at most1.8mm. No flat overlay.
            relief=.0008+.001*max(-1.0,min(1.0,normal.y))
            p.y=hit.y+relief
            vertex.co=p
            ids=faces[triangle]
            bary=barycentric(hit,*[points[i] for i in ids])
            weights={}
            for source,factor in zip(ids,bary):
                for group in cloth.data.vertices[source].groups:
                    name=names[group.group]
                    weights[name]=weights.get(name,0.0)+factor*group.weight
            weights={k:max(0,v) for k,v in weights.items() if v>1e-8}
            total=sum(weights.values());assert total>.99
            for group in list(vertex.groups):trim.vertex_groups[group.group].remove([index])
            for name,value in weights.items():
                if name not in trim.vertex_groups:trim.vertex_groups.new(name=name)
                trim.vertex_groups[name].add([index],value/total,'REPLACE')
            moved.append((p-position).length)
            bindings.append({'vertex':index,'cloth_triangle':list(ids),'barycentric':list(bary),
                             'surface':list(hit),'relief_m':relief,'top_lowering_m':lowering})
        trim.data.update()
        assert all(trim.data.vertices[i].co==point for i,(point,_) in old.items() if i not in component)
        record={'mesh':trim.name,'material':'secondary','source_component_vertices':len(component),
                'original_component_vertices':original_component_size,'subdivision_cuts':2,
                'vertices':component,'maximum_displacement_m':max(moved),'relief_range_m':[-.0002,.0018],
                'method':'Actual shirt triangle projection plus barycentric shared skin weights; shallow closed piping',
                'bindings':bindings,'buttons_cuffs_collar_unchanged':True,'main_garment_unchanged':True}
        trim['garment_trim09']=json.dumps(record)
        records.append(record)
    return records
