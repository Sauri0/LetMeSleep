"""Mosquito specialist geometry; Blender helpers are injected by the orchestrator.

Source coordinates are metres, +Z up, -Y forward; Unity applies scale .5.
create_mosquito returns a bound Character without animation, export or rendering.
"""
import math
from author_mosquito_face import create_face, facial_contract

REVISION = 'mosquito-facial-flight-r4-source'
UNITY_SCALE = .5
COLLISION_RADIUS = .055
SURFACE_ROOT_OFFSET = .057
SUPPORT_Z = -.114
MOUTH = (0, -.190, 0)
PROBOSCIS_BASE = (0, -.096, .092)
THORAX = (0, .015, .110)

# Absolute bind coordinates, independent of the new visual sections.
# Seven existing sockets, including the concealed wing axillae, stay unchanged.
SOCKETS = (
    ('Socket.Mouth', MOUTH, (0, -.200, 0), 'Proboscis'),
    ('Socket.Back', (0, .015, .085), (0, .015, .100), 'Thorax'),
    ('Socket.CameraTarget', (0, 0, 0), (0, -.020, 0), 'Thorax'),
    ('Socket.AimForward', (0, -.080, 0), (0, -.120, 0), 'Head'),
    ('Socket.GroundContact', (0, 0, SUPPORT_Z), (0, -.020, SUPPORT_Z), 'Root'),
    ('Socket.WingRoot.L', (.017, 0, .082), (.017, -.020, .082), 'Thorax'),
    ('Socket.WingRoot.R', (-.017, 0, .082), (-.017, -.020, .082), 'Thorax'),
)

# Broad roof, sloping temples, chamfered cheeks and narrow flat underside.
SECTION = ((.40, 1), (.85, .70), (1, .10), (.75, -.60), (.32, -.95),
           (-.32, -.95), (-.75, -.60), (-1, .10), (-.85, .70), (-.40, 1))
THORAX_SECTIONS = ((-.040, .102, .019, .026), (-.020, .112, .038, .040),
                   (.012, .113, .040, .041), (.039, .107, .028, .032),
                   (.053, .099, .017, .021))
HEAD_SECTIONS = ((-.039, .100, .020, .023), (-.055, .106, .034, .030),
                 (-.073, .107, .047, .031), (-.090, .104, .040, .026),
                 (-.103, .098, .013, .012))
ABDOMEN_SECTIONS = ((.039, .105, .018, .019), (.062, .097, .025, .029),
                    (.081, .086, .032, .032), (.104, .067, .030, .029),
                    (.125, .049, .026, .025), (.148, .027, .020, .020),
                    (.169, .007, .014, .014), (.188, -.012, .007, .008),
                    (.204, -.030, .0015, .002))


def section_mesh(sections, profile=SECTION):
    """Pure data for a faceted shell, with closed planar end caps."""
    n = len(profile)
    vertices = [(x * width, y, z + dz * height)
                for y, z, width, height in sections for x, dz in profile]
    faces = [tuple(reversed(range(n)))]
    for row in range(len(sections) - 1):
        for j in range(n):
            a = row * n + j
            b = row * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple((len(sections) - 1) * n + j for j in range(n)))
    return vertices, faces


def leg_points(side, index):
    """Six longer legs, staggered laterally on the unchanged support plane."""
    y, dy = ((-.033, -.052), (.006, .012), (.042, .067))[index - 1]
    knee_x = (.097, .112, .089)[index - 1]
    knee_z = (.006, -.003, .002)[index - 1]
    ankle_x = (.107, .094, .081)[index - 1]
    return ((side * .027, y, .099 - (index - 1) * .004),
            (side * knee_x, y + dy * .4, knee_z),
            (side * ankle_x, y + dy, -.1076),
            (side * (ankle_x + .012), y + dy + .006, -.1126))


