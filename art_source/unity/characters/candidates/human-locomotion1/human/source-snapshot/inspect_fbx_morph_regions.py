"""Read FBX morph/skin data with Blender's standalone parser; launches no native app.

This is a source-data diagnostic, not a Unity import or visual acceptance test.
The parser directory must contain parse_fbx.py, data_types.py and
fbx_utils_threading.py from the same local Blender installation.
"""
import argparse
import hashlib
import importlib
import json
import math
from pathlib import Path
import sys
import types


def child(node, name):
    return next((item for item in node.elems if item.id == name.encode()), None)


def array(node, name):
    item = child(node, name)
    return list(item.props[0]) if item is not None else []


def label(node):
    return node.props[1].split(b'\x00')[0].decode('utf8')


def triples(values):
    assert len(values) % 3 == 0
    return list(zip(values[::3], values[1::3], values[2::3]))


def bounds(points):
    return {'min': [min(p[i] for p in points) for i in range(3)],
            'max': [max(p[i] for p in points) for i in range(3)]} if points else None


def inspect(path, parser):
    tree, version = parser.parse(str(path))
    objects = {n.props[0]: n for n in child(tree, 'Objects').elems}
    links = [n.props for n in child(tree, 'Connections').elems if n.props[0] == b'OO']
    parents = {}
    children = {}
    for _, source, target in links:
        parents.setdefault(source, []).append(target)
        children.setdefault(target, []).append(source)

    def parent_of_type(ident, kind):
        matches = [objects[p] for p in parents.get(ident, []) if p in objects
                   and len(objects[p].props) > 2 and objects[p].props[2] == kind]
        assert len(matches) == 1, (ident, kind, len(matches))
        return matches[0]

    report = {'path': str(path.resolve()), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'fbx_version': version, 'scope': 'Raw FBX geometry, sparse morph deltas and skin links; no Unity evaluation.',
              'global_settings': {}, 'meshes': {}, 'shapes': []}
    from author_human_facial import eyelids
    reference = types.SimpleNamespace()
    eyelids(reference, lambda *args: None, None)
    for prop in child(child(tree, 'GlobalSettings'), 'Properties70').elems:
        report['global_settings'][prop.props[0].decode()] = [
            v.decode('utf8') if isinstance(v, bytes) else v for v in prop.props[4:]]
    for node in objects.values():
        if node.id == b'Geometry' and node.props[2] == b'Mesh':
            vertices = triples(array(node, 'Vertices'))
            models = [objects[i] for i in parents.get(node.props[0], []) if i in objects and objects[i].id == b'Model']
            report['meshes'][str(node.props[0])] = {'name': label(node), 'vertices': len(vertices),
                'bounds_raw': bounds(vertices), 'models': [label(n) for n in models]}

    for shape in objects.values():
        if shape.id != b'Geometry' or shape.props[2] != b'Shape':
            continue
        channel = parent_of_type(shape.props[0], b'BlendShapeChannel')
        deformer = parent_of_type(channel.props[0], b'BlendShape')
        mesh = parent_of_type(deformer.props[0], b'Mesh')
        vertices = triples(array(mesh, 'Vertices'))
        indices = array(shape, 'Indexes')
        deltas = triples(array(shape, 'Vertices'))
        normal_deltas = triples(array(shape, 'Normals'))
        assert len(indices) == len(deltas) and len(set(indices)) == len(indices)
        assert not normal_deltas or len(normal_deltas) == len(indices)
        assert all(0 <= i < len(vertices) for i in indices)
        active = [(i, d) for i, d in zip(indices, deltas) if math.dist(d, (0, 0, 0)) > 1e-7]
        targets = reference.eyelid_targets.get(label(shape), {})
        assert all(sum(all(abs(a-b) < .000002 for a, b in zip(key, vertices[i]))
                       for key in targets) == 1 for i in indices)
        matched = []
        for i, delta in active:
            matches = [key for key in targets if all(abs(a-b) < .000002 for a, b in zip(key, vertices[i]))]
            assert len(matches) == 1, (label(shape), i, 'Delta outside authored eyelid region')
            actual = tuple(a+b for a, b in zip(vertices[i], delta))
            matched.append(math.dist(actual, targets[matches[0]]))
        weights = {i: {} for i, _ in active}
        for skin_id in children.get(mesh.props[0], []):
            skin = objects[skin_id]
            if skin.id != b'Deformer' or skin.props[2] != b'Skin':
                continue
            for cluster_id in children.get(skin_id, []):
                cluster = objects[cluster_id]
                if cluster.id != b'Deformer' or cluster.props[2] != b'Cluster':
                    continue
                bones = [objects[i] for i in children.get(cluster_id, []) if objects[i].id == b'Model']
                assert len(bones) == 1
                for i, weight in zip(array(cluster, 'Indexes'), array(cluster, 'Weights')):
                    if i in weights and weight > 1e-7:
                        weights[i][label(bones[0])] = weight
        report['shapes'].append({'name': label(shape), 'channel': label(channel), 'mesh': label(mesh),
            'sparse_count': len(indices), 'moving_count': len(active),
            'sparse_normals_count': len(normal_deltas), 'all_sparse_indices_match_eyelids': True,
            'base_bounds_raw': bounds([vertices[i] for i, _ in active]),
            'target_bounds_raw': bounds([tuple(a+b for a, b in zip(vertices[i], d)) for i, d in active]),
            'max_delta_raw': max((math.dist(d, (0, 0, 0)) for _, d in active), default=0),
            'authored_eyelid_matches': len(matched), 'max_authored_target_error_m': max(matched, default=0),
            'influencing_bones': sorted({bone for value in weights.values() for bone in value}),
            'vertices': [{'index': i, 'basis': vertices[i], 'delta': d, 'skin': weights[i]} for i, d in active]})
    return report


def main():
    arg = argparse.ArgumentParser(description=__doc__)
    arg.add_argument('--parser-dir', type=Path, required=True)
    arg.add_argument('--output', type=Path, required=True)
    arg.add_argument('files', nargs='+', type=Path)
    args = arg.parse_args()
    package = types.ModuleType('_lms_standalone_fbx')
    package.__path__ = [str(args.parser_dir.resolve())]
    sys.modules[package.__name__] = package
    threading = importlib.import_module(package.__name__ + '.fbx_utils_threading')
    threading._MULTITHREADING_ENABLED = False
    parser = importlib.import_module(package.__name__ + '.parse_fbx')
    reports = [inspect(path, parser) for path in args.files]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(reports, indent=2), encoding='utf8')
    for report in reports:
        print(report['path'], report['sha256'])
        print('Globals:', report['global_settings'])
        for shape in report['shapes']:
            print(json.dumps({k: v for k, v in shape.items() if k != 'vertices'}))


if __name__ == '__main__':
    main()
