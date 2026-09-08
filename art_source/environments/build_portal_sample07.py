"""A real, reusable kitchen portal slice. Metres, hinge origin, original textures.

Blender localX follows the closed leaf, +Y points into kitchen, Z is vertical.
World placement uses DoorCatalog.kitchen closed transform, identical to DoorView.
"""
from pathlib import Path
import bpy, math, sys, json, hashlib
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_house as H
import build_house07 as D

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'game/assets/art/house/samples'
SOURCE=Path(__file__).resolve().parent/'samples'
OUT.mkdir(parents=True,exist_ok=True);SOURCE.mkdir(parents=True,exist_ok=True)

def texture(name,wall,detailed):
    width=256;height=768 if wall else 256
    image=bpy.data.images.new(name,width=width,height=height,alpha=False)
    pixels=[]
    for j in range(height):
        z=j/height*(3.0 if wall else 1.0)
        for i in range(width):
            u=i/width
            tile=not wall or z<1.30
            base=(.69,.76,.72) if wall and tile else (.64,.73,.76) if wall else (.72,.64,.51)
            grout=tile and (min((u*4)%1,1-(u*4)%1)<.009 or min((z*4)%1,1-(z*4)%1)<.009)
            tint=-.17 if grout else 0.0
            if detailed and not grout:
                # Low frequency brush variation, not a noisy photoreal texture.
                tint+=.010*math.sin(u*12+z*3)+.006*math.sin(z*27+u*5)
                tint+=.004*math.sin(i*.39+j*.16)
            pixels.extend([max(0,min(1,c+tint)) for c in base]+[1.0])
    image.pixels.foreach_set(pixels);image.update()
    image.filepath_raw=str(OUT/(name+'.png'));image.file_format='PNG';image.save()
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.82
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;node.interpolation='Linear'
    mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
    return mat

def join(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return obj

def capture(builder,name,scale_x=1.0):
    before=set(bpy.context.scene.objects);builder()
    objects=[o for o in bpy.context.scene.objects if o not in before and o.type=='MESH']
    result=join(objects,name);result.scale.x=scale_x
    bpy.context.view_layer.objects.active=result;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return result

def planar_uv(obj,wall=False):
    if not obj.data.uv_layers:obj.data.uv_layers.new(name='Metres')
    layer=obj.data.uv_layers.active.data
    for poly in obj.data.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a]))
        for loop_id in poly.loop_indices:
            v=obj.matrix_world@obj.data.vertices[obj.data.loops[loop_id].vertex_index].co
            uv=(v.x,v.y) if axis==2 else ((v.y,v.z/3.0 if wall else v.z) if axis==0 else (v.x,v.z/3.0 if wall else v.z))
            layer[loop_id].uv=uv

def trim_segment(a,b,z,scale_y=1.0):
    p=Vector(a);q=Vector(b);length=(q-p).length
    obj=capture(D.molding,'Profile')
    obj.scale=(length,1,scale_y)
    obj.rotation_euler.z=math.atan2(q.y-p.y,q.x-p.x)
    obj.location=(p.x,p.y,z)
    return obj

