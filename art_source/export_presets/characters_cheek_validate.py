"""Verify the localized cheek correction against its frozen pre-fix .blend.

Run with Blender --background --python this.py -- --before path/to/before.blend
The validator never saves or exports a model.
"""
import argparse, hashlib, json, struct, sys
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--before',type=Path,required=True)
parser.add_argument('--output',type=Path,default=ROOT/'work/cheek07-geometry.json')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])

def digest(values):
    return hashlib.sha256(json.dumps(values,separators=(',',':')).encode()).hexdigest()

def read(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    result={}
    for obj in bpy.data.objects:
        if obj.type!='MESH':continue
        points=[list(v.co) for v in obj.data.vertices]
        topology=[list(p.vertices) for p in obj.data.polygons]
        weights=[[(obj.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in obj.data.vertices]
        morphs={key.name:[list(v.co) for v in key.data] for key in obj.data.shape_keys.key_blocks} if obj.data.shape_keys else {}
        result[obj.name]={'points':points,'topology':topology,'weights':weights,'morphs':morphs,
                          'smooth':all(p.use_smooth for p in obj.data.polygons)}
    rigs=[obj for obj in bpy.data.objects if obj.type=='ARMATURE']
    bones={b.name:{'parent':b.parent.name if b.parent else '',
                   'matrix':[list(row) for row in b.matrix_local]} for b in rigs[0].data.bones}
    return result,bones

before,old_bones=read(args.before)
after,new_bones=read(ROOT/'art_source/characters/human/human_lms06.blend')
checks=[]
def check(value,label):
    checks.append({'ok':bool(value),'label':label})
    if not value:print('CHEEK_GEOMETRY FAIL',label)
check(before.keys()==after.keys(),'No mesh/cosmetic part added or removed')
check(old_bones==new_bones and len(new_bones)==36,'All 36 bone rest transforms and parents preserved')
changed=[]
for name,old in before.items():
    new=after[name]
    check(old['topology']==new['topology'],name+' topology preserved')
    check(old['weights']==new['weights'],name+' skin weights preserved')
    check(old['morphs']==new['morphs'],name+' facial/other shape keys preserved exactly')
    if name!='human_head':check(old['points']==new['points'],name+' vertex positions preserved exactly')
    else:
        check(new['smooth'],'Head smooth vertex normals requested')
        for index,(a,b) in enumerate(zip(old['points'],new['points'])):
            distance=(Vector(a)-Vector(b)).length
            if distance>1e-7:changed.append({'vertex':index,'distance_m':distance,'before':a,'after':b})
        check(0<len(changed)<len(old['points'])//2,'Only a localized subset of head vertices changed')
        check(all(0.064<abs(v['before'][0])<.176 and 1.424<v['before'][2]<1.567 and .060<v['before'][1]<.166 for v in changed),'Every modified vertex lies in the measured cheek transition region')
        flipped=0;degenerate=0
        for polygon in old['topology']:
            for j in range(1,len(polygon)-1):
                indices=[polygon[0],polygon[j],polygon[j+1]]
                a,b,c=[Vector(old['points'][i]) for i in indices]
                x,y,z=[Vector(new['points'][i]) for i in indices]
                first=(b-a).cross(c-a);second=(y-x).cross(z-x)
                if second.length<1e-10:degenerate+=1
                if first.dot(second)<0:flipped+=1
        def intersections(item):
            triangles=[(p[0],p[j],p[j+1]) for p in item['topology'] for j in range(1,len(p)-1)]
            tree=BVHTree.FromPolygons(item['points'],triangles,all_triangles=True,epsilon=0.0)
            return [(a,b) for a,b in tree.overlap(tree) if a<b and not set(triangles[a]).intersection(triangles[b])]
        old_intersections=intersections(old)
        new_intersections=intersections(new)
        print('CHEEK_GEOMETRY_NORMALS rotated_over90',flipped,'self_intersections_before',len(old_intersections),'after',len(new_intersections))
        check(len(new_intersections)<=len(old_intersections),'No additional non-adjacent triangle self intersections')
        check(degenerate==0,'No degenerate head triangles introduced')
        check(all(len(w)==1 and w[0][0]=='head' and w[0][1]==1 for w in new['weights']),'Shared head remains rigidly attached to the head bone')
report={'checks':len(checks),'failures':sum(not c['ok'] for c in checks),'results':checks,
        'modified_head_vertices':len(changed),'max_displacement_m':max((v['distance_m'] for v in changed),default=0),
        'mesh_vertex_digests':{name:{'before':digest(before[name]['points']),'after':digest(after[name]['points'])} for name in before},
        'morph_digests':{name:digest(after[name]['morphs']) for name in after if after[name]['morphs']}}
args.output.write_text(json.dumps(report,indent=2),encoding='utf8')
print('CHEEK_GEOMETRY_RESULT',report['checks'],report['failures'],'changed',len(changed),'max_m',report['max_displacement_m'])
if report['failures']:raise RuntimeError('Localized cheek validation failed')
