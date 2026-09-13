"""Read saved .blend and standard exports back in isolated headless Blender; never save."""
import bpy
import json
from collections import Counter
from pathlib import Path
from mathutils import Vector

FOLDER=Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento')
OUT=FOLDER/'NormalsV2'
proof=json.loads((OUT/'normals-repair.json').read_text())
names=[c['object'] for c in proof['changes']]

def inspect(scene):
    rows=[]
    for name in names:
        obj=scene.objects[name];mesh=obj.data;mesh.calc_loop_triangles()
        points=[obj.matrix_world @ v.co for v in mesh.vertices];center=sum(points,Vector())/len(points)
        dirs=Counter();volume=0.
        for t in mesh.loop_triangles:
            a,b,c=(points[i] for i in t.vertices);n=(b-a).cross(c-a).normalized()
            dirs['up' if n.z>.1 else 'down' if n.z<-.1 else 'side']+=1
            volume+=(a-center).dot((b-center).cross(c-center))/6
        if name=='CAMP_Prod_ChoppingBoard':assert volume>0,(name,volume)
        else:assert dict(dirs)=={'up':len(mesh.loop_triangles)},(name,dict(dirs))
        rows.append(dict(name=name,triangles=len(mesh.loop_triangles),worldTriangleDirections=dict(dirs),signedWorldVolume=volume))
    return rows

bpy.ops.wm.open_mainfile(filepath=str(FOLDER/'HF_MAP_03_campamento.blend'),load_ui=False)
scene=bpy.data.scenes['HF_MAP_03_campamento'];bpy.context.window.scene=scene
result=dict(status='PASS_SAVED_BLEND_AND_STANDARD_EXPORT_WINDING_READBACK',blender=bpy.app.version_string,
            savedBlend=inspect(scene),pending=['Unity v2 visual culling and one-sided collider ground validation'])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'HF_MAP_03_campamento_UNITY_V2.fbx'),use_custom_normals=True)
result['fbxReadback']=inspect(bpy.context.scene)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(OUT/'HF_MAP_03_campamento_UNITY_V2.glb'))
result['glbReadback']=inspect(bpy.context.scene)
(OUT/'normals-readback.json').write_text(json.dumps(result,indent=2))
print('READBACK_COMPLETE',result['status'])