def wing_mesh(side):
    """Lanceolate membrane with a raised spar and real thickness."""
    # The membrane's transverse axis lies mainly in X/Z, facing front/back.
    # R2's span/y-width alignment collapsed to a needle in the neutral front view.
    outline = ((.017, 0, .082), (.04882, .01530, .11743), (.11394, .04675, .19067),
               (.224, .085, .239), (.18346, .06120, .17193), (.10955, .03230, .12333),
               (.04667, .01020, .09446))
    ridge = ((.05914, .01415, .11396), (.12174, .03775, .16144), (.18334, .06515, .20816))
    top = [(side * x, y, z) for x, y, z in outline + ridge]
    triangles = [(0, 1, 7), (0, 7, 6), (1, 2, 8), (1, 8, 7),
                 (2, 3, 9), (2, 9, 8), (3, 4, 9), (4, 5, 8),
                 (4, 8, 9), (5, 6, 7), (5, 7, 8)]
    vertices = top + [(x - side * .247 * .00045, y + .95 * .00045, z - .187 * .00045) for x, y, z in top]
    faces = triangles + [tuple(i + 10 for i in reversed(f)) for f in triangles]
    faces += [(i, i + 10, (i + 1) % 7 + 10, (i + 1) % 7) for i in range(7)]
    if side < 0:
        faces = [tuple(reversed(face)) for face in faces]
    return vertices, faces


def _weighted_abdomen(obj):
    first = obj.vertex_groups.new(name='Abdomen01')
    second = obj.vertex_groups.new(name='Abdomen02')
    for v in obj.data.vertices:
        t = max(0, min(1, (v.co.y - .088) / .045))
        t = t * t * (3 - 2 * t)
        if t < 1:
            first.add([v.index], 1 - t, 'REPLACE')
        if t > 0:
            second.add([v.index], t, 'REPLACE')


def _brow_mesh(side):
    # Broad carapace overhang; no torus or sphere surrounding the white eyeball.
    front = [(side * x, y, z) for x, y, z in
             ((.005, -.108, .134), (.020, -.110, .143),
              (.044, -.097, .139), (.050, -.088, .127),
              (.037, -.102, .133), (.020, -.115, .137))]
    back = [(x, y + .008, z + .001) for x, y, z in front]
    front_faces = [(0, 1, 5), (1, 2, 4), (1, 4, 5), (2, 3, 4)]
    faces = front_faces + [tuple(i + 6 for i in reversed(f)) for f in front_faces]
    faces += [(i, i + 6, (i + 1) % 6 + 6, (i + 1) % 6) for i in range(6)]
    if side < 0:
        faces = [tuple(reversed(face)) for face in faces]
    return front + back, faces


