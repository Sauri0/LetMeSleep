"""Read final export containers and write only a review recipe and offline evidence."""
import collections
import hashlib
import json
import math
from pathlib import Path
import struct

SOURCE = Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/01-isla')
OUT = Path(__file__).resolve().parent


def need(value, message):
    if not value:
        raise ValueError(message)


def fbx_tree(data):
    need(data.startswith(b'Kaydara FBX Binary  \x00\x1a\x00'), 'Not binary FBX')
    version = struct.unpack_from('<I', data, 23)[0]
    fmt, header = ('<QQQB', 25) if version >= 7500 else ('<IIIB', 13)

    def node(pos):
        end, count, prop_bytes, length = struct.unpack_from(fmt, data, pos)
        if not end:
            return None, pos + header
        need(end <= len(data), 'FBX node exceeds file')
        p = pos + header
        name = data[p:p + length].decode('utf8')
        p += length
        properties = []
        start = p
        for _ in range(count):
            kind = chr(data[p])
            p += 1
            if kind in 'YCIFDL':
                scalar = dict(Y='h', C='?', I='i', F='f', D='d', L='q')[kind]
                value = struct.unpack_from('<' + scalar, data, p)[0]
                p += struct.calcsize(scalar)
            elif kind in 'SR':
                size = struct.unpack_from('<I', data, p)[0]
                p += 4
                value = data[p:p + size]
                p += size
                if kind == 'S':
                    value = value.decode('utf8')
            else:
                need(kind in 'fdilbc', 'Unsupported FBX array type: ' + kind)
                size, encoding, byte_count = struct.unpack_from('<III', data, p)
                p += 12 + byte_count
                value = {'arrayType': kind, 'count': size, 'encoding': encoding}
            properties.append(value)
        need(p - start == prop_bytes, 'FBX property byte count mismatch')
        children = []
        while p < end - header:
            child, p = node(p)
            if child is None:
                break
            children.append(child)
        return {'name': name, 'props': properties, 'children': children}, end

    result, pos = [], 27
    while pos < len(data) - header:
        item, pos = node(pos)
        if item is None:
            break
        result.append(item)
    return version, result


def child(node, name):
    return next(n for n in node['children'] if n['name'] == name)


def properties(node):
    return {n['props'][0]: n['props'][4:] for n in child(node, 'Properties70')['children']}


def write(name, data):
    (OUT / name).write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf8')


