"""Original handheld newspaper/slipper. Canonical +Y, metres, real grip offsets.
Run Blender --background --python this_file. Does not edit character cosmetics.
"""
import importlib.util, json, math
from pathlib import Path
import bpy, bmesh
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('character_builder',ROOT/'art_source/export_presets/characters_pipeline.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
bpy.context.preferences.filepaths.save_version=0

def colour(name,hex_value,roughness=.8):
    material=b.mat(name,hex_value,roughness)
    rgb=[int(hex_value[i:i+2],16)/255 for i in (0,2,4)]
    linear=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]+[1]
    material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=linear
    material.diffuse_color=linear

def finish(mesh,name,grip,contact):
    obj=mesh.finish()
    for modifier in list(obj.modifiers):obj.modifiers.remove(modifier)
    for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
    obj.parent=None
    obj['lms_original_authoring']='art_source/characters/tools/generate_throwables.py'
    if 'lms_rig' in obj:del obj['lms_rig']
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(obj.data);bm.free()
    source=ROOT/'art_source/characters/tools';out=ROOT/'game/assets/art/characters/tools'
    out.mkdir(parents=True,exist_ok=True)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(name+'.blend')))
    bpy.ops.export_scene.gltf(filepath=str(out/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_skins=False)
    obj.data.calc_loop_triangles()
    (source/(name+'.json')).write_text(json.dumps({'original':True,'units':'metres','shaft':'+Y','normal':'+Z','grip':grip,'contact_relative_grip':contact,'source':name+'.blend','triangles':len(obj.data.loop_triangles),'materials':len(obj.data.materials),'distinct_from_foot_cosmetic':name=='slipper'},indent=2)+'\n')
    print('LMS07_THROWABLE',name,len(obj.data.loop_triangles),'triangles',len(obj.data.materials),'materials')

b.reset('newspaper');colour('tool_paper','E8DFBD');colour('tool_ink','3A5660');colour('tool_edge','B9A887')
m=b.Mesh('newspaper')
length=.31875;radius=.046875;centres=[];widths=[]
for i in range(49):
    y=length*i/48
    distance=max(radius-y,y-(length-radius),0)
    r=max(.0005,math.sqrt(max(0,radius*radius-distance*distance)))
    centres.append((0,y,0));widths.append((r,r))
m.loft(centres,widths,'tool_paper',segments=32)
# Coarse printed columns on the actual surface, visible from either side.
# Material regions add no triangles or collision extent and avoid tiny text.
for j in range(48):
    for i in range(32):
        column=i//8;within=i%8
        headline=13<=j<=14 and 1<=within<=6
        line=j in [18,21,24,28,31,34,37] and 1<=within<=(6 if (j+column)%3 else 4)
        photo=26<=j<=29 and column in [0,2] and 1<=within<=4
        rolled_edge=j in [43,46] or (j==3)
        if headline or line or photo:m.mi[j*32+i]=m.material('tool_ink')
        elif rolled_edge:m.mi[j*32+i]=m.material('tool_edge')
finish(m,'newspaper',[0,.06,0],[0,.24,0])

b.reset('slipper');colour('tool_sole','354C5A',.9);colour('tool_cloth','C97961',.94);colour('tool_lining','E9CAA0',.96);colour('tool_seam','EAB889',.9)
m=b.Mesh('slipper')
profile=[(0,.015,.008),(.02,.044,.022),(.05,.052,.024),(.12,.059,.024),(.20,.064,.027),(.255,.055,.025),(.283,.023,.014),(.29,.003,.003)]
m.loft([(0,y,.005) for y,_,_ in profile],[(rx,rz) for _,rx,rz in profile],'tool_sole',segments=32)
m.ellipsoid((0,.205,.030),(.064,.081,.025),'tool_cloth',segments=32,rings=20)
m.ellipsoid((0,.084,.027),(.042,.047,.004),'tool_lining',segments=28,rings=16)
# Visible opening and padded rim distinguish a handheld slipper from a paddle.
opening=[(.043*math.cos(math.pi*i/24),.128+.011*math.sin(math.pi*i/24),.039+.006*math.sin(math.pi*i/24)) for i in range(25)]
m.tube(opening,.003,'tool_seam',segments=8)
heel=[(.039*math.cos(b.TAU*i/40),.059+.024*math.sin(b.TAU*i/40),.029) for i in range(41)]
m.tube(heel,.003,'tool_cloth',segments=8)
for sign in [-1,1]:
    m.tube([(sign*.049,.153,.046),(sign*.055,.198,.044),(sign*.044,.248,.044)],.0018,'tool_seam',segments=6)
finish(m,'slipper',[0,.035,0],[0,.18,0])
