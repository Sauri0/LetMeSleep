"""Mosquito specialist geometry; Blender helpers are injected by the orchestrator.

Source coordinates are metres, +Z up, -Y forward; Unity applies scale .5.
create_mosquito returns a bound Character without animation, export or rendering.

Sketch r1 (PER-03 mosquito 01 red, PER-07 BASE MOSQUITO, UI-06): compact head
behind huge bulging white eyes, round robust faceted thorax, big pointed
abdomen tilted back/down with dark bands, long thin dark jointed legs, long thin
proboscis forward/down and broad faceted leaf wings. Gameplay contracts
(Root, support plane, collision radius, root offset, Mouth tip, sockets, bone
names/hierarchy, material names, renderer buckets) are unchanged.

Sketch r2 (art-director round 2, in priority order): wing blades re-aimed about
their unchanged roots (front view ~32 -> 52 deg from vertical, profile ~22 ->
50 deg, +15% longer, twisted about the span, lavender #C9C0F2 alpha .5 with
lighter edges); three masses in profile (eyes in cups, humped thorax,
abdomen): head radius -35% and 9 mm deeper, thorax +17% taller with the crest
up/back and wider, eyes moved back so the cups overlap the thorax front;
coarser facets (thorax/head 80-face geodesics instead of 320, 8-sided abdomen,
10x6 eyes); eye cups, rolled lip and brows in author_mosquito_face; maroon
tapered legs with dark knuckles, coxae and feet; a faceted snout wedge plus a
~45% finer needle on the unchanged Proboscis line; broad stepped dorsal
abdomen bands and a wide short waist.

Sketch r3 (art-director round 3, in priority order): (1) leaf/teardrop abdomen
with a waist 40% of the thorax height, thickest (~0.93 thorax height) at ~36%
of its visible length, tapering to a point over the last 40%, axis turned
~12 deg further down (~46 deg below horizontal), smooth silhouette (0.5 mm plate
bevel, colour-only #6E1418 bands over ~28% of each step); (2) thorax radii -15%
(hump and 80-face facets kept) so legs, needle and eyes regain PER-07's ratios,
femora keep a visual root inside the smaller thorax (hip bones unchanged);
(3) lanceolate wings: chord -32%, widest at ~57% of the span, petiole <=25% of
the widest chord, straight leading edge, profile sweep 50 -> 40 deg and 10 deg
more outward, #DCDDF5 alpha .45 membrane rendered blended (no dither grain);
(5) matte shell (roughness .9, specular .2) and a faceted belly shade: per-face
vertex colour multiplies the lower ~40% of thorax, head and abdomen toward
#6E1418; (6) knee knuckles x1.65 (the thorax also shrank), a second dark ring
at mid tibia (geometry only) and femora .0045 at the knee. The base has no
brows (PER-07 / PER-03 NEUTRAL / UI-06): the rolled lip is the thick upper rim
and the brow band stays available as a customization option (BASE_BROWS).
Look-dev only (Blender; the audit/Unity palette is unchanged): the eye white
gets the same faint self-light the human eyes use, and wing membranes/veins
cast no shadow, as Unity already renders them.
"""
import math
import random
from author_mosquito_face import (create_face, facial_contract, outward, eye_mesh, pupil_mesh,
                                  lid_front_degrees, upper_lid_point, upper_lip_mesh)

REVISION = 'mosquito-sketch-r3-source'
UNITY_SCALE = .5
COLLISION_RADIUS = .055
SURFACE_ROOT_OFFSET = .057
SUPPORT_Z = -.114
MOUTH = (0, -.190, 0)
# Proboscis bone head is the unchanged R4 bind; the visible snout and needle
# continue along the same line back into the head.
PROBOSCIS_BASE = (0, -.096, .092)
PROBOSCIS_ROOT_EXTENSION = .22
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

# Palette from docs/v030/GUIA-ESTILO-BOCETOS.md section 3 plus the round-2 art
# direction (sRGB hex); Blender material colours are scene-linear, so each hex
# is converted once here.
PALETTE_HEX = {
    'shell': '#B8262B',      # saturated body red
    'segment': '#6E1418',    # dark abdomen bands and the belly shade target
    'legs': '#3A1418',       # maroon legs (r2: was neutral #2A1A1A, read grey-brown)
    'joint': '#1A0C0E',      # knuckles, coxae, tibia rings and feet
    'eye': '#FAFAF8',        # r3: cleaner white
    'pupil': '#141218',
    'wing': '#DCDDF5',       # r3: style-guide lavender-white membrane, alpha .45
    'vein': '#C3C3E6',       # subtle veins
    'wing_edge': '#ECECFA',  # blade edges a little lighter than the membrane
}
# Belly shade: per-face vertex-colour multiplier (scene linear) that turns the
# shell #B8262B into the segment #6E1418 at the lowest faces.
BELLY_SHADE_FRACTION = .40


