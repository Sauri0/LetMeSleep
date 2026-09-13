"""Read-only Blender headless inspection. No addons, bridge, rendering or source writes."""
import bpy
import json
from collections import Counter
from pathlib import Path
from mathutils import Vector

FOLDER = Path('N:/LetMeSleep/Artifacts/Higgsfield/Mapas/03-campamento')
# Always inspect the preserved original after a repair, never relabel v2 as "before".
source = FOLDER / 'SourceBeforeNormals' / 'HF_MAP_03_campamento.blend'
assert source.is_file(), 'Verified source preservation must precede inspection'
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
scene = bpy.data.scenes['HF_MAP_03_campamento']
bpy.context.window.scene = scene
rows = []
for obj in scene.objects:
    if obj.type != 'MESH' or obj.get('export_exclude', False):
        continue
    mesh = obj.data
    mesh.calc_loop_triangles()
    points = [obj.matrix_world @ v.co for v in mesh.vertices]
    center = sum(points, Vector()) / max(1, len(points))
    directions = Counter()
    edges = Counter()
    volume = 0.
    area = dict(up=0., down=0., side=0., degenerate=0.)
    for poly in mesh.polygons:
        ids = list(poly.vertices)
        for a, b in zip(ids, ids[1:] + ids[:1]):
            edges[tuple(sorted((a, b)))] += 1
    for tri in mesh.loop_triangles:
        a, b, c = (points[i] for i in tri.vertices)
        cross = (b-a).cross(c-a)
        normal = cross.normalized()
        key = 'degenerate' if cross.length < 1e-10 else 'up' if normal.z > .1 else 'down' if normal.z < -.1 else 'side'
        directions[key] += 1
        area[key] += cross.length / 2
        volume += (a-center).dot((b-center).cross(c-center)) / 6
    rows.append(dict(name=obj.name, mesh=mesh.name, users=mesh.users, polygons=len(mesh.polygons), triangles=len(mesh.loop_triangles),
                     worldTriangleDirections=dict(directions), areaByDirection=area, signedWorldVolume=volume,
                     boundaryEdges=sum(n == 1 for n in edges.values()), nonManifoldEdges=sum(n != 2 for n in edges.values()),
                     determinant=obj.matrix_world.determinant(), properties={k:obj[k] for k in obj.keys() if isinstance(obj[k],(str,int,float,bool))},
                     boundsMin=[min(p[i] for p in points) for i in range(3)], boundsMax=[max(p[i] for p in points) for i in range(3)]))
report = dict(status='READ_ONLY_WORLD_TRIANGLE_WINDING_INSPECTION', blender=bpy.app.version_string, source=bpy.data.filepath,
              meshes=len(rows), triangles=sum(r['triangles'] for r in rows), objects=rows)
(FOLDER/'normals-before.json').write_text(json.dumps(report, indent=2))
for row in rows:
    if any(s in row['name'].lower() for s in ('terrain','water','foam','lake','bank','bridge','washroom','shelter')):
        print('NORMALS', json.dumps(row))
print('CLOSED_NEGATIVE', json.dumps([r['name'] for r in rows if r['nonManifoldEdges'] == 0 and r['signedWorldVolume'] < -1e-6]))
