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

Sketch r4 (art-director round 4, in priority order): (1) the thorax is the
dominant mass again, as in PER-07: radii (.068, .062, .060), hump rise .009,
centre 6 mm further back, broader shoulders and a 180-face geodesic, so the eye
is ~0.51 of the thorax width, the crest is ~13 mm above the cup tops in
profile and the cups show <= ~7 mm per side from behind;
(2) darker, faceted red WITHOUT vertex colours (URP/Lit ignores them): the
shell albedo drops to #8C1E24 (the sheet render then reads ~(125-130,38,44) on
the thorax) and every shell face picks one of five tone materials
(Mosquito_ShellLight/Shell/ShellShade/ShellDark/ShellDeep, ~12% value steps)
from a gradient to #6E1418 over the lower 55% of each mass times a +/-12%
facet jitter; abdomen bands (Mosquito_Abdomen) sit 60% of the way from the
shell to #6E1418; (3) a solid, legible cone for the proboscis
(radii .0088 -> .0007) continuing the snout, with its underside on the dark
tones (bone and Socket.Mouth untouched); (4) broad leaf wings: chord +25%
(span:chord ~3.7), widest at ~54% of the span, tapering over the last 40%,
55 deg from vertical in front view, membrane #CFC8F2 alpha .5 whose triangles
differ in value through deeper pleats (no extra wing materials: Unity's
membrane branch matches the exact name Mosquito_Wing); (5) a wider abdomen,
shifted 12 mm back so its waist still clears the larger thorax, 44 deg descent
and tip 56 mm above the support plane kept; (6) the pupil sits ~1.5 mm lower
(author_mosquito_face) and the eye white look-dev self-light rises to .70 so
its lower half stays bright in the sheet (Blender only). The thorax gets
broader shoulders and the wing blades a -50 deg twist (see below).

Sketch r5 (art-director round 5, in priority order): (1) eyes: the white is a
faceted dome, not a flat disc: look-dev self-light .70 -> .10 (URP has no
emission, so the sheet anticipates Unity: lower half ~200-215 grey), rounder
white, larger lids, upper shutter at rest 80 deg, thinner rolled lip, and a
fixed red collar inside each cup that hides the unlit inside of the shutter
(the dark crescent over the white) - see author_mosquito_face; (2) abdomen: four
segments (0/.25/.48/.70) without step-in or bevel, three colour-only bands at
85% toward #6E1418, every other ring turned pi/8 with each band split in two
triangles (PER-07 diamond facets), wobble .025 -> .05, belly shade over the
lower 35% only and ShellLight on up-facing faces; (3) subtle leg joints (~1.4x
the leg): knuckles (.0065, .0036) x1.1 long, coxa .0062, tibia ring .0036;
(4) wings: 5 midrib vertices with an alternating pleat (17 triangles), edges
half as wide in the membrane's lavender #DAD5F2, membrane #D8D2F2 alpha .38;
(5) thorax/head/cups: +/-6% facet jitter and the tone baked from the face
normal (nz > .5 ShellLight, nz < -.3 ShellShade, nz < -.6 ShellDark);
(6) legs #3A1C20 (joint colour only on knuckles, rings and feet). The shell
base red is the approved #9E2228 (tones #B1262D/#9E2228/#8D1E24/#6E1418/#611215).

