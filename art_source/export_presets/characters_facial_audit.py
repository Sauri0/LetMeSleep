"""Read-only, factored triangle audit of every selectable facial part.

Run Blender --background --python-exit-code 1 --python this.py.
It records exact triangle contacts and witnesses, not a visual approval. AABB
tests only discard impossible pairs; retained pairs use edge/triangle tests.
None options have no mesh. Colour/garment invariance is a separate Godot gate.
"""
import bpy,itertools,json,hashlib,math,time,argparse,sys
from pathlib import Path
from mathutils import Vector,geometry
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(R/'art_source/characters/shared'))
from eyelid_geometry import globe_components
HEAD_CATS={'head','eyes','brows','mouth','hair','accessory','mustache','beard'}

def eye_components(obj):
 mesh=obj.data
 components=globe_components(mesh)
 selected=[face for _,faces in components for face in faces]
 edge_counts={}
 for face in selected:
  for a,b in zip(face,face[1:]+face[:1]):
   key=tuple(sorted((a,b)));edge_counts[key]=edge_counts.get(key,0)+1
 return selected,{'white_component_sizes':[len(indices) for indices,_ in components],'eyeball_components':len(components),
                 'eyeball_boundary_or_nonmanifold_edges':sum(n!=2 for n in edge_counts.values())}

def variants(obj):
 if not obj.data.shape_keys:return [('neutral',{})]
 cat=obj.name.split('_')[1]
 if cat=='eyes':
  result=[]
  for l,r,x,y,cheek in itertools.product([0,.5,1],[0,.5,1],[-1,1],[-1,1],[0,.35]):
   vis=1-max(l,r)
   human=obj.name.startswith('human')
   w={'BlinkL':l,'BlinkR':r,'GazeX':x*(.72 if human else .65)*vis,
      'GazeY':(y*.55 if y>0 else y*.60)*vis if human else y*.45*vis,'CheekLift':cheek}
   result.append(('blink_gaze',w))
 elif cat=='brows':
  result=[('brow_corner',dict(zip(['BrowUp','BrowDown'],values))) for values in itertools.product([0,1],repeat=2)]
  if obj.name.startswith('mosquito'):
   result=[('brow_blink',dict(w,BlinkL=l,BlinkR=r)) for label,w in result for l,r in itertools.product([0,.5,1],repeat=2)]
 elif cat=='mouth':result=[('mouth_corner',dict(zip(['MouthOpen','MouthSmile','MouthPress'],values))) for values in itertools.product([0,1],repeat=3)]
 else:return [('neutral',{})]
 result.append(('neutral',{}))
 # Midpoints are explicit extra samples; the report never claims a continuous
 # collision-free morph box from testing its endpoints alone.
 result.append(('middle',{key:.5 for key in {'eyes':['BlinkL','BlinkR'],'brows':['BrowUp','BrowDown'],'mouth':['MouthOpen','MouthSmile','MouthPress']}[cat]}))
 return result

def state(obj,weights):
 mesh=obj.data;points=[v.co.copy() for v in mesh.vertices]
 if mesh.shape_keys:
  base=mesh.shape_keys.key_blocks['Basis']
  effective=dict(weights)
  for control in ['BrowUp','BrowDown']:
   for blink in ['BlinkL','BlinkR']:
    if control+blink in mesh.shape_keys.key_blocks:effective[control+blink]=weights.get(control,0)*weights.get(blink,0)
  if 'BrowUpBrowDown' in mesh.shape_keys.key_blocks:
   effective['BrowUpBrowDown']=weights.get('BrowUp',0)*weights.get('BrowDown',0)
   for blink in ['BlinkL','BlinkR']:effective['BrowUpBrowDown'+blink]=effective['BrowUpBrowDown']*weights.get(blink,0)
  for blink in ['BlinkL','BlinkR']:
   for knot in range(1,8):
    name=blink+'Arc'+str(knot)
    if name in mesh.shape_keys.key_blocks:effective[name]=max(0,1-abs(max(0,min(1,weights.get(blink,0)))*8-knot))
  for name,value in effective.items():
   if not value:continue
   for i,v in enumerate(mesh.shape_keys.key_blocks[name].data):points[i]+=(v.co-base.data[i].co)*value
 mesh.calc_loop_triangles();tris=[tuple(t.vertices) for t in mesh.loop_triangles]
 bvh=BVHTree.FromPolygons(points,tris,all_triangles=True,epsilon=0)
 return {'points':points,'triangles':tris,'bvh':bvh,'minimum':Vector(tuple(min(v[i] for v in points) for i in range(3))),
         'maximum':Vector(tuple(max(v[i] for v in points) for i in range(3)))}

