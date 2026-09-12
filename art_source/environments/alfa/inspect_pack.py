"""Read actual GLB bytes, register reused local models, and check pack limits.
No renderer/Blender execution. Mesh world transforms included in AABB measurement.
"""
import json,struct,hashlib,sys,math
from pathlib import Path
import numpy as np
SOURCE=Path(__file__).resolve().parent;ROOT=SOURCE.parents[2];OUT=ROOT/'game/assets/art/house/alfa'
class GLB:
 def __init__(self,path):
  raw=path.read_bytes();assert raw[:4]==b'glTF';assert struct.unpack_from('<I',raw,8)[0]==len(raw)
  length=struct.unpack_from('<I',raw,12)[0];self.doc=json.loads(raw[20:20+length]);self.binary=raw[28+length:];self.sha=hashlib.sha256(raw).hexdigest();self.bytes=len(raw)
 def values(self,index):
  a=self.doc['accessors'][index];v=self.doc['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];fmt='<'+{5121:'B',5123:'H',5125:'I',5126:'f'}[a['componentType']]*n;step=v.get('byteStride',struct.calcsize(fmt));offset=v.get('byteOffset',0)+a.get('byteOffset',0)
  return np.array([struct.unpack_from(fmt,self.binary,offset+i*step) for i in range(a['count'])])
 def inspect(self):
  points=[];triangles=0;degenerate=0;normals_bad=0
  def visit(index,parent):
   nonlocal triangles,degenerate,normals_bad
   node=self.doc['nodes'][index]
   if 'matrix' in node:local=np.array(node['matrix']).reshape(4,4).T
   else:
    x,y,z,w=node.get('rotation',[0,0,0,1]);local=np.eye(4);local[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(node.get('scale',[1,1,1]));local[:3,3]=node.get('translation',[0,0,0])
   transform=parent@local
   if 'mesh' in node:
    for p in self.doc['meshes'][node['mesh']]['primitives']:
     assert p.get('mode',4)==4
     v=self.values(p['attributes']['POSITION']);assert np.isfinite(v).all();world=np.column_stack((v,np.ones(len(v))))@transform.T;points.extend(world[:,:3]);ix=self.values(p['indices']).reshape(-1,3);triangles+=len(ix)
     cross=np.cross(v[ix[:,1]]-v[ix[:,0]],v[ix[:,2]]-v[ix[:,0]]);degenerate+=int((np.linalg.norm(cross,axis=1)<1e-10).sum())
     n=self.values(p['attributes']['NORMAL']);normals_bad+=int((abs(np.linalg.norm(n,axis=1)-1)>1e-4).sum())
   for c in node.get('children',[]):visit(c,transform)
  for i in self.doc['scenes'][self.doc.get('scene',0)]['nodes']:visit(i,np.eye(4))
  points=np.array(points);lo=points.min(0);hi=points.max(0)
  return {'visual_bounds':{'position':lo.tolist(),'size':(hi-lo).tolist()},'triangles':triangles,'degenerate_triangles':degenerate,'invalid_normals':normals_bad,'materials':len(self.doc.get('materials',[]))}
REUSE=['bed','table','desk','nightstand','bookcase','wardrobe','dresser','sofa','armchair','sink','stove','fridge','washer','toilet','bath_vanity','wall_shelf','lamp']
def main():
 path=OUT/'manifest.json';package=json.loads(path.read_text());assets={a['id']:a for a in package['assets']}
 for name in REUSE:
  glb=GLB(ROOT/'game/assets/art/house'/f'{name}.glb');measured=glb.inspect();box=measured['visual_bounds'];p=np.array(box['position']);s=np.array(box['size']);offset=-(p+s*np.array([.5,0,.5]));normalized={'position':(-s*np.array([.5,0,.5])).tolist(),'size':s.tolist()}
  if name=='wall_shelf':offset[2]=-(p[2]+s[2]);normalized['position'][2]=-float(s[2])
  source=ROOT/'art_source/environments/house/v07'/f'{name}.blend'
  if not source.exists():source=ROOT/'art_source/environments/house'/f'{name}.blend'
  surfaces=[]
  if name in ['table','desk','nightstand']:
   h=.59 if name=='nightstand' else .72;surfaces=[{'center':[0,h,0],'size':[float(s[0])-.16,float(s[2])-.16],'edge_margin_m':.06}]
  assets['alfa_'+name]={'id':'alfa_'+name,'path':f'res://assets/art/house/{name}.glb','source':source.relative_to(ROOT).as_posix(),'source_kind':'reused_project','pivot':'back_base_center' if name=='wall_shelf' else 'base_center','front':'-Z' if name!='bed' else 'head at -X','placement_kind':'wall' if name=='wall_shelf' else 'ground','import_offset':offset.tolist(),'visual_bounds':normalized,'placement_bounds':normalized,'collision_boxes':[normalized],'support_surfaces':surfaces,'triangles':measured['triangles'],'material_slots':measured['materials'],'sha256':glb.sha,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'bytes':glb.bytes}
 for asset in assets.values():
  boxes=[asset['visual_bounds']]+asset['collision_boxes'];lo=np.min([b['position'] for b in boxes],axis=0);hi=np.max([np.array(b['position'])+b['size'] for b in boxes],axis=0);asset['placement_bounds']={'position':lo.tolist(),'size':(hi-lo).tolist()}
 package['assets']=list(assets.values());path.write_text(json.dumps(package,indent=2));(SOURCE/'manifest.json').write_text(json.dumps(package,indent=2))
 checks=0;failures=[];records=[]
 def check(ok,label):
  nonlocal checks
  checks+=1
  if not ok:failures.append(label)
 for asset in package['assets']:
  name=asset['id'];glb=GLB(ROOT/'game'/asset['path'].removeprefix('res://'));m=glb.inspect();declared=asset['visual_bounds'];offset=np.array(asset.get('import_offset',[0,0,0]));lo=np.array(m['visual_bounds']['position'])+offset;size=np.array(m['visual_bounds']['size']);end=lo+size
  check(np.max(abs(lo-declared['position']))<2e-5 and np.max(abs(size-declared['size']))<2e-5,name+' measured bounds match')
  check(abs(lo[1])<2e-5,name+' rests on base Y=0')
  check(glb.sha==asset['sha256'],name+' source digest')
  check(not glb.doc.get('skins') and not glb.doc.get('animations'),name+' static environment only')
  check(not any('uri' in b for b in glb.doc.get('buffers',[])),name+' self contained buffers')
  if asset['source_kind']=='original_alfa':
   check(m['triangles']<2500,name+' triangle budget')
   check(m['invalid_normals']==0 and m['degenerate_triangles']==0,name+' finite unit normals and nondegenerate faces')
   check(m['materials']<=8,name+' material budget')
   check(all(mat.get('alphaMode','OPAQUE')=='OPAQUE' and mat.get('pbrMetallicRoughness',{}).get('roughnessFactor',1)>=.75 for mat in glb.doc.get('materials',[])),name+' opaque matte materials')
  for collision in asset['collision_boxes']:
   c=np.array(collision['position']);cs=np.array(collision['size'])
   check(bool((cs>0).all() and np.isfinite(c).all() and np.isfinite(cs).all()),name+' valid physical volume')
   # Primitive AABBs can conservatively enclose beveled/faceted silhouettes.
   check(bool((c>=lo-.08).all() and (c+cs<=end+.08).all()),name+' physical volume near visible surface')
  for surface in asset['support_surfaces']:
   c=surface['center'];s=surface['size'];check(c[1]<=end[1]+2e-5 and c[1]>=0 and min(s)>.1,name+' usable support surface')
  records.append({'id':name,**m,'sha256':glb.sha})
 report={'checks':checks,'failures':failures,'assets':records,'scope':'Static GLB bytes, world-space AABBs, geometry/material policy, physical metadata. Import/render and placement integration require separate checks.'}
 (SOURCE/'validation').mkdir(exist_ok=True);(SOURCE/'validation/static.json').write_text(json.dumps(report,indent=2));print('ALFA_STATIC',checks,'failures',failures);print('ASSETS',[(a['id'],[round(v,3) for v in a['visual_bounds']['size']],a['triangles']) for a in package['assets']]);return bool(failures)
if __name__=='__main__':sys.exit(main())