def srgb(hex_color, alpha=None):
    values = tuple(int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5))
    return values + ((alpha,) if alpha is not None else ())


def srgb_to_linear(hex_color, alpha=None):
    linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in srgb(hex_color)]
    return tuple(linear) + ((alpha,) if alpha is not None else ())


def palette_material(material, name, key, roughness, alpha=None, specular=None):
    """Same convention as the human build: the audit/Unity colour keeps the
    sketch sRGB value (Unity SetColor is gamma space); only the Blender
    Principled node receives the linear equivalent so source renders match."""
    m = material(name, srgb(PALETTE_HEX[key], alpha), roughness)
    node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*srgb_to_linear(PALETTE_HEX[key]), 1)
    if specular is not None and 'Specular IOR Level' in node.inputs:
        node.inputs['Specular IOR Level'].default_value = specular
    return m


def belly_shade():
    """Linear multiplier taking the shell colour to the segment colour."""
    return tuple(d / s for d, s in zip(srgb_to_linear(PALETTE_HEX['segment']), srgb_to_linear(PALETTE_HEX['shell'])))


def shade_factor(height):
    """Faceted gradient: 1 above BELLY_SHADE_FRACTION, belly_shade() at 0,
    convex so the lowest facet rows read as the dark crimson belly."""
    t = max(0.0, min(1.0, height / BELLY_SHADE_FRACTION)) ** 1.5
    return tuple(b + (1 - b) * t for b in belly_shade())


def multiply_vertex_color(m, layer):
    """Blender look-dev: Base Color x the per-face shade attribute. The audit
    colour is unchanged; Unity receives the attribute as mesh colours."""
    tree = m.node_tree
    node = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    attribute = tree.nodes.new('ShaderNodeVertexColor')
    attribute.layer_name = layer
    mix = tree.nodes.new('ShaderNodeMix')
    mix.data_type, mix.blend_type = 'RGBA', 'MULTIPLY'
    mix.inputs[0].default_value = 1.0
    a = next(s for s in mix.inputs if s.name == 'A' and s.type == 'RGBA')
    b = next(s for s in mix.inputs if s.name == 'B' and s.type == 'RGBA')
    a.default_value = node.inputs['Base Color'].default_value
    tree.links.new(attribute.outputs['Color'], b)
    tree.links.new(next(s for s in mix.outputs if s.type == 'RGBA'), node.inputs['Base Color'])


# Round, robust faceted thorax whose extra height rises up and back (humped
# crest above the eyes); its front overlaps the eye cups. r3: radii -15%.
THORAX_CENTER, THORAX_RADII = (0, .002, .124), (.05525, .05185, .0493)
THORAX_HUMP_RISE, THORAX_HUMP_BACK = .0064, .0051
THORAX_FREQUENCY = 2
# Head r2: radius -35% and 9 mm deeper into the thorax; mostly hidden between
# the eye cups, it bridges them to the thorax and anchors the snout.
HEAD_CENTER, HEAD_RADII = (0, -.041, .122), (.0275, .027, .026)
HEAD_FREQUENCY = 2
# Big pointed leaf abdomen: slightly arched axis from inside the thorax,
# 44 deg below horizontal (r2 ~34; ~46 visually with the arch), .245 m long;
# the tip stays ~60 mm above the support plane.
ABDOMEN_START, ABDOMEN_ARCH = (0, .038, .112), .008
ABDOMEN_LENGTH, ABDOMEN_DESCENT_DEGREES = .245, 44.0
ABDOMEN_TIP = (0, ABDOMEN_START[1] + ABDOMEN_LENGTH * math.cos(math.radians(ABDOMEN_DESCENT_DEGREES)),
               ABDOMEN_START[2] - ABDOMEN_LENGTH * math.sin(math.radians(ABDOMEN_DESCENT_DEGREES)))
ABDOMEN_RADII = (.045, .0465)
# Leaf/teardrop: hidden root inside the thorax, waist just outside it (.44 of
# the max = 40% of the thorax height), widest at t=.42 (~36% of the visible
# length), then a long taper to the point over the last 40%.
ABDOMEN_PROFILE = ((0, .60), (.05, .50), (.10, .44), (.18, .64), (.27, .84), (.35, .96), (.42, 1.0),
                   (.50, .97), (.60, .86), (.70, .67), (.80, .46), (.90, .24), (1.0, .03))
