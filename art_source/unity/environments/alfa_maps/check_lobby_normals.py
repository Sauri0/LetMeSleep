"""Read-only Blender mesh inspection. No render, scene edits or source save."""
import bpy, json, sys
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
arguments = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
report_name = arguments[0] if arguments else 'lobby_normals_after.json'
bpy.ops.wm.open_mainfile(filepath=str(HERE / 'lobby_alfa_static.blend'))
obj = bpy.data.objects['LobbyShell']
normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()

def authored(v):
    return Vector((v.x, v.z, -v.y))

planes = [
    ('floor', 1, 0, (0, 1, 0)), ('ceiling', 1, 3.2, (0, -1, 0)),
    ('west', 0, -5, (1, 0, 0)), ('east', 0, 5, (-1, 0, 0)),
    ('south', 2, -4, (0, 0, 1)), ('north', 2, 4, (0, 0, -1)),
]
results = []
for name, axis, coordinate, expected in planes:
    dots = []
    for polygon in obj.data.polygons:
        points = [authored(obj.matrix_world @ obj.data.vertices[i].co) for i in polygon.vertices]
        if not all(abs(p[axis] - coordinate) < .0001 for p in points):
            continue
        center = authored(obj.matrix_world @ polygon.center)
        if not (-5.0001 <= center.x <= 5.0001 and -.0001 <= center.y <= 3.2001 and -4.0001 <= center.z <= 4.0001):
            continue
        dots.append(authored(normal_matrix @ polygon.normal).normalized().dot(Vector(expected)))
    results.append(dict(plane=name, faces=len(dots), wrong=sum(dot < .999 for dot in dots), minimum_dot=min(dots) if dots else None))
report = dict(scope='Source lobby cavity normals only; no native import/render tested', blender=bpy.app.version_string,
              passed=all(item['faces'] > 0 and item['wrong'] == 0 for item in results), planes=results)
(HERE / report_name).write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print('LMS_LOBBY_NORMALS ' + json.dumps(report), flush=True)
if not report['passed'] and report_name != 'lobby_normals_before.json':
    raise RuntimeError('Lobby interior normals must face the room')
