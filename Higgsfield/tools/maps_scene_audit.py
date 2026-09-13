"""Read-only inspection of the active map, with concise console summary."""
import json,site,sys
site.addsitedir(r'C:\Users\brank\AppData\Roaming\Blender Foundation\Blender\5.2\extensions\.local\lib\python3.13\site-packages')
from blmcp.tools_helpers.connection import send_code
code='''import bpy,json,math
from pathlib import Path
from mathutils import Vector
scene=bpy.context.scene
assert scene.name.startswith('HF_MAP_'),'Not an assigned map scene'
folder=Path(bpy.data.filepath).parent
assert folder.is_relative_to(Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas'))
items=[]
for o in scene.objects:
    item={'name':o.name,'type':o.type,'location':list(o.matrix_world.translation),'scale':list(o.scale),'parent':o.parent.name if o.parent else None,'collections':[c.name for c in o.users_collection]}
    item['matrix_world']=[list(row) for row in o.matrix_world]
    item['properties']={k:o[k] for k in o.keys() if isinstance(o[k],(str,int,float,bool))}
    if o.type=='CAMERA':item['camera']={'lens':o.data.lens,'type':o.data.type,'ortho_scale':o.data.ortho_scale}
    if o.type=='LIGHT':item['light']={'type':o.data.type,'color':list(o.data.color),'energy':o.data.energy}
    if o.type=='MESH':
        o.data.calc_loop_triangles()
        corners=[o.matrix_world@Vector(v) for v in o.bound_box]
        item.update(vertices=len(o.data.vertices),triangles=len(o.data.loop_triangles),bounds_min=[min(v[i] for v in corners) for i in range(3)],bounds_max=[max(v[i] for v in corners) for i in range(3)],materials=[m.name for m in o.data.materials if m],shape_keys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [])
    items.append(item)
mat_names={m for o in items for m in o.get('materials',[])}
materials=[]
for name in sorted(mat_names):
    m=bpy.data.materials[name]
    bsdf=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.use_nodes else None
    materials.append({'name':name,'diffuse':list(m.diffuse_color),'base_color':list(bsdf.inputs['Base Color'].default_value) if bsdf else None})
report={'scene':scene.name,'file':bpy.data.filepath,'objects':items,'materials':materials,'fps':scene.render.fps,'frame_start':scene.frame_start,'frame_end':scene.frame_end,'camera':scene.camera.name if scene.camera else None,'triangles':sum(o.get('triangles',0) for o in items),'mesh_count':sum(o['type']=='MESH' for o in items)}
out=folder/'scene-audit.json'
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
result={'scene':scene.name,'objects':len(items),'meshes':report['mesh_count'],'triangles':report['triangles'],'materials':len(materials),'water':[o for o in items if o['name'].startswith('Water_')],'markers':[o for o in items if o['type']=='EMPTY'],'report':str(out)}
'''
print(json.dumps(send_code(code,strict_json=True),ensure_ascii=False))
