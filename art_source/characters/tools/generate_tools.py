"""Original held racket/broom, with the same active-face centres as HumanPose.
Run with bundled Blender --background --python this_file. No downloaded assets.
"""
import importlib.util, json, math
from pathlib import Path
import bpy, bmesh
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('character_builder',ROOT/'art_source/export_presets/characters_pipeline.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
bpy.context.preferences.filepaths.save_version=0

def finish(mesh,name,face):
    obj=mesh.finish()
    for modifier in list(obj.modifiers):obj.modifiers.remove(modifier)
    for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
    obj.parent=None
    obj['lms_original_authoring']='art_source/characters/tools/generate_tools.py'
    obj['active_face_metres']=face
    if 'lms_rig' in obj:del obj['lms_rig']
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(obj.data);bm.free()
    source=ROOT/'art_source/characters/tools';output=ROOT/'game/assets/art/characters/tools'
    output.mkdir(parents=True,exist_ok=True)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.world.color=(.18,.22,.26)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(name+'.blend')))
    bpy.ops.export_scene.gltf(filepath=str(output/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_skins=False)
    obj.data.calc_loop_triangles()
    (source/(name+'.json')).write_text(json.dumps({'original':True,'units':'metres','forward':'-Z','shaft':'-Y','active_face':[0,-face,0],'source':name+'.blend','triangles':len(obj.data.loop_triangles),'surfaces':len(obj.data.materials)},indent=2))
    print('LMS07_TOOL',name,len(obj.data.loop_triangles),'triangles',len(obj.data.materials),'surfaces')

b.reset('racket');b.mat('tool_body','DE8165',.64);b.mat('tool_grip','284653',.85);b.mat('tool_wire','BDC4BA',.36,.35);b.mat('tool_light','EAC260',.5)
m=b.Mesh('racket')
m.tube([(0,.055,0),(0,-.025,0),(0,-.16,0),(0,-.34,0)], [.022,.026,.025,.019],'tool_grip',segments=20)
m.ellipsoid((0,-.17,0),(.035,.09,.026),'tool_body',segments=24,rings=14)
m.tube(b.curve([(-.025,-.27,0),(-.052,-.36,0),(0,-.365,0),(.052,-.36,0),(.025,-.27,0)],5),.010,'tool_body',segments=10)
ring=[(.142*math.cos(b.TAU*i/64),-.51+.165*math.sin(b.TAU*i/64),0) for i in range(65)]
m.tube(ring,.012,'tool_body',segments=10)
for index in range(-4,5):
    x=index*.026;span=.152*math.sqrt(max(0,1-(x/.13)**2))
    m.tube([(x,-.51-span,.001),(x,-.51+span,.001)],.0019,'tool_wire',segments=6)
    y=index*.030;span=.13*math.sqrt(max(0,1-(y/.152)**2))
    m.tube([(-span,-.51+y,-.001),(span,-.51+y,-.001)],.0019,'tool_wire',segments=6)
for y in [-.015,-.045,-.075]:
    m.tube([(-.018,y,-.017),(.018,y,-.017)],.002,'tool_body',segments=6)
m.ellipsoid((0,-.151,-.027),(.010,.014,.003),'tool_light',segments=14,rings=10)
finish(m,'racket',.51)

b.reset('broom');b.mat('tool_wood','AA7453',.82);b.mat('tool_grip','315563',.79);b.mat('tool_brush','D8B46A',.92);b.mat('tool_tie','ED9871',.75)
m=b.Mesh('broom')
m.tube([(0,.06,0),(0,0,0),(0,-.72,0),(0,-.79,0)],[.018,.021,.018,.024],'tool_wood',segments=20)
m.tube([(0,.055,0),(0,-.08,0)],[.023,.023],'tool_grip',segments=20)
for y in [0.02,-.015,-.05]:
    m.tube([(math.cos(b.TAU*i/24)*.024,y,math.sin(b.TAU*i/24)*.024) for i in range(25)],.002,'tool_tie',segments=6)
m.loft([(0,-.77,0),(0,-.79,0),(0,-.835,0)],[ (.14,.038),(.17,.049),(.166,.044)],'tool_grip',segments=24)
for layer in [-1,0,1]:
    for index in range(12):
        x=-.15+index*.0273;z=layer*.027
        path=b.curve([(x,-.815,z),(x*1.02,-.876,z+layer*.002),(x*1.045,-.961+abs(x)*.10,z+layer*.009)],4)
        m.tube(path,[.012*(1-.25*i/(len(path)-1)) for i in range(len(path))],'tool_brush',segments=8)
m.tube([(-.157,-.839,-.045),(.157,-.839,-.045)],.003,'tool_tie',segments=6)
finish(m,'broom',.88)
