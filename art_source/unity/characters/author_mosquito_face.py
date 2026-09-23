"""Independent pupil pivots and articulated insect eye shutters.

Pure geometry/contract functions import without Blender. Open is the bind pose;
runtime owns gaze/blink continuously after the body animation has been evaluated.

Sketch r1 (PER-03 mosquito 01, PER-07 base, UI-06): the white eyes are the main
read of the character. Each eye is a faceted ellipsoid almost as large as the
head, both eyes nearly touching and bulging in front of it, with a small black
pupil looking forward. Lids/housing radii follow the larger eye so a closed
blink still covers the white and the pupil from the front and the sides.

Sketch r2 (art-director round 2): coarser 10x6 white; a thick red cup rim; the
upper shutter rests lowered over the top of the white and blinks from there; a
rolled lip on the upper shutter gives PER-07's thick rim.

Sketch r3 (art-director round 3, items 2 and 4): the white is 11% wider and
taller seen from the front (diameter ~0.6 of the thorax) and cleaner (#FAFAF8,
roughness .6). It bulges out of its cup: its centre sits 11 mm ahead of the
lid pivot (was 5.5 mm) and it is a little shallower front-to-back, so ~70% of
the white shows in profile instead of ~58% (pure-geometry ray count). The
upper shutter rests at 68 deg (was 46: no tired look, 0% of the white covered
from the front) and the rest edges are nearly vertical toward the lateral
poles (84 / -86 deg) so the profile cut runs through the pivot.

Why the cup is not yawed outward (the other r3 option): the runtime blink
rotates both shutters about source X through the pivot. A white point on that
axis never moves, so it is either hidden by a shell in the open pose too or
exposed when closed; yawing the cup opens exactly the lateral axis point and
the side coverage check fails (measured: 2 exposed points at 4 deg, 32 at 20
deg). For the same reason every circle around the axis can be at most half
open, which is why the white has to move forward, not the cup rotate.

Lid pivots, bone names, axes and the 90 deg runtime closure are unchanged; every
rest edge keeps upper - lower <= 170 deg so the rotated shutters plus the fixed
housing still hide the white and the pupil when closed (check_mosquito_face.py),
and the lid vertex centroids still let Unity's FacialContentBuilder derive
+/-90 deg. The pupil stays within 2 deg of the character front (Unity requires
< 16 deg).
"""
import math

FACE_REVISION = 'mosquito-facial-controls-v1'
LID_GEOMETRY_REVISION = 'mosquito-sketch-r3-forward-white-cups'
# Eye white, source metres (x lateral, y depth along the look axis, z up).
EYE_RADII = (.0355, .029, .0366)
EYE_SEGMENTS, EYE_RINGS = 10, 6
# The white bulges out of its cup: its centre sits ahead (-Y) of the pivot the
# shutters and the pupil rotate about, so ~70% of it shows in profile.
EYE_FORWARD_OFFSET = .011
# Pupil: small flattened black disc sitting on the white, aimed forward.
PUPIL_RADII = (.0087, .0022, .0095)
PUPIL_INWARD_DEGREES = 2
PUPIL_EMBED = .0006
PUPIL_SEGMENTS, PUPIL_RINGS = 8, 5
# Closed shutters must clear the white (farthest ~.040 from the pivot) and the
# pupil front (~.0416), including faceting sag and the recessed rear housing.
# Upper shutter outermost; lower shutter and housings step inward so no two
# shells ever share a radius (no z-fighting where they overlap). Equal Y/Z
# radii let every shell slide over itself when rotated about source X.
LID_RADIUS_X = .0392
LID_RADIUS_YZ = .0447
LOWER_LID_INSET = .0005
LID_THICKNESS = .0006
HOUSING_RECESS = .0008
# Rest (open) edges, degrees about source X measured from the look axis (0 =
# front, 90 = top, 180 = back, -90 = bottom). Front edges ramp with
# |sin(latitude)| from the centre value (x = 0) to the pole value (lateral
# poles) while upper - lower stays <= 170 deg: after +/-90 deg the shutters
# still overlap by >= 10 deg.
UPPER_FRONT_CENTER, UPPER_FRONT_POLE, UPPER_BACK = 68.0, 84.0, 198.0
LOWER_FRONT_CENTER, LOWER_FRONT_POLE, LOWER_BACK = -92.0, -86.0, -208.0
LID_EDGE_RAMP_POWER = 1.0
# Rolled lip on the upper shutter's front edge: (degrees past the edge,
# radial offset in m) section, tapered toward the lateral poles.
LIP_SAMPLES, LIP_LATITUDE = 11, 76.0
LIP_SECTION = ((-1.0, -.0004), (0.0, .0017), (3.2, .0024), (6.5, .0012), (8.0, -.0004))
LID_LATITUDE_STEPS, UPPER_ARC_STEPS, LOWER_ARC_STEPS = 8, 7, 5
GAZE_YAW_LIMIT_DEGREES = 12
GAZE_PITCH_LIMIT_DEGREES = 10
FACE_BONES = tuple(role + '.' + side for side in ('L', 'R')
                   for role in ('Pupil', 'LidUpper', 'LidLower'))


