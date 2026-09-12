"""Pure facial coverage and flight-channel checks; no Blender/native approval."""
import json
import math
from pathlib import Path

from author_mosquito_face import (EYE_SECTIONS, FACE_BONES, eye_center,
                                 facial_contract, lid_mesh, rotate_x)
from author_mosquito_geometry import section_mesh
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


def main():
    errors, topology, coverage = [], [], []
    for sign in (1, -1):
        center = eye_center(sign)
        white, _ = section_mesh(EYE_SECTIONS)
        eye_points = [(x + center[0], y, z) for x, y, z in white]
        # Sample the actual pupil ellipsoid bounds at the gaze limits and center.
        pupils = []
        for yaw in (-12, 0, 12):
            for pitch in (-10, 0, 10):
                for row in range(7):
                    latitude = -math.pi * .5 + math.pi * row / 6
                    for col in range(10):
                        longitude = math.tau * col / 10
                        p = (sign * .002 + .006 * math.cos(latitude) * math.cos(longitude),
                             -.026 + .0035 * math.cos(latitude) * math.sin(longitude), .009 * math.sin(latitude))
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
            if closure == 0:
                occluded = sum(min(front_intersection(v, f, x, z) for v, f in lids) < y - 1e-5
                               for x, y, z in pupils)
                if occluded:
                    errors.append(f'{sign}: open lids occlude pupil points')
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
              'scope': 'pure thick-lid topology at five closures, front-ray coverage of white/pupil samples, open pupil exposure, flight channels/seams',
              'facial_contract': facial_contract(), 'flight_contract': flight_contract(),
              'closed_front_coverage': coverage, 'lid_topology': topology, 'flight_ranges': channels,
              'blender_executed': False, 'runtime_verified': False, 'art_accepted': False,
              'limitations': 'front rays only; no skin evaluation, oblique closure, side intersections, imported axes or normal-speed perception'}
    output = ROOT.parents[2] / 'docs/unity/mosquito/FACE-FLIGHT-SOURCE-CHECK-20260912.json'
    output.write_text(json.dumps(report, indent=2), encoding='utf8', newline='\n')
    print(json.dumps({'passed': not errors, 'errors': errors, 'coverage': coverage, 'flight_ranges': channels}, indent=2))
    assert not errors, 'Facial/flight source checks failed'


if __name__ == '__main__':
    main()
