"""Read-only binary FBX geometry inspection; standard library, no Blender/Unity."""
import argparse, hashlib, json, math, pathlib, struct, zlib

def read_fbx(path):
    data = pathlib.Path(path).read_bytes()
    if not data.startswith(b'Kaydara FBX Binary  \x00\x1a\x00'):
        raise ValueError('Expected binary FBX')
    version = struct.unpack_from('<I', data, 23)[0]
    wide = version >= 7500
    header = '<QQQB' if wide else '<IIIB'
    size = struct.calcsize(header)

    def prop(pos):
        kind = chr(data[pos]); pos += 1
        scalar = {'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}
        array = {'f':'f','d':'d','l':'q','i':'i','b':'b','c':'b'}
        if kind in scalar:
            fmt = '<'+scalar[kind]
            return struct.unpack_from(fmt, data, pos)[0], pos+struct.calcsize(fmt)
        if kind in ('S','R'):
            n = struct.unpack_from('<I',data,pos)[0];pos+=4
            raw=data[pos:pos+n]
            return raw.decode('utf8','replace') if kind=='S' else raw.hex(),pos+n
        if kind in array:
            count,enc,n=struct.unpack_from('<III',data,pos);pos+=12
            raw=data[pos:pos+n]
            if enc:raw=zlib.decompress(raw)
            return list(struct.unpack('<'+str(count)+array[kind],raw)),pos+n
        raise ValueError('Unsupported FBX property '+kind)

    def node(pos):
        end,count,_,length=struct.unpack_from(header,data,pos)
        if end==0:return None,pos+size
        pos+=size;name=data[pos:pos+length].decode();pos+=length
        values=[]
        for _ in range(count):value,pos=prop(pos);values.append(value)
        children=[]
        while pos<end-size:
            child,pos=node(pos)
            if child:children.append(child)
            else:break
        return {'name':name,'values':values,'children':children},end
    nodes=[];pos=27
    while pos<len(data)-size:
        n,pos=node(pos)
        if not n:break
        nodes.append(n)
    return version,nodes,hashlib.sha256(data).hexdigest()

def child(n,name):return next(x for x in n['children'] if x['name']==name)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('fbx');parser.add_argument('output');args=parser.parse_args()
    version,nodes,sha=read_fbx(args.fbx)
    objects=child({'children':nodes},'Objects')['children']
    connections=child({'children':nodes},'Connections')['children']
    out=pathlib.Path(args.output);out.mkdir(parents=True,exist_ok=True)
    summaries=[]
    for obj in objects:
        if obj['name']!='Geometry' or not any('Path_South_Arrival' in str(x) for x in obj['values']):continue
        flat=child(obj,'Vertices')['values'][0];vertices=[flat[i:i+3] for i in range(0,len(flat),3)]
        polygon=[];polygons=[]
        for idx in child(obj,'PolygonVertexIndex')['values'][0]:
            polygon.append(idx if idx>=0 else -idx-1)
            if idx<0:polygons.append(polygon);polygon=[]
        ids=[c['values'][2] for c in connections if c['values'][0]=='OO' and c['values'][1]==obj['values'][0]]
        models=[x for x in objects if x['name']=='Model' and x['values'][0] in ids]
        result={'source':args.fbx,'sourceSha256':sha,'fbxVersion':version,'identity':obj['values'],'vertices':vertices,'polygons':polygons,'modelRecords':models,'layerRecords':[x for x in obj['children'] if x['name'].startswith('LayerElement')], 'boundsMin':[min(v[a] for v in vertices) for a in range(3)],'boundsMax':[max(v[a] for v in vertices) for a in range(3)]}
        target=out/'south-arrival-source-mesh.json';target.write_text(json.dumps(result,indent=2),encoding='utf8')
        summaries.append({'identity':obj['values'],'vertices':len(vertices),'polygons':len(polygons),'boundsMin':result['boundsMin'],'boundsMax':result['boundsMax'],'models':models,'output':str(target)})
    if not summaries:raise ValueError('No Path_South_Arrival geometry found')
    print(json.dumps(summaries,indent=2))

if __name__=='__main__':main()