Sketch r6 (art-director round 6, in priority order): (1) wings: the blades are
aimed from the idle/stance wing pose (author_mosquito_motion stance(): fold
.24, flap .12) so that there the span points up, back and out (~42 deg above
the horizontal, swept ~60 deg back from lateral, ~29 deg from vertical in
front view; ~40 deg in the bind pose) and the membrane faces sideways (normal
~(.93, -.23, -.29)): broad in the side and three-quarter views, foreshortened
from the front (the r5 blades were edge-on in profile).
The bind frame is that stance frame with the stance rotation undone. Blades
+25% long (scale 1.2 -> 1.5), lanceolate and ~20% wider over the proximal 40%;
veins and edges at alpha .35. (2) Big clean planes: an 80-face thorax
geodesic with 9% vertex jitter, an 8-sided abdomen on six shape rings (band
loops are coplanar subdivisions of those planes), and no random per-face tone:
only the lit base red (Mosquito_Shell), one shade tone on down-facing faces
(Mosquito_ShellShade) and the dark band tones. (3) Eyes: see
author_mosquito_face (ball white 7 mm forward / 3.5 mm out, ~67% of it seen
in profile, one rim, fixed eyelid wedge over the top ~13%; the cup depth is
unchanged, bound by the shutter blink). (4) Legs in an A: the leg BONES are the unchanged R4 bind (Unity's
MosquitoRagdollBuilder rejects hip/knee/ankle moves > .2 mm), so only the skin
changes: the visible knee sits 82% down the femur (femur -18%, tibia ~+15%),
the visible tibia opens outward so each foot is 23% further out than its knee
in front view, and the foot keeps the support plane. The visible tibia blends
from the femur bone (top) to the tarsus bone (bottom), whose IK keeps it
planted, so the foot does not skate and the knee never gaps; knee knuckle
.0080 and tibia ring .0045 in Mosquito_LegJoint #140A0A, legs #2A1A1A.
(5) Proboscis: one thick faceted cone from a snout under the eyes (radius
.0115, ~32% of the white's diameter) tapering linearly to Socket.Mouth, the
last 25% on Mosquito_ShellDark #6E1418 (bone, socket and tip vertex
unchanged). (6) Abdomen: five bands on ring loops (30% of each segment,
#6E1418 with a #611215 trailing border) and a waist 30% thinner. (7) Thorax
10% wider and its back 12% taller (crest ~24 mm above the eye cups).

Sketch r8 (art director r6, integration review r2): (1) the thorax volume moves
back instead of up (hump .002, height radius .060 on the same base, depth
radius .070 with the centre 10 mm further back, shoulders .05): the crest now
sits ~4 mm over the eye cups, so the eyes are up and ahead of the body; (2)
blades 20% smaller (WING_SCALE 1.20) with a #CCC4F6 alpha .50 membrane that
Unity keeps (lavender, not grey); (3) eyes: see author_mosquito_face (a
shallower white further ahead, a lower fixed lid, pupils 6 deg in / 4 deg
down, jittered shell facets instead of concentric rings). The idle wing aim
(WING_STANCE_*) is unchanged here: the open V of the idle wings belongs to the
animation owner of author_mosquito_motion.stance(), which these constants
mirror.
"""
import math
import random
from author_mosquito_face import (create_face, facial_contract, outward, eye_mesh, pupil_mesh, eye_center,
                                  lid_front_degrees, upper_lid_point, collar_mesh, cap_mesh, COLLAR_ARCS_DEGREES)

REVISION = 'mosquito-sketch-r8-source'
UNITY_SCALE = .5
COLLISION_RADIUS = .055
SURFACE_ROOT_OFFSET = .057
SUPPORT_Z = -.114
MOUTH = (0, -.190, 0)
# Proboscis bone head is the unchanged R4 bind; the visible cone is authored
# separately (PROBOSCIS_VISUAL_BASE) and ends exactly on Mouth.
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

# Palette from docs/v030/GUIA-ESTILO-BOCETOS.md section 3 plus the art
# direction rounds (sRGB hex); Blender material colours are scene-linear, so
# each hex is converted once here.
PALETTE_HEX = {
    'shell': '#9E2228',      # approved darker base red (guide #B8262B)
    'segment': '#6E1418',    # bands, proboscis tip
    'legs': '#2A1A1A',       # r6: dark legs (guide)
    'joint': '#140A0A',      # r6: knee knuckles, tibia rings, coxae, ankles, feet
    'eye': '#FAFAF8',
    'pupil': '#141218',
    'wing': '#CCC4F6',       # r8: more saturated lavender membrane, alpha WING_ALPHA
    'vein': '#C3C3E6',       # veins, alpha VEIN_ALPHA
    'wing_edge': '#DAD5F2',  # outline in the membrane's tone, alpha VEIN_ALPHA
}
# r8 (art director r6): .38 -> .50 so the membrane reads light lavender on
# a dark background (it read grey); Unity keeps this authored alpha.
WING_ALPHA = .50
# r6: outline and veins at alpha .35 so they no longer read as a wireframe
# (Blender look-dev renders them blended; Unity's CharacterContentBuilder only
# makes Mosquito_Wing transparent today, see the audit unity_note).
VEIN_ALPHA = .35
# r6 tones: no random per-face value any more. Every shell face is the lit
# base red unless it looks down (normal z < NORMAL_SHADE_Z: ShellShade); the
# dark tones are only used where they mean something (bands, band borders,
# proboscis tip). Mosquito_ShellLight is retired.
SHELL_TONES = (('Mosquito_Shell', 0), ('Mosquito_ShellShade', 1), ('Mosquito_ShellDark', 2),
               ('Mosquito_ShellDeep', 3))
TONE_FACTORS = {0: ('shell', 1.0), 1: ('shell', .82), 2: ('segment', 1.0), 3: ('segment', .88)}
NORMAL_SHADE_Z = -.3


def srgb(hex_color, alpha=None):
    values = tuple(int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5))
    return values + ((alpha,) if alpha is not None else ())


def _hex(rgb):
    return '#' + ''.join('%02X' % max(0, min(255, round(c * 255))) for c in rgb)


def tone_hex(k):
    """sRGB hex of shell tone k (0..3, see SHELL_TONES): shell, shade
    (#821C21), segment #6E1418 and band border #611215."""
    key, factor = TONE_FACTORS[k]
    return _hex(tuple(c * factor for c in srgb(PALETTE_HEX[key])))


def band_hex():
    return PALETTE_HEX['segment']


def srgb_to_linear(hex_color, alpha=None):
    linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in srgb(hex_color)]
    return tuple(linear) + ((alpha,) if alpha is not None else ())


def palette_material(material, name, key, roughness, alpha=None, specular=None):
    """Same convention as the human build: the audit/Unity colour keeps the
    sketch sRGB value (Unity SetColor is gamma space); only the Blender
    Principled node receives the linear equivalent so source renders match.
    key is a PALETTE_HEX key or a literal '#RRGGBB'."""
    hex_color = key if key.startswith('#') else PALETTE_HEX[key]
    m = material(name, srgb(hex_color, alpha), roughness)
    node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*srgb_to_linear(hex_color), 1)
    if specular is not None and 'Specular IOR Level' in node.inputs:
        node.inputs['Specular IOR Level'].default_value = specular
    return m


