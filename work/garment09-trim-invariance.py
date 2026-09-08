"""Compare complete GLB data outside the three identified central trim components."""
import hashlib,importlib.util,json,struct
from pathlib import Path
from collections import Counter
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('reader',ROOT/'work/hair08-inspect.py')
reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
before=ROOT/'outputs/0.9-garment-trim/source-fit4/game/assets/art/characters/human/human_lms06.glb'
after=ROOT/'game/assets/art/characters/human/human_lms06.glb'
a,b=reader.GLB(before),reader.GLB(after)
checks=0
def check(ok,label):
    global checks
    checks+=1
    assert ok,label
def values(glb,index):
    item=glb.doc['accessors'][index]
    if 'sparse' not in item:return glb.accessor(index)
    code,size=reader.COMPONENTS[item['componentType']];width=reader.WIDTH[item['type']]
    def block(viewid,offset,count,fmt,stride):
        view=glb.doc['bufferViews'][viewid];start=view.get('byteOffset',0)+offset
        return [struct.unpack_from('<'+fmt,glb.binary,start+i*view.get('byteStride',stride)) for i in range(count)]
    rows=block(item['bufferView'],item.get('byteOffset',0),item['count'],code*width,size*width) if 'bufferView' in item else [(0,)*width]*item['count']
    sparse=item['sparse'];ids=sparse['indices'];val=sparse['values'];ic,isz=reader.COMPONENTS[ids['componentType']]
    indices=block(ids['bufferView'],ids.get('byteOffset',0),sparse['count'],ic,isz)
    replacements=block(val['bufferView'],val.get('byteOffset',0),sparse['count'],code*width,size*width)
    for ix,value in zip(indices,replacements):rows[ix[0]]=value
    return rows
def primitive(glb,p):
    return {'attributes':{k:values(glb,v) for k,v in p['attributes'].items()},
            'indices':values(glb,p['indices']),
            'targets':[{k:values(glb,v) for k,v in t.items()} for t in p.get('targets',[])],
            'material':glb.doc['materials'][p['material']]['name']}
def except_central(glb,p):
    obj=primitive(glb,p);pos=obj['attributes']['POSITION'];idx=[x[0] for x in obj['indices']]
    parent=list(range(len(pos)))
    def root(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    def join(i,j):parent[root(i)]=root(j)
    welded={}
    for i,q in enumerate(pos):
        key=tuple(round(x,7) for x in q)
        if key in welded:join(i,welded[key])
        else:welded[key]=i
    for i in range(0,len(idx),3):join(idx[i],idx[i+1]);join(idx[i+1],idx[i+2])
    groups={}
    for i in range(len(pos)):groups.setdefault(root(i),[]).append(i)
    removed=set()
    for group in groups.values():
        if max(pos[i][0] for i in group)-min(pos[i][0] for i in group)<.02 and max(pos[i][1] for i in group)-min(pos[i][1] for i in group)>.3:
            removed.update(group)
    check(bool(removed),'central component identified')
    keys=sorted(obj['attributes'])
    # Preserve every noncentral attribute and triangle, independent of export
    # index renumbering after subdivision. Normals are included exactly.
    verts=[tuple((k,tuple(obj['attributes'][k][i])) for k in keys) for i in range(len(pos))]
    vertex_counter=Counter(verts[i] for i in range(len(pos)) if i not in removed)
    triangles=[]
    for i in range(0,len(idx),3):
        face=idx[i:i+3]
        if any(v in removed for v in face):continue
        triangle=tuple(verts[v] for v in face)
        triangles.append(min(triangle,triangle[1:]+triangle[:1],triangle[2:]+triangle[:2]))
    return (vertex_counter,Counter(triangles),obj['material']),len(removed)

ma={m['name']:m for m in a.doc['meshes']};mb={m['name']:m for m in b.doc['meshes']}
check(ma.keys()==mb.keys(),'all mesh IDs unchanged')
check(a.doc['materials']==b.doc['materials'],'all material values unchanged')
def nodes(glb):
    result=[]
    for source in glb.doc['nodes']:
        node=dict(source)
        if node.get('name') in ['human_outfit_%d_trim'%i for i in range(3)]:
            extras=dict(node.get('extras',{}));extras.pop('garment_trim09',None);node['extras']=extras
        result.append(node)
    return result
check(nodes(a)==nodes(b),'node hierarchy and every transform unchanged except trim audit extras')
check(a.doc.get('animations',[])==b.doc.get('animations',[]),'animation schema unchanged')
for old,new in zip(a.doc.get('animations',[]),b.doc.get('animations',[])):
    for sa,sb in zip(old['samplers'],new['samplers']):
        for key in ['input','output']:check(values(a,sa[key])==values(b,sb[key]),'animation samples exact')
unchanged=[];trim=[]
for name in ma:
    pa,pb=ma[name]['primitives'],mb[name]['primitives']
    check(len(pa)==len(pb),name+' surface count')
    changed=name.removesuffix('_mesh') in ['human_outfit_%d_trim'%i for i in range(3)]
    for old,new in zip(pa,pb):
        mat=a.doc['materials'][old['material']]['name']
        if changed and mat=='secondary':
            va,ca=except_central(a,old);vb,cb=except_central(b,new)
            check(va==vb,name+' every noncentral vertex/triangle attribute exact')
            trim.append({'mesh':name,'before_central_export_vertices':ca,'after_central_export_vertices':cb})
        else:check(primitive(a,old)==primitive(b,new),name+' complete surface exact '+mat)
    if not changed:unchanged.append(name)
check(a.doc['skins']==b.doc['skins'],'skin joint IDs/count/bind accessor schema')
for old,new in zip(a.doc['skins'],b.doc['skins']):
    check(values(a,old['inverseBindMatrices'])==values(b,new['inverseBindMatrices']),'all inverse bind matrices exact')
check(len(unchanged)==30 and len(trim)==3,'scope exactly three trim components')
report={'before_sha256':hashlib.sha256(a.raw).hexdigest(),'after_sha256':hashlib.sha256(b.raw).hexdigest(),
        'checks':checks,'failures':0,'unchanged_complete_meshes':unchanged,'trim_components':trim,
        'scope':'Complete mesh positions/normals/UV/weights/morphs/materials/triangles exact for30meshes; noncentral trim exact. No motion clearance inferred.'}
(ROOT/'work/garment09-trim-invariance.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('GARMENT_TRIM09_INVARIANCE checks='+str(checks)+' failures=0 meshes=30 components=3')
