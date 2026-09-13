"""Prepare a map recipe only after explicit final-source confirmation. No native APIs."""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
import struct
import re

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
            if kind in 'ZYBCIFDL':
                # Blender 5.2 io_scene_fbx: B=bool, C=char, Z=signed byte.
                # Uppercase scalar types must not enter the lowercase array branch.
                scalar = dict(Z='b', Y='h', B='?', C='c', I='i', F='f', D='d', L='q')[kind]
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


def validate_config(config):
    need(config.get('sourceFinal') is True, 'Source not confirmed final; no source files will be read')
    need(re.fullmatch(r'hf-[a-z0-9]+(?:-[a-z0-9]+)*', config.get('mapId', '')), 'Invalid mapId')
    for key in ('fbx', 'glb', 'audit', 'report'):
        need(isinstance(config.get(key), str) and config[key].strip(), 'Missing source path: ' + key)
    for key, default in (('humanCount', 5), ('mosquitoCount', 16)):
        value = config.get(key, default)
        need(type(value) is int and value > 0, 'Invalid capacity: ' + key)
    need(isinstance(config.get('collisionLayer', 'Default'), str) and config.get('collisionLayer', 'Default').strip(), 'Missing collision layer')
    need(type(config.get('firstSurfaceId', 1000000)) is int and 0 < config.get('firstSurfaceId', 1000000) <= 4294967295, 'Invalid firstSurfaceId')
    if 'sourceSha256' in config:
        need(re.fullmatch('[a-fA-F0-9]{64}', config['sourceSha256']), 'Invalid expected sourceSha256')
    need(isinstance(config.get('kinds', {}), dict), 'kinds must map exact paths to categories')
    if 'expectedScene' in config:
        need(isinstance(config['expectedScene'], str) and config['expectedScene'].strip(), 'Invalid expected scene')
    if 'expectedWaterCount' in config:
        need(type(config['expectedWaterCount']) is int and config['expectedWaterCount'] >= 0, 'Invalid expected water/foam count')
    if 'playBoundsMin' in config or 'playBoundsMax' in config:
        low, high = config.get('playBoundsMin'), config.get('playBoundsMax')
        need(isinstance(low, list) and isinstance(high, list) and len(low) == len(high) == 3, 'Both XYZ bounds required')
        need(all(math.isfinite(a) and math.isfinite(b) and b > a for a, b in zip(low, high)), 'Invalid bounds')


def world_position(authored):
    matrix = authored.get('matrix_world')
    need(isinstance(matrix, list) and len(matrix) == 4 and all(len(row) == 4 for row in matrix), 'Missing world matrix: ' + authored['name'])
    need(all(math.isfinite(v) for row in matrix for v in row), 'Non-finite world matrix: ' + authored['name'])
    return [matrix[i][3] for i in range(3)]


def classify(authored, glb_node, path, overrides):
    props = authored.get('properties', {})
    role = props.get('collision_role')
    need(role in ('static_solid', 'non_solid'), 'Missing/unsupported audit properties.collision_role: ' + path)
    exported = glb_node.get('extras', {}).get('collision_role')
    need(exported is None or exported == role, 'Audit/GLB collision role conflict: ' + path)
    explicit = overrides.get(path, props.get('environment_kind'))
    if explicit is not None:
        kind = explicit
    elif role == 'static_solid':
        kind = 'solid'
    elif 'wave_loop_seconds' in props or authored['name'].lower().startswith('water_'):
        kind = 'water'
    elif 'foam' in authored['name'].lower():
        kind = 'foam'
    else:
        kind = 'decoration'  # Non-solid canopy/grass still casts shadows in this importer.
    need(kind in ('solid', 'water', 'foam', 'foliage', 'decoration'), 'Unsupported kind: ' + path)
    need((kind == 'solid') == (role == 'static_solid'), 'Visual kind cannot override authored collision role: ' + path)
    return role, kind