# Round, robust faceted thorax whose extra height rises up and back (humped
# crest above the eyes); its front overlaps the eye cups. r6: 10% wider, the
# back 12% taller (centre 7.5 mm up, height radius +7.5 mm, bottom unchanged)
# so the crest clears the eye cups in profile by ~24 mm; 80-face geodesic
# (was 180) with 9% vertex jitter: few big, slightly irregular planes.
# r8 (art director r6): the dome over the eyes read as a helmet over goggles.
# The volume moves back, not up: hump rise .009 -> .002, height radius .0675
# -> .060 on the same base (centre z .1315 -> .124), depth radius .062 ->
# .070 with the centre 10 mm further back (y .008 -> .018), shoulders .15 ->
# .05: the crest sits ~4 mm above the eye cups (was ~28 mm), so the eyes are
# up and ahead of the body (PER-07). Wing axillae (z .082) and Socket.Back
# stay inside the volume.
THORAX_CENTER, THORAX_RADII = (0, .018, .124), (.075, .070, .060)
THORAX_HUMP_RISE, THORAX_HUMP_BACK = .002, .0051
# Shoulders: the upper half widens (x * (1 + .15 * up)) so, seen from behind,
# the crest covers the cup tops instead of forming a 'heart'.
THORAX_SHOULDER = .05
THORAX_FREQUENCY, THORAX_JITTER, THORAX_SEED = 2, .09, 31
# Head: compact, mostly hidden between the eye cups; r6 a little wider (the
# eyes moved 3.5 mm out) and lower, so its underside is the snout's root.
HEAD_CENTER, HEAD_RADII = (0, -.045, .114), (.031, .029, .030)
HEAD_FREQUENCY, HEAD_JITTER, HEAD_SEED = 2, .04, 17
# Big pointed leaf abdomen: slightly arched axis from inside the thorax,
# 44 deg below horizontal, .245 m long; the tip stays ~56 mm above the support.
ABDOMEN_START, ABDOMEN_ARCH = (0, .050, .112), .008
ABDOMEN_LENGTH, ABDOMEN_DESCENT_DEGREES = .245, 44.0
ABDOMEN_TIP = (0, ABDOMEN_START[1] + ABDOMEN_LENGTH * math.cos(math.radians(ABDOMEN_DESCENT_DEGREES)),
               ABDOMEN_START[2] - ABDOMEN_LENGTH * math.sin(math.radians(ABDOMEN_DESCENT_DEGREES)))
ABDOMEN_RADII = (.062, .050)
ABDOMEN_SIDES = 8
# r6: six shape rings (t along the axis, section scale) and a point: root
# hidden in the thorax, waist (.31: -30% from .44, a drop separate from the
# thorax), widest at t=.42, taper to the tip. The loft columns are straight
# (no twisted diamonds) and every band loop is a linear subdivision of a
# row, so each column stays one clean plane per row.
ABDOMEN_RINGS = ((0.0, .50), (.10, .31), (.26, .80), (.42, 1.0), (.60, .88), (.78, .52))
# One dark band at the start of each visible row (the tip cone included):
# 30% of the row in #6E1418, its last quarter a #611215 border.
ABDOMEN_BAND_FRACTION, ABDOMEN_BORDER_FRACTION = .30, .25
# Leaf wing drawn in (span, chord) and mapped on to a plane through the
# unchanged R4 axilla (Wing.L/R bone heads untouched).
# r6: the plane is authored in the stance wing pose (author_mosquito_motion
# stance(): Rz(sign * .24) @ Ry(-sign * .12) about the axilla), where the
# rest/idle wings are seen, then carried back to the bind pose.
WING_STANCE_FOLD, WING_STANCE_FLAP = .24, .12
WING_STANCE_SPAN = (.371, .641, .672)
WING_STANCE_NORMAL = (.93, -.23, -.29)
# r8 (art director r6): -20% (1.50 -> 1.20, leaf proportion unchanged): in
# profile the blade was 1.43x the abdomen (PER-03 SIDE ~1.0, 3/4 ~1.3).
WING_SCALE = 1.20
# r6 lanceolate leaf: straight leading edge (0-4), widest at u ~.10 (~40% of
# the span, +20% over the proximal 40%), long taper to the pointed tip; the
# petiole (8) stays inside the thorax.
WING_OUTLINE = tuple((u * WING_SCALE, v * WING_SCALE) for u, v in (
    (0, 0), (.070, -.008), (.135, -.011), (.200, -.008), (.262, 0),
    (.212, .026), (.150, .052), (.095, .062), (.040, .020)))
