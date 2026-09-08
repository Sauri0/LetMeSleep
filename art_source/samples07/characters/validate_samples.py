"""Read-only checks on the actual exported staged GLBs and authored metadata."""
import json, math, struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
CHANNELS={'BlinkL','BlinkR','GazeX','GazeY','BrowUp','BrowDown','MouthOpen','MouthSmile','MouthPress','CheekLift'}
checks=0
def check(value, label):
    global checks
    checks+=1
    assert value, label

for variant in ['A','B']:
    for species in ['human','mosquito']:
        folder=ROOT/'game/assets/art/samples07/characters'/variant/species
        raw=(folder/(species+'_lms06.glb')).read_bytes()
        magic,version,length=struct.unpack_from('<III',raw)
        check((magic,version,length)==(0x46546C67,2,len(raw)),f'{variant}/{species}: GLB header')
        n,kind=struct.unpack_from('<II',raw,12)
        document=json.loads(raw[20:20+n]);check(kind==0x4E4F534A,'JSON chunk')
        offset=20+n;binary_size,binary_type=struct.unpack_from('<II',raw,offset)
        binary=raw[offset+8:offset+8+binary_size]
        check(binary_type==0x004E4942,'BIN chunk')
        metadata=json.loads((folder/'model.json').read_text())
        source=ROOT/'art_source/samples07/characters'/variant/species
        check((source/(species+'_lms06.blend')).stat().st_size>100000,'Editable Blender source exists')
        check(len(document.get('skins',[]))==1,'One coherent skin per species')
        check(len(document['skins'][0]['joints'])==len(metadata['bones']),'Exported bones match contract')
        faces=[mesh for mesh in document['meshes'] if mesh['name'].startswith(species+'_face_')]
        check(len(faces)==3,'Three editable face variants')
        for face in faces:
            check(set(face.get('extras',{}).get('targetNames',[]))==CHANNELS,face['name']+': shared facial controls')
            for primitive in face['primitives']:
                check(len(primitive.get('targets',[]))==10,'All material surfaces retain morphs')
        for accessor in document['accessors']:
            check(accessor['count']>0,'Nonempty accessor')
            if accessor['componentType']!=5126 or 'bufferView' not in accessor:continue
            view=document['bufferViews'][accessor['bufferView']]
            components={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[accessor['type']]
            stride=view.get('byteStride',components*4)
            start=view.get('byteOffset',0)+accessor.get('byteOffset',0)
            check(all(math.isfinite(v) for i in range(accessor['count']) for v in struct.unpack_from('<'+'f'*components,binary,start+i*stride)),'Finite positions/normals/weights/morphs')
        check(not list(source.glob('*.blend1')),'No backup Blender file in delivery')
        print(f'{variant}/{species}: {metadata["triangles_default"]} default triangles, {len(metadata["bones"])} bones, 3 faces x 10 controls')
print(f'SAMPLES07_ASSETS_RESULT checks={checks} failures=0')
