import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
manifest=json.loads((HERE/'source_manifest.json').read_text())
checks=[];files={}
def check(name,ok,detail=None):
 checks.append(dict(name=name,passed=bool(ok),detail=detail))
 if not ok:print('FAIL',name,detail,flush=True)
def snap():
 out={}
 for o in bpy.context.scene.objects:
  points=[o.matrix_world@Vector(v) for v in o.bound_box] if o.type=='MESH' else [o.matrix_world.translation]
  out[o.name]=dict(parent=o.parent.name if o.parent else None,
   values=[c for a in range(3) for c in (min(p[a] for p in points),max(p[a] for p in points))])
 return out
for name,data in manifest.items():
 bpy.ops.wm.open_mainfile(filepath=str(HERE/(name+'.blend')))
 bpy.context.view_layer.update();before=snap()
 for o in bpy.context.scene.objects:
  check(name+' unit scale '+o.name,max(abs(s-1) for s in o.scale)<1e-5)
  if o.type!='MESH':continue
  bm=bmesh.new();bm.from_mesh(o.data)
  check(name+' manifold '+o.name,all(e.is_manifold for e in bm.edges))
  check(name+' positive volume '+o.name,bm.calc_volume(signed=True)>1e-9)
  bm.free()
 for c in data['box_colliders']:
  check(name+' collider '+c['source'],c['node'] in before and all(s>0 for s in c['size']))
 for c in data['mesh_colliders']:check(name+' mesh collider '+c['node'],c['node'] in before)
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.fbx(filepath=str(HERE/(name+'.fbx')),use_anim=False)
 bpy.context.view_layer.update();after=snap()
 check(name+' roundtrip object set',set(before)==set(after))
 errors=[];max_error=0
 for n in set(before)&set(after):
  if before[n]['parent']!=after[n]['parent']:errors.append(n)
  max_error=max(max_error,max(abs(a-b) for a,b in zip(before[n]['values'],after[n]['values'])))
 check(name+' roundtrip hierarchy',not errors,errors)
 check(name+' roundtrip bounds within 0.1mm',max_error<.0001,max_error)
 for suffix in ('.blend','.fbx'):
  p=HERE/(name+suffix);files[p.name]=dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
report=dict(scope='Blender source geometry and FBX roundtrip only; no Unity maps or renders validated',blender=bpy.app.version_string,
 passed=sum(c['passed'] for c in checks),failed=sum(not c['passed'] for c in checks),checks=checks,files=files)
(HERE/'source_validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('LMS_ALFA_SOURCE_VALIDATION',report['passed'],report['failed'],flush=True)
if report['failed']:raise RuntimeError('Source validation failed')