def eye_center(sign):
    """Pivot of Pupil/LidUpper/LidLower (bone heads); not the white's centre."""
    return (sign * .037, -.066, .132)


def eye_white_center(sign):
    x, y, z = eye_center(sign)
    return (x, y - EYE_FORWARD_OFFSET, z)


def rotate_x(point, angle):
    x, y, z = point
    c, s = math.cos(angle), math.sin(angle)
    return (x, c * y - s * z, s * y + c * z)


def rotate_z(point, angle):
    x, y, z = point
    c, s = math.cos(angle), math.sin(angle)
    return (c * x - s * y, s * x + c * y, z)


def _signed_volume(vertices, faces):
    volume = 0
    for face in faces:
        a = vertices[face[0]]
        for i in range(1, len(face) - 1):
            b, c = vertices[face[i]], vertices[face[i + 1]]
            volume += (a[0] * (b[1] * c[2] - b[2] * c[1]) - a[1] * (b[0] * c[2] - b[2] * c[0])
                       + a[2] * (b[0] * c[1] - b[1] * c[0]))
    return volume / 6


def outward(vertices, faces):
    """Closed meshes are emitted with outward winding (export also re-checks)."""
    if _signed_volume(vertices, faces) < 0:
        faces = [tuple(reversed(face)) for face in faces]
    return vertices, faces


def ellipsoid_mesh(radii, segments, rings, offset=(0, 0, 0), yaw=0.0, center=(0, 0, 0)):
    """Faceted UV ellipsoid with its poles on the source Y (look) axis.

    Built at the local origin, shifted by offset, rotated about Z by yaw and
    finally translated to center. Pure data, closed and manifold.
    """
    local = [(0, -radii[1], 0)]
    for ring in range(1, rings):
        theta = math.pi * ring / rings
        y, radius = -math.cos(theta), math.sin(theta)
        for segment in range(segments):
            phi = math.tau * segment / segments
            local.append((radii[0] * radius * math.cos(phi), radii[1] * y, radii[2] * radius * math.sin(phi)))
    local.append((0, radii[1], 0))
    vertices = []
    for point in local:
        moved = tuple(a + b for a, b in zip(point, offset))
        vertices.append(tuple(a + b for a, b in zip(rotate_z(moved, yaw), center)))
    last = len(vertices) - 1
    faces = [(0, 1 + (j + 1) % segments, 1 + j) for j in range(segments)]
    for ring in range(rings - 2):
        for j in range(segments):
            a = 1 + ring * segments + j
            b = 1 + ring * segments + (j + 1) % segments
            faces.append((a, b, b + segments, a + segments))
    base = 1 + (rings - 2) * segments
    faces += [(base + j, base + (j + 1) % segments, last) for j in range(segments)]
    return outward(vertices, faces)


def eye_mesh(sign):
    return ellipsoid_mesh(EYE_RADII, EYE_SEGMENTS, EYE_RINGS, center=eye_white_center(sign))