def intersects(a,b):
 """Closed segment/triangle intersections, including coplanar contacts."""
 hits=[]
 for source,target in [(a,b),(b,a)]:
  for i in range(3):
   start,end=source[i],source[(i+1)%3];direction=end-start;length=direction.length
   if length<1e-10:continue
   hit=geometry.intersect_ray_tri(*target,direction/length,start,True)
   if hit is not None:
    distance=(hit-start).dot(direction/length)
    if -1e-7<=distance<=length+1e-7:hits.append(hit)
 if hits:return hits[0]
 # A coplanar containment/edge crossing is not found by a parallel ray.
 normal=(a[1]-a[0]).cross(a[2]-a[0])
 if normal.length<1e-10:return None
 normal.normalize()
 if max(abs((q-a[0]).dot(normal)) for q in b)>1e-7:return None
 drop=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=drop]
 aa=[Vector((q[axes[0]],q[axes[1]])) for q in a];bb=[Vector((q[axes[0]],q[axes[1]])) for q in b]
 for q in aa:
  if geometry.intersect_point_tri_2d(q,*bb):return a[aa.index(q)]
 for q in bb:
  if geometry.intersect_point_tri_2d(q,*aa):return b[bb.index(q)]
 for i in range(3):
  for j in range(3):
   hit=geometry.intersect_line_line_2d(aa[i],aa[(i+1)%3],bb[j],bb[(j+1)%3])
   if hit is not None:
    span=aa[(i+1)%3]-aa[i];t=(hit-aa[i]).dot(span)/max(span.length_squared,1e-12)
    return a[i].lerp(a[(i+1)%3],t)
 return None

def contacts(a,b,head):
 if any(a['maximum'][i]<b['minimum'][i]-1e-7 or b['maximum'][i]<a['minimum'][i]-1e-7 for i in range(3)):return 0,None,0,None
 count=0;witness=None;exposed=0;exposed_witness=None
 for x,y in a['bvh'].overlap(b['bvh']):
  aa=[a['points'][i] for i in a['triangles'][x]];bb=[b['points'][i] for i in b['triangles'][y]]
  hit=intersects(aa,bb)
  if hit is not None:
   count+=1
   if witness is None:witness=[float(q) for q in hit]
   if head is not None:
    support,normal,_,distance=head.find_nearest(hit)
    if support is not None and (hit-support).dot(normal)>.0005:
     exposed+=1
     if exposed_witness is None:exposed_witness=[float(q) for q in hit]
 return count,witness,exposed,exposed_witness

def compatible(a,b):
 ac=a.split('_')[1];bc=b.split('_')[1]
 if ac==bc:return False
 if 'hair' in [ac,bc] and 'accessory' in [ac,bc] and a.startswith('human'):
  hair=a if ac=='hair' else b;acc=b if ac=='hair' else a
  return hair.endswith('_capped')==(int(acc.split('_')[2]) in [1,3])
 return True

def contact_classification(a,b,count,exposed):
 if not count:return 'disjoint_at_all_samples'
 cats={a.split('_')[1],b.split('_')[1]}
 if 'head' in cats:return 'intentional_skin_attachment'
 if cats=={'accessory','hair'} and a.startswith('human'):
  return 'intentional_capped_hair_root' # capped variant is selected at the brim.
 if cats=={'mustache','beard'}:return 'intentional_shared_colour_hair_join'
 if not exposed:return 'buried_in_head_attachment'
 return 'unexpected_exposed_contact'