# Plates: each segment is a light row, then a colour-only dark band covering
# ~28% of the step, ending in a 0.5 mm bevel down to the next segment (1%
# step, <=0.5 mm). The dark colour sits on the dorsal/lateral faces.
ABDOMEN_SEGMENTS = (0, .17, .32, .46, .59, .71, .83)
ABDOMEN_BAND_FRACTION = .28
ABDOMEN_BEVEL_M = .0005
ABDOMEN_STEP_IN = .99
ABDOMEN_SIDES = 8
# Leaf wing drawn in (span, chord) and mapped on to a tilted plane. The pivot
# stays at the concealed R4 axilla (Wing.L/R bone heads untouched); only the
# blade turns about it. r3: span 62 deg from vertical in front view (+10 deg
# outward) and 40 deg in profile (was 50), so the far wing leans well away
# from vertical in the three-quarter view instead of showing its edge.
WING_SPAN = (math.tan(math.radians(62)), math.tan(math.radians(40)), 1.0)
WING_CHORD_HINT = (.40, .34, -.85)
# Blade twisted about its own span so both membranes stay readable in the
# front, profile and three-quarter views (no edge-on 'rabbit ear').
WING_TWIST_DEGREES = -20
WING_SCALE = 1.15
# r3 lanceolate blade: straight leading edge (indices 0-4), widest chord
# ~.0545 at u ~.15 (57% of the span, -32% vs r2), petiole at u=.05 ~24% of it.
WING_OUTLINE = tuple((u * WING_SCALE, v * WING_SCALE) for u, v in (
    (0, 0), (.070, -.007), (.135, -.010), (.200, -.008), (.262, 0),
    (.222, .024), (.160, .046), (.108, .042), (.050, .008)))
WING_RIDGE = tuple((u * WING_SCALE, v * WING_SCALE) for u, v in ((.080, .007), (.135, .014), (.195, .011)))
WING_RIDGE_RAISE = .004
WING_THICKNESS = .00045
WING_TRIANGLES = ((0, 1, 9), (0, 9, 8), (1, 2, 10), (1, 10, 9), (2, 3, 11), (2, 11, 10),
                  (3, 4, 11), (4, 5, 11), (5, 6, 10), (5, 10, 11), (6, 7, 9), (6, 9, 10), (7, 8, 9))
# (prefix, outline indices, width, material key). Prefixes keep the export
# renderer buckets: WingLeadingEdge.* / WingVein.* -> MosquitoVeins.
WING_VEINS = (('WingLeadingEdge.', (0, 1, 2, 3, 4), .0008, 'wing_edge'),
              ('WingVein.Edge.', (4, 5, 6, 7, 8, 0), .00045, 'wing_edge'),
              ('WingVein.', (0, 9, 10, 11, 4), .00055, 'vein'),
              ('WingVein.Branch.', (10, 6), .0004, 'vein'),
              ('WingVein.Branch2.', (11, 5), .0004, 'vein'),
              ('WingVein.Branch3.', (9, 7), .0004, 'vein'))
# Leg joints are the unchanged R4 bind: Unity's MosquitoRagdollBuilder
# validates hip/knee/ankle within .2 mm, and surface_step reads them. Only the
# skin changes: tapered segments, dark faceted knuckles split between the two
# bones, coxae, a dark ring at mid tibia and dark feet. r3: femur .0045 at the
# knee, knee knuckle x1.65 (~0.15 of the smaller thorax), ankle knuckle x1.2
# (kept above the support plane).
LEG_RADII = ((.0064, .0045), (.0048, .0024), (.0017, .00135))
LEG_JOINT_RADII = (.0095, .0048)
LEG_COXA_RADIUS = .0080
TIBIA_RING_T, TIBIA_RING_RADIUS = .5, .0054
# Hips outside the smaller thorax get a visual femur root this deep inside it.
FEMUR_ROOT_DEPTH = .80
LEG_FOOT_START = .70
LEG_TOE_RADIUS = .0012
# Snout wedge between/below the eyes on the unchanged Proboscis line
# (t: 0 = extended base, 1 = Mouth), then a needle ~45% finer than r1.
SNOUT_STATIONS = ((-.14, .0075, .0075, .0065), (-.05, .0100, .0098, .0082), (.04, .0138, .0104, .0094),
                  (.12, .0092, .0070, .0066), (.19, .0046, .0040, .0040))
SNOUT_SECTION = ((0, 1), (.86, .30), (.58, -.62), (0, -1), (-.58, -.62), (-.86, .30))
NEEDLE_STATIONS = ((.12, .0046), (.30, .0034), (.55, .0022), (.80, .0012), (1.0, .0005))
# Brow band lying on the upper shutter (0.3 mm clear of it: the shutter
# rotates on the same ellipsoid, so it never cuts the brow). r3: not part of
# the base look (it read as a worried brow / a ridged helmet in profile); kept
# as the customization option, emitted only when BASE_BROWS is True.
BASE_BROWS = False
BROW_LATITUDES = (-34, 36)
BROW_CLEARANCE, BROW_THICKNESS, BROW_WIDTH_DEGREES = .0003, .0011, 17.0
BROW_ABOVE_EDGE_DEGREES = 9.0


