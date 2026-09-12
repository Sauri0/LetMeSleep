"""Exact triangle contacts between native baked hand/core and held tool dumps.

Run in Blender's Python for its BVH and mathutils. No asset is opened or edited.
Contacts are witnesses, not automatic visual approval; touching a held object
is intentional, while crossing its surface requires reviewing the location.
"""
import argparse
import json
from pathlib import Path
import sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0, str(Path(__file__).resolve().parent))
from characters_facial_audit import intersects


def build(points, faces):
    return {'points': [Vector(p) for p in points], 'faces': faces,
            'tree': BVHTree.FromPolygons([Vector(p) for p in points], faces, all_triangles=True, epsilon=0)}


def main(folder, output):
    records = []
    for tool_file in sorted(folder.glob('*-tool.json')):
        tool_points = json.loads(tool_file.read_text())
        if not tool_points:
            continue
        source_file = tool_file.with_name(tool_file.name.replace('-tool.json', '-core.json'))
        if not source_file.exists():
            continue
        core_points, core_faces = [], []
        for surface in json.loads(source_file.read_text()):
            offset = len(core_points)
            core_points += [row[0] for row in surface['rows']]
            indices = surface['indices']
            core_faces += [tuple(offset + i for i in indices[start:start+3]) for start in range(0, len(indices), 3)]
        core = build(core_points, core_faces)
        tool = build(tool_points, [tuple(range(start, start+3)) for start in range(0, len(tool_points), 3)])
        contacts, witnesses = 0, []
        for a, b in core['tree'].overlap(tool['tree']):
            hit = intersects([core['points'][i] for i in core['faces'][a]], [tool['points'][i] for i in tool['faces'][b]])
            if hit is not None:
                contacts += 1
                if len(witnesses) < 20:
                    witnesses.append({'core_triangle': a, 'tool_triangle': b, 'point': list(hit)})
        records.append({'name': tool_file.stem, 'core_triangles': len(core_faces), 'tool_triangles': len(tool['faces']),
                        'triangle_contacts': contacts, 'witnesses': witnesses})
    report = {'records': records, 'scope': 'Native rendered triangle surfaces at the captured poses. Exact crossing/touch witnesses; no continuous-time or containment certification.'}
    output.write_text(json.dumps(report, indent=2))
    print('CHARACTER092_TOOL_CONTACTS', [(row['name'], row['triangle_contacts']) for row in records])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--folder', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    main(args.folder, args.output)
