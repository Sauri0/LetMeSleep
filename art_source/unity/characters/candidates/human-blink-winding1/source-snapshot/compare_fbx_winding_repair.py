"""Compare preserved FBX geometry/skin/bone transforms/curve payloads after winding repair."""
import argparse
import hashlib
import importlib
import json
from pathlib import Path
import sys
import types
from inspect_fbx_morph_regions import child, array, label


def canonical(value):
    if isinstance(value, bytes):return value.hex()
    if isinstance(value, (str,int,float,bool)) or value is None:return value
    return [canonical(v) for v in value]


def element(node):
    return [node.id.decode(),canonical(node.props),[element(c) for c in node.elems]]


def collect(path, parser):
    tree,_=parser.parse(str(path));nodes=child(tree,'Objects').elems
    groups={'positions':[],'shapes':[],'skin':[],'bone_model_properties':[],'animation_curve_payloads':[],'materials':[]}
    meshes={}
    for n in nodes:
        if n.id==b'Geometry':
            if n.props[2]==b'Mesh':
                groups['positions'].append([label(n),array(n,'Vertices')])
                groups['materials'].append([label(n),[element(c) for c in n.elems if c.id==b'LayerElementMaterial']])
                faces=[];face=[]
                for i in array(n,'PolygonVertexIndex'):
                    face.append(i if i>=0 else -i-1)
                    if i<0:faces.append(face);face=[]
                meshes[label(n)]=faces
            elif n.props[2]==b'Shape':groups['shapes'].append([label(n),array(n,'Indexes'),array(n,'Vertices')])
        elif n.id==b'Deformer' and n.props[2]==b'Cluster':
            groups['skin'].append([label(n)]+[array(n,key) for key in ['Indexes','Weights','Transform','TransformLink']])
        elif n.id==b'Model':groups['bone_model_properties'].append([label(n),element(child(n,'Properties70'))])
        elif n.id==b'AnimationCurve':groups['animation_curve_payloads'].append([label(n),[element(c) for c in n.elems]])
    hashes={key:hashlib.sha256(json.dumps(sorted(values,key=lambda v:json.dumps(v)),separators=(',',':')).encode()).hexdigest()
            for key,values in groups.items()}
    return {'path':str(path.resolve()),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'counts':{k:len(v) for k,v in groups.items()},'hashes':hashes},meshes


def main():
    arg=argparse.ArgumentParser(description=__doc__)
    arg.add_argument('--parser-dir',type=Path,required=True);arg.add_argument('--before',type=Path,required=True)
    arg.add_argument('--after',type=Path,required=True);arg.add_argument('--output',type=Path,required=True)
    args=arg.parse_args()
    package=types.ModuleType('_compare_fbx');package.__path__=[str(args.parser_dir.resolve())];sys.modules[package.__name__]=package
    importlib.import_module(package.__name__+'.fbx_utils_threading')._MULTITHREADING_ENABLED=False
    parser=importlib.import_module(package.__name__+'.parse_fbx')
    before,old=collect(args.before,parser);after,new=collect(args.after,parser)
    unchanged={key:before['hashes'][key]==after['hashes'][key] for key in before['hashes']}
    assert set(old)==set(new)
    changed={}
    for name in old:
        assert len(old[name])==len(new[name])
        flipped=[]
        for i,(a,b) in enumerate(zip(old[name],new[name])):
            if a==b:continue
            r=list(reversed(a));assert any(b==r[k:]+r[:k] for k in range(len(r))),(name,i,'Unexpected topology')
            flipped.append(i)
        if flipped:changed[name]=flipped
    result={'before':before,'after':after,'unchanged':unchanged,'changed_faces':changed,
        'scope':'Exact raw coordinate/shape/skin/transform payloads and material assignments; animation curve payload multiset plus native per-action invariant. Normals and winding intentionally differ.'}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,indent=2),encoding='utf8',newline='\n')
    print(json.dumps({'unchanged':unchanged,'changed_faces':{k:len(v) for k,v in changed.items()}}))
    assert all(unchanged.values())
    assert set(changed)=={'HeadAuthoredPlanes'} and len(changed['HeadAuthoredPlanes'])==120


if __name__=='__main__':main()
