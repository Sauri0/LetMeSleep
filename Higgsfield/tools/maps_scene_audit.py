"""Read-only inspection of the active map, with concise console summary."""
import json,site,sys
from maps_connection import send_code
code='''import bpy,json,math
from pathlib import Path
from mathutils import Vector
scene=bpy.context.scene
assert scene.name.startswith('HF_MAP_'),'Not an assigned map scene'
folder=Path(bpy.data.filepath).parent
assert folder.is_relative_to(Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas'))
items=[]
excluded=[]
for o in scene.objects:
    if o.get('export_exclude',False):
        assert o.type=='MESH' and o.hide_render,'Only explicitly hidden source meshes may be excluded'
        excluded.append({'name':o.name,'type':o.type,'reason':'authored export_exclude=true, hide_render=true'})
        continue
    item={'name':o.name,'type':o.type,'location':list(o.matrix_world.translation),'scale':list(o.scale),'parent':o.parent.name if o.parent else None,'collections':[c.name for c in o.users_collection]}
    item['matrix_world']=[list(row) for row in o.matrix_world]
    item['properties']={k:o[k] for k in o.keys() if isinstance(o[k],(str,int,float,bool))}
    if o.type=='CAMERA':item['camera']={'lens':o.data.lens,'type':o.data.type,'ortho_scale':o.data.ortho_scale}
    if o.type=='LIGHT':item['light']={'type':o.data.type,'color':list(o.data.color),'energy':o.data.energy}
    if o.type=='MESH':
        o.data.calc_loop_triangles()
        corners=[o.matrix_world@Vector(v) for v in o.bound_box]
        item.update(vertices=len(o.data.vertices),triangles=len(o.data.loop_triangles),bounds_min=[min(v[i] for v in corners) for i in range(3)],bounds_max=[max(v[i] for v in corners) for i in range(3)],materials=[slot.material.name for slot in o.material_slots if slot.material],shape_keys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [])
    items.append(item)
mat_names={m for o in items for m in o.get('materials',[])}
materials=[]
for name in sorted(mat_names):
    m=bpy.data.materials[name]
    bsdf=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.use_nodes else None
    materials.append({'name':name,'diffuse':list(m.diffuse_color),'base_color':list(bsdf.inputs['Base Color'].default_value) if bsdf else None,'alpha':float(bsdf.inputs['Alpha'].default_value) if bsdf else 1,'emission_color':list(bsdf.inputs['Emission Color'].default_value) if bsdf else [0,0,0,1],'emission_strength':float(bsdf.inputs['Emission Strength'].default_value) if bsdf else 0})
report={'scene':scene.name,'file':bpy.data.filepath,'source_object_count':len(scene.objects),'excluded_objects':excluded,'objects':items,'materials':materials,'fps':scene.render.fps,'frame_start':scene.frame_start,'frame_end':scene.frame_end,'camera':scene.camera.name if scene.camera else None,'triangles':sum(o.get('triangles',0) for o in items),'mesh_count':sum(o['type']=='MESH' for o in items)}
out=folder/'scene-audit.json'
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
result={'scene':scene.name,'objects':len(items),'meshes':report['mesh_count'],'triangles':report['triangles'],'materials':len(materials),'water':[{'name':o['name'],'vertices':o.get('vertices',0),'shape_keys':o.get('shape_keys',[])} for o in items if o['name'].startswith('Water_')],'markers':[{'name':o['name'],'location':o['location']} for o in items if o['type']=='EMPTY'],'report':str(out)}
'''
print(json.dumps(send_code(code,strict_json=True),ensure_ascii=False))