def pupil_yaw(sign):
    """Rotation about Z that turns the pupil slightly toward the snout."""
    return -sign * math.radians(PUPIL_INWARD_DEGREES)


def _white_surface_distance():
    """Pivot-to-white distance along the pupil direction (analytic ellipsoid;
    identical for both eyes)."""
    angle = math.radians(PUPIL_INWARD_DEGREES)
    dx, dy = -math.sin(angle), -math.cos(angle)
    # Point t*d relative to the white centre (0, -offset): (t dx, t dy + offset).
    a = (dx / EYE_RADII[0]) ** 2 + (dy / EYE_RADII[1]) ** 2
    b = 2 * dy * EYE_FORWARD_OFFSET / EYE_RADII[1] ** 2
    c = (EYE_FORWARD_OFFSET / EYE_RADII[1]) ** 2 - 1
    return (-b + math.sqrt(b * b - 4 * a * c)) / (2 * a)


# The pupil centre sits just under the white's surface along its own axis.
PUPIL_DEPTH = _white_surface_distance() - PUPIL_EMBED


def pupil_mesh(sign):
    return ellipsoid_mesh(PUPIL_RADII, PUPIL_SEGMENTS, PUPIL_RINGS, offset=(0, -PUPIL_DEPTH, 0),
                          yaw=pupil_yaw(sign), center=eye_center(sign))


def lid_front_degrees(upper, latitude):
    """Rest front edge of a shutter at a latitude (radians, 0 at x = 0)."""
    s = abs(math.sin(latitude)) ** LID_EDGE_RAMP_POWER
    if upper:
        return UPPER_FRONT_CENTER + (UPPER_FRONT_POLE - UPPER_FRONT_CENTER) * s
    return LOWER_FRONT_CENTER + (LOWER_FRONT_POLE - LOWER_FRONT_CENTER) * s


def lid_radii(upper, recess=0):
    inset = 0 if upper else LOWER_LID_INSET
    return LID_RADIUS_X - inset - recess, LID_RADIUS_YZ - inset - recess


def _lid_world(sign, local, closure_degrees):
    """Pivot-relative point -> source, after the runtime closure about source X
    through the pivot (exactly like the bone)."""
    point = rotate_x(local, math.radians(closure_degrees))
    return tuple(a + b for a, b in zip(eye_center(sign), point))


def lid_mesh(sign, upper, closure=0, recess=0):
    """Closed thick ellipsoidal zone between a rest front edge and a back edge.

    closure rotates it about source X through the pivot exactly like the
    runtime bone (+90 deg upper, -90 deg lower). Equal Y/Z radii let it rotate
    without cutting through the stationary white; recess>0 is the fixed rear
    housing, a recessed copy of the open shutter. No flattened eyeball or
    transparent substitute participates in blinking.
    """
    polarity = 1 if upper else -1
    turn = polarity * 90 * max(0, min(1, closure))
    latitude_steps = LID_LATITUDE_STEPS
    arc_steps = UPPER_ARC_STEPS if upper else LOWER_ARC_STEPS
    back = UPPER_BACK if upper else LOWER_BACK
    vertices = []
    for inset in (0, LID_THICKNESS):
        rx, radius = (r - inset for r in lid_radii(upper, recess))
        layer = [(-rx, 0, 0)]
        for row in range(1, latitude_steps):
            latitude = -math.pi * .5 + math.pi * row / latitude_steps
            x, ring = rx * math.sin(latitude), math.cos(latitude)
            front = lid_front_degrees(upper, latitude)
            for col in range(arc_steps + 1):
                phi = math.radians(front + (back - front) * col / arc_steps)
                layer.append((x, -radius * ring * math.cos(phi), radius * ring * math.sin(phi)))
        layer.append((rx, 0, 0))
        vertices.extend(_lid_world(sign, v, turn) for v in layer)

    width = arc_steps + 1
    size = len(vertices) // 2
    top = []
    for col in range(arc_steps):
        top.append((0, 1 + col, 2 + col))
    for row in range(latitude_steps - 2):
        for col in range(arc_steps):
            a = 1 + row * width + col
            top.append((a, a + width, a + width + 1, a + 1))
    start_last = 1 + (latitude_steps - 2) * width
    for col in range(arc_steps):
        top.append((start_last + col, size - 1, start_last + col + 1))
    faces = top + [tuple(size + i for i in reversed(f)) for f in top]
    # Boundary follows the two quarter-shell meridians and the two poles.
    boundary = [0] + [1 + row * width for row in range(latitude_steps - 1)]
    boundary += [size - 1] + [1 + row * width + arc_steps for row in reversed(range(latitude_steps - 1))]
    faces.extend((a, a + size, b + size, b)
                 for a, b in zip(boundary, boundary[1:] + boundary[:1]))
    if not upper:
        faces = [tuple(reversed(f)) for f in faces]
    return vertices, faces