def run(role):
 start=time.time();path=R/'art_source/characters'/role/(role+'_lms06.blend')
 bpy.ops.wm.open_mainfile(filepath=str(path))
 objects=sorted([o for o in bpy.data.objects if o.type=='MESH' and o.name.split('_')[1] in HEAD_CATS],key=lambda o:o.name)
 report={'role':role,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'status':'measured_not_visually_approved',
 'scope':'Every compatible pair of selected head pieces, at every listed finite morph sample; not a proof over all continuous animation times.',
 'scale_to_runtime':1 if role=='human' else .35,'coordinate_system':'Blender x/y/z metres before runtime scale',
 'parts':{},'pairs':[],'semantic_failures':[],'limitations':['Triangle intersections include deliberate embedded attachment seams. Nonzero pairs have explicit classifications; numerical classification is not a visual approval.',
 'Antenna bones, head/torso relative rotation and body garments are not transformed in this authoring-space table. Separate rig-motion coverage is required.']}
 cached={}
 head=bpy.data.objects[role+'_head' if role=='human' else role+'_core']
 if role=='human':head_bvh=state(head,{})['bvh']
 else:
  head_id=head.vertex_groups['head'].index
  indices={v.index for v in head.data.vertices if any(w.group==head_id and w.weight>.99 for w in v.groups)}
  faces=[list(p.vertices) for p in head.data.polygons if all(i in indices for i in p.vertices)]
  head_bvh=BVHTree.FromPolygons([v.co for v in head.data.vertices],faces)
 for o in objects:
  mats={o.data.materials[p.material_index].name for p in o.data.polygons};cat=o.name.split('_')[1]
  if cat in ['mouth','brows'] and mats.intersection({'eye_white','pupil'}):report['semantic_failures'].append(o.name+' contains eye components')
  proof=json.loads(o.get('partition_proof','{}'))
  if cat in ['eyes','mouth','brows'] and not proof.get('no_component_split',False):report['semantic_failures'].append(o.name+' missing partition proof')
  active_channels=[]
  if o.data.shape_keys:
   basis=o.data.shape_keys.key_blocks['Basis']
   active_channels=[key.name for key in o.data.shape_keys.key_blocks if key.name!='Basis' and any((v.co-b.co).length>1e-7 for v,b in zip(key.data,basis.data))]
   expected={'eyes':{'BlinkL','BlinkR','GazeX','GazeY','CheekLift'},'brows':{'BrowUp','BrowDown'},'mouth':{'MouthOpen','MouthSmile','MouthPress'}}[cat]
   if cat=='eyes':expected|={blink+'Arc'+str(k) for blink in ['BlinkL','BlinkR'] for k in range(1,8)}
   if role=='mosquito' and cat=='brows':expected|={'BlinkL','BlinkR','BrowUpBlinkL','BrowUpBlinkR','BrowDownBlinkL','BrowDownBlinkR','BrowUpBrowDown','BrowUpBrowDownBlinkL','BrowUpBrowDownBlinkR'}
   if set(active_channels)-expected:report['semantic_failures'].append(o.name+' undeclared deformation dependency '+str(set(active_channels)-expected))
  states=[];keys={}
  for label,weights in variants(o):
   geom=state(o,weights)
   key=tuple(tuple(v) for v in geom['points'])
   if key in keys:continue
   keys[key]=len(states);states.append((weights,geom))
  cached[o.name]=states
  bone_weights={o.vertex_groups[w.group].name for v in o.data.vertices for w in v.groups if w.weight>1e-7}
  report['parts'][o.name]={'states':[w for w,g in states],'state_count':len(states),'vertices':len(o.data.vertices),'triangles':len(states[0][1]['triangles']),
   'materials':sorted(mats),'weighted_bones':sorted(bone_weights),'partition':proof,'active_channels':active_channels}
  if cat=='eyes':
   whites,components=eye_components(o)
   report['parts'][o.name]['eye_components']=components
   if components['eyeball_boundary_or_nonmanifold_edges']:report['semantic_failures'].append(o.name+' cut or nonmanifold eyeball')
   rim={i for p in o.data.polygons if o.data.materials[p.material_index].name in ['skin','skin_shadow','insect_primary'] for i in p.vertices}
   maximum=0;minimum=1e8;witness=None
   ocular_faces=[list(p.vertices) for p in o.data.polygons if o.data.materials[p.material_index].name in ['eye_white','pupil']]
   for weights,geom in states:
    white_bvh=BVHTree.FromPolygons(geom['points'],ocular_faces)
    for index in rim:
     point=geom['points'][index];hit,normal,_,distance=white_bvh.find_nearest(point)
     if hit is None:continue
     minimum=min(minimum,distance)
     if distance>maximum:maximum=distance;witness={'weights':weights,'vertex':index,'point':list(point)}
   report['parts'][o.name]['lid_support']={'support':'ocular union including pupil/highlight; closure ray validation is characters_blink_audit',
    'maximum_gap_m':maximum,'minimum_gap_m':minimum,'sampled_rim_vertices':len(rim)*len(states),'witness':witness,
    'replacement':json.loads(o.get('eyelid_replacement','{}'))}
  elif cat in ['mouth','brows','mustache','beard']:
   maximum=0;minimum=1e8
   for weights,geom in states:
    for point in geom['points']:
     hit,normal,_,distance=head_bvh.find_nearest(point)
     maximum=max(maximum,distance);minimum=min(minimum,distance)
   report['parts'][o.name]['head_support']={'maximum_distance_m':maximum,'minimum_distance_m':minimum,'sampled_vertices':len(o.data.vertices)*len(states)}
 for index,(a,b) in enumerate(itertools.combinations(objects,2)):
  if not compatible(a.name,b.name):continue
  samples=0;count=0;worst=0;witnesses=[];exposed_samples=0;exposed_witnesses=[]
  for wa,ga in cached[a.name]:
   for wb,gb in cached[b.name]:
    shared=set(report['parts'][a.name]['active_channels'])&set(report['parts'][b.name]['active_channels'])
    if any(wa.get(key,0)!=wb.get(key,0) for key in shared):continue
    samples+=1;n,witness,exposed,exposed_point=contacts(ga,gb,head_bvh)
    if exposed:
     exposed_samples+=1
     if len(exposed_witnesses)<3:exposed_witnesses.append({'a':wa,'b':wb,'exposed_triangle_contacts':exposed,'point':exposed_point})
    if n:
     count+=1
     if len(witnesses)<3:witnesses.append({'a':wa,'b':wb,'triangle_contacts':n,'point':witness})
     worst=max(worst,n)
  report['pairs'].append({'a':a.name,'b':b.name,'samples':samples,'samples_with_contact':count,'max_triangle_contacts':worst,
   'classification':'disjoint_at_all_samples' if not count else 'contact_requires_interpretation','witnesses':witnesses})
  report['pairs'][-1].update(exposed_samples=exposed_samples,exposed_witnesses=exposed_witnesses)
  report['pairs'][-1]['classification']=contact_classification(a.name,b.name,count,exposed_samples)
  if len(report['pairs'])%20==0:print('FACIAL_AUDIT_PROGRESS',role,len(report['pairs']),round(time.time()-start,1),flush=True)
 report['elapsed_seconds']=time.time()-start
 report['pair_samples']=sum(p['samples'] for p in report['pairs'])
 report['unexpected_pairs']=[{'a':p['a'],'b':p['b']} for p in report['pairs'] if p['classification']=='unexpected_exposed_contact']
 report['numerical_passed']=not report['semantic_failures'] and not report['unexpected_pairs']
 out=R/'work'/('facial08-geometry-'+role+'.json');out.write_text(json.dumps(report,indent=2))
 print('FACIAL_AUDIT_RESULT',role,len(report['pairs']),report['pair_samples'],'semantic_failures',len(report['semantic_failures']),flush=True)
 return report

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--role',choices=['human','mosquito','both'],default='both');parser.add_argument('--verify',action='store_true')
 args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
 results=[run(role) for role in ['human','mosquito'] if args.role=='both'] if args.role=='both' else [run(args.role)]
 if args.verify and any(not result['numerical_passed'] for result in results):raise RuntimeError('Facial triangle/partition/support audit has unresolved numerical findings')
