"""Independent pupil pivots and articulated insect eye shutters.

Pure geometry/contract functions import without Blender. Open is the bind pose;
runtime owns gaze/blink continuously after the body animation has been evaluated.
"""
import math

FACE_REVISION = 'mosquito-facial-controls-v1'
EYE_SECTIONS = ((-.080, .112, .017, .020), (-.094, .113, .025, .026),
                (-.110, .114, .022, .024), (-.119, .114, .012, .015))
LID_RADIUS_X = .029
LID_RADIUS_YZ = .035
LID_THICKNESS = .0006
HOUSING_RECESS = .0008
GAZE_YAW_LIMIT_DEGREES = 12
GAZE_PITCH_LIMIT_DEGREES = 10
FACE_BONES = tuple(role + '.' + side for side in ('L', 'R')
                   for role in ('Pupil', 'LidUpper', 'LidLower'))


def eye_center(sign):
    return (sign * .027, -.094, .113)


def rotate_x(point, angle):
    x, y, z = point
    c, s = math.cos(angle), math.sin(angle)
    return (x, c * y - s * z, s * y + c * z)


def lid_mesh(sign, upper, closure=0, recess=0):
    """Closed thick quarter-ellipsoid, rotated back in the open bind pose.

    Upper/lower quarter shells meet at the equator at closure=1. Equal Y/Z
    radii let them rotate without cutting through the stationary white eye.
    No flattened eyeball or transparent substitute participates in blinking.
    """
    center = eye_center(sign)
    polarity = 1 if upper else -1
    angle = -polarity * math.pi * .5 * (1 - max(0, min(1, closure)))
    latitude_steps, arc_steps = 10, 4
    vertices = []
    for inset in (0, LID_THICKNESS):
        rx, radius = LID_RADIUS_X - recess - inset, LID_RADIUS_YZ - recess - inset
        layer = [(-rx, 0, 0)]
        for row in range(1, latitude_steps):
            latitude = -math.pi * .5 + math.pi * row / latitude_steps
            x, ring = rx * math.sin(latitude), math.cos(latitude)
            for col in range(arc_steps + 1):
                arc = math.pi * .5 * col / arc_steps
                layer.append((x, -radius * ring * math.cos(arc),
                              polarity * radius * ring * math.sin(arc)))
        layer.append((rx, 0, 0))
        vertices.extend(tuple(a + b for a, b in zip(center, rotate_x(v, angle))) for v in layer)

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


def facial_contract():
    return {
        'revision': FACE_REVISION, 'bind_is_open': True, 'parent': 'Head',
        'bones': list(FACE_BONES), 'total_rig_bones': 39,
        'source_coordinates': 'metres, Z up, -Y forward; actor scale .5 once',
        'eye_centers_source_m': {s: eye_center(sign) for s, sign in (('L', 1), ('R', -1))},
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
        'eye_housing': 'fixed rear shell weighted to Head, recessed .8 mm behind movable lids; covers posterior white during closure',
        'runtime_writer': 'one shared facial driver after Animator/Playable evaluation; menu supplies target only',
        'runtime_binding': 'serialize axes transformed into each imported bone bind-local frame; do not assume FBX local XYZ',
        'body_clips': '15 existing IDs unchanged; all facial tracks neutral, runtime overwrites six facial controls only',
        'customization': 'MosquitoSkin renderer; existing Mosquito_Shell color binding. EyeWhite/Expression remain unchanged.',
        'native_status': 'source candidate only; geometry, FBX axes, closure and runtime visibility await Director slot',
    }


def create_face(c, *, mesh, ellipsoid, section_mesh, shell, eye, pupil):
    for side, sign in (('L', 1), ('R', -1)):
        center = eye_center(sign)
        for role in ('Pupil', 'LidUpper', 'LidLower'):
            c.bone(role + '.' + side, center, (center[0] + .012, center[1], center[2]), 'Head')
        vertices, faces = section_mesh(EYE_SECTIONS)
        mesh('Eye.' + side, [(x + center[0], y, z) for x, y, z in vertices], faces, eye, 'Head')
        ellipsoid('Pupil.' + side, (sign * .029, -.120, .113),
                  (.006, .0035, .009), pupil, 'Pupil.' + side, 10, 6)
        for upper in (True, False):
            name = ('LidUpper.' if upper else 'LidLower.') + side
            vertices, faces = lid_mesh(sign, upper)
            mesh(name, vertices, faces, shell, name)
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