WING_RIDGE = tuple((u * WING_SCALE, v * WING_SCALE) for u, v in (
    (.058, .012), (.098, .024), (.142, .022), (.185, .015), (.225, .007)))
# Alternating pleat (per ridge vertex) so neighbouring membrane triangles
# differ in value under the key light (the membrane must stay the single
# Mosquito_Wing material for Unity's membrane branch).
WING_RIDGE_RAISE = (.012, .006, .012, .006, .012)
WING_THICKNESS = .00045
# Leading strip (outline 0-4 against the midrib 0, 9-13, 4) and trailing
# strip (midrib against 4-8, 0); wing_mesh fixes each winding from the uv area.
WING_TRIANGLES = ((0, 1, 9), (1, 10, 9), (1, 2, 10), (2, 11, 10), (2, 3, 11), (3, 12, 11), (3, 13, 12),
                  (3, 4, 13),
                  (0, 9, 8), (9, 7, 8), (9, 10, 7), (10, 6, 7), (10, 11, 6), (11, 12, 6), (12, 5, 6),
                  (12, 13, 5), (13, 4, 5))
# (prefix, outline indices, width, material key). Prefixes keep the export
# renderer buckets: WingLeadingEdge.* / WingVein.* -> MosquitoVeins.
WING_VEINS = (('WingLeadingEdge.', (0, 1, 2, 3, 4), .0004, 'wing_edge'),
              ('WingVein.Edge.', (4, 5, 6, 7, 8, 0), .0003, 'wing_edge'),
              ('WingVein.', (0, 9, 10, 11, 12, 13, 4), .00055, 'vein'),
              ('WingVein.Branch.', (11, 6), .0004, 'vein'),
              ('WingVein.Branch2.', (12, 5), .0004, 'vein'),
              ('WingVein.Branch3.', (10, 7), .0004, 'vein'))
# Leg joints are the unchanged R4 bind: Unity's MosquitoRagdollBuilder
# validates hip/knee/ankle within .2 mm, and surface_step reads them. r6: only
# the skin draws the A (visual_leg_points).
LEG_RADII = ((.0064, .0045), (.0046, .0023), (.0017, .00135))
LEG_VISUAL_FEMUR = .82
LEG_FOOT_SPLAY = 1.23
LEG_KNEE_RADIUS, LEG_ANKLE_RADIUS = .0080, .0036
LEG_KNUCKLE_LENGTH = 1.1
LEG_COXA_RADIUS = .0062
TIBIA_RING_T, TIBIA_RING_RADIUS = .5, .0045
TIBIA_STATIONS = (0.0, .25, .5, .75, 1.0)
# Hips outside the thorax get a visual femur root this deep inside it.
FEMUR_ROOT_DEPTH = .80
LEG_FOOT_START = .70
LEG_TOE_RADIUS = .0012
# r6 proboscis: one faceted cone from a thick snout under the eyes (fused
# with the underside of the head) to Socket.Mouth; linear taper; the last 25%
# on the dark tone. Stations are fractions from the visual base to Mouth
# (the first one is hidden inside the head).
PROBOSCIS_VISUAL_BASE = (0, -.066, .098)
PROBOSCIS_BASE_RADIUS, PROBOSCIS_TIP_RADIUS = .0115, .0007
PROBOSCIS_STATIONS = (-.08, 0.0, .15, .35, .55, .75, .88, 1.0)
PROBOSCIS_DARK_FROM = .75
PROBOSCIS_SIDES = 6
# Brow band lying on the upper shutter (0.3 mm clear of it: the shutter
# rotates on the same ellipsoid, so it never cuts the brow). Not part of the
# base look; kept as the customization option, emitted only when BASE_BROWS.
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


def _rot_y(v, angle):
    c, s = math.cos(angle), math.sin(angle)
    return (v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c)