def _add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def _sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def _scale(a, s):
    return tuple(x * s for x in a)


def _dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(a):
    length = math.sqrt(_dot(a, a))
    return tuple(x / length for x in a)


def _lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def rings_mesh(rings):
    """Closed loft through equal-size vertex rings, planar caps at both ends."""
    n = len(rings[0])
    vertices = [v for ring in rings for v in ring]
    faces = [tuple(reversed(range(n)))]
    for row in range(len(rings) - 1):
        for j in range(n):
            a = row * n + j
            b = row * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    return outward(vertices, faces)


def section_mesh(sections, profile=None):
    """Legacy helper: rings perpendicular to Y from (y, z, width, height) tuples."""
    profile = profile or tuple((math.cos(math.tau * j / 10), math.sin(math.tau * j / 10)) for j in range(10))
    return rings_mesh([[(x * width, y, z + dz * height) for x, dz in profile]
                       for y, z, width, height in sections])


def icosphere(frequency):
    """Unit geodesic sphere: each icosahedron face split into frequency^2."""
    t = (1 + math.sqrt(5)) / 2
    corners = [_normalize(p) for p in ((-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0),
                                       (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
                                       (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1))]
    base = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4),
            (11, 10, 2), (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8),
            (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    points, index, faces = [], {}, []

    def vertex(weights):
        # Barycentric key shared by neighbouring faces along edges/corners.
        key = tuple(sorted((c, w) for c, w in weights if w))
        if key not in index:
            index[key] = len(points)
            points.append(_normalize(tuple(sum(corners[c][k] * w for c, w in key) for k in range(3))))
        return index[key]
    for a, b, c in base:
        grid = {}
        for i in range(frequency + 1):
            for j in range(frequency + 1 - i):
                grid[i, j] = vertex(((a, frequency - i - j), (b, i), (c, j)))
        for i in range(frequency):
            for j in range(frequency - i):
                faces.append((grid[i, j], grid[i + 1, j], grid[i, j + 1]))
                if i + j < frequency - 1:
                    faces.append((grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
    return points, faces


def faceted_ellipsoid(center, radii, frequency=3, jitter=.035, seed=0, deform=None):
    """Geodesic sphere scaled to an ellipsoid with deterministic facet jitter."""
    points, faces = icosphere(frequency)
    rng = random.Random(seed)
    vertices = []
    for p in points:
        s = 1 + jitter * rng.uniform(-1, 1)
        v = tuple(c + r * n * s for c, r, n in zip(center, radii, p))
        vertices.append(deform(v, p) if deform else v)
    return outward(vertices, faces)


def _thorax_hump(vertex, direction):
    """Extra crest volume rises up and back (PER-07 hunched thorax)."""
    x, y, z = vertex
    up, back = max(0, direction[2]), direction[1]
    return (x, y + THORAX_HUMP_BACK * up * max(0, back), z + THORAX_HUMP_RISE * up ** 1.5 * (.7 + .3 * back))


def thorax_mesh():
    return faceted_ellipsoid(THORAX_CENTER, THORAX_RADII, THORAX_FREQUENCY, jitter=.035, seed=31,
                             deform=_thorax_hump)


def head_mesh():
    return faceted_ellipsoid(HEAD_CENTER, HEAD_RADII, HEAD_FREQUENCY, jitter=.03, seed=17)


def _abdomen_scale(t):
    for (t0, s0), (t1, s1) in zip(ABDOMEN_PROFILE, ABDOMEN_PROFILE[1:]):
        if t <= t1:
            return s0 + (s1 - s0) * (t - t0) / (t1 - t0)
    return ABDOMEN_PROFILE[-1][1]


def _abdomen_point(t):
    axis = (0, ABDOMEN_TIP[1] - ABDOMEN_START[1], ABDOMEN_TIP[2] - ABDOMEN_START[2])
    up = _normalize((0, -axis[2], axis[1]))
    if up[2] < 0:
        up = _scale(up, -1)
    return _add(_add(ABDOMEN_START, _scale(axis, t)), _scale(up, ABDOMEN_ARCH * math.sin(math.pi * t)))


def abdomen_stations():
    """(t, scale factor, row-after-is-band) for the plate loft; the leaf
    profile knots are extra rings so the waist and taper are real geometry."""
    length = math.dist(ABDOMEN_START, ABDOMEN_TIP)
    bevel = ABDOMEN_BEVEL_M / length
    bounds = ABDOMEN_SEGMENTS + (1.0,)
    factors, bands = {}, []
    for index, (a, b) in enumerate(zip(bounds, bounds[1:])):
        factors[a] = ABDOMEN_STEP_IN if index else 1.0
        if index == len(bounds) - 2:
            break
        band = b - (b - a) * ABDOMEN_BAND_FRACTION
        factors[band] = 1.0
        factors[b - bevel] = 1.0
        bands.append((band, b))
    for t, _ in ABDOMEN_PROFILE:
        if all(abs(t - s) > .012 for s in factors):
            factors[t] = 1.0
    factors[1.0] = 1.0
    return [(t, factors[t], any(lo - 1e-9 <= t < hi - 1e-9 for lo, hi in bands)) for t in sorted(factors)]


def abdomen_mesh():
    """Stepped faceted loft; returns vertices, faces and per-face dark-band flags."""
    stations = abdomen_stations()
    rng = random.Random(7)
    rings, band_rows = [], []
    for index, (t, factor, band_after) in enumerate(stations):
        center = _abdomen_point(t)
        ahead = _abdomen_point(min(1, t + .01))
        behind = _abdomen_point(max(0, t - .01))
        tangent = _normalize(tuple(a - b for a, b in zip(ahead, behind)))
        up = _normalize((0, -tangent[2], tangent[1]))
        if up[2] < 0:
            up = _scale(up, -1)
        scale = _abdomen_scale(t) * factor
        ring = []
        for j in range(ABDOMEN_SIDES):
            angle = math.tau * j / ABDOMEN_SIDES + math.pi / ABDOMEN_SIDES
            wobble = 1 + (.025 * rng.uniform(-1, 1) if 0 < t < .95 else 0)
            lateral = ABDOMEN_RADII[0] * scale * wobble * math.cos(angle)
            vertical = ABDOMEN_RADII[1] * scale * wobble * math.sin(angle)
            ring.append(_add(_add(center, (lateral, 0, 0)), _scale(up, vertical)))
        rings.append(ring)
        if index < len(stations) - 1:
            band_rows.append(band_after)
    vertices, faces = rings_mesh(rings)
    n = ABDOMEN_SIDES
    # Dark plates cover the back and flanks (tergites) only: seen from the
    # front the ventral faces stay red, so the body never reads as a bee target.
    dorsal = [math.sin(math.tau * (j + .5) / n + math.pi / n) > -1e-9 for j in range(n)]
    flags = [False] + [band_rows[row] and dorsal[j] for row in range(len(rings) - 1) for j in range(n)] + [False]
    return vertices, faces, flags


def abdomen_face_heights():
    """Per-face 0 (ventral) .. 1 (dorsal) height across the section, for the
    belly shade; caps take their ring's mid height."""
    n = ABDOMEN_SIDES
    rows = len(abdomen_stations()) - 1
    around = [(math.sin(math.tau * (j + .5) / n + math.pi / n) + 1) * .5 for j in range(n)]
    return [.5] + [around[j] for _ in range(rows) for j in range(n)] + [.5]


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


def _thorax_norm(point):
    return sum(((a - c) / r) ** 2 for a, c, r in zip(point, THORAX_CENTER, THORAX_RADII))


def femur_root(hip, knee):
    """Visual femur start: the hip bone head itself, or, where the r3 thorax
    no longer contains it, the point on the femur line extended back into
    the thorax (FEMUR_ROOT_DEPTH). Bones and weights are unchanged."""
    back = _normalize(_sub(hip, knee))
    for step in range(121):
        point = _add(hip, _scale(back, step * .0005))
        if _thorax_norm(point) <= FEMUR_ROOT_DEPTH:
            return point
    raise ValueError('femur line never enters the thorax')


def thorax_exit(hip, knee):
    """Where a femur leaves the (unjittered, unhumped) thorax ellipsoid."""
    lo, hi = 0.0, 1.0
    for _ in range(40):
        mid = (lo + hi) * .5
        p = _lerp(hip, knee, mid)
        inside = _thorax_norm(p) < 1
        lo, hi = (mid, hi) if inside else (lo, mid)
    return _lerp(hip, knee, hi)


def _wing_frame(side):
    span = _normalize(WING_SPAN)
    hint = WING_CHORD_HINT
    chord = _normalize(tuple(h - span[i] * _dot(hint, span) for i, h in enumerate(hint)))
    twist = math.radians(WING_TWIST_DEGREES)
    chord = _normalize(_add(_scale(chord, math.cos(twist)), _scale(_cross(span, chord), math.sin(twist))))
    normal = _cross(span, chord)
    if side < 0:
        span, chord, normal = ((-v[0], v[1], v[2]) for v in (span, chord, normal))
    return span, chord, normal


def wing_mesh(side):
    """Broad faceted leaf membrane with a raised midrib and real thickness."""
    root = (side * .017, 0, .082)
    span, chord, normal = _wing_frame(side)
    uv = WING_OUTLINE + WING_RIDGE
    count = len(uv)
    top = []
    for index, (u, v) in enumerate(uv):
        raise_ = WING_RIDGE_RAISE if index >= len(WING_OUTLINE) else 0
        top.append(_add(root, _add(_add(_scale(span, u), _scale(chord, v)), _scale(normal, raise_))))
    triangles = []
    for a, b, c in WING_TRIANGLES:
        area = ((uv[b][0] - uv[a][0]) * (uv[c][1] - uv[a][1]) - (uv[c][0] - uv[a][0]) * (uv[b][1] - uv[a][1]))
        triangles.append((a, b, c) if area > 0 else (a, c, b))
    vertices = top + [_add(p, _scale(normal, -WING_THICKNESS)) for p in top]
    faces = triangles + [tuple(i + count for i in reversed(f)) for f in triangles]
    outline = len(WING_OUTLINE)
    faces += [((i + 1) % outline, i, i + count, (i + 1) % outline + count) for i in range(outline)]
    if side < 0:
        faces = [tuple(reversed(face)) for face in faces]
    return vertices, faces


def _brow_mesh(side):
    """Dark arched band lying on the upper shutter, just above its rolled lip.

    Inner end toward the snout, ~80 deg of arc seen from the front, only
    1.1 mm proud of the lid so the profile reads as a band, not a hook.
    """
    samples = 7
    rings = []
    for i in range(samples):
        t = i / (samples - 1)
        latitude = math.radians(BROW_LATITUDES[0] + (BROW_LATITUDES[1] - BROW_LATITUDES[0]) * t)
        taper = .55 + .45 * math.sin(math.pi * t) ** .7
        low = lid_front_degrees(True, latitude) + BROW_ABOVE_EDGE_DEGREES + 2.5 * math.sin(math.pi * t)
        width = BROW_WIDTH_DEGREES * taper
        inner, outer = BROW_CLEARANCE, BROW_CLEARANCE + BROW_THICKNESS * taper
        rings.append([upper_lid_point(side, latitude, low, inner), upper_lid_point(side, latitude, low + width * .18, outer),
                      upper_lid_point(side, latitude, low + width * .82, outer), upper_lid_point(side, latitude, low + width, inner)])
    return rings_mesh(rings)


def _proboscis_line():
    base = tuple(b + (b - m) * PROBOSCIS_ROOT_EXTENSION for b, m in zip(PROBOSCIS_BASE, MOUTH))
    tangent = _normalize(_sub(MOUTH, base))
    lateral = (1.0, 0.0, 0.0)
    up = _normalize(_cross(tangent, lateral))
    if up[2] < 0:
        up = _scale(up, -1)
    return base, tangent, lateral, up


def snout_mesh():
    """Faceted wedge between/below the eyes (~.45 eye diameter wide)."""
    base, _, lateral, up = _proboscis_line()
    rings = []
    for t, half_width, top, bottom in SNOUT_STATIONS:
        center = _lerp(base, MOUTH, t)
        ring = []
        for a, b in SNOUT_SECTION:
            ring.append(_add(center, _add(_scale(lateral, a * half_width), _scale(up, b * (top if b > 0 else bottom)))))
        rings.append(ring)
    return rings_mesh(rings)


def pure_meshes():
    """Every pure-data closed mesh the generator emits (plus the optional brow
    band, so the customization option stays checked), for Blender-free checks."""
    abdomen = abdomen_mesh()
    meshes = [('Thorax', *thorax_mesh()), ('Head', *head_mesh()), ('Abdomen', abdomen[0], abdomen[1]),
              ('Snout', *snout_mesh())]
    for side in (1, -1):
        meshes += [('Wing.' + str(side), *wing_mesh(side)), ('Brow.' + str(side), *_brow_mesh(side)),
                   ('LidLip.' + str(side), *upper_lip_mesh(side)),
                   ('Eye.' + str(side), *eye_mesh(side)), ('Pupil.' + str(side), *pupil_mesh(side))]
    return meshes


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


def _knuckle(tube, name, before, joint, after, radius, material, parent, child):
    """Elongated hexagonal knuckle; its halves follow the two bones."""
    incoming = _normalize(_sub(joint, before))
    outgoing = _normalize(_sub(after, joint))
    length = radius * 1.5
    obj = tube(name, [_sub(joint, _scale(incoming, length)), joint, _add(joint, _scale(outgoing, length))],
               [radius * .62, radius, radius * .62], [radius * .62, radius, radius * .62], material, parent, 6)
    if child:
        upper, lower = obj.vertex_groups[parent], obj.vertex_groups.new(name=child)
        middle, last = list(range(6, 12)), list(range(12, 18))
        upper.add(middle, .5, 'REPLACE')
        lower.add(middle, .5, 'REPLACE')
        upper.remove(last)
        lower.add(last, 1.0, 'REPLACE')
    return obj


SHADE_LAYER = 'Col'
EYE_LOOKDEV_EMISSION = .35


def _paint_belly_shade(bpy, thorax, head, abdomen):
    """Per-face (flat, faceted) shade attribute on every mosquito mesh: white
    everywhere except the lower ~40% of thorax, head and abdomen. Every mesh
    gets the layer so the export join never fills a missing one with black."""
    def paint(obj, factors):
        layer = obj.data.color_attributes.new(SHADE_LAYER, 'FLOAT_COLOR', 'CORNER')
        for poly, factor in zip(obj.data.polygons, factors):
            for loop in poly.loop_indices:
                layer.data[loop].color = (*factor, 1.0)

    def by_height(obj):
        # Face centre height within the mesh's full vertex extent.
        low = min(v.co.z for v in obj.data.vertices)
        high = max(v.co.z for v in obj.data.vertices)
        return [shade_factor((p.center.z - low) / (high - low)) for p in obj.data.polygons]

    painted = {thorax.name: by_height(thorax), head.name: by_height(head),
               abdomen.name: [shade_factor(h) for h in abdomen_face_heights()]}
    for obj in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
        paint(obj, painted.get(obj.name, [(1.0, 1.0, 1.0)] * len(obj.data.polygons)))


def create_mosquito(*, Character, material, tube, ellipsoid, strip, mesh):
    import bpy
    c = Character('Mosquito')
    # r3: matte shell (no pink sheen), belly shade by vertex colour.
    shell = palette_material(material, 'Mosquito_Shell', 'shell', .90, specular=.20)
    bands = palette_material(material, 'Mosquito_Abdomen', 'segment', .90, specular=.20)
    for m in (shell, bands):
        multiply_vertex_color(m, SHADE_LAYER)
    dark = palette_material(material, 'Mosquito_Legs', 'legs', .86, specular=.25)
    joint = palette_material(material, 'Mosquito_LegJoint', 'joint', .80, specular=.30)
    eye = palette_material(material, 'Mosquito_EyeWhite', 'eye', .60)
    pupil = palette_material(material, 'Mosquito_Expression', 'pupil', .40)
    # Preserve the exact name used by Unity's membrane shader branch. Blender
    # look-dev renders it blended with back faces culled (one layer, like
    # Unity's alpha material) instead of dithered hashing (grainy membrane).
    wing = palette_material(material, 'Mosquito_Wing', 'wing', .85, alpha=.45)
    if hasattr(wing, 'surface_render_method'):
        wing.surface_render_method = 'BLENDED'
    wing.use_backface_culling = True
    vein = palette_material(material, 'Mosquito_WingVein', 'vein', .80)
    edge = palette_material(material, 'Mosquito_WingEdge', 'wing_edge', .80)
    wing_materials = {'vein': vein, 'wing_edge': edge}
    c.bone('Root', (0, 0, 0), (0, 0, .03), deform=False)
    c.bone('Thorax', THORAX, (0, -.035, .104), 'Root')
    c.bone('Head', (0, -.041, .100), (0, -.084, .105), 'Thorax')
    c.bone('Abdomen01', (0, .039, .105), (0, .110, .062), 'Thorax')
    c.bone('Abdomen02', (0, .110, .062), (0, .204, -.030), 'Abdomen01')
    c.bone('Proboscis', PROBOSCIS_BASE, MOUTH, 'Head')
    for name, head, tail, parent in SOCKETS:
        c.bone(name, head, tail, parent, False)
    thorax = mesh('Thorax', *thorax_mesh(), shell, 'Thorax')
    head = mesh('Head', *head_mesh(), shell, 'Head')
    vertices, faces, flags = abdomen_mesh()
    abdomen = mesh('Abdomen', vertices, faces, shell)
    _weighted_abdomen(abdomen)
    abdomen.data.materials.append(bands)
    for poly, band in zip(abdomen.data.polygons, flags):
        poly.material_index = 1 if band else 0
    # Snout wedge stays with the face; the finer needle follows Proboscis and
    # still ends exactly at Socket.Mouth.
    mesh('Snout', *snout_mesh(), shell, 'Head')
    base = _proboscis_line()[0]
    tube('Proboscis', [_lerp(base, MOUTH, t) for t, _ in NEEDLE_STATIONS], [r for _, r in NEEDLE_STATIONS],
         [r for _, r in NEEDLE_STATIONS], shell, 'Proboscis', 6)
    create_face(c, mesh=mesh, shell=shell, eye=eye, pupil=pupil)
    # Look-dev: faint self-light keeps the underside of the huge white readable
    # under the sheet's top key (same practice as the human eye whites).
    eye_node = next(n for n in eye.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    eye_node.inputs['Emission Color'].default_value = eye_node.inputs['Base Color'].default_value
    eye_node.inputs['Emission Strength'].default_value = EYE_LOOKDEV_EMISSION
    for side, sign in (('L', 1), ('R', -1)):
        if BASE_BROWS:
            mesh('Brow.' + side, *_brow_mesh(sign), shell, 'Head')
        c.bone('Wing.' + side, (sign * .017, 0, .082),
               (sign * .220, .065, .181), 'Thorax')
        vertices, faces = wing_mesh(sign)
        membrane = mesh('WingMembrane.' + side, vertices, faces, wing, 'Wing.' + side)
        membrane.visible_shadow = False
        for prefix, indices, width, key in WING_VEINS:
            strip(prefix + side, [vertices[i] for i in indices], width, wing_materials[key],
                  'Wing.' + side).visible_shadow = False
        for i in range(1, 4):
            points = leg_points(sign, i)
            parent = 'Thorax'
            names = [f'Leg{i}{j + 1:02d}.{side}' for j in range(3)]
            for j, name in enumerate(names):
                c.bone(name, points[j], points[j + 1], parent)
                parent = name
                start, end = LEG_RADII[j]
                if j == 0:
                    # Femur from its visual root inside the thorax, full
                    # width up to where it leaves the shell (the coxa).
                    root = femur_root(points[0], points[1])
                    coxa = thorax_exit(root, points[1])
                    tube('Limb_' + name, [root, coxa, points[1]], [start, start, end], [start, start, end],
                         dark, name, 6)
                elif j == 1:
                    tube('Limb_' + name, [points[1], points[2]], [start, end], [start, end], dark, name, 6)
                    # Second dark ring at mid tibia: skin only, no bone.
                    ring = _lerp(points[1], points[2], TIBIA_RING_T)
                    along = _scale(_normalize(_sub(points[2], points[1])), TIBIA_RING_RADIUS * 1.1)
                    r = TIBIA_RING_RADIUS
                    tube('LegRing_' + name, [_sub(ring, along), ring, _add(ring, along)], [r * .66, r, r * .66],
                         [r * .66, r, r * .66], joint, name, 6)
                if j < 2:
                    _knuckle(tube, 'LegJoint_' + name, points[j], points[j + 1], points[j + 2],
                             LEG_JOINT_RADII[j], joint, name, names[j + 1])
                else:
                    # Tarsus: maroon, then a dark foot whose end ring keeps
                    # the r1 toe radius so the support plane is unchanged.
                    split = _lerp(points[2], points[3], LEG_FOOT_START)
                    tube('Limb_' + name, [points[2], split], [start, end], [start, end], dark, name, 6)
                    tube('LegFoot_' + name, [split, points[3]], [end * 1.15, LEG_TOE_RADIUS],
                         [end * 1.15, LEG_TOE_RADIUS], joint, name, 6)
            root = femur_root(points[0], points[1])
            coxa = thorax_exit(root, points[1])
            _knuckle(tube, 'LegCoxa_' + names[0], root, coxa, points[1], LEG_COXA_RADIUS, joint, names[0], None)
    _paint_belly_shade(bpy, thorax, head, abdomen)
    c.bind()
    mouth = c.rig.data.bones['Socket.Mouth'].head_local
    c.contact = {
        'gameplay_tip_rest_unity_m': [mouth.x * UNITY_SCALE, mouth.z * UNITY_SCALE, -mouth.y * UNITY_SCALE],
        'gameplay_collision_radius_m': COLLISION_RADIUS,
        'gameplay_surface_root_offset_m': SURFACE_ROOT_OFFSET,
        'ground_contact_rest_unity_m': [0, -SURFACE_ROOT_OFFSET, 0],
        'surface_rotation_contract': 'local +Y outward normal, forward projected tangent; runtime validation belongs to Gameplay/Presentation',
        'geometry_revision': REVISION,
        'palette_srgb_hex': dict(PALETTE_HEX),
        'belly_shade': {'vertex_color_layer': SHADE_LAYER, 'lower_fraction': BELLY_SHADE_FRACTION,
                        'lowest_multiplier_linear': list(belly_shade()),
                        'unity_note': 'URP/Lit ignores vertex colour; a vertex-colour multiply is needed to show it'},
        'face': facial_contract(),
        'proboscis_descent_degrees': math.degrees(math.atan2(PROBOSCIS_BASE[2], abs(MOUTH[1] - PROBOSCIS_BASE[1]))),
        'art_acceptance': 'pending real render, complete clips, Unity and independent review',
    }
    return c
