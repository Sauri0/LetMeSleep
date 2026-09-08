"""Reconstruct only new trim from frozen native bone matrices; native control required."""
import argparse,hashlib,importlib.util,json,itertools
from collections import Counter
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('reader',ROOT/'work/hair08-inspect.py')
reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
NAMES=['human_outfit_%d_trim'%i for i in range(3)]
def mat(value):
    result=np.eye(4);result[:3,:3]=np.asarray(value['basis']).T;result[:3,3]=value['origin'];return result
def load_mesh(glb,name,topology):
    mesh=next(m for m in glb.doc['meshes'] if m['name'].removesuffix('_mesh')==name)
    node=next(n for n in glb.doc['nodes'] if n.get('mesh',-1)==glb.doc['meshes'].index(mesh))
    assert not any(k in node for k in ['matrix','translation','rotation','scale']),'nonidentity mesh transform needs explicit decoding'
    joint_names=[glb.doc['nodes'][i]['name'] for i in glb.doc['skins'][node['skin']]['joints']]
    bind_ids={x['name']:i for i,x in enumerate(topology['binds'])}
    positions=[];joints=[];weights=[];indices=[];surfaces=[]
    for p in mesh['primitives']:
        material=glb.doc['materials'][p['material']]['name'];attrs=p['attributes'];start=len(positions)
        ps=glb.accessor(attrs['POSITION']);js=glb.accessor(attrs['JOINTS_0']);ws=glb.accessor(attrs['WEIGHTS_0'])
        idx=[x[0]+start for x in glb.accessor(p['indices'])]
        surfaces.append({'index':len(surfaces),'material':material,'vertex_start':start,'vertex_count':len(ps),'triangle_start':len(indices)//3,'triangle_count':len(idx)//3})
        positions.extend(ps);joints.extend([[bind_ids[joint_names[x]] for x in row] for row in js]);weights.extend(ws);indices.extend(idx)
    return {'positions':np.asarray(positions),'joints':np.asarray(joints),'weights':np.asarray(weights),'indices':indices,'surfaces':surfaces}
def deform(mesh,topology,case):
    palette=np.asarray([mat(case['bones'][bind['name']])@mat(bind['pose']) for bind in topology['binds']])
    points=np.c_[mesh['positions'],np.ones(len(mesh['positions']))]
    selected=palette[mesh['joints']]
    transformed=np.einsum('nvij,nj->nvi',selected,points)
    return np.einsum('nvi,nv->ni',transformed,mesh['weights'])[:,:3]
def imported_order(mesh,topology,case,expected):
    # Godot reorders imported vertices/indices. Resolve that permutation from
    # the native control, then verify the ENTIRE geometric triangle multiset.
    got=deform(mesh,topology,case);grid={};cell=.0002
    for i,p in enumerate(got):grid.setdefault(tuple(np.floor(p/cell).astype(int)),[]).append(i)
    order=[]
    for vertex,p in enumerate(expected):
        key=np.floor(p/cell).astype(int);candidates=[]
        for delta in itertools.product([-1,0,1],repeat=3):candidates.extend(grid.get(tuple(key+delta),()))
        assert candidates,('native vertex missing',vertex)
        index=min(candidates,key=lambda i:float(np.linalg.norm(got[i]-p)))
        assert np.linalg.norm(got[index]-p)<.00015
        native_weights={int(j):round(float(w),6) for j,w in zip(topology['vertex_bind_indices'][vertex],topology['vertex_weights'][vertex]) if w>1e-7}
        source_weights={int(j):round(float(w),6) for j,w in zip(mesh['joints'][index],mesh['weights'][index]) if w>1e-7}
        # Import packs fractional weights. Match joint identity with the
        # observed <=2e-5 quantization bound, then use actual imported weights
        # below rather than claiming the authoring floats are identical.
        assert set(native_weights)==set(source_weights),('joint mapping',vertex)
        assert max(abs(native_weights[j]-source_weights[j]) for j in native_weights)<=.0000201,('import weight quantization',vertex)
        order.append(index)
    keys=[tuple(p) for p in mesh['positions']]
    def triangles(indices,mapping=None):
        return Counter(tuple(sorted(keys[int(mapping[v]) if mapping is not None else int(v)] for v in indices[i:i+3])) for i in range(0,len(indices),3))
    assert triangles(topology['indices'],order)==triangles(mesh['indices']),'all imported rest triangles must match exactly'
    return np.asarray(order)
def ordered_mesh(mesh,topology,case,expected):
    order=imported_order(mesh,topology,case,expected)
    js=np.zeros((len(order),4),dtype=int);ws=np.zeros((len(order),4))
    for i,(joints,weights) in enumerate(zip(topology['vertex_bind_indices'],topology['vertex_weights'])):
        assert len(joints)<=4
        js[i,:len(joints)]=joints;ws[i,:len(weights)]=weights
    return dict(mesh,positions=mesh['positions'][order],joints=js,weights=ws,indices=topology['indices'],order=order)
def verify(glb,document,tag):
    rows=[]
    for name in NAMES:
        topology=document['topologies'][name];mesh=load_mesh(glb,name,topology)
        assert len(mesh['positions'])==topology['vertex_count'],(name,'count')
        first=next(c for c in document['cases'] if c['role']=='human')
        expected=np.asarray(document['geometries'][first['meshes'][name]['geometry']]['vertices'])
        mesh=ordered_mesh(mesh,topology,first,expected)
        for case in document['cases']:
            if case['role']!='human':continue
            expected=np.asarray(document['geometries'][case['meshes'][name]['geometry']]['vertices'])
            got=deform(mesh,topology,case)
            error=np.linalg.norm(got-expected,axis=1)
            rows.append({'tag':tag,'case':case['id'],'mesh':name,'vertices':len(error),'max_error_m':float(error.max())})
    return rows
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--native');parser.add_argument('--output',default='work/garment09-trim-reskin-control.json');args=parser.parse_args()
    old=reader.GLB(ROOT/'outputs/0.9-garment-trim/source-fit4/game/assets/art/characters/human/human_lms06.glb')
    doc=json.loads((ROOT/'work/facial09-motion-export-fit4/human-000-026.json').read_text())
    rows=verify(old,doc,'old-native-controls')
    if args.native:
        new=reader.GLB(ROOT/'game/assets/art/characters/human/human_lms06.glb');native=json.loads(Path(args.native).read_text())
        rows+=verify(new,native,'current-native-control')
    report={'tolerance_m':.00015,'controls':rows,'maximum_error_m':max(x['max_error_m'] for x in rows),'current_control_supplied':bool(args.native),'complete_285':False}
    (ROOT/args.output).write_text(json.dumps(report,indent=2),encoding='utf8')
    assert report['maximum_error_m']<=.00015,report['maximum_error_m']
    print('TRIM_RESKIN_CONTROL cases_meshes='+str(len(rows))+' max_error_m='+str(report['maximum_error_m']))