def select_spawns(audit_objects, paths, prefix, count):
    objects = sorted((o for o in audit_objects.values() if o['name'].startswith(prefix)), key=lambda o: o['name'])
    need(len(objects) >= count, f'Need {count} {prefix} markers; found {len(objects)}')
    need(all(o['type'] == 'EMPTY' for o in objects), 'Spawn prefix belongs to geometry: ' + prefix)
    return [(paths[o['name']], world_position(o)) for o in objects]


def emission_fields(gltf_material, audited):
    name = gltf_material['name']
    need('emissiveTexture' not in gltf_material, 'Textured emission is outside the flat-colour contract: ' + name)
    factor = gltf_material.get('emissiveFactor', [0, 0, 0])
    strength = gltf_material.get('extensions', {}).get('KHR_materials_emissive_strength', {}).get('emissiveStrength', 1)
    need(isinstance(factor, list) and len(factor) == 3 and all(math.isfinite(v) and 0 <= v <= 3.402823466e38 for v in factor), 'Invalid GLB emission RGB: ' + name)
    need(math.isfinite(strength) and 0 <= strength <= 3.402823466e38, 'Invalid GLB emission strength: ' + name)
    exported = [v * strength for v in factor]
    need(all(math.isfinite(v) and v <= 3.402823466e38 for v in exported), 'GLB emission product overflows float: ' + name)
    has_color, has_strength = 'emission_color' in audited, 'emission_strength' in audited
    need(has_color == has_strength, 'Audit must provide both emission fields: ' + name)
    if has_color:
        color, authored_strength = audited['emission_color'], audited['emission_strength']
        need(isinstance(color, list) and len(color) in (3, 4) and all(math.isfinite(v) and v >= 0 for v in color), 'Invalid audit emission colour: ' + name)
        need(math.isfinite(authored_strength) and authored_strength >= 0, 'Invalid audit emission strength: ' + name)
        authored = [v * authored_strength for v in color[:3]]
        need(all(math.isfinite(v) for v in authored), 'Audit emission overflows: ' + name)
        need(all(math.isclose(a, b, rel_tol=1e-5, abs_tol=1e-6) for a, b in zip(authored, exported)), 'GLB/audit emission mismatch: ' + name)
    else:
        need(not any(exported), 'Emissive GLB needs the updated emission audit: ' + name)
    return dict(emissionRgb=factor if any(exported) else [0, 0, 0], emissionStrength=strength if any(exported) else 0)


def surface_fields(gltf_material, audited):
    mode = gltf_material.get('alphaMode', 'OPAQUE')
    need(mode in ('OPAQUE', 'BLEND'), 'Unsupported alpha mode: ' + mode)
    rgba = gltf_material['pbrMetallicRoughness']['baseColorFactor']
    color = audited['base_color']
    alpha = audited.get('alpha', color[3])
    need(len(rgba) == 4 and math.isfinite(alpha) and 0 < alpha <= 1, 'Invalid material alpha')
    need(max(abs(a - b) for a, b in zip(rgba, color[:3] + [alpha])) < 1e-6, 'GLB/audit RGBA mismatch: ' + gltf_material['name'])
    need(mode != 'BLEND' or 'alpha' in audited, 'Transparent material requires audited Principled Alpha')
    need(mode != 'OPAQUE' or alpha == 1, 'Opaque material has non-unit opacity')
    return dict(alphaMode=mode, opacity=alpha, doubleSided=mode == 'BLEND' and gltf_material.get('doubleSided', False))


