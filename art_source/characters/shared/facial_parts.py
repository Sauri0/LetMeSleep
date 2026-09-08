"""Independent facial selections from the chosen original meshes.

No facial geometry is duplicated in a visible avatar: each old combined face is
partitioned into eyes, brows and mouth while retaining its ten morph channels.
"""
import importlib.util, math, json, hashlib
from pathlib import Path
import bpy
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree
from eyelid_geometry import rebuild_eyelids, globe_components

ROOT=Path(__file__).resolve().parents[3]

def _white_faces(mesh):
    """Use the two complete eyeballs, never the smaller moving highlights."""
    return [face for _,faces in globe_components(mesh) for face in faces]

def _eye_anchors(points,faces,indices,normals):
    """Fixed barycentric attachment survives every linear morph combination."""
    triangles=[]
    for face in faces:
        for i in range(1,len(face)-1):triangles.append((face[0],face[i],face[i+1]))
    bvh=BVHTree.FromPolygons(points,triangles,all_triangles=True)
    result={}
    for index in indices:
        hit,normal,triangle,_=bvh.find_nearest(points[index])
        if hit is None:continue
        ids=triangles[triangle]
        weights=geometry.barycentric_transform(hit,*[points[i] for i in ids],Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
        result[index]=(ids,weights,max(-1,min(1,normals[index].dot(normal))))
    return result

def _anchored_eye_point(points,anchor,offset):
    ids,weights,_=anchor
    a,b,c=[points[i] for i in ids]
    normal=(b-a).cross(c-a).normalized()
    return a*weights.x+b*weights.y+c*weights.z+normal*offset

def _subset(obj,name,polygons):
    old=obj.data
    indices=sorted({index for polygon in polygons for index in polygon.vertices})
    remap={old_index:new_index for new_index,old_index in enumerate(indices)}
    mesh=bpy.data.meshes.new(name+'_mesh')
    mesh.from_pydata([old.vertices[i].co.copy() for i in indices],[],
                     [[remap[i] for i in polygon.vertices] for polygon in polygons])
    for material in old.materials:mesh.materials.append(material)
    uv=mesh.uv_layers.new(name='UVMap') if old.uv_layers.active else None
    for new_polygon,old_polygon in zip(mesh.polygons,polygons):
        new_polygon.use_smooth=True
        new_polygon.material_index=old_polygon.material_index
        if uv:
            for target,source in zip(new_polygon.loop_indices,old_polygon.loop_indices):
                uv.data[target].uv=old.uv_layers.active.data[source].uv
    result=obj.copy();result.data=mesh;result.name=name
    bpy.context.collection.objects.link(result)
    groups={group.index:result.vertex_groups.get(group.name) or result.vertex_groups.new(name=group.name) for group in obj.vertex_groups}
    for index,source in enumerate(indices):
        for weight in old.vertices[source].groups:
            groups[weight.group].add([index],weight.weight,'REPLACE')
    for old_key in old.shape_keys.key_blocks:
        key=result.shape_key_add(name=old_key.name)
        key.slider_min=old_key.slider_min;key.slider_max=old_key.slider_max
        for target,source in zip(key.data,indices):target.co=old_key.data[source].co
    result['facial_part']=name.split('_')[1]
    result['source_face']=obj.name
    result['source_vertex_indices']=json.dumps(indices)
    result['source_polygon_indices']=json.dumps([p.index for p in polygons])
    mesh.update()
    return result

def _baseline_channel(obj,name,amount):
    """Add a authored rest expression without changing its full control limit."""
    keys=obj.data.shape_keys.key_blocks
    basis=[p.co.copy() for p in keys['Basis'].data]
    delta=[point.co-base for point,base in zip(keys[name].data,basis)]
    for key in keys:
        if key.name==name:continue
        for vertex,offset in zip(key.data,delta):vertex.co+=offset*amount
    for vertex,basis_vertex in zip(obj.data.vertices,keys['Basis'].data):vertex.co=basis_vertex.co
    obj.data.update()

def _attach_human_feature(obj,category):
    """Carry each endpoint morph along its support, instead of floating forward.

    The eye whites keep their authored vertices. Eyelid skin follows those same
    whites, including their blink endpoints; brows slide on the unchanged head.
    A small embedded thickness preserves a closed, readable strip or eyebrow.
    """
    mesh=obj.data
    base_normals=[v.normal.copy() for v in mesh.vertices]
    original_basis=[p.co.copy() for p in mesh.shape_keys.key_blocks['Basis'].data]
    head=bpy.data.objects['human_head']
    head_bvh=BVHTree.FromPolygons([v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    crease_indices=set()
    if category=='eyes':
        indices={i for p in mesh.polygons if mesh.materials[p.material_index].name in ['skin','skin_shadow'] for i in p.vertices}
        # The inherited lower dark crease crossed the nasal surface after
        # attachment. It is now the actual thin lower eyelid rim, not a painted
        # floating line; CheekLift still raises these same weighted vertices.
        skin_slot=next(i for i,m in enumerate(mesh.materials) if m.name=='skin')
        for polygon in mesh.polygons:
            if mesh.materials[polygon.material_index].name=='skin_shadow':polygon.material_index=skin_slot
        white_polygons=_white_faces(mesh)
    else:indices=set(range(len(mesh.vertices)));white_polygons=[]
    anchors=_eye_anchors(original_basis,white_polygons,indices,base_normals) if white_polygons else {}
    for key in mesh.shape_keys.key_blocks:
        points=[p.co.copy() for p in key.data]
        eye_bvh=BVHTree.FromPolygons(points,white_polygons) if white_polygons else None
        for index in indices:
            point=points[index]
            if category in ['brows','mouth']:
                if category=='brows':
                    base=original_basis[index]
                    point=base+(point-base)*.45
                    point=point.copy();point.x*=.83;point.z-=.013
                origin=Vector((point.x,.5,point.z))
                support,normal,_,_=head_bvh.ray_cast(origin,Vector((0,-1,0)),.8)
                if support is None:continue
                # Geometry is in Blender axes: +Y faces out of the human face.
                material_names={mesh.materials[p.material_index].name for p in mesh.polygons if index in p.vertices} if category=='mouth' else set()
                front=max(-1,min(1,base_normals[index].dot(normal)))
                offset=(.0007+.0015*front if 'ink' in material_names else .001+.0025*front) if category=='mouth' else .0015+.0045*front
                key.data[index].co=support+normal*offset
            else:
                # A thin closed strip entirely outside the white avoids its
                # rear triangles crossing the eyeball at a coplanar rim.
                key.data[index].co=_anchored_eye_point(points,anchors[index],.0018+.0012*anchors[index][2])
        for index in crease_indices:
            support,normal,_,_=head_bvh.ray_cast(Vector((points[index].x,.5,points[index].z)),Vector((0,-1,0)),.8)
            if support is not None:
                key.data[index].co=support+normal*(.0008+.0005*max(-1,min(1,base_normals[index].dot(normal))))
    for vertex,basis in zip(mesh.vertices,mesh.shape_keys.key_blocks['Basis'].data):vertex.co=basis.co
    mesh.update()
    obj['surface_attachment']='eyes' if category=='eyes' else 'forehead' if category=='brows' else 'mouth_skin'

def _attach_mosquito_feature(obj,category):
    """Keep the existing B features on the head/eyes in every morph endpoint."""
    if category=='brows':
        _attach_mosquito_brows(obj)
        return
    mesh=obj.data
    normals=[v.normal.copy() for v in mesh.vertices]
    original_basis=[p.co.copy() for p in mesh.shape_keys.key_blocks['Basis'].data]
    core=bpy.data.objects['mosquito_core']
    head_id=core.vertex_groups['head'].index
    head_indices={v.index for v in core.data.vertices if any(w.group==head_id and w.weight>.99 for w in v.groups)}
    head_faces=[list(p.vertices) for p in core.data.polygons if all(i in head_indices for i in p.vertices)]
    head_bvh=BVHTree.FromPolygons([v.co for v in core.data.vertices],head_faces)
    if category=='eyes':
        indices={i for p in mesh.polygons if mesh.materials[p.material_index].name=='insect_primary' for i in p.vertices}
        whites=_white_faces(mesh)
    else:indices=set(range(len(mesh.vertices)));whites=[]
    anchors=_eye_anchors(original_basis,whites,indices,normals) if whites else {}
    for key in mesh.shape_keys.key_blocks:
        points=[p.co.copy() for p in key.data]
        support_bvh=BVHTree.FromPolygons(points,whites) if whites else head_bvh
        for index in indices:
            point=points[index]
            if category=='eyes':
                key.data[index].co=_anchored_eye_point(points,anchors[index],.0015+.001*anchors[index][2])
                continue
            if category=='brows':
                point=original_basis[index]+(point-original_basis[index])*.4
                point=point.copy();point.x*=.38;point.z+=.006
            elif category=='mouth' and key.name=='MouthSmile':
                point=original_basis[index]+(point-original_basis[index])*.60
            if category=='mouth':
                hit,normal,_,_=head_bvh.ray_cast(Vector((point.x,.4,point.z)),Vector((0,-1,0)),.6)
            else:hit,normal,_,_=support_bvh.find_nearest(point)
            if hit is None:continue
            front=max(-1,min(1,normals[index].dot(normal)))
            # Metres in the authored model; runtime B has scale .35.
            offset=(.0015+.001*front) if category=='eyes' else (.0015+.0025*front) if category=='brows' else (.0007+.0005*front)
            key.data[index].co=hit+normal*offset
    for vertex,basis in zip(mesh.vertices,mesh.shape_keys.key_blocks['Basis'].data):vertex.co=basis.co
    mesh.update();obj['surface_attachment']='eyes' if category=='eyes' else 'head'

def _attach_mosquito_brows(obj):
    """Readable arches ride the upper eye surface, including blink+expression."""
    mesh=obj.data;keys=mesh.shape_keys.key_blocks
    original={key.name:[p.co.copy() for p in key.data] for key in keys}
    normals=[v.normal.copy() for v in mesh.vertices]
    eye=bpy.data.objects['mosquito_eyes_0'].data
    eye_points={key.name:[p.co.copy() for p in key.data] for key in eye.shape_keys.key_blocks}
    whites=[list(p.vertices) for p in eye.polygons if eye.materials[p.material_index].name=='insect_primary']
    # The real upper lid now covers the white at the brow crown. Use that
    # enclosing surface, never bury an eyebrow on the hidden white underneath.
    closed_points=[b+l+r-2*b for b,l,r in zip(eye_points['Basis'],eye_points['BlinkL'],eye_points['BlinkR'])]
    # The authoring seed is kept across the full width of the original brow;
    # projecting from slightly forward places it on the visible upper crown.
    bindings={}
    for control in ['Basis','BrowUp','BrowDown']:
        seeds=[]
        for base,point in zip(original['Basis'],original[control]):
            q=base+(point-base)*.65;q=q.copy();q.y+=.025
            seeds.append(q)
        triangles=[]
        for face in whites:
            for i in range(1,len(face)-1):triangles.append((face[0],face[i],face[i+1]))
        bvh=BVHTree.FromPolygons(closed_points,triangles,all_triangles=True)
        anchors=[]
        for index,q in enumerate(seeds):
            hit,normal,triangle,_=bvh.find_nearest(q);ids=triangles[triangle]
            weights=geometry.barycentric_transform(hit,*[closed_points[i] for i in ids],Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
            anchors.append((ids,weights,max(-1,min(1,normals[index].dot(normal)))))
        bindings[control]=anchors
    def evaluated(control,blink='Basis'):
        return [_anchored_eye_point(closed_points,anchor,.0018+.0009*anchor[2]) for anchor in bindings[control]]
    basis=evaluated('Basis')
    posed={control:evaluated(control) for control in ['BrowUp','BrowDown']}
    blinks={blink:evaluated('Basis',blink) for blink in ['BlinkL','BlinkR']}
    for key in list(keys):
        points=posed.get(key.name,blinks.get(key.name,basis))
        for target,point in zip(key.data,points):target.co=point
    for control in ['BrowUp','BrowDown']:
        for blink in ['BlinkL','BlinkR']:
            key=obj.shape_key_add(name=control+blink)
            combined=evaluated(control,blink)
            for index,target in enumerate(key.data):
                target.co=basis[index]+combined[index]-posed[control][index]-blinks[blink][index]+basis[index]
    # Opposing full controls cancel to the neutral arch. This corrective keeps
    # their sum from sinking a curved support chord into the eye surface.
    opposing=obj.shape_key_add(name='BrowUpBrowDown')
    for i,target in enumerate(opposing.data):target.co=3*basis[i]-posed['BrowUp'][i]-posed['BrowDown'][i]
    for blink in ['BlinkL','BlinkR']:
        up=evaluated('BrowUp',blink);down=evaluated('BrowDown',blink)
        key=obj.shape_key_add(name='BrowUpBrowDown'+blink)
        for i,target in enumerate(key.data):target.co=2*blinks[blink][i]-up[i]-down[i]-basis[i]+posed['BrowUp'][i]+posed['BrowDown'][i]
    for vertex,point in zip(mesh.vertices,basis):vertex.co=point
    mesh.update();obj['surface_attachment']='outer_eyelid_crown_barycentric_with_expression_correctives'

def separate_faces(species):
    created=[]
    for variant in range(3):
        obj=bpy.data.objects[species+'_face_'+str(variant)]
        rebuild_eyelids(obj,species,variant)
        groups={'eyes':[],'brows':[],'mouth':[]}
        for polygon in obj.data.polygons:
            material=obj.data.materials[polygon.material_index].name
            y=sum(obj.data.vertices[i].co.z for i in polygon.vertices)/len(polygon.vertices)
            if species=='human':
                group='brows' if material=='hair' else 'mouth' if material in ['lip','ink'] else 'eyes'
            else:
                group='mouth' if material=='ink' or (material=='insect_dark' and y<0) else 'brows' if material=='insect_dark' else 'eyes'
            groups[group].append(polygon)
        for category,polygons in groups.items():
            assert polygons,(species,category,variant)
            part=_subset(obj,'%s_%s_%d'%(species,category,variant),polygons)
            # The former complete human faces 0/2 shared identical eyes and 0/1
            # shared brows. Independent selectors now need visible differences.
            if species=='human' and category=='brows' and variant==1:
                _baseline_channel(part,'BrowUp',.28)
            if species=='human' and category in ['brows','mouth']:
                _attach_human_feature(part,category)
            if species=='mosquito' and category!='eyes':_attach_mosquito_feature(part,category)
            if category=='eyes':part['surface_attachment']='volume_preserving_curved_upper_lower_eyelids'
            created.append(part)
        assigned={p.index:category for category,polygons in groups.items() for p in polygons}
        assert len(assigned)==len(obj.data.polygons),'Facial partition omitted or duplicated a polygon'
        vertex_categories={}
        for p in obj.data.polygons:
            for index in p.vertices:vertex_categories.setdefault(index,set()).add(assigned[p.index])
        assert all(len(categories)==1 for categories in vertex_categories.values()),'A connected facial component was cut between categories'
        for category,polygons in groups.items():
            part=bpy.data.objects['%s_%s_%d'%(species,category,variant)]
            part['partition_proof']=json.dumps({'source_polygons':len(obj.data.polygons),'assigned_polygons':len(polygons),
                'source_vertices':len(obj.data.vertices),'no_component_split':True,'no_polygon_omitted_or_duplicated':True,
                'source_polygon_sha256':hashlib.sha256(json.dumps([list(p.vertices) for p in obj.data.polygons]).encode()).hexdigest()})
        bpy.data.objects.remove(obj,do_unlink=True)
    return created

def add_human_facial_hair(rig):
    spec=importlib.util.spec_from_file_location('lms_hair_geometry',ROOT/'art_source/export_presets/characters_pipeline.py')
    source=importlib.util.module_from_spec(spec);spec.loader.exec_module(source)
    source.RIG=rig;source.MATS={material.name:material for material in bpy.data.materials}
    created=[]
    head=bpy.data.objects['human_head']
    bvh=BVHTree.FromPolygons([v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    def support(x,y,offset):
        hit,normal,_,_=bvh.ray_cast(source.g((x,y,-.35)),Vector((0,-1,0)),.6)
        # The lower jaw narrows into the neck. Keep a band endpoint on actual
        # skin instead of extending its rectangular authoring grid into air.
        for _ in range(16):
            if hit is not None:break
            x*=.97
            hit,normal,_,_=bvh.ray_cast(source.g((x,y,-.35)),Vector((0,-1,0)),.6)
        if hit is None:return None
        point=hit+normal*offset
        return Vector((point.x,point.z,-point.y))
    for style in [1,2]:
        mesh=source.Mesh('human_mustache_'+str(style))
        for sign in [-1,1]:
            path=[(sign*.007,1.480,-.165),(sign*.031,1.478,-.162),(sign*.061,1.466,-.145)]
            if style==2:
                path[-1]=(sign*.074,1.477,-.145)
                path.extend([(sign*.092,1.448,-.134),(sign*.090,1.426,-.127)])
            points=source.curve(path,4)
            radii=[(.005+.009*math.sin(math.pi*i/(len(points)-1)))*(1-.42*i/(len(points)-1)) for i in range(len(points))]
            radii[0]=.006;radii[-1]=.002
            mesh.tube(points,radii,'hair','head',10)
        obj=mesh.finish()
        normals=[v.normal.copy() for v in obj.data.vertices]
        for vertex,normal in zip(obj.data.vertices,normals):
            x=vertex.co.x;y=vertex.co.z
            # Preserve the drooping tails outside the lips; across the mouth
            # the lower moustache boundary stays above the smile envelope.
            if abs(x)<.075:
                y=max(y,1.484+.003*(1-abs(x)/.075))
            point=support(x,y,.002+.007*normal.y)
            if point is not None:vertex.co=source.g(point)
        obj.data.update();obj['facial_hair']='mustache';created.append(obj)
    for style in [1,2]:
        mesh=source.Mesh('human_beard_'+str(style))
        if style==1:
            segments=32;rings=7
            mesh.vertex(support(0,1.393,.007),(0.5,0.5),{'head':1})
            for ring in range(1,rings+1):
                r=ring/rings
                for segment in range(segments):
                    a=2*math.pi*segment/segments
                    x=.028*r*math.cos(a);y=1.393+.029*r*math.sin(a)
                    mesh.vertex(support(x,y,.003+.004*(1-r*r)),(.5+x*10,.5+(y-1.393)*10),{'head':1})
            for segment in range(segments):mesh.face((0,1+(segment+1)%segments,1+segment),'hair')
            for ring in range(rings-1):
                for segment in range(segments):
                    a=1+ring*segments+segment;b=1+ring*segments+(segment+1)%segments
                    mesh.face((a,a+segments,b+segments,b),'hair')
        else:
            # Keep the chin part on the visible chin, above the narrow neck.
            # The previous low ribbon dipped under the jaw and read as two
            # side patches; its clamped lower rows made a stepped silhouette.
            path=source.curve([(-.105,1.468,0),(-.101,1.434,0),(-.067,1.405,0),(0,1.400,0),(.067,1.405,0),(.101,1.434,0),(.105,1.468,0)],14)
            columns=16
            for row,center in enumerate(path):
                tangent=(path[min(row+1,len(path)-1)]-path[max(0,row-1)]).normalized()
                across=Vector((-tangent.y,tangent.x,0))
                phase=row/(len(path)-1)
                width=.003+.018*math.sin(math.pi*phase)**.45
                for column in range(columns+1):
                    u=-1+2*column/columns
                    q=center+across*(width*u)
                    mesh.vertex(support(q.x,q.y,.003+.004*max(0,1-u*u)),(phase,(u+1)/2),{'head':1})
            for row in range(len(path)-1):
                for column in range(columns):
                    a=row*(columns+1)+column;b=a+columns+1
                    mesh.face((a,a+1,b+1,b),'hair')
        obj=mesh.finish();obj['facial_hair']='beard';created.append(obj)
    for obj in created:
        obj['lms_original_authoring']='facial_parts.py'
        for polygon in obj.data.polygons:polygon.use_smooth=True
        obj.data.update()
        neighbors=[set() for _ in obj.data.vertices]
        for edge in obj.data.edges:
            a,b=edge.vertices;neighbors[a].add(b);neighbors[b].add(a)
        shading=[v.normal.copy() for v in obj.data.vertices]
        for _ in range(6):
            following=[]
            for index,normal in enumerate(shading):
                nearby=[shading[j] for j in neighbors[index] if normal.dot(shading[j])>.45]
                mean=sum(nearby,normal.copy())/(len(nearby)+1)
                following.append(normal.lerp(mean,.55).normalized())
            shading=following
        obj.data.normals_split_custom_set_from_vertices(shading)
    return created

def weld_human_hair_roots(rig):
    """Union the overlapping root shells; retain each chosen hair silhouette."""
    previous=rig.data.pose_position;rig.data.pose_position='REST'
    for style in range(3):
        obj=bpy.data.objects['human_hair_'+str(style)]
        original=[v.co.copy() for v in obj.data.vertices]
        original_bvh=BVHTree.FromPolygons(original,[list(p.vertices) for p in obj.data.polygons])
        old_tris=sum(len(p.vertices)-2 for p in obj.data.polygons)
        assert all(all(obj.vertex_groups[w.group].name=='head' for w in v.groups if w.weight>0) for v in obj.data.vertices)
        bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj
        modifier=obj.modifiers.new('Connected hair roots','REMESH')
        modifier.mode='VOXEL';modifier.voxel_size=.0018;modifier.adaptivity=.15;modifier.use_smooth_shade=True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        triangle_count=sum(len(p.vertices)-2 for p in obj.data.polygons)
        if triangle_count>6000:
            reduce=obj.modifiers.new('Root union surface budget','DECIMATE')
            reduce.ratio=6000.0/triangle_count;reduce.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=reduce.name)
        for polygon in obj.data.polygons:polygon.use_smooth=True
        group=obj.vertex_groups.get('head') or obj.vertex_groups.new(name='head')
        group.add(list(range(len(obj.data.vertices))),1.0,'REPLACE')
        new_tris=sum(len(p.vertices)-2 for p in obj.data.polygons)
        maximum=max(original_bvh.find_nearest(v.co)[3] for v in obj.data.vertices)
        before_min=[min(v[i] for v in original) for i in range(3)];before_max=[max(v[i] for v in original) for i in range(3)]
        after_min=[min(v.co[i] for v in obj.data.vertices) for i in range(3)];after_max=[max(v.co[i] for v in obj.data.vertices) for i in range(3)]
        bounds_delta=max(abs(a-b) for a,b in zip(before_min+before_max,after_min+after_max))
        # The union fills narrow channels between formerly intersecting roots;
        # new vertices there need not remain within one voxel of either shell.
        # The exterior axis extrema have a stricter three-millimetre bound.
        assert bounds_delta<.003 and maximum<.012,(obj.name,bounds_delta,maximum)
        shading_report={'applied':False,'position_delta_m':0.0}
        if style==0:
            # The previous normal pass was attached to facial hair by mistake.
            # Apply it to the confirmed H0 crown itself, with a strict 12 degree
            # cap and no changed position, topology, silhouette or root weight.
            obj.data.update()
            neighbors=[set() for _ in obj.data.vertices]
            for edge in obj.data.edges:
                a,b=edge.vertices;neighbors[a].add(b);neighbors[b].add(a)
            initial=[v.normal.copy() for v in obj.data.vertices];shading=[n.copy() for n in initial]
            for _ in range(6):
                following=[]
                for i,normal in enumerate(shading):
                    nearby=[shading[j] for j in neighbors[i] if normal.dot(shading[j])>.45]
                    mean=(sum(nearby,normal.copy())/(len(nearby)+1)).normalized()
                    weight=max(0,min(1,(obj.data.vertices[i].co.z-1.70)/.035))
                    proposed=normal.lerp(mean,.55*weight).normalized()
                    angle=initial[i].angle(proposed)
                    if angle>math.radians(12):proposed=initial[i].slerp(proposed,math.radians(12)/angle).normalized()
                    following.append(proposed)
                shading=following
            obj.data.normals_split_custom_set_from_vertices(shading)
            shading_report={'applied':True,'position_delta_m':0.0,'iterations':6,
                'max_normal_angle_degrees':max(math.degrees(a.angle(b)) for a,b in zip(initial,shading)),
                'region_blender_z_m':[1.70,1.735]}
        obj['root_union']=json.dumps({'old_triangles':old_tris,'new_triangles':new_tris,'voxel_m':.0018,
            'maximum_new_vertex_distance_to_old_surface_m':maximum,'maximum_bounds_delta_m':bounds_delta,
            'weights':'head=1; no new bones','capped_variants_untouched':True,'custom_smooth_normals':shading_report})
    rig.data.pose_position=previous
