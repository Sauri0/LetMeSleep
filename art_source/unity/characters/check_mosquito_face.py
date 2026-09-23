"""Pure facial coverage and flight-channel checks; no Blender/native approval."""
import json
import math
from pathlib import Path

from author_mosquito_face import (COLLAR_ARCS_DEGREES, HOUSING_RECESS, cap_mesh, collar_mesh, eye_center,
                                 eye_mesh, pupil_mesh, facial_contract, lid_mesh, rotate_x)
from author_mosquito_motion import flight_channels, flight_contract
from check_mosquito_source import inspect_mesh

ROOT = Path(__file__).resolve().parent


def front_intersection(vertices, faces, x, z):
    """First shell hit by an orthographic ray travelling along source +Y."""
    hits = []
    for face in faces:
        for i in range(1, len(face) - 1):
            a, b, c = (vertices[j] for j in (face[0], face[i], face[i + 1]))
            determinant = (b[2] - c[2]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[2] - c[2])
            if abs(determinant) < 1e-14:
                continue
            u = ((b[2] - c[2]) * (x - c[0]) + (c[0] - b[0]) * (z - c[2])) / determinant
            v = ((c[2] - a[2]) * (x - c[0]) + (a[0] - c[0]) * (z - c[2])) / determinant
            if min(u, v, 1 - u - v) >= -1e-8:
                hits.append(u * a[1] + v * b[1] + (1 - u - v) * c[1])
    return min(hits) if hits else math.inf


def ray_hits(origin, direction, vertices, faces):
    """Sorted distances of every triangle hit along a ray (Moller-Trumbore)."""
    hits = []
    for face in faces:
        for i in range(1, len(face) - 1):
            a, b, c = (vertices[j] for j in (face[0], face[i], face[i + 1]))
            e1 = [b[k] - a[k] for k in range(3)]
            e2 = [c[k] - a[k] for k in range(3)]
            h = [direction[1] * e2[2] - direction[2] * e2[1], direction[2] * e2[0] - direction[0] * e2[2],
                 direction[0] * e2[1] - direction[1] * e2[0]]
            det = sum(e1[k] * h[k] for k in range(3))
            if abs(det) < 1e-15:
                continue
            f = 1 / det
            s = [origin[k] - a[k] for k in range(3)]
            u = f * sum(s[k] * h[k] for k in range(3))
            if u < -1e-9 or u > 1 + 1e-9:
                continue
            q = [s[1] * e1[2] - s[2] * e1[1], s[2] * e1[0] - s[0] * e1[2], s[0] * e1[1] - s[1] * e1[0]]
            v = f * sum(direction[k] * q[k] for k in range(3))
            if v < -1e-9 or u + v > 1 + 1e-9:
                continue
            t = f * sum(e2[k] * q[k] for k in range(3))
            if t > 1e-9:
                hits.append(t)
    return sorted(hits)


def rim_poke_through(sign=1, closures=(0, .125, .25, .375, .5, .625, .75, .875, 1)):
    """r7 rim gate: the fixed cap and collar vertices never stand outside every
    shell (rotating shutters, fixed housings) that covers them, at any closure.
    A vertex beyond the outermost covering layer would show through the lid."""
    center = eye_center(sign)
    fixed = [v for v, _ in [cap_mesh(sign)] + [collar_mesh(sign, part) for part in COLLAR_ARCS_DEGREES]]
    points = [tuple(a - b for a, b in zip(p, center)) for group in fixed for p in group]
    housings = [lid_mesh(sign, upper, recess=HOUSING_RECESS) for upper in (True, False)]
    worst, failures = -1.0, []
    for closure in closures:
        shells = [lid_mesh(sign, upper, closure) for upper in (True, False)] + housings
        shells = [([tuple(a - b for a, b in zip(p, center)) for p in v], f) for v, f in shells]
        for point in points:
            length = math.sqrt(sum(c * c for c in point))
            direction = tuple(c / length for c in point)
            outer = [hits[-1] for hits in (ray_hits((0, 0, 0), direction, v, f) for v, f in shells) if hits]
            if not outer:
                continue
            excess = length - max(outer)
            worst = max(worst, excess)
            if excess > 0:
                failures.append({'closure': closure, 'point_source_m': point, 'excess_m': excess})
    return {'side_sign': sign, 'points': len(points), 'closures': list(closures),
            'max_excess_over_outermost_cover_m': worst, 'failures': failures[:10], 'failure_count': len(failures)}