def validate_nested_report_counts(report, audit, object_count, mesh_count, material_count, human_count, mosquito_count):
    """Compare explicitly reported export counts; scene counts include preserved exclusions."""
    if 'counts' not in report:
        return
    counts = report['counts']
    need(isinstance(counts, dict), 'Report counts must be an object')
    expected = dict(export_objects=object_count, export_mesh_objects=mesh_count, materials=material_count,
                    human_spawns=human_count, mosquito_spawns=mosquito_count)
    expected.update(objects=object_count, mesh_objects=mesh_count,
                    triangles_instanced=sum(o.get('triangles', 0) for o in audit['objects'] if o['type'] == 'MESH'))
    if 'triangles_instanced_total' in counts:
        meshes = [o for o in audit['objects'] if o['type'] == 'MESH']
        need(all(type(o.get('triangles')) is int and o['triangles'] >= 0 for o in meshes), 'Missing audited triangle count')
        expected['triangles_instanced_total'] = sum(o['triangles'] for o in meshes)
    if 'scene_objects' in counts:
        excluded = audit.get('excluded_objects', [])
        exported_names = {o['name'] for o in audit['objects']}
        excluded_names = [o['name'] for o in excluded]
        need(len(set(excluded_names)) == len(excluded_names) and not exported_names.intersection(excluded_names), 'Ambiguous exported/excluded object list')
        expected['scene_objects'] = object_count + len(excluded)
        need(audit.get('source_object_count', expected['scene_objects']) == expected['scene_objects'], 'Audit source object count differs')
    for key, actual in expected.items():
        if key in counts:
            need(type(counts[key]) is int and counts[key] == actual, 'Report nested count differs: ' + key)
    if 'collision_roles' in counts:
        actual = dict(collections.Counter(o['properties']['collision_role'] for o in audit['objects'] if o['type'] == 'MESH'))
        need(counts['collision_roles'] == actual, 'Report collision role counts differ')