def build(detailed):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    H.M=H.materials()
    finish='sutil' if detailed else 'liso'
    wall_mat=texture('portal_'+finish+'_pared',True,detailed)
    floor_mat=texture('portal_'+finish+'_suelo',False,detailed)
    floor=H.box('Floor',( .9,.33,-.10),(4.6,2.66,.20),'wood',0)
    floor.data.materials.clear();floor.data.materials.append(floor_mat);planar_uv(floor)
    ceiling=H.box('Ceiling',(.9,.33,3.0945),(4.6,2.66,.211),'cream',0)
    ceiling.data.materials[0]=H.M['cream']
    # Exact kitchen wall AABBs, clipped to this reusable slice; the header is
    # structural. Boolean union removes interior/coplanar corner faces.
    walls=[H.box('Wall',(-.60,-.14,1.5),(1.2,.20,3.0),'blue',0),
           H.box('Wall',(2.7,-.14,1.5),(1.0,.20,3.0),'blue',0),
           H.box('Header',(1.1,-.14,2.725),(2.2,.20,.55),'blue',0),
           H.box('Return',(-1.3,.71,1.5),(.20,1.9,3.0),'blue',0)]
    wall=walls[0]
    for other in walls[1:]:
        bpy.context.view_layer.objects.active=wall
        modifier=wall.modifiers.new('Joined wall construction','BOOLEAN');modifier.operation='UNION';modifier.solver='EXACT';modifier.object=other
        bpy.ops.object.modifier_apply(modifier=modifier.name);bpy.data.objects.remove(other,do_unlink=True)
    wall.name='Walls';wall.data.materials.clear();wall.data.materials.append(wall_mat)
    for face in wall.data.polygons:face.material_index=0;face.use_smooth=False
    planar_uv(wall,True)
    frame=capture(D.door_frame,'Frame',2.2)
    leaf=capture(D.door_leaf,'Leaf',2.2)
    hinge=bpy.data.objects.new('DoorHinge',None);bpy.context.collection.objects.link(hinge);leaf.parent=hinge
    hinge['door_id']='kitchen';hinge['axis']='Z in Blender / Y in Godot';hinge['width_m']=2.2;hinge['gap_m']=.14
    hinge.rotation_euler.z=0;hinge.keyframe_insert(data_path='rotation_euler',frame=1)
    hinge.rotation_euler.z=math.pi/2;hinge.keyframe_insert(data_path='rotation_euler',frame=61)
    hinge.keyframe_insert(data_path='rotation_euler',frame=91)
    hinge.rotation_euler.z=0;hinge.keyframe_insert(data_path='rotation_euler',frame=151)
    trims=[]
    for z,scale in [(0,1.0),(2.911,.6)]:
        if z==0:
            for a,b in [((-1.2,-.038),(0,-.038)),((2.2,-.038),(3.2,-.038)),((0,-.242),(-1.2,-.242)),((3.2,-.242),(2.2,-.242))]:trims.append(trim_segment(a,b,z,scale))
        else:
            trims.append(trim_segment((-1.2,-.038),(3.2,-.038),z,scale));trims.append(trim_segment((3.2,-.242),(-1.2,-.242),z,scale))
        trims.append(trim_segment((-1.198,1.66),(-1.198,-.04),z,scale))
    trim=join(trims,'Trim')
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1.0
    scene.frame_start=1;scene.frame_end=151;scene.render.fps=30;scene.frame_set(1)
    # Two exports have identical geometry and only differ in the two original
    # painted texture maps. All six pieces retain meaningful object names.
    objects=[floor,ceiling,wall,frame,leaf,trim,hinge]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=floor
    for obj in objects:
        if obj.type=='MESH':
            obj.data.calc_loop_triangles()
            if obj not in [floor,wall]:planar_uv(obj)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/('portal_'+finish+'.blend')),compress=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/('portal_'+finish+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_animations=True,export_frame_range=True,export_cameras=False,export_lights=False)
    meshes=[o for o in objects if o.type=='MESH']
    topology=hashlib.sha256()
    for obj in meshes:
        for v in obj.data.vertices:topology.update(('%0.6f,%0.6f,%0.6f'%tuple(obj.matrix_world@v.co)).encode())
    return {'finish':finish,'meshes':len(meshes),'triangles':sum(len(o.data.loop_triangles) for o in meshes),'materials':sorted({m.name for o in meshes for m in o.data.materials}),
            'geometry_sha256':topology.hexdigest(),'bytes':(OUT/('portal_'+finish+'.glb')).stat().st_size,'door_width_m':2.2,'door_top_m':2.45,'door_gap_m':.14,'wall_thickness_m':.20,'ceiling_visible_m':2.989,'floor_size_m':[4.6,2.66],
            'world_hinge_godot':[-2.14,0,-7.1],'world_closed_yaw_radians':math.pi/2,'pivot_blender':[0,0,0],'animation':'0 to90deg, closed-open-closed,151frames@30fps'}

if __name__=='__main__':
    results=[build(False),build(True)]
    (SOURCE/'portal_manifest.json').write_text(json.dumps({'original_models_and_textures':True,'source':'MapCatalog kitchen portal + DoorCatalog.kitchen + build_house07 door carpentry','options':results},indent=2),encoding='utf-8')
    print('PORTAL_SAMPLE_EXPORT',json.dumps(results))