# r9 (review r8: a row of black pixels on the side of the cup and a thin dark
# line under the lid edge in the customization preview): rasterised views of
# both eyes (white, pupil, shutters, housings, collars, cap) in front of the
# head and thorax at RIM_VIEW_PIXELS across RIM_VIEW_FIELD_M (source metres;
# ~1024 px on the eyes, like the preview's close views), from the side,
# above, below, 3/4 and the front. Every visible eye-rim pixel must shade as
# an outward cup surface (shading normal within 60 deg of the radial direction
# from its eye pivot, i.e. after author_mosquito_face.light_eye_interiors,
# which bends the rim's edge walls and rest-edge faces), and no pixel may see
# through a back face of the eye assembly (a slot into the cup). The
# preview's fill light (CharacterPreviewOrbit: from the camera's front left,
# above) gives, for information, how many visible rim pixels would be unlit
# edge walls or rim faces without the bend.
RIM_VIEW_PIXELS, RIM_VIEW_FIELD_M = 1024, .12
RIM_VIEWS = (('side', 90, 0), ('side_high', 90, 25), ('side_low', 90, -20), ('threequarter', 50, 12),
             ('front_high', 0, 30))


def _triangles(vertices, faces):
    return [(vertices[f[0]], vertices[f[i]], vertices[f[i + 1]]) for f in faces for i in range(1, len(f) - 1)]


