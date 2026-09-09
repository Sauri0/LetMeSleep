"""Strict, finite A2 fit audit. No blanket head/hair attachment exception."""
import argparse,hashlib,json,sys,time
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from characters_facial_audit import state,variants,contacts
parser=argparse.ArgumentParser()
parser.add_argument('--blend',default='work/glasses09-candidate/human_lms06.blend')
parser.add_argument('--output',default='work/glasses09-fit-audit.json')
parser.add_argument('--group',choices=['all','support','eyes','brows','other'],default='all')
parser.add_argument('--verify',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
path=Path(args.blend);path=path if path.is_absolute() else ROOT/path
bpy.ops.wm.open_mainfile(filepath=str(path));start=time.time()
glasses=bpy.data.objects['human_accessory_2'];shape=state(glasses,{})
head=state(bpy.data.objects['human_head'],{});tree=head['bvh']
record=json.loads(glasses.get('glasses_fit09','{}'))
report={'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'group':args.group,
    'scope':'Actual authoring triangles, every compatible human A2 neighbour at listed finite morph samples; continuous morph-space not proven',
    'exceptions':[],'parts':[],'failures':[],'checks':0,'support':{},
    'motion_limit':'A2/head/facial/hair rigid head binding is checked; native render and joint-policy checks are separate. The 285-pose garment consumer does not test A2.'}
def check(ok,label):
    report['checks']+=1
    if not ok:report['failures'].append(label)
def godot(q):return Vector((q.x,q.z,-q.y))

if args.group in ['all','support']:
    check(bool(record),'compact semantic components present')
    check(len(glasses.vertex_groups)==1 and glasses.vertex_groups[0].name=='head','single rigid head binding')
    check(all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in glasses.data.vertices),'every vertex rigid head weight1')
    check(not glasses.data.shape_keys,'accessory does not independently deform')
    counts={};minimum=1e8;buried=[];frontal=[];samples=0;ear={}
    polygons=glasses.data.polygons
    for part in record.get('parts',[]):
        name=part['name'];points=[];triangles=[]
        first=part['polygon_start'];last=first+part['polygon_count']
        if name.startswith('ring'):
            edges={}
            for polygon in polygons[first:last]:
                ids=list(polygon.vertices)
                for a,b in zip(ids,ids[1:]+ids[:1]):
                    key=tuple(sorted((a,b)));edges[key]=edges.get(key,0)+1
            check(bool(edges) and all(n==2 for n in edges.values()),name+' actual tube topology closed without boundary edges')
        for triangle in glasses.data.loop_triangles:
            if not first<=triangle.polygon_index<last:continue
            p=[shape['points'][i] for i in triangle.vertices];triangles.append(p)
            # Vertices, edge midpoints and face interior: chords are tested,
            # because clear vertices alone did not prove the previous trim.
            points.extend(p+[(p[i]+p[(i+1)%3])*.5 for i in range(3)]+[sum(p,Vector())/3])
        for q in points:
            hit,normal,_,distance=tree.find_nearest(q);signed=(q-hit).dot(normal)
            minimum=min(minimum,signed);samples+=1
            if signed<-.0001 and len(buried)<12:buried.append({'part':name,'point':list(godot(q)),'signed_m':signed})
            if name.startswith('ring') or name=='bridge':
                # Godot front is Blender +Y. Every exposed ring section must
                # remain in front of the whole nose surface at its own XY.
                h,_,_,_=tree.ray_cast(Vector((q.x,1.0,q.z)),Vector((0,-1,0)),2)
                if h is not None and q.y<h.y-.0001 and len(frontal)<12:frontal.append({'part':name,'point':list(godot(q)),'depth_m':h.y-q.y})
        counts[name]={'samples':len(points),'triangles':len(triangles)}
        if name.startswith('temple'):
            selected=[]
            region=record['ear_support_region']
            for q in points:
                g=godot(q)
                if all(a<=v<=b for (a,b),v in [(region['abs_x'],abs(g.x)),(region['y'],g.y),(region['z'],g.z)]):
                    h,n,_,d=tree.find_nearest(q);selected.append((d,g,h))
            nearest=min(selected,key=lambda item:item[0]) if selected else None
            check(nearest is not None and nearest[0]<=.001,name+' reaches real ear surface within1mm')
            ear[name]={'minimum_surface_gap_m':nearest[0] if nearest else None,'sampled_points':len(selected),
                       'point':list(nearest[1]) if nearest else None,'head_point':list(godot(nearest[2])) if nearest else None}
    check(not buried,'no sampled A2 surface buried in head')
    check(not frontal,'no ring/bridge samples behind front nasal surface')
    for part in record.get('parts',[]):
        if part['name'].startswith('ring'):check(part['closed_path'] is True,part['name']+' remains closed')
    report['support']={'minimum_signed_head_distance_m':minimum,'sample_count':samples,'parts':counts,
                       'buried_witnesses':buried,'nasal_witnesses':frontal,'ear_support':ear}

names=['human_head']+['human_%s_%d'%(cat,i) for cat in ['eyes','brows','mouth','hair'] for i in range(3)]+['human_%s_%d'%(cat,i) for cat in ['mustache','beard'] for i in [1,2]]
for name in names:
    category=name.split('_')[1]
    group='eyes' if category=='eyes' else 'brows' if category=='brows' else 'other'
    if args.group not in ['all',group]:continue
    obj=bpy.data.objects[name];states=[];seen=set();witnesses=[];contact_states=0;maximum=0
    check(all(obj.vertex_groups[w.group].name=='head' for v in obj.data.vertices for w in v.groups if w.weight>1e-6),name+' same rigid head transform')
    for label,weights in variants(obj):
        geom=state(obj,weights)
        key=hashlib.sha256(b''.join(float(x).hex().encode()+b',' for p in geom['points'] for x in p)).hexdigest()
        if key in seen:continue
        seen.add(key);states.append(weights)
        count,hit,_,_=contacts(shape,geom,tree)
        check(count==0,'A2 intersects '+name+' '+json.dumps(weights,sort_keys=True))
        if count:
            contact_states+=1;maximum=max(maximum,count)
            if len(witnesses)<8:witnesses.append({'weights':weights,'triangle_contacts':count,'point_godot_m':list(godot(Vector(hit)))})
    report['parts'].append({'mesh':name,'states':states,'samples':len(states),'contact_states':contact_states,'maximum_triangle_contacts':maximum,'witnesses':witnesses})
    print('GLASSES09_AUDIT_PART',name,len(states),contact_states,round(time.time()-start,2),flush=True)
report['elapsed_seconds']=time.time()-start;report['passed']=not report['failures']
out=Path(args.output);out=out if out.is_absolute() else ROOT/out
out.write_text(json.dumps(report,indent=2),encoding='utf8')
print('GLASSES09_AUDIT_RESULT checks='+str(report['checks'])+' failures='+str(len(report['failures'])),flush=True)
if args.verify and not report['passed']:raise RuntimeError('A2 fit has unresolved findings')