def upper_lid_point(sign, latitude, phi_degrees, offset, closure=0):
    """Point on the upper shutter ellipsoid grown by offset, rotated like the
    LidUpper bone for a closure (shared by the rolled lip and the brows)."""
    rx, radius = (r + offset for r in lid_radii(True))
    phi = math.radians(phi_degrees)
    local = (sign * rx * math.sin(latitude), -radius * math.cos(latitude) * math.cos(phi),
             radius * math.cos(latitude) * math.sin(phi))
    return _lid_world(sign, local, 90 * max(0, min(1, closure)))


def upper_lip_mesh(sign, closure=0):
    """Thick rolled red rim along the upper shutter's front edge (PER-07).

    Rigid on LidUpper, entirely outside the shutter's inner layer, so it never
    reaches the white; the lower shutter and housings are always inside it.
    """
    rings = []
    for row in range(LIP_SAMPLES):
        latitude = math.radians(-LIP_LATITUDE + 2 * LIP_LATITUDE * row / (LIP_SAMPLES - 1))
        front = lid_front_degrees(True, latitude)
        taper = math.cos(latitude) ** .5
        rings.append([upper_lid_point(sign, latitude, front + d * taper, o * taper, closure)
                      for d, o in LIP_SECTION])
    vertices = [v for ring in rings for v in ring]
    n = len(LIP_SECTION)
    faces = [tuple(reversed(range(n)))]
    for row in range(len(rings) - 1):
        for j in range(n):
            a, b = row * n + j, row * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    return outward(vertices, faces)


def facial_contract():
    return {
        'revision': FACE_REVISION, 'bind_is_open': True, 'parent': 'Head',
        'bones': list(FACE_BONES), 'total_rig_bones': 39,
        'source_coordinates': 'metres, Z up, -Y forward; actor scale .5 once',
        'eye_centers_source_m': {s: eye_center(sign) for s, sign in (('L', 1), ('R', -1))},
        'eye_radii_source_m': list(EYE_RADII),
        'eye_white_forward_offset_source_m': EYE_FORWARD_OFFSET,
        'lid_radii_source_m': {'x': LID_RADIUS_X, 'yz': LID_RADIUS_YZ, 'lower_inset': LOWER_LID_INSET},
        'lid_geometry_revision': LID_GEOMETRY_REVISION,
        'lid_rest_edges_degrees_about_x_from_front': {
            'upper_front_center_to_pole': [UPPER_FRONT_CENTER, UPPER_FRONT_POLE], 'upper_back': UPPER_BACK,
            'lower_front_center_to_pole': [LOWER_FRONT_CENTER, LOWER_FRONT_POLE], 'lower_back': LOWER_BACK},
        'pupil': {'radii_source_m': list(PUPIL_RADII), 'depth_source_m': PUPIL_DEPTH,
                  'inward_yaw_degrees': PUPIL_INWARD_DEGREES},
        'bone_tail_direction_source': [1, 0, 0],
        'gaze': {'controls': ['Pupil.L', 'Pupil.R'],
                 'yaw_axis_source': [0, 0, 1], 'pitch_axis_source': [1, 0, 0],
                 'yaw_limit_degrees': GAZE_YAW_LIMIT_DEGREES,
                 'pitch_limit_degrees': GAZE_PITCH_LIMIT_DEGREES,
                 'order': 'Rz(yaw) @ Rx(pitch) applied to bind around each eye center',
                 'white_eye_moves': False},
        'blink': {'closure_range': [0, 1], 'upper_angle_source_x_degrees': [0, 90],
                  'lower_angle_source_x_degrees': [0, -90],
                  'scale': [1, 1, 1], 'material': 'Mosquito_Shell'},
        'eye_housing': 'fixed rear cup weighted to Head: recessed .8 mm copy of each open shutter; covers the posterior white during closure',
        'rest_pose': 'upper shutter rests at ~68 deg over the top of the white (<=5% cover); blink closure 0..1 starts from that bind pose',
        'runtime_writer': 'one shared facial driver after Animator/Playable evaluation; menu supplies target only',
        'runtime_binding': 'serialize axes transformed into each imported bone bind-local frame; do not assume FBX local XYZ',
        'body_clips': '15 existing IDs unchanged; all facial tracks neutral, runtime overwrites six facial controls only',
        'customization': 'MosquitoSkin renderer; existing Mosquito_Shell color binding. EyeWhite/Expression remain unchanged.',
        'native_status': 'source candidate only; geometry, FBX axes, closure and runtime visibility await Director slot',
    }