def rim_views():
    import numpy as np
    from author_mosquito_face import EYE_RIM_FACING, EYE_RIM_RADII
    from author_mosquito_geometry import head_mesh, thorax_mesh
    parts = []  # (triangles, kind, eye sign)
    for sign in (1, -1):
        parts.append((_triangles(*eye_mesh(sign)), 'white', sign))
        parts.append((_triangles(*pupil_mesh(sign)), 'pupil', sign))
        rims = [lid_mesh(sign, upper) for upper in (True, False)]
        rims += [lid_mesh(sign, upper, recess=HOUSING_RECESS) for upper in (True, False)]
        rims += [collar_mesh(sign, part) for part in COLLAR_ARCS_DEGREES] + [cap_mesh(sign)]
        for data in rims:
            parts.append((_triangles(*data), 'rim', sign))
    parts.append((_triangles(*head_mesh()), 'body', 0))
    parts.append((_triangles(*thorax_mesh()), 'body', 0))
    kinds = {'white': 0, 'pupil': 1, 'rim': 2, 'body': 3}
    tris = np.array([t for p in parts for t in p[0]], dtype=float)
    kind = np.array([kinds[p[1]] for p in parts for _ in p[0]])
    side = np.array([p[2] for p in parts for _ in p[0]])
    normal = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    normal /= np.maximum(np.linalg.norm(normal, axis=1), 1e-20)[:, None]
    shading = normal.copy()
    rim_bent = np.zeros(len(tris), bool)
    radial = np.zeros_like(normal)
    for sign in (1, -1):
        pivot = np.array(eye_center(sign))
        mine = side == sign
        out = tris[mine].mean(1) - pivot
        radial[mine] = out / np.linalg.norm(out, axis=1)[:, None]
        distance = np.linalg.norm(tris - pivot, axis=2)
        inside = (distance >= EYE_RIM_RADII[0]).all(1) & (distance <= EYE_RIM_RADII[1]).all(1)
        bend = (kind == 2) & mine & inside & ((normal * radial).sum(1) < EYE_RIM_FACING)
        shading[bend] = radial[bend]
        rim_bent |= bend
    target = (np.array(eye_center(1)) + np.array(eye_center(-1))) / 2
    report, errors = [], []
    for name, yaw_degrees, pitch_degrees in RIM_VIEWS:
        yaw, pitch = math.radians(yaw_degrees), math.radians(pitch_degrees)
        # Camera around the source front (-Y) by yaw toward +X, pitch above.
        eye = np.array([math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)])
        forward = -eye
        right = np.cross(forward, [0, 0, 1.0])
        right /= np.linalg.norm(right)
        up = np.cross(right, forward)
        fill = -forward * .85 - right * .45 + up * .25
        fill /= np.linalg.norm(fill)
        n = RIM_VIEW_PIXELS
        scale = n / RIM_VIEW_FIELD_M
        rel = tris - target
        u = (rel @ right) * scale + n / 2
        v = (rel @ up) * scale + n / 2
        depth = rel @ forward
        zbuf = np.full((n, n), np.inf)
        ids = np.full((n, n), -1, int)
        for t in range(len(tris)):
            x0, x1 = int(max(0, math.floor(u[t].min()))), int(min(n - 1, math.ceil(u[t].max())))
            y0, y1 = int(max(0, math.floor(v[t].min()))), int(min(n - 1, math.ceil(v[t].max())))
            if x0 > x1 or y0 > y1:
                continue
            (ax, bx, cx), (ay, by, cy) = u[t], v[t]
            det = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(det) < 1e-12:
                continue
            px, py = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
            l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / det
            l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / det
            l3 = 1 - l1 - l2
            hit = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
            if not hit.any():
                continue
            z = l1 * depth[t, 0] + l2 * depth[t, 1] + l3 * depth[t, 2]
            window = zbuf[y0:y1 + 1, x0:x1 + 1]
            closer = hit & (z < window)
            window[closer] = z[closer]
            ids[y0:y1 + 1, x0:x1 + 1][closer] = t
        seen = ids[ids >= 0]
        eye_parts = kind[seen] != 3
        back = eye_parts & ((normal[seen] @ forward) > 1e-6)
        rim_pixels = kind[seen] == 2
        shade_facing = (shading[seen] * radial[seen]).sum(1)
        inward = rim_pixels & (shade_facing < EYE_RIM_FACING - 1e-6)
        unlit_without_bend = rim_pixels & rim_bent[seen] & ((normal[seen] @ fill) < 0)
        row = {'view': name, 'yaw_degrees': yaw_degrees, 'pitch_degrees': pitch_degrees,
               'eye_pixels': int(eye_parts.sum()), 'rim_pixels': int(rim_pixels.sum()),
               'back_face_pixels': int(back.sum()), 'rim_pixels_not_outward': int(inward.sum()),
               'rim_pixels_bent': int((rim_pixels & rim_bent[seen]).sum()),
               'rim_pixels_unlit_by_preview_fill_without_bend': int(unlit_without_bend.sum())}
        report.append(row)
        if row['back_face_pixels']:
            errors.append(f"rim view {name}: {row['back_face_pixels']} px see through a back face of the eye")
        if row['rim_pixels_not_outward']:
            errors.append(f"rim view {name}: {row['rim_pixels_not_outward']} rim px shade as an edge wall/inward face")
    return {'pixels': RIM_VIEW_PIXELS, 'field_m': RIM_VIEW_FIELD_M, 'views': report}, errors