def create_mosquito(*, Character, material, tube, ellipsoid, strip, mesh):
    c = Character('Mosquito')
    shell = material('Mosquito_Shell', (.36, .045, .027), .76)
    belly = material('Mosquito_Abdomen', (.49, .085, .040), .80)
    dark = material('Mosquito_Legs', (.11, .060, .067), .82)
    eye = material('Mosquito_EyeWhite', (.94, .91, .82), .58)
    pupil = material('Mosquito_Expression', (.024, .022, .035), .64)
    # Preserve the exact name used by Unity's membrane shader branch.
    wing = material('Mosquito_Wing', (.62, .67, .83, .42), .85)
    vein = material('Mosquito_WingVein', (.39, .40, .53), .86)
    c.bone('Root', (0, 0, 0), (0, 0, .03), deform=False)
    c.bone('Thorax', THORAX, (0, -.035, .104), 'Root')
    c.bone('Head', (0, -.041, .100), (0, -.084, .105), 'Thorax')
    c.bone('Abdomen01', (0, .039, .105), (0, .110, .062), 'Thorax')
    c.bone('Abdomen02', (0, .110, .062), (0, .204, -.030), 'Abdomen01')
    c.bone('Proboscis', PROBOSCIS_BASE, MOUTH, 'Head')
    for name, head, tail, parent in SOCKETS:
        c.bone(name, head, tail, parent, False)
    for name, sections, mat, bone in (
            ('Thorax', THORAX_SECTIONS, shell, 'Thorax'),
            ('Head', HEAD_SECTIONS, shell, 'Head')):
        vertices, faces = section_mesh(sections)
        mesh(name, vertices, faces, mat, bone)
    vertices, faces = section_mesh(ABDOMEN_SECTIONS)
    abdomen = mesh('Abdomen', vertices, faces, belly)
    _weighted_abdomen(abdomen)
    abdomen.data.materials.append(shell)
    for poly in abdomen.data.polygons:
        row = (poly.index - 1) // len(SECTION)
        if poly.index > 0 and row in (0, 3, 6):
            poly.material_index = 1
    tube('Proboscis', [PROBOSCIS_BASE, (0, -.112, .077), (0, -.153, .035), MOUTH],
         [.008, .006, .003, .0007], [.0065, .005, .0025, .0007], shell, 'Proboscis', 6)
    create_face(c, mesh=mesh, ellipsoid=ellipsoid, section_mesh=section_mesh,
                shell=shell, eye=eye, pupil=pupil)
    for side, sign in (('L', 1), ('R', -1)):
        vertices, faces = _brow_mesh(sign)
        mesh('Brow.' + side, vertices, faces, shell, 'Head')
        strip('Antenna.' + side,
              [(sign * .013, -.062, .134), (sign * .023, -.068, .166),
               (sign * .041, -.081, .175)], .0017, dark, 'Head')
        c.bone('Wing.' + side, (sign * .017, 0, .082),
               (sign * .220, .065, .181), 'Thorax')
        vertices, faces = wing_mesh(sign)
        mesh('WingMembrane.' + side, vertices, faces, wing, 'Wing.' + side)
        strip('WingLeadingEdge.' + side, [vertices[i] for i in (0, 1, 2, 3)],
              .00085, vein, 'Wing.' + side)
        strip('WingVein.' + side, [vertices[i] for i in (0, 7, 8, 9, 3)],
              .00055, vein, 'Wing.' + side)
        strip('WingVein.Branch.' + side, [vertices[i] for i in (8, 5)],
              .00042, vein, 'Wing.' + side)
        for i in range(1, 4):
            points = leg_points(sign, i)
            parent = 'Thorax'
            for j in range(3):
                name = f'Leg{i}{j + 1:02d}.{side}'
                c.bone(name, points[j], points[j + 1], parent)
                parent = name
                radius = (.0033, .0025, .00165)[j]
                tube('Limb_' + name, [points[j], points[j + 1]],
                     [radius, radius * .72], [radius, radius * .72],
                     shell if j == 0 else dark, name, 6)
                if j < 2:
                    ellipsoid('LegJoint_' + name, points[j + 1],
                              (.0036, .0036, .0036), dark, name, 8, 4)
    c.bind()
    mouth = c.rig.data.bones['Socket.Mouth'].head_local
    c.contact = {
        'gameplay_tip_rest_unity_m': [mouth.x * UNITY_SCALE, mouth.z * UNITY_SCALE, -mouth.y * UNITY_SCALE],
        'gameplay_collision_radius_m': COLLISION_RADIUS,
        'gameplay_surface_root_offset_m': SURFACE_ROOT_OFFSET,
        'ground_contact_rest_unity_m': [0, -SURFACE_ROOT_OFFSET, 0],
        'surface_rotation_contract': 'local +Y outward normal, forward projected tangent; runtime validation belongs to Gameplay/Presentation',
        'geometry_revision': REVISION,
        'face': facial_contract(),
        'proboscis_descent_degrees': math.degrees(math.atan2(PROBOSCIS_BASE[2], abs(MOUTH[1] - PROBOSCIS_BASE[1]))),
        'art_acceptance': 'pending real render, complete clips, Unity and independent review',
    }
    return c