def prepare(config, config_dir):
    validate_config(config)  # Finality and counts checked before touching export files.
    source_paths = {key: (config_dir / config[key]).resolve() for key in ('fbx', 'glb', 'audit', 'report')}
    files = {key: path.read_bytes() for key, path in source_paths.items()}
    hashes = {key: hashlib.sha256(data).hexdigest() for key, data in files.items()}
    need('sourceSha256' not in config or hashes['fbx'] == config['sourceSha256'].lower(), 'Expected FBX hash mismatch')
    audit = json.loads(files['audit'].decode('utf-8-sig'))
    report = json.loads(files['report'].decode('utf-8-sig'))
    summary = report.get('checks', report)
    need(isinstance(summary, dict), 'Report checks must be an object')
    if 'expectedScene' in config:
        need(audit.get('scene') == config['expectedScene'], 'Audit belongs to a different scene')
        need(report.get('scene', summary.get('scene', report.get('map'))) == config['expectedScene'], 'Report belongs to a different scene')
    glb = files['glb']
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
    need(len(audit_objects) == len(audit['objects']), 'Ambiguous audit names')
    need(set(names) == set(audit_objects), 'Audit/GLB object names differ')
    fbx_version, tree = fbx_tree(files['fbx'])
    objects = next(n for n in tree if n['name'] == 'Objects')['children']
    models = {n['props'][0]: n for n in objects if n['name'] == 'Model'}
    model_names = {i: n['props'][1].split('\x00')[0] for i, n in models.items()}
    need(len(model_names) == len(names) and set(model_names.values()) == set(names), 'FBX/GLB object names differ')
    connections = [n['props'] for n in next(n for n in tree if n['name'] == 'Connections')['children']]
    model_parents = {c[1]: c[2] for c in connections if c[0] == 'OO' and c[1] in models and c[2] in models}

    def fbx_path(index):
        return (fbx_path(model_parents[index]) + '/' if index in model_parents else '') + model_names[index]

    paths = {model_names[i]: fbx_path(i) for i in models}
    need(all(paths[names[i]] == glb_path(i) for i in range(len(nodes))), 'FBX/GLB hierarchy differs')
    fbm = {n['props'][0]: n['props'][1].split('\x00')[0] for n in objects if n['name'] == 'Material'}
    fbx_slots = collections.defaultdict(list)
    for c in connections:
        if c[0] == 'OO' and c[1] in fbm and c[2] in models:
            fbx_slots[model_names[c[2]]].append(fbm[c[1]])
    audit_materials = {m['name']: m for m in audit['materials']}
    need(len(audit_materials) == len(audit['materials']), 'Ambiguous audit materials')
    need(set(fbm.values()) == set(audit_materials) == {m['name'] for m in gltf['materials']}, 'Material names differ between final exports')
    emissions, surfaces = {}, {}
    for m in gltf['materials']:
        color = audit_materials[m['name']]['base_color']
        need(len(color) == 4 and all(math.isfinite(v) and 0 <= v <= 1 for v in color), 'Invalid audit RGBA: ' + m['name'])
        surfaces[m['name']] = surface_fields(m, audit_materials[m['name']])
        emissions[m['name']] = emission_fields(m, audit_materials[m['name']])
    rules, mesh_evidence, deduplicated_slots = [], [], []
    overrides = config.get('kinds', {})
    for index, n in enumerate(nodes):
        if 'mesh' not in n:
            continue
        name, path = n['name'], glb_path(index)
        authored = audit_objects[name]
        world_position(authored)  # Matrix presence/finite check; never substitute local location.
        role, kind = classify(authored, n, path, overrides)
        slots = fbx_slots[name]
        need(slots == authored['materials'] or slots == list(dict.fromkeys(authored['materials'])), 'FBX/audit slot order differs: ' + name)
        if slots != authored['materials']:
            deduplicated_slots.append(dict(name=name, audit=authored['materials'], fbx=slots))
        mesh = gltf['meshes'][n['mesh']]
        primitive_materials = [gltf['materials'][p['material']]['name'] for p in mesh['primitives']]
        need(set(primitive_materials) <= set(slots), 'GLB primitive references foreign material: ' + name)
        props = authored['properties']
        period = props.get('wave_loop_seconds', props.get('wave_period_seconds', 8))
        amplitude = props.get('wave_amplitude', props.get('wave_amplitude_m', .025))
        wavelength = props.get('wave_length', 4)
        if kind == 'water' and 'wave_params' in props:
            wave = json.loads(props['wave_params']) if isinstance(props['wave_params'], str) else props['wave_params']
            need(isinstance(wave, dict), 'Wave parameters must be an object')
            period = props.get('wave_loop_seconds', wave.get('loop_seconds', period))
            if 'Wave_A' in wave and 'Wave_B' in wave:
                # Preserve the source amplitude envelope/period; the shared runtime uses its own two-sine directions.
                amplitude = props.get('wave_amplitude', sum(wave[k]['amplitude'] for k in ('Wave_A', 'Wave_B')))
                frequency = math.hypot(wave['Wave_A']['kx'], wave['Wave_A']['ky'])
                need(frequency > 0 and math.isfinite(frequency), 'Invalid authored wave frequency')
                wavelength = props.get('wave_length', 2 * math.pi / frequency)
        need(math.isfinite(period) and period > 0 and math.isfinite(amplitude) and 0 <= amplitude <= .15 and math.isfinite(wavelength) and wavelength > 0, 'Invalid wave properties: ' + path)
        rules.append(dict(path=path, kind=kind, descendants=False, canPerch=kind == 'solid' and props.get('can_perch', True) is not False,
                            waveAmplitude=amplitude, waveLength=wavelength, waveSpeed=2 * math.pi / period))
        if path in config.get('gpuWaterPaths', []):
            need(kind in ('water', 'foam'), 'GPU water path must be visual water/foam: ' + path)
            rules[-1]['waterAnimation'] = 'gpu'
        mesh_evidence.append(dict(path=path, role=role, kind=kind, materialSlots=slots,
                                  glbPrimitiveMaterials=primitive_materials,
                                  glbPositionAccessorVertices=sum(gltf['accessors'][p['attributes']['POSITION']]['count'] for p in mesh['primitives'])))
    need(set(overrides) <= {r['path'] for r in rules}, 'Kind override does not match a mesh path')
    need(set(config.get('gpuWaterPaths', [])) <= {r['path'] for r in rules}, 'GPU water path does not match a mesh path')
    if 'expectedWaterCount' in config:
        need(sum(r['kind'] in ('water', 'foam') for r in rules) == config['expectedWaterCount'], 'Unexpected water/foam geometry classification')
    actual_mesh_count = sum(o['type'] == 'MESH' for o in audit_objects.values())
    need(len(rules) == actual_mesh_count > 0, 'Audit/export mesh counts differ')
    if 'mesh_count' in audit:
        need(audit['mesh_count'] == actual_mesh_count, 'Audit summary mesh count differs')
    if 'mesh_objects' in summary:
        need(summary['mesh_objects'] == actual_mesh_count, 'Report mesh count differs')
    if 'objects' in summary:
        need(summary['objects'] == len(nodes), 'Report object count differs')
    solids = [audit_objects[e['path'].split('/')[-1]] for e in mesh_evidence if e['kind'] == 'solid']
    need(solids, 'No explicit static solids')
    need(config.get('firstSurfaceId', 1000000) + len(solids) - 1 <= 4294967295, 'Surface IDs overflow uint')
    human = select_spawns(audit_objects, paths, config.get('humanPrefix', 'Spawn_Human_'), config.get('humanCount', 5))
    mosquito = select_spawns(audit_objects, paths, config.get('mosquitoPrefix', 'Spawn_Mosquito_'), config.get('mosquitoCount', 16))
    validate_nested_report_counts(report, audit, len(nodes), len(rules), len(audit_materials), len(human), len(mosquito))
    spawns = human + mosquito
    need(len({path for path, _ in spawns}) == len(spawns), 'Cross-role spawn marker overlap')
    for i, (path, position) in enumerate(spawns):
        for other, other_position in spawns[i + 1:]:
            need(math.dist(position, other_position) > .1, 'Overlapping spawn origins: ' + path + '/' + other)
    if 'playBoundsMin' in config:
        low, high = config['playBoundsMin'], config['playBoundsMax']
        bounds_policy = 'Explicit Unity XYZ bounds from final input config'
    else:
        for o in solids:
            need(len(o.get('bounds_min', [])) == len(o.get('bounds_max', [])) == 3, 'World bounds required: ' + o['name'])
            need(all(math.isfinite(a) and math.isfinite(b) and b >= a for a, b in zip(o['bounds_min'], o['bounds_max'])), 'Invalid world bounds: ' + o['name'])
        extent_x = math.ceil(max(max(abs(o['bounds_min'][0]), abs(o['bounds_max'][0])) for o in solids) + 2)
        extent_z = math.ceil(max(max(abs(o['bounds_min'][1]), abs(o['bounds_max'][1])) for o in solids) + 2)
        min_y = math.floor(min(o['bounds_min'][2] for o in solids) - .5)
        max_y = math.ceil(max(o['bounds_max'][2] for o in solids) + 2)
        low, high = [-extent_x, min_y, -extent_z], [extent_x, max_y, extent_z]
        bounds_policy = 'Solid world AABBs only; symmetric X/Z +2m, Y -0.5/+2m rounded out; Blender XYZ to Unity XZY; excludes non-solid water'
    for path, position in spawns:
        unity_position = [position[0], position[2], position[1]]
        need(all(a <= v <= b for a, b, v in zip(low, high, unity_position)), 'Spawn outside bounds: ' + path)
    recipe = dict(mapId=config['mapId'], sourceFbx=str(source_paths['fbx']), sourceSha256=hashes['fbx'], sourceFinal=True,
                  importScale=1, collisionLayer=config.get('collisionLayer', 'Default'), firstSurfaceId=config.get('firstSurfaceId', 1000000),
                  requiredHumanSpawns=config.get('humanCount', 5), requiredMosquitoSpawns=config.get('mosquitoCount', 16),
                  humanSpawns=[p for p, _ in human], mosquitoSpawns=[p for p, _ in mosquito],
                  lobbySpawns=[], toolPickups=[], presentationRoot='', boundsMinEmpty='', boundsMaxEmpty='',
                  playBoundsMin=low, playBoundsMax=high, nodes=sorted(rules, key=lambda r: r['path']),
                  materials=[dict(sourceName=m['name'], rgb=m['base_color'][:3], colorSpace='linear', **emissions[m['name']], **surfaces[m['name']]) for m in sorted(audit['materials'], key=lambda m: m['name'])])
    channels = [n['props'][1].split('\x00')[0] for n in objects if n['name'] == 'Deformer' and n['props'][2] == 'BlendShapeChannel']
    validation = dict(status='PASS_OFFLINE_FINAL_EXPORT_CONTRACT_ONLY', mapId=config['mapId'],
                      sources={key: dict(path=str(source_paths[key]), sha256=hashes[key]) for key in source_paths},
                      objects=len(nodes), meshes=len(rules), materials=len(audit_materials),
                      classification=dict(collections.Counter(r['kind'] for r in rules)),
                      checks=['All names/full paths agree audit/GLB/FBX', 'Every mesh classified from audit properties.collision_role; GLB role conflicts rejected',
                              'Present nested report export/scene/triangle/material/spawn/collision counts agree with audit and exports',
                              'FBX unique material slots equal audit; duplicates recorded', 'All GLB primitive materials covered and base colours agree',
                              'Required EMPTY spawn counts, finite world matrices, distinct world origins and bounds checked'],
                      fbxVersion=fbx_version, fbxGlobalSettings=properties(next(n for n in tree if n['name'] == 'GlobalSettings')),
                      fbxBlendShapeChannels=channels, meshesAndSlots=mesh_evidence, exporterDeduplicatedSlots=deduplicated_slots,
                      emissionChecks=emissions,
                      humanSpawns=len(human), mosquitoSpawns=len(mosquito),
                      spawnEvidence=[dict(path=p, blenderWorldXYZ=v, unityExpectedXYZ=[v[0], v[2], v[1]]) for p, v in spawns],
                      boundsPolicy=bounds_policy, playBoundsMin=low, playBoundsMax=high,
                      pending=['Native hierarchy, axis/scale, material slots and colliders', 'Spawn rotation/capsule/flight clearance and allowed room roles',
                               'Water renderer/motion/culling/performance', 'Visual and lighting approval owned by root',
                               'SpatialData navigation schema and RoomRules/UI/Online integration owned by root'])
    need(all(hashlib.sha256(path.read_bytes()).hexdigest() == hashes[key] for key, path in source_paths.items()), 'Source changed while preparing')
    return recipe, validation


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True, type=Path, help='Final source descriptor; sourceFinal must be true')
    parser.add_argument('--output-dir', type=Path, default=OUT)
    args = parser.parse_args(argv)
    config_path = args.config.resolve()
    config = json.loads(config_path.read_text(encoding='utf-8-sig'))
    validate_config(config)
    outputs = [args.output_dir / (config['mapId'] + suffix) for suffix in ('.recipe.json', '.validation.json')]
    need(not any(path.exists() for path in outputs), 'Output already exists; do not overwrite a delivered recipe')
    recipe, validation = prepare(config, config_path.parent)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for path, content in zip(outputs, (recipe, validation)):
        with path.open('x', encoding='utf8') as stream:
            json.dump(content, stream, indent=2, ensure_ascii=False)
            stream.write('\n')
    print(str(outputs[0].resolve()))
    print(json.dumps(dict(mapId=recipe['mapId'], meshes=len(recipe['nodes']), materials=len(recipe['materials']), humans=len(recipe['humanSpawns']), mosquitoes=len(recipe['mosquitoSpawns']))))


if __name__ == '__main__':
    main()
