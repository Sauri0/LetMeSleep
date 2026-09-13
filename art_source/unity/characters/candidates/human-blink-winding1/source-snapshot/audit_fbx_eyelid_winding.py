"""Standalone FBX winding evidence; optional in-memory test of the source repair.

No Blender/Unity process. --test-source-fix exercises the authoring function on
an in-memory adapter of the FBX. It does not produce or certify a repaired FBX.
"""
import argparse
import hashlib
import importlib
import json
from pathlib import Path
import sys
import types

from author_human_facial import CONTRACT, eyelids, orient_lid_faces, _outward_measure
from inspect_fbx_morph_regions import child, array, triples, label


class Polygon:
    def __init__(self, index, vertices):
        self.index, self.vertices = index, list(vertices)

    def flip(self):
        self.vertices.reverse()


def inspect(path, parser, test_fix):
    tree, _ = parser.parse(str(path))
    objects = child(tree, 'Objects').elems
    head = next(n for n in objects if n.id == b'Geometry' and n.props[2] == b'Mesh'
                and label(n) == 'HeadAuthoredPlanes')
    basis = triples(array(head, 'Vertices'))
    polygons = []
    face = []
    for index in array(head, 'PolygonVertexIndex'):
        face.append(index if index >= 0 else -index-1)
        if index < 0:
            polygons.append(Polygon(len(polygons), face))
            face = []
    def points(values):
        return [types.SimpleNamespace(index=i, co=p) for i, p in enumerate(values)]
    keys = {'Basis': types.SimpleNamespace(data=points(basis))}
    shape_ids = {}
    for shape in objects:
        if shape.id != b'Geometry' or shape.props[2] != b'Shape':
            continue
        name = label(shape)
        indices = array(shape, 'Indexes')
        values = list(basis)
        for i, d in zip(indices, triples(array(shape, 'Vertices'))):
            values[i] = tuple(a+b for a, b in zip(basis[i], d))
        keys[name] = types.SimpleNamespace(data=points(values))
        shape_ids[name] = set(indices)
    obj = types.SimpleNamespace(name='HumanHead', data=types.SimpleNamespace(
        vertices=points(basis), polygons=polygons,
        shape_keys=types.SimpleNamespace(key_blocks=keys), update=lambda: None))
    def measure():
        rows = []
        for side, sign in [('L', 1), ('R', -1)]:
            ids = shape_ids['Blink.'+side]
            for upper in [True, False]:
                selected = [p for p in polygons if all(i in ids for i in p.vertices)
                            and (sum(basis[i][2] for i in p.vertices)/len(p.vertices) > 1.558) == upper]
                for name in CONTRACT['blink_samples'][side]:
                    signs = [_outward_measure([keys[name].data[i].co for i in p.vertices],
                                             (sign*.081, -.108, 1.558)) for p in selected]
                    rows.append({'side': side, 'lid': 'upper' if upper else 'lower', 'shape': name,
                                 'faces': len(signs), 'outward': sum(v > 1e-12 for v in signs),
                                 'inward': sum(v < -1e-12 for v in signs),
                                 'degenerate': sum(abs(v) <= 1e-12 for v in signs)})
        return rows
    result = {'path': str(path.resolve()), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'exported_winding': measure(), 'scope': 'FBX polygon winding relative to eye center, five samples per lid.'}
    if test_fix:
        c = types.SimpleNamespace()
        eyelids(c, lambda *args: None, None)
        before = [[tuple(v.co) for v in key.data] for key in keys.values()]
        materials_faces = [set(p.vertices) for p in polygons]
        result['in_memory_source_repair'] = orient_lid_faces(c, obj)
        result['after_in_memory_repair'] = measure()
        assert before == [[tuple(v.co) for v in key.data] for key in keys.values()]
        assert materials_faces == [set(p.vertices) for p in polygons]
        result['shape_coordinates_and_face_vertex_sets_unchanged'] = True
    return result


def main():
    arg = argparse.ArgumentParser(description=__doc__)
    arg.add_argument('--parser-dir', type=Path, required=True)
    arg.add_argument('--output', type=Path, required=True)
    arg.add_argument('--test-source-fix', action='store_true')
    arg.add_argument('--require-outward', action='store_true')
    arg.add_argument('files', nargs='+', type=Path)
    args = arg.parse_args()
    pkg = types.ModuleType('_winding_fbx')
    pkg.__path__ = [str(args.parser_dir.resolve())]
    sys.modules[pkg.__name__] = pkg
    importlib.import_module(pkg.__name__+'.fbx_utils_threading')._MULTITHREADING_ENABLED = False
    parser = importlib.import_module(pkg.__name__+'.parse_fbx')
    result = [inspect(path, parser, args.test_source_fix) for path in args.files]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2), encoding='utf8', newline='\n')
    for item in result:
        print(item['sha256'], 'export inward sample-faces', sum(r['inward'] for r in item['exported_winding']))
        if args.test_source_fix:
            print('IN-MEMORY ONLY:', item['in_memory_source_repair'])
        if args.require_outward:
            assert all(r['faces'] == 60 and r['outward'] == 60 for r in item['exported_winding']), 'Export has inward/missing lid faces'


if __name__ == '__main__':
    main()