def _rot_z(v, angle):
    c, s = math.cos(angle), math.sin(angle)
    return (v[0] * c - v[1] * s, v[0] * s + v[1] * c, v[2])


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
    """Extra crest volume rises up and back (PER-07 hunched thorax), with
    broader shoulders on the upper half."""
    x, y, z = vertex
    up, back = max(0, direction[2]), direction[1]
    x = THORAX_CENTER[0] + (x - THORAX_CENTER[0]) * (1 + THORAX_SHOULDER * up)
    return (x, y + THORAX_HUMP_BACK * up * max(0, back), z + THORAX_HUMP_RISE * up ** 1.5 * (.7 + .3 * back))


def thorax_mesh():
    return faceted_ellipsoid(THORAX_CENTER, THORAX_RADII, THORAX_FREQUENCY, jitter=THORAX_JITTER, seed=THORAX_SEED,
                             deform=_thorax_hump)


def head_mesh():
    return faceted_ellipsoid(HEAD_CENTER, HEAD_RADII, HEAD_FREQUENCY, jitter=HEAD_JITTER, seed=HEAD_SEED)


def _abdomen_point(t):
    axis = (0, ABDOMEN_TIP[1] - ABDOMEN_START[1], ABDOMEN_TIP[2] - ABDOMEN_START[2])
    up = _normalize((0, -axis[2], axis[1]))
    if up[2] < 0:
        up = _scale(up, -1)
    return _add(_add(ABDOMEN_START, _scale(axis, t)), _scale(up, ABDOMEN_ARCH * math.sin(math.pi * t)))


def _abdomen_ring(t, scale):
    center = _abdomen_point(t)
    ahead = _abdomen_point(min(1, t + .01))
    behind = _abdomen_point(max(0, t - .01))
    tangent = _normalize(_sub(ahead, behind))
    up = _normalize((0, -tangent[2], tangent[1]))
    if up[2] < 0:
        up = _scale(up, -1)
    ring = []
    for j in range(ABDOMEN_SIDES):
        # Half-side offset: flat faces on top, bottom and flanks.
        angle = math.tau * j / ABDOMEN_SIDES + math.pi / ABDOMEN_SIDES
        ring.append(_add(_add(center, (ABDOMEN_RADII[0] * scale * math.cos(angle), 0, 0)),
                         _scale(up, ABDOMEN_RADII[1] * scale * math.sin(angle))))
    return ring


def abdomen_loops():
    """[(t, kind of the row that starts at this loop)] including the band
    loops; kind is 'shell', 'band' or 'border'. Rows are (shape ring i, shape
    ring i+1) and the tip cone (last ring, apex)."""
    bounds = [t for t, _ in ABDOMEN_RINGS] + [1.0]
    loops = [(bounds[0], 'shell')]
    for a, b in zip(bounds[1:], bounds[2:]):
        band = (b - a) * ABDOMEN_BAND_FRACTION
        loops += [(a, 'band'), (a + band * (1 - ABDOMEN_BORDER_FRACTION), 'border'), (a + band, 'shell')]
    return loops


def abdomen_mesh():
    """Eight-sided loft on six shape rings plus a point; band loops are linear
    subdivisions of the shape rows (coplanar), so bands are colour only.
    Returns vertices, faces and a per-face kind ('shell', 'band', 'border')."""
    shape = [(t, _abdomen_ring(t, s)) for t, s in ABDOMEN_RINGS]
    apex = ABDOMEN_TIP
    rings, kinds = [], []
    for t, kind in abdomen_loops():
        for (t0, r0), nxt in zip(shape, shape[1:] + [(1.0, None)]):
            t1 = nxt[0]
            if t0 - 1e-9 <= t < t1 - 1e-9:
                f = (t - t0) / (t1 - t0)
                target = nxt[1] if nxt[1] is not None else [apex] * ABDOMEN_SIDES
                rings.append([_lerp(p, q, f) for p, q in zip(r0, target)])
                kinds.append(kind)
                break
    n = ABDOMEN_SIDES
    vertices = [v for ring in rings for v in ring] + [apex]
    top = len(vertices) - 1
    faces, face_kinds = [tuple(reversed(range(n)))], ['shell']
    for row in range(len(rings) - 1):
        for j in range(n):
            a, b = row * n + j, row * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
            face_kinds.append(kinds[row])
    last = (len(rings) - 1) * n
    for j in range(n):
        faces.append((last + j, last + (j + 1) % n, top))
        face_kinds.append(kinds[-1])
    vertices, oriented = outward(vertices, faces)
    return vertices, oriented, face_kinds


def leg_points(side, index):
    """Six longer legs, staggered laterally on the unchanged support plane
    (bone joints: hip, knee, ankle, toe; the R4 bind Unity validates)."""
    y, dy = ((-.033, -.052), (.006, .012), (.042, .067))[index - 1]
    knee_x = (.097, .112, .089)[index - 1]
    knee_z = (.006, -.003, .002)[index - 1]
    ankle_x = (.107, .094, .081)[index - 1]
    return ((side * .027, y, .099 - (index - 1) * .004),
            (side * knee_x, y + dy * .4, knee_z),
            (side * ankle_x, y + dy, -.1076),
            (side * (ankle_x + .012), y + dy + .006, -.1126))