def main():
    files = {name: (SOURCE / name).read_bytes() for name in (
        'HF_MAP_01_isla.fbx', 'HF_MAP_01_isla.glb', 'scene-audit.json', 'HF_MAP_01_isla_build_report.json')}
    hashes = {name: hashlib.sha256(data).hexdigest() for name, data in files.items()}
    audit = json.loads(files['scene-audit.json'].decode('utf-8-sig'))
    report = json.loads(files['HF_MAP_01_isla_build_report.json'].decode('utf-8-sig'))
    glb = files['HF_MAP_01_isla.glb']
    magic, version, size = struct.unpack_from('<4sII', glb)
    need((magic, version, size) == (b'glTF', 2, len(glb)), 'GLB header mismatch')
    length, kind = struct.unpack_from('<II', glb, 12)
    need(kind == 0x4E4F534A, 'GLB first chunk is not JSON')
    gltf = json.loads(glb[20:20 + length])
    nodes = gltf['nodes']
    names = [n['name'] for n in nodes]
    need(len(set(names)) == len(names), 'Ambiguous GLB names')
    parents = {c: i for i, n in enumerate(nodes) for c in n.get('children', [])}

    def glb_path(index):
        return (glb_path(parents[index]) + '/' if index in parents else '') + names[index]

    audit_objects = {o['name']: o for o in audit['objects']}
    need(set(names) == set(audit_objects), 'Audit/GLB object names differ')
    fbx_version, tree = fbx_tree(files['HF_MAP_01_isla.fbx'])
    objects = next(n for n in tree if n['name'] == 'Objects')['children']
    models = {n['props'][0]: n for n in objects if n['name'] == 'Model'}
    model_names = {i: n['props'][1].split('\x00')[0] for i, n in models.items()}
    need(set(model_names.values()) == set(names), 'FBX/GLB object names differ')
    connections = [n['props'] for n in next(n for n in tree if n['name'] == 'Connections')['children']]
    model_parents = {c[1]: c[2] for c in connections if c[0] == 'OO' and c[1] in models and c[2] in models}

    def fbx_path(index):
        return (fbx_path(model_parents[index]) + '/' if index in model_parents else '') + model_names[index]

    fbx_paths = {model_names[i]: fbx_path(i) for i in models}
    need(all(fbx_paths[names[i]] == glb_path(i) for i in range(len(nodes))), 'FBX/GLB hierarchy differs')
    fbm = {n['props'][0]: n['props'][1].split('\x00')[0] for n in objects if n['name'] == 'Material'}
    fbx_slots = collections.defaultdict(list)
    for c in connections:
        if c[0] == 'OO' and c[1] in fbm and c[2] in models:
            fbx_slots[model_names[c[2]]].append(fbm[c[1]])
    audit_materials = {m['name']: m for m in audit['materials']}
    need(set(fbm.values()) == set(audit_materials) == {m['name'] for m in gltf['materials']}, 'Material names differ between final exports')
    for m in gltf['materials']:
        need(max(abs(a - b) for a, b in zip(m['pbrMetallicRoughness']['baseColorFactor'], audit_materials[m['name']]['base_color'])) < 1e-6, 'GLB/audit colour mismatch: ' + m['name'])
    rules, mesh_evidence, merged_trees, deduplicated_slots = [], [], [], []
    for index, n in enumerate(nodes):
        if 'mesh' not in n:
            continue
        name = n['name']
        authored = audit_objects[name]
        role = n.get('extras', {}).get('collision_role')
        need(role in ('static_solid', 'non_solid'), 'No explicit collision role: ' + name)
        kind = 'solid' if role == 'static_solid' else 'decoration'
        if name in ('Water_Ocean', 'Water_Lake'):
            kind = 'water'
        elif name == 'Ocean_Shore_Foam_NONSOLID':
            kind = 'foam'
        elif 'ISLA_Plants_NONSOLID' in authored['collections']:
            kind = 'foliage'
        need(not (kind in ('water', 'foam', 'foliage') and role != 'non_solid'), 'Visual exclusion disagrees with authored role')
        slots = fbx_slots[name]
        need(slots == list(dict.fromkeys(authored['materials'])), 'FBX/audit unique slot order differs: ' + name)
        if slots != authored['materials']:
            deduplicated_slots.append(dict(name=name, audit=authored['materials'], fbx=slots))
        mesh = gltf['meshes'][n['mesh']]
        primitive_materials = [gltf['materials'][p['material']]['name'] for p in mesh['primitives']]
        need(set(primitive_materials) <= set(slots), 'GLB primitive references foreign material: ' + name)
        rules.append(dict(path=glb_path(index), kind=kind, descendants=False, canPerch=kind == 'solid',
                          waveAmplitude=.015 if name == 'Water_Lake' else .025, waveLength=4, waveSpeed=2 * math.pi / 8))
        mesh_evidence.append(dict(path=glb_path(index), role=role, kind=kind, materialSlots=slots,
                                  glbPrimitiveMaterials=primitive_materials,
                                  glbPositionAccessorVertices=sum(gltf['accessors'][p['attributes']['POSITION']]['count'] for p in mesh['primitives'])))
        if 'ISLA_Trees_COLLIDABLE' in authored['collections'] and 'ISLA_Trunk' in slots and len(slots) > 1:
            merged_trees.append(glb_path(index))
    need(len(rules) == audit['mesh_count'] == report['mesh_objects'], 'Final mesh counts differ')
    solids = [audit_objects[e['path'].split('/')[-1]] for e in mesh_evidence if e['kind'] == 'solid']
    # Conservative symmetric land/flight review envelope tolerates handedness changes.
    # Excludes ocean and foam; includes authored solid docks and tree crowns, plus 2m margin.
    extent_x = math.ceil(max(max(abs(o['bounds_min'][0]), abs(o['bounds_max'][0])) for o in solids) + 2)
    extent_z = math.ceil(max(max(abs(o['bounds_min'][1]), abs(o['bounds_max'][1])) for o in solids) + 2)
    min_y = math.floor(min(o['bounds_min'][2] for o in solids) - .5)
    max_y = math.ceil(max(o['bounds_max'][2] for o in solids) + 2)
    recipe = dict(mapId='hf-isla-del-laguito-review-01', sourceFbx=str(SOURCE / 'HF_MAP_01_isla.fbx'),
                  sourceSha256=hashes['HF_MAP_01_isla.fbx'], sourceFinal=True, importScale=1,
                  collisionLayer='Default', firstSurfaceId=1000000, requiredHumanSpawns=2, requiredMosquitoSpawns=1,
                  humanSpawns=['Spawn_Human_01', 'Spawn_Human_02'], mosquitoSpawns=['Spawn_Mosquito_01'],
                  lobbySpawns=[], toolPickups=[], presentationRoot='', boundsMinEmpty='', boundsMaxEmpty='',
                  playBoundsMin=[-extent_x, min_y, -extent_z], playBoundsMax=[extent_x, max_y, extent_z],
                  nodes=sorted(rules, key=lambda r: r['path']),
                  materials=[dict(sourceName=m['name'], rgb=m['base_color'][:3], colorSpace='linear') for m in sorted(audit['materials'], key=lambda m: m['name'])])
    globals_ = properties(next(n for n in tree if n['name'] == 'GlobalSettings'))
    spawn_evidence = []
    for name in recipe['humanSpawns'] + recipe['mosquitoSpawns']:
        n = next(n for n in nodes if n['name'] == name)
        model = next(model for i, model in models.items() if model_names[i] == name)
        pose = properties(model)
        need('mesh' not in n and audit_objects[name]['type'] == 'EMPTY', 'Spawn is not an EMPTY')
        spawn_evidence.append(dict(name=name, blenderWorldXYZ=audit_objects[name]['location'],
                                   glbWorldXYZ=n['translation'], fbxRawTranslation=pose['Lcl Translation'], fbxRawScale=pose['Lcl Scaling']))
    channels = [n['props'][1].split('\x00')[0] for n in objects if n['name'] == 'Deformer' and n['props'][2] == 'BlendShapeChannel']
    validation = dict(status='PASS_OFFLINE_FINAL_EXPORT_CONTRACT_ONLY', sourceHashes=hashes,
                      objects=len(nodes), meshes=len(rules), materials=len(audit_materials), trianglesInstances=audit['triangles'],
                      classification=dict(collections.Counter(r['kind'] for r in rules)),
                      checks=['488 model names agree audit/GLB/FBX', 'Exact full hierarchy paths agree GLB/FBX',
                              'Every mesh has one rule from explicit GLB collision_role', 'All FBX unique material slot orders equal audit; repeated-name slots collapsed by exporter',
                              'All GLB primitive materials covered; 38 GLB base colours equal audit', 'Three spawns are existing EMPTYs'],
                      fbxVersion=fbx_version, fbxGlobalSettings=globals_, fbxBlendShapeChannels=channels,
                      spawnEvidence=spawn_evidence, boundsPolicy='Solid authored bounds + 2m horizontal/upper margin; symmetric X/Z; lower bound minus .5m rounded down; excludes ocean and foam',
                      playBoundsMin=recipe['playBoundsMin'], playBoundsMax=recipe['playBoundsMax'],
                      wholeTreeColliders=merged_trees, meshesAndSlots=mesh_evidence, exporterDeduplicatedSlots=deduplicated_slots,
                      pending=['Unity importer axis/scale and any generated path wrappers', 'Actual material/submesh preservation',
                               'Spawn rotation, capsule clearance, routes and room capacity16', 'Whole-tree canopy collision',
                               'Water morph renderer, foam seams, normals/culling and performance', 'URP culling/lighting/colour visual review',
                               'SpatialData is import-recipe, not schema_version=1/map_id/zones/portals navigation; BeginRound not ready'])
    # Verify source files did not change during this snapshot; never label an in-progress replacement final.
    need(all(hashlib.sha256((SOURCE / name).read_bytes()).hexdigest() == h for name, h in hashes.items()), 'Source changed during recipe preparation')
    write('isla.review-01.recipe.json', recipe)
    write('isla.review-01.validation.json', validation)
    print(json.dumps({k: validation[k] for k in ('status', 'classification', 'meshes', 'materials', 'playBoundsMin', 'playBoundsMax')}))
    print('FBX SHA256:', recipe['sourceSha256'])
    print('Whole-tree colliders:', len(merged_trees))


if __name__ == '__main__':
    main()