def create_face(c, *, mesh, shell, eye, pupil, **_unused):
    for side, sign in (('L', 1), ('R', -1)):
        center = eye_center(sign)
        for role in ('Pupil', 'LidUpper', 'LidLower'):
            c.bone(role + '.' + side, center, (center[0] + .012, center[1], center[2]), 'Head')
        vertices, faces = eye_mesh(sign)
        mesh('Eye.' + side, vertices, faces, eye, 'Head')
        vertices, faces = pupil_mesh(sign)
        mesh('Pupil.' + side, vertices, faces, pupil, 'Pupil.' + side)
        for upper in (True, False):
            name = ('LidUpper.' if upper else 'LidLower.') + side
            vertices, faces = lid_mesh(sign, upper)
            mesh(name, vertices, faces, shell, name)
            if upper:
                mesh('LidLip.' + side, *upper_lip_mesh(sign), shell, name)
            # A fixed rear housing prevents the white back of the eye becoming
            # exposed in profile when the articulated shutters rotate forward.
            vertices, faces = lid_mesh(sign, upper, recess=HOUSING_RECESS)
            mesh('EyeHousing.' + name, vertices, faces, shell, 'Head')


def apply_facial_pose(rig, yaw_degrees=0, pitch_degrees=0, blink_left=0, blink_right=0):
    """Native QA overlay after sampling a body clip; never saves or creates clips.

    Source and imported FBX have different local bone bases. Apply the contract
    in armature coordinates, then convert through each real imported bind.
    Runtime must make the equivalent conversion during prefab binding.
    """
    import bpy
    from mathutils import Matrix, Vector
    yaw = math.radians(max(-GAZE_YAW_LIMIT_DEGREES, min(GAZE_YAW_LIMIT_DEGREES, yaw_degrees)))
    pitch = math.radians(max(-GAZE_PITCH_LIMIT_DEGREES, min(GAZE_PITCH_LIMIT_DEGREES, pitch_degrees)))
    bpy.context.view_layer.update()
    parent = rig.pose.bones['Head'].matrix @ rig.data.bones['Head'].matrix_local.inverted()
    for side, blink in (('L', blink_left), ('R', blink_right)):
        for role in ('Pupil', 'LidUpper', 'LidLower'):
            name = role + '.' + side
            rest = rig.data.bones[name].matrix_local
            center = Vector(rest.translation)
            if role == 'Pupil':
                rotation = Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(pitch, 4, 'X')
            else:
                angle = (1 if role == 'LidUpper' else -1) * math.pi * .5 * max(0, min(1, blink))
                rotation = Matrix.Rotation(angle, 4, 'X')
            rig.pose.bones[name].matrix = parent @ Matrix.Translation(center) @ rotation @ Matrix.Translation(-center) @ rest
    bpy.context.view_layer.update()