def visual_leg_points(side, index):
    """r6 skin-only joints (hip, visible knee, visible ankle, toe): the knee
    82% down the bone femur, the foot LEG_FOOT_SPLAY times further out than
    that knee (front view A), ankle/toe heights on the unchanged support."""
    hip, knee, ankle, toe = leg_points(side, index)
    visible_knee = _lerp(hip, knee, LEG_VISUAL_FEMUR)
    foot_x = abs(visible_knee[0]) * LEG_FOOT_SPLAY
    return (hip, visible_knee, (side * foot_x, ankle[1], ankle[2]),
            (side * (foot_x + .012), toe[1], toe[2]))


def _thorax_norm(point):
    return sum(((a - c) / r) ** 2 for a, c, r in zip(point, THORAX_CENTER, THORAX_RADII))


def femur_root(hip, knee):
    """Visual femur start: the hip bone head itself, or, where the thorax
    does not contain it, the point on the femur line extended back into the
    thorax (FEMUR_ROOT_DEPTH). Bones and weights are unchanged."""
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
    """(span, chord, normal) of the blade in the bind pose. r6: authored in
    the stance pose (span up/back/out, membrane facing sideways, chord toward
    the back and down) and carried back through the inverse stance rotation
    Ry(+flap) @ Rz(-fold) (left wing; the right one mirrors)."""
    span = _normalize(WING_STANCE_SPAN)
    hint = WING_STANCE_NORMAL
    normal = _normalize(_sub(hint, _scale(span, _dot(hint, span))))
    chord = _normalize(_cross(span, normal))
    span, chord = (_rot_y(_rot_z(v, -WING_STANCE_FOLD), WING_STANCE_FLAP) for v in (span, chord))
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
        raise_ = WING_RIDGE_RAISE[index - len(WING_OUTLINE)] if index >= len(WING_OUTLINE) else 0
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
    """Dark arched band lying on the upper shutter (customization option).

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


def _proboscis_frame():
    tangent = _normalize(_sub(MOUTH, PROBOSCIS_VISUAL_BASE))
    lateral = (1.0, 0.0, 0.0)
    up = _normalize(_cross(tangent, lateral))
    if up[2] < 0:
        up = _scale(up, -1)
    return tangent, lateral, up


def proboscis_radius(t):
    return PROBOSCIS_BASE_RADIUS + (PROBOSCIS_TIP_RADIUS - PROBOSCIS_BASE_RADIUS) * max(0.0, t)


def proboscis_mesh():
    """Snout + needle as one hexagonal cone (flat top), ending on Mouth."""
    _, lateral, up = _proboscis_frame()
    rings = []
    for t in PROBOSCIS_STATIONS:
        center = MOUTH if t == 1.0 else _lerp(PROBOSCIS_VISUAL_BASE, MOUTH, t)
        radius = proboscis_radius(t)
        ring = []
        for j in range(PROBOSCIS_SIDES):
            angle = math.tau * j / PROBOSCIS_SIDES + math.pi / PROBOSCIS_SIDES
            ring.append(_add(center, _add(_scale(lateral, radius * math.cos(angle)), _scale(up, radius * math.sin(angle)))))
        rings.append(ring)
    return rings_mesh(rings)


def proboscis_face_t(center):
    """Fraction of a point along the visual cone (0 base, 1 Mouth)."""
    axis = _sub(MOUTH, PROBOSCIS_VISUAL_BASE)
    return _dot(_sub(center, PROBOSCIS_VISUAL_BASE), axis) / _dot(axis, axis)


def pure_meshes():
    """Every pure-data closed mesh the generator emits (plus the optional brow
    band, so the customization option stays checked), for Blender-free checks."""
    abdomen = abdomen_mesh()
    meshes = [('Thorax', *thorax_mesh()), ('Head', *head_mesh()), ('Abdomen', abdomen[0], abdomen[1]),
              ('Proboscis', *proboscis_mesh())]
    for side in (1, -1):
        meshes += [('Wing.' + str(side), *wing_mesh(side)), ('Brow.' + str(side), *_brow_mesh(side)),
                   ('EyeCap.' + str(side), *cap_mesh(side)),
                   ('Eye.' + str(side), *eye_mesh(side)), ('Pupil.' + str(side), *pupil_mesh(side))]
        meshes += [(part + '.' + str(side), *collar_mesh(side, part)) for part in COLLAR_ARCS_DEGREES]
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


def _knuckle(tube, name, before, joint, after, radius, material, bone):
    """Elongated hexagonal knuckle, rigid on one bone."""
    incoming = _normalize(_sub(joint, before))
    outgoing = _normalize(_sub(after, joint))
    length = radius * LEG_KNUCKLE_LENGTH
    return tube(name, [_sub(joint, _scale(incoming, length)), joint, _add(joint, _scale(outgoing, length))],
                [radius * .62, radius, radius * .62], [radius * .62, radius, radius * .62], material, bone, 6)


def _blend_rings(obj, first, second, fractions, sides):
    """Ring k of a tube follows `second` by fractions[k] and `first` by the
    rest (weights sum to one)."""
    a, b = obj.vertex_groups.new(name=first), obj.vertex_groups.new(name=second)
    for k, f in enumerate(fractions):
        indices = list(range(k * sides, (k + 1) * sides))
        if f < 1:
            a.add(indices, 1 - f, 'REPLACE')
        if f > 0:
            b.add(indices, f, 'REPLACE')


# Look-dev self-light (Blender only; Unity's URP material has none): the
# white reads as a faceted dome with its lower half light grey.
EYE_LOOKDEV_EMISSION = .10


def _paint_shell_tones(bpy, tone_materials, shell):
    """r6: every face still on Mosquito_Shell becomes ShellShade when it
    looks down; bands, borders and the proboscis tip keep their tones."""
    shade = tone_materials[1]
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH' or shell.name not in obj.data.materials:
            continue
        if shade.name not in obj.data.materials:
            obj.data.materials.append(shade)
        slots = list(obj.data.materials)
        base, low = slots.index(shell), slots.index(shade)
        for poly in obj.data.polygons:
            if poly.material_index == base and poly.normal.z < NORMAL_SHADE_Z:
                poly.material_index = low


def create_mosquito(*, Character, material, tube, ellipsoid, strip, mesh):
    import bpy
    c = Character('Mosquito')
    # Matte shell; per-face tone materials (no vertex colour: URP/Lit ignores
    # it); Mosquito_Shell is tone 0.
    tone_materials = {k: palette_material(material, name, tone_hex(k), .90, specular=.20)
                      for name, k in SHELL_TONES}
    shell = tone_materials[0]
    bands = palette_material(material, 'Mosquito_Abdomen', band_hex(), .90, specular=.20)
    dark = palette_material(material, 'Mosquito_Legs', 'legs', .86, specular=.25)
    joint = palette_material(material, 'Mosquito_LegJoint', 'joint', .80, specular=.30)
    eye = palette_material(material, 'Mosquito_EyeWhite', 'eye', .60)
    pupil = palette_material(material, 'Mosquito_Expression', 'pupil', .40)
    # Preserve the exact name used by Unity's membrane shader branch. Blender
    # look-dev renders it blended with back faces culled (one layer, like
    # Unity's alpha material) instead of dithered hashing (grainy membrane).
    wing = palette_material(material, 'Mosquito_Wing', 'wing', .85, alpha=WING_ALPHA)
    vein = palette_material(material, 'Mosquito_WingVein', 'vein', .80, alpha=VEIN_ALPHA)
    edge = palette_material(material, 'Mosquito_WingEdge', 'wing_edge', .80, alpha=VEIN_ALPHA)
    for m in (wing, vein, edge):
        if hasattr(m, 'surface_render_method'):
            m.surface_render_method = 'BLENDED'
        m.use_backface_culling = m is wing
    wing_materials = {'vein': vein, 'wing_edge': edge}
    c.bone('Root', (0, 0, 0), (0, 0, .03), deform=False)
    c.bone('Thorax', THORAX, (0, -.035, .104), 'Root')
    c.bone('Head', (0, -.041, .100), (0, -.084, .105), 'Thorax')
    c.bone('Abdomen01', (0, .039, .105), (0, .110, .062), 'Thorax')
    c.bone('Abdomen02', (0, .110, .062), (0, .204, -.030), 'Abdomen01')
    c.bone('Proboscis', PROBOSCIS_BASE, MOUTH, 'Head')
    for name, head, tail, parent in SOCKETS:
        c.bone(name, head, tail, parent, False)
    mesh('Thorax', *thorax_mesh(), shell, 'Thorax')
    mesh('Head', *head_mesh(), shell, 'Head')
    vertices, faces, kinds = abdomen_mesh()
    abdomen = mesh('Abdomen', vertices, faces, shell)
    _weighted_abdomen(abdomen)
    abdomen.data.materials.append(bands)
    abdomen.data.materials.append(tone_materials[3])
    slot = {'shell': 0, 'band': 1, 'border': 2}
    for poly, kind in zip(abdomen.data.polygons, kinds):
        poly.material_index = slot[kind]
    # Snout and needle are one rigid cone on Proboscis (child of Head, never
    # posed apart from it); its last ring sits on Socket.Mouth.
    proboscis = mesh('Proboscis', *proboscis_mesh(), shell, 'Proboscis')
    proboscis.data.materials.append(tone_materials[2])
    for poly in proboscis.data.polygons:
        if proboscis_face_t(tuple(poly.center)) >= PROBOSCIS_DARK_FROM:
            poly.material_index = 1
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
            _, knee, ankle, toe = visual_leg_points(sign, i)
            names = [f'Leg{i}{j + 1:02d}.{side}' for j in range(3)]
            parent = 'Thorax'
            for j, name in enumerate(names):
                c.bone(name, points[j], points[j + 1], parent)
                parent = name
            femur, tibia, tarsus = names
            (f0, f1), (t0, t1), (s0, s1) = LEG_RADII
            # Femur from its visual root inside the thorax to the visible knee
            # (on the bone line, 82% down): rigid on the femur bone.
            root = femur_root(points[0], points[1])
            coxa = thorax_exit(root, points[1])
            tube('Limb_' + femur, [root, coxa, knee], [f0, f0, f1], [f0, f0, f1], dark, femur, 6)
            _knuckle(tube, 'LegCoxa_' + femur, root, coxa, knee, LEG_COXA_RADIUS, joint, femur)
            _knuckle(tube, 'LegJoint_' + femur, coxa, knee, ankle, LEG_KNEE_RADIUS, joint, femur)
            # Visible tibia opening outward: femur-rigid at the knee, tarsus-
            # rigid at the foot (planted by the surface IK), blended between.
            centers = [_lerp(knee, ankle, s) for s in TIBIA_STATIONS]
            radii = [t0 + (t1 - t0) * s for s in TIBIA_STATIONS]
            limb = tube('Limb_' + tibia, centers, radii, radii, dark, None, 6)
            _blend_rings(limb, femur, tarsus, TIBIA_STATIONS, 6)
            along = _scale(_normalize(_sub(ankle, knee)), TIBIA_RING_RADIUS * 1.1)
            middle = _lerp(knee, ankle, TIBIA_RING_T)
            r = TIBIA_RING_RADIUS
            ring = tube('LegRing_' + tibia, [_sub(middle, along), middle, _add(middle, along)],
                        [r * .66, r, r * .66], [r * .66, r, r * .66], joint, None, 6)
            spread = math.dist(knee, ankle)
            _blend_rings(ring, femur, tarsus, [TIBIA_RING_T + d * r * 1.1 / spread for d in (-1, 0, 1)], 6)
            # Ankle, tarsus and dark foot: rigid on the tarsus bone; the toe
            # keeps the support plane.
            _knuckle(tube, 'LegJoint_' + tibia, knee, ankle, toe, LEG_ANKLE_RADIUS, joint, tarsus)
            split = _lerp(ankle, toe, LEG_FOOT_START)
            tube('Limb_' + tarsus, [ankle, split], [s0, s1], [s0, s1], dark, tarsus, 6)
            tube('LegFoot_' + tarsus, [split, toe], [s1 * 1.15, LEG_TOE_RADIUS],
                 [s1 * 1.15, LEG_TOE_RADIUS], joint, tarsus, 6)
    _paint_shell_tones(bpy, tone_materials, shell)
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
        'shell_tones': {'materials_srgb_hex': {name: tone_hex(k) for name, k in SHELL_TONES},
                        'abdomen_band_srgb_hex': band_hex(), 'abdomen_band_border_material': 'Mosquito_ShellDeep',
                        'proboscis_tip_material': 'Mosquito_ShellDark',
                        'shade_rule': 'faces on Mosquito_Shell whose normal z < %.2f use Mosquito_ShellShade' % NORMAL_SHADE_Z,
                        'unity_note': ('per-face materials, no vertex colour; the runtime Mosquito colour binding '
                                       'covers Mosquito_Shell/Mosquito_Abdomen only, so tinting must also scale '
                                       'Mosquito_ShellShade/ShellDark/ShellDeep to keep a recoloured body faceted')},
        'wing_vein_alpha': VEIN_ALPHA,
        'wing_vein_unity_note': ('Mosquito_WingVein/Mosquito_WingEdge carry alpha .35; CharacterContentBuilder '
                                 'renders every wing material transparent with its authored alpha (membrane '
                                 'first, veins and edge sorted after it) and rejects opaque wing materials'),
        'leg_skin_note': ('leg bones are the unchanged R4 bind; the visible knee/foot (A stance) are skin only: '
                          'tibia skin blends femur -> tarsus, foot rigid on the tarsus'),
        'face': facial_contract(),
        'proboscis_descent_degrees': math.degrees(math.atan2(PROBOSCIS_BASE[2], abs(MOUTH[1] - PROBOSCIS_BASE[1]))),
        'art_acceptance': 'pending real render, complete clips, Unity and independent review',
    }
    return c
