"""Read actual eyelid morph geometry and verify closure over white/iris/glints."""
import sys,json,hashlib,time
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(R/'art_source/characters/shared'))
from eyelid_geometry import globe_components

def posed(mesh,value):
    keys=mesh.shape_keys.key_blocks;base=keys['Basis']
    result=[v.co.copy() for v in base.data]
    for blink in ['BlinkL','BlinkR']:
        values={blink:value}
        for k in range(1,8):values[blink+'Arc'+str(k)]=max(0,1-abs(value*8-k))
        for name,w in values.items():
            if w:
                for i,v in enumerate(keys[name].data):result[i]+=(v.co-base.data[i].co)*w
    return result

def run(role):
    path=R/'art_source/characters'/role/(role+'_lms06.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path));checks=0;failures=[];cases=[]
    directions=[Vector(v).normalized() for v in [(0,1,0),(1,1,0),(-1,1,0),(0,1,1),(0,1,-1),(1,1,1),(-1,1,-1)]]
    for option in range(3):
        obj=bpy.data.objects[f'{role}_eyes_{option}'];mesh=obj.data
        globes=globe_components(mesh)
        checks+=1
        if len(globes)!=2:failures.append('missing two real ocular volumes')
        ocular={i for p in mesh.polygons if mesh.materials[p.material_index].name in ['eye_white','pupil'] for i in p.vertices}
        lid_faces=[list(p.vertices) for p in mesh.polygons if mesh.materials[p.material_index].name in ['skin','insect_primary']]
        neutral=posed(mesh,0);maximum=0
        for step in range(17):
            points=posed(mesh,step/16)
            maximum=max(maximum,max((points[i]-neutral[i]).length for i in ocular));checks+=len(ocular)
        if maximum>1e-7:failures.append(obj.name+' ocular vertex moved under blink')
        closed=posed(mesh,1);bvh=BVHTree.FromPolygons(closed,lid_faces)
        misses=[];rays=0
        for i in sorted(ocular):
            for direction in directions:
                # Every vertex faces at least one actual requested view. A ray
                # through its back side is not evidence of an exposed surface.
                if mesh.vertices[i].normal.dot(direction)<.05:continue
                hit,normal,_,distance=bvh.ray_cast(closed[i]+direction*.25,-direction,.251)
                rays+=1;checks+=1
                if hit is None or distance>.25-1e-5:
                    if len(misses)<24:misses.append({'vertex':i,'material':[mesh.materials[p.material_index].name for p in mesh.polygons if i in p.vertices][:1],
                        'point':list(closed[i]),'direction':list(direction),'distance':distance})
        if misses:failures.append(obj.name+' closure leaves ocular vertices visible')
        brows=bpy.data.objects[f'{role}_brows_{option}'].data.shape_keys.key_blocks
        shifts={name:max((q.co-b.co).length for q,b in zip(brows[name].data,brows['Basis'].data))*(.35 if role=='mosquito' else 1) for name in ['BrowUp','BrowDown']}
        cases.append({'role':role,'eyes':option,'ocular_vertices':len(ocular),'closure_rays':rays,'ocular_blink_delta_m':maximum,
                      'closure_misses':misses,'brow_max_displacement_runtime_m':shifts})
    report={'role':role,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'checks':checks,'failures':failures,'cases':cases,
            'scope':'Actual vertices, 17 blink weights; full-closure occlusion against lid triangles from seven front/oblique/high/low directions, including pupil and highlights.'}
    (R/'work'/f'blink08-geometry-{role}.json').write_text(json.dumps(report,indent=2))
    print('BLINK_GEOMETRY',role,checks,'failures',len(failures),flush=True)
    return report

if __name__=='__main__':
    results=[run(role) for role in ['human','mosquito']]
    if any(row['failures'] for row in results):raise RuntimeError('Ocular coverage remains incomplete')