def main():
    errors, topology, coverage = [], [], []
    for sign in (1, -1):
        center = eye_center(sign)
        # Sketch r1: the white is the real emitted faceted ellipsoid, not the
        # retired EYE_SECTIONS loft; the pupil samples are its real vertices.
        eye_points, _ = eye_mesh(sign)
        pupil_points, _ = pupil_mesh(sign)
        # Sample the actual pupil mesh at the gaze limits and center.
        pupils = []
        for yaw in (-12, 0, 12):
            for pitch in (-10, 0, 10):
                for point in pupil_points:
                    p = tuple(a - b for a, b in zip(point, center))
                    x, y, z = rotate_x(p, math.radians(pitch))
                    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
                    pupils.append((center[0] + cy * x - sy * y, center[1] + sy * x + cy * y, center[2] + z))
        for closure in (0, .25, .5, .75, 1):
            lids = [lid_mesh(sign, upper, closure) for upper in (True, False)]
            for upper, data in zip((True, False), lids):
                result = inspect_mesh(f'Lid/{sign}/{upper}/{closure}', *data)
                topology.append(result)
                errors.extend(result['errors'])
            if closure == 1:
                uncovered = sum(min(front_intersection(v, f, x, z) for v, f in lids) > y - 1e-5
                                for x, y, z in eye_points + pupils)
                coverage.append({'side_sign': sign, 'points': len(eye_points) + len(pupils), 'uncovered_front_points': uncovered})
                if uncovered:
                    errors.append(f'{sign}: closed lids leave eye/pupil points exposed')
                housing = [lid_mesh(sign, upper, recess=HOUSING_RECESS) for upper in (True, False)]
                for side_camera in (-1, 1):
                    # Remap a side ray to the same first-hit routine. Both
                    # shutters and fixed rear housing must hide the white.
                    side_shells = [([(y, side_camera * x, z) for x, y, z in v], f) for v, f in lids + housing]
                    uncovered_side = sum(min(front_intersection(v, f, y, z) for v, f in side_shells) > side_camera * x - 1e-5
                                         for x, y, z in eye_points + pupils)
                    coverage.append({'side_sign': sign, 'camera_source_x_sign': side_camera,
                                     'points': len(eye_points) + len(pupils), 'uncovered_side_points': uncovered_side})
                    if uncovered_side:
                        errors.append(f'{sign}/{side_camera}: closed eye exposed from side')
            if closure == 0:
                occluded = sum(min(front_intersection(v, f, x, z) for v, f in lids) < y - 1e-5
                               for x, y, z in pupils)
                if occluded:
                    errors.append(f'{sign}: open lids occlude pupil points')
    rim = rim_poke_through()
    if rim['failure_count']:
        errors.append('cap/collar pokes through the lids or housing')
    views, view_errors = rim_views()
    errors.extend(view_errors)
    channels = {}
    for hover in (False, True):
        values = [flight_channels(i / 120, hover) for i in range(121)]
        ranges = {name: [min(v[name] for v in values), max(v[name] for v in values)] for name in values[0]}
        channels['Hover' if hover else 'Fly'] = ranges
        if max(abs(values[0][k] - values[-1][k]) for k in values[0]) > 1e-10:
            errors.append('flight channel seam')
        for name in ('flap', 'thorax_x', 'abdomen01_x', 'abdomen02_x'):
            if ranges[name][1] - ranges[name][0] < .001:
                errors.append(name + ': missing flight channel motion')
    if any(abs(flight_channels(0)[k] - flight_channels(0, True)[k]) > 1e-10 for k in flight_channels(0)):
        errors.append('Fly/Hover common endpoint mismatch')
    report = {'schema': 'lms-mosquito-face-flight-source-v1', 'passed': not errors, 'errors': errors,
              'scope': 'pure thick-lid topology at five closures, front/side-ray coverage of white/pupil samples with rear housing, open pupil exposure, cap/collar never outside every covering shell at nine closures (r7), rasterised side/above/below/3-4/front rim views at 1024 px with shading normals after the rim bend (r9), flight channels/seams',
              'facial_contract': facial_contract(), 'flight_contract': flight_contract(),
              'closed_front_coverage': coverage, 'lid_topology': topology, 'rim_poke_through': rim, 'rim_views': views,
              'flight_ranges': channels,
              'blender_executed': False, 'runtime_verified': False, 'art_accepted': False,
              'limitations': 'orthographic front/side rays only; no skin evaluation, oblique closure, head intersections, imported axes or normal-speed perception'}
    output = ROOT.parents[2] / 'docs/unity/mosquito/FACE-FLIGHT-SOURCE-CHECK-20260912.json'
    output.write_text(json.dumps(report, indent=2), encoding='utf8', newline='\n')
    print(json.dumps({'passed': not errors, 'errors': errors, 'coverage': coverage, 'rim': rim, 'rim_views': views,
                      'flight_ranges': channels}, indent=2))
    assert not errors, 'Facial/flight source checks failed'


if __name__ == '__main__':
    main()
