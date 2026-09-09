"""Exact GLB semantic invariance outside human_accessory_2, including morphs."""
import argparse,hashlib,importlib.util,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('reader',ROOT/'work/hair08-inspect.py')
reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
parser=argparse.ArgumentParser();parser.add_argument('--after',default='work/glasses09-candidate/human_lms06.glb');args=parser.parse_args()
before=ROOT/'outputs/0.9-glasses-fit/source-trim2'
a=reader.GLB(before/'game/assets/art/characters/human/human_lms06.glb');b=reader.GLB(ROOT/args.after)
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
 return {'attributes':{k:values(glb,v) for k,v in p['attributes'].items()},'indices':values(glb,p['indices']),
  'targets':[{k:values(glb,v) for k,v in t.items()} for t in p.get('targets',[])],
  'material':glb.doc['materials'][p['material']]['name'],'mode':p.get('mode',4)}
def nodes(glb):
 result=[]
 for source in glb.doc['nodes']:
  node=dict(source)
  if node.get('name')=='human_accessory_2':
   extras=dict(node.get('extras',{}));extras.pop('glasses_fit09',None)
   if extras:node['extras']=extras
   else:node.pop('extras',None)
  result.append(node)
 return result
check(a.doc['materials']==b.doc['materials'],'all material values exact')
check(nodes(a)==nodes(b),'all node transforms/hierarchy/skin references exact except A2 audit extras')
ma={m['name']:m for m in a.doc['meshes']};mb={m['name']:m for m in b.doc['meshes']}
check(ma.keys()==mb.keys(),'all mesh names exact')
unchanged=[]
for name in ma:
 if name=='human_accessory_2_mesh':continue
 pa,pb=ma[name]['primitives'],mb[name]['primitives']
 check(len(pa)==len(pb),name+' surface count')
 check({k:v for k,v in ma[name].items() if k!='primitives'}=={k:v for k,v in mb[name].items() if k!='primitives'},name+' mesh metadata/target names')
 for old,new in zip(pa,pb):check(primitive(a,old)==primitive(b,new),name+' complete attributes/weights/UV/normals/indices/morphs')
 unchanged.append(name)
check(len(unchanged)==32,'exactly32 unchanged complete meshes')
check(len(a.doc.get('animations',[]))==len(b.doc.get('animations',[])),'animation count')
for old,new in zip(a.doc.get('animations',[]),b.doc.get('animations',[])):
 check(old['channels']==new['channels'],'animation channels')
 for x,y in zip(old['samplers'],new['samplers']):
  check(x.get('interpolation')==y.get('interpolation'),'animation interpolation')
  for key in ['input','output']:check(values(a,x[key])==values(b,y[key]),'animation samples '+key)
check(len(a.doc['skins'])==len(b.doc['skins']),'skin count')
for old,new in zip(a.doc['skins'],b.doc['skins']):
 check({k:v for k,v in old.items() if k!='inverseBindMatrices'}=={k:v for k,v in new.items() if k!='inverseBindMatrices'},'skin joint IDs and metadata exact')
 check(values(a,old['inverseBindMatrices'])==values(b,new['inverseBindMatrices']),'every inverse bind matrix exact')
for relative in ['game/assets/art/characters/mosquito/mosquito_lms06.glb','art_source/characters/mosquito/mosquito_lms06.blend']:
 check((before/relative).read_bytes()==(ROOT/relative).read_bytes(),'mosquito byte exact '+relative)
report={'before_sha256':hashlib.sha256(a.raw).hexdigest(),'after_sha256':hashlib.sha256(b.raw).hexdigest(),
 'checks':checks,'failures':0,'unchanged_complete_meshes':unchanged,
 'scope':'32 complete meshes, all materials, nodes, hierarchy, skin/animation samples exact. Only human_accessory_2 geometry changes; no clearance inferred for A2.',
 'mosquito_blend_and_glb_byte_identical':True}
(ROOT/'work/glasses09-invariance.json').write_text(json.dumps(report,indent=2))
print('GLASSES09_INVARIANCE checks='+str(checks)+' failures=0 meshes=32')
