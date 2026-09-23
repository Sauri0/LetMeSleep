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

Sketch r4 (art-director round 4, item 6): the pupil is pitched ~2.1 deg down
about the unchanged pivot (~1.5 mm lower on the white), so the gaze reads
slightly down/forward like PER-07; the white, cups and lids are unchanged.

Sketch r5 (art-director round 5, item 1): the white reads as a faceted dome.
It is rounder (depth radius .029 -> .035, close to its .0355/.0366 front
radii); the lids grow to .040 / .048 and the upper shutter rests at 80 deg
(lower -90, keeping upper - lower <= 170) with a rolled lip half as thick, so
the neutral eye is a full round white (PER-07 'NORMAL'). The white's centre
moves from 11 to 8 mm ahead of the pivot: with 11 mm the deeper white put the
pupil ~.0476 from the pivot and the closed .048 shutters (facet sag ~1.4 mm,
X radius .040) left up to 146 of 358 pupil samples exposed at the gaze
limits; with 8 mm (and a .0018 pupil half-depth) every sample is covered
with ~0.7-1 mm to spare. A fixed collar (EyeCollar.L/R, Head) hugs the
white's equator inside each cup so the unlit inside of the shutter no longer
shows as a dark crescent over the white; it lies inside the innermost shell,
so no shutter touches it at any closure.

Sketch r6 (art-director round 6, item 3: the eye read as a lighthouse in
profile - a flat-backed white half-dome glued to a red cylinder with two
stacked rims): the white is a ball that sticks out of the head. The whole eye
(pivot, shutters, housings, white) moves 3.5 mm outward and 1 mm forward and
the white moves a further 6 mm ahead of the pivot (white 7 mm forward in
total); its front-view radii are unchanged and its depth is .030 (was .035)
so its front stays 44 mm from the pivot and the closed .048 shutters still
clear the pupil. The shutters rest 14 deg further round (upper 66, lower
-104: the lower rim sits behind the pivot, so the ball's round underside shows
in profile). The rolled lip on the shutter is gone and the r5 collar (a cone
at the white's equator: the second rim) is replaced by a flat rim face in the
shutters' rest-edge surface (EyeCollar/EyeCollarInner, edge-on in profile, a
thin red ring from the front): one rim. A fixed eyelid wedge (EyeCap, Head)
rests on the top of the white, covering its upper ~13% on the centre column
seen from the front (PER-03 NEUTRAL), and rises to just under the upper
shutter's rim instead of leaving that rim floating above the ball; it lies
inside the innermost shell, so blinks are unchanged. Ray probe of the built
bind pose (scratch measure, left eye): 66.9% of the white's profile visible
from the side, 13.0% of the centre column covered from the front.
Not done: the cup depth. The rest cup is the closed-shutter sphere (.048
around the pivot), fixed by the 90 deg shutter blink having to contain the
white's front plus the pupil; a shallower back makes the rotating shutters
dip through the housing/rim face or leaves the rim crevice open.

Review r7 (Unity showed dark dotted lines around both eyes): not z-fighting
but the rim crevices. On the rest-edge surface the collar stopped at 97% and
the cap at 96.5% of the innermost housing, leaving a 1-1.5 mm slot into the
unlit cup, and the shutter and housing front-edge walls faced forward-down
into their own shadow; at game scale these sub-pixel dark strips aliased into
dots. Now the inner layer of every shutter and housing ends 2 deg further
toward the front (a 45 deg chamfer that faces forward-up and is lit), the
collar lies 0.6 deg behind the rest-edge surface and reaches 98.5% of its
housing's inner layer (the part past the faceted inner layer is inside the
housing shell, hidden behind the chamfer), and the cap rises to 99% of the
upper housing's inner layer at the rim (settling back to 96.5% behind it).
From the front the housing chamfer overlaps the collar/cap edge, so no slot
is left open; blink rotations are unchanged and the cap/collar never stand
outside every covering shell at any closure (check_mosquito_face.py rim gate).

Review r8 (art director r6 item 3 and integration review r2): the lid read
sleepy, the cup ribbed. The fixed cap starts at 52 deg on the centre column
(75 deg at its sides, was 38/68: <= ~6% of the white covered from the front,
edge following the ball), the pupils turn 6 deg in and 4 deg down (the comic
cross-eyed look at the proboscis), the white is shallower (depth .027) and
1.5 mm further ahead (see EYE_FORWARD_OFFSET), and every interior shell vertex
slides along its own ellipsoid (LID_JITTER_*) with alternating diagonals, so
the profile no longer shows 5-7 parallel rings. The collar edge drops to 98%
of the housing inner layer to stay under the jittered facets at every
closure; the cap's inner layer is found by a scan (its outer rows' rays can
start outside the forward white).

Lid pivots, bone names, axes and the 90 deg runtime closure are unchanged; every
rest edge keeps upper - lower <= 170 deg so the rotated shutters plus the fixed
housing still hide the white and the pupil when closed (check_mosquito_face.py),
and the lid vertex centroids still let Unity's FacialContentBuilder derive
+/-90 deg. The pupil stays within 3 deg of the character front (Unity requires
< 16 deg).
"""
import math

FACE_REVISION = 'mosquito-facial-controls-v1'
LID_GEOMETRY_REVISION = 'mosquito-sketch-r8-jittered-shells'
# Eye white, source metres (x lateral, y depth along the look axis, z up).
# r6: depth .035 -> .030 (front view unchanged) so the white can sit further
# ahead of the pivot inside the same closed-shutter radius.
# r8 (art director r6): depth .030 -> .027 with the centre 1.5 mm further
# ahead (offset .014 -> .0155) so the profile 'D' softens. The requested 3 mm
# (.017, front kept at 44 mm) left the lateral equator vertices of the white
# (and the pupil at the outer gaze limit) up to 1.2 mm outside the closed
# faceted shutters (check_mosquito_face coverage); at .0155 every sample is
# covered and the front of the white sits 42.5 mm from the pivot.
EYE_RADII = (.0355, .027, .0366)
EYE_SEGMENTS, EYE_RINGS = 10, 6
# The white bulges out of its cup: its centre sits ahead (-Y) of the pivot the
# shutters and the pupil rotate about (r5: 8 mm; r6: 14 mm; the white's front
# stays 44 mm and the pupil front ~45 mm from the pivot, inside the closed
# .048 shutters with their facet sag).
EYE_FORWARD_OFFSET = .0155
# Pupil: small flattened black disc sitting on the white, aimed forward.
PUPIL_RADII = (.0087, .0018, .0095)
# r8 (art director r6): 6 deg in and 4 deg down: the comic, slightly
# cross-eyed look at the proboscis of PER-07 / UI-06 (6).
PUPIL_INWARD_DEGREES = 6
# r4: pupil pitched down about the pivot (~1.5 mm lower on the white).
PUPIL_DOWN_DEGREES = 4
PUPIL_EMBED = .0006
PUPIL_SEGMENTS, PUPIL_RINGS = 8, 5
# Closed shutters must clear the white (front ~.043 from the pivot) and the
# pupil front (~.0442), including faceting sag and the recessed rear housing.
# Upper shutter outermost; lower shutter and housings step inward so no two
# shells ever share a radius (no z-fighting where they overlap). Equal Y/Z
# radii let every shell slide over itself when rotated about source X.
LID_RADIUS_X = .0400
LID_RADIUS_YZ = .0480
LOWER_LID_INSET = .0005
LID_THICKNESS = .0006
HOUSING_RECESS = .0008
# Rest (open) edges, degrees about source X measured from the look axis (0 =
# front, 90 = top, 180 = back, -90 = bottom). Front edges ramp with
# |sin(latitude)| from the centre value (x = 0) to the pole value (lateral
# poles) while upper - lower stays <= 170 deg: after +/-90 deg the shutters
# still overlap by >= 10 deg.
# r6: both shutters rest 14 deg further round (upper 80 -> 66, lower -90 ->
# -104; upper - lower still 170 at every latitude): the lower rim moves behind
# the pivot so the bottom of the ball shows its round back in profile, and the
# upper rim comes down onto the fixed eyelid cap. The vertex centroids sit at
# ~133 / ~-155 deg (closed ~43 / ~-65), so Unity still derives +90 / -90.
UPPER_FRONT_CENTER, UPPER_FRONT_POLE, UPPER_BACK = 66.0, 70.0, 198.0
LOWER_FRONT_CENTER, LOWER_FRONT_POLE, LOWER_BACK = -104.0, -100.0, -208.0
LID_EDGE_RAMP_POWER = 1.0
# r7: the inner layer of each shell ends this much further toward the front
# than the outer layer, so the front-edge wall is a chamfer facing forward-up.
LID_EDGE_CHAMFER_DEGREES = 2.0
LID_LATITUDE_STEPS, UPPER_ARC_STEPS, LOWER_ARC_STEPS = 8, 7, 5
# r8 (art director r6, "copa con aspecto de fuelle"): the latitude rows of the
# shells read as 5-7 parallel concentric rings (a ribbed helmet) in profile.
# Every interior vertex (not the front/back edge columns, not the poles) moves
# along its own ellipsoid by a deterministic fraction of a latitude step and
# of an arc step; the radius is unchanged. Shutter, housing and both layers of
# a shell share the pattern (it depends on row/column/shell only), so chord
# sag and clearances stay as before; quads split on alternating diagonals.
LID_JITTER_LATITUDE, LID_JITTER_PHI = .30, .25
# The columns next to the rest edges stay regular: the fixed collar and cap sit
# within ~1 mm of those facets (check_mosquito_face rim gate).
LID_JITTER_EDGE_COLUMNS = 2


def _lid_jitter(upper, row, col, channel):
    """Deterministic value in [-1, 1] for a shell vertex (pattern shared by
    the shutter, its housing and both layers)."""
    value = math.sin((row * 12.9898 + col * 78.233 + (1 if upper else 2) * 37.719 + channel * 4.581) * 43758.5453)
    return 2 * (value - math.floor(value)) - 1
# r6 rim face (EyeCollar, Head): fills the crevice between the white and the
# shutters' rim so the cup's dark inside never shows, lying in the shutters'
# rest-edge surface (the upper edge angle above the pivot axis, the lower one
# below): edge-on in profile, a thin red ring around the white from the front.
# psi is the front-view angle about the pivot (0 = lateral, 90 = top). Two
# closed strips (sphere topology each): lateral/lower and medial/lower; the
# top is the eyelid cap's. Inner edge 0.3 mm off the white, outer edge at 98.5%
# (r6: 97%) of its housing's inner layer, so no shutter ever touches it.
# r8 (review r2, dark wedges at the top of each cup): the arcs run 12 deg
# further up under the cap's lateral ends (was 64 / 116), closing the slot
# between the cap end and the collar.
COLLAR_ARCS_DEGREES = {'EyeCollar': (-78.0, 76.0), 'EyeCollarInner': (104.0, 258.0)}
COLLAR_SEGMENTS = 16
# r8: .985 -> .980 of the housing inner layer: the jittered shells (LID_JITTER_*)
# keep the collar edge >= 0.08 mm under every covering facet at all closures.
COLLAR_INNER_CLEARANCE, COLLAR_OUTER_FILL = .0003, .980
# r7: the collar sits this far behind the rest-edge surface (inside the
# housing where it overlaps it, behind the housing's front chamfer).
COLLAR_BEHIND_DEGREES = .6
COLLAR_THICKNESS = .0012
# r6 fixed eyelid cap (EyeCap, Head): a wedge over the top of the white. Its
# front edge (angle about the pivot axis) is 38 deg on the eye's centre
# column (the top ~13% of the white's front-view height, PER-03 NEUTRAL) and
# ramps to 68 deg on the outer columns the side view sees, so the profile
# keeps ~67% of the ball white. From that edge it hugs the white, then rises
# to just under the upper shutter's rim and runs on under it: one lid resting
# on the ball instead of a ring floating above it. It stays inside the
# upper housing's inner layer (r7: 99%, was 96.5% of the lower housing), so
# blinks never touch it.
# r8 (art director r6): the fixed lid cut the white with a straight line
# (~13% covered) and read sleepy; 52 / 75 deg cover <= ~6% from the front with
# an edge that follows the ball (round, alert eyes of PER-03 NEUTRAL).
CAP_FRONT_DEGREES, CAP_FRONT_SIDE_DEGREES, CAP_FRONT_RAMP_POWER = 52.0, 75.0, 1.5
CAP_BACK_DEGREES, CAP_MIN_RISE_DEGREES = 112.0, 6.0
CAP_RISE_STEPS, CAP_BACK_STEPS = 3, 4
CAP_X_EXTENT, CAP_ROWS = .030, 9
CAP_INNER_CLEARANCE, CAP_EDGE_THICKNESS, CAP_OUTER_FILL = .0004, .0012, .99
# r7: only the rim needs the 99% fill; past it the cap settles back to the r6
# 96.5% within CAP_RIM_TAPER_DEGREES, so its back end (uncovered by the closed
# shutter) stays inside the faceted housing.
CAP_BACK_FILL, CAP_RIM_TAPER_DEGREES = .965, 20.0
GAZE_YAW_LIMIT_DEGREES = 12
GAZE_PITCH_LIMIT_DEGREES = 10
FACE_BONES = tuple(role + '.' + side for side in ('L', 'R')
                   for role in ('Pupil', 'LidUpper', 'LidLower'))


def eye_center(sign):
    """Pivot of Pupil/LidUpper/LidLower (bone heads); not the white's centre.
    r6: 3.5 mm outward and 1 mm forward (was (sign * .037, -.066, .132))."""
    return (sign * .0405, -.067, .132)


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


def pupil_direction():
    """Unit look direction of the pupil for the +X eye before the side yaw
    sign: inward yaw then the r4 down pitch (source -Y is forward)."""
    yaw, pitch = math.radians(PUPIL_INWARD_DEGREES), math.radians(PUPIL_DOWN_DEGREES)
    return (-math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), -math.sin(pitch))


def _white_surface_distance():
    """Pivot-to-white distance along the pupil direction (analytic ellipsoid;
    identical for both eyes)."""
    dx, dy, dz = pupil_direction()
    # Point t*d relative to the white centre (0, -offset, 0): (t dx, t dy + offset, t dz).
    a = (dx / EYE_RADII[0]) ** 2 + (dy / EYE_RADII[1]) ** 2 + (dz / EYE_RADII[2]) ** 2
    b = 2 * dy * EYE_FORWARD_OFFSET / EYE_RADII[1] ** 2
    c = (EYE_FORWARD_OFFSET / EYE_RADII[1]) ** 2 - 1
    return (-b + math.sqrt(b * b - 4 * a * c)) / (2 * a)


# The pupil centre sits just under the white's surface along its own axis.
PUPIL_DEPTH = _white_surface_distance() - PUPIL_EMBED


def pupil_mesh(sign):
    """Flattened disc on the white: built on the look axis, pitched down about
    the pivot (source X), then yawed toward the snout and moved to the pivot."""
    vertices, faces = ellipsoid_mesh(PUPIL_RADII, PUPIL_SEGMENTS, PUPIL_RINGS, offset=(0, -PUPIL_DEPTH, 0))
    pitch, yaw, center = math.radians(PUPIL_DOWN_DEGREES), pupil_yaw(sign), eye_center(sign)
    placed = [tuple(a + b for a, b in zip(rotate_z(rotate_x(v, pitch), yaw), center)) for v in vertices]
    return outward(placed, faces)


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
        chamfer = LID_EDGE_CHAMFER_DEGREES if inset else 0.0
        layer = [(-rx, 0, 0)]
        for row in range(1, latitude_steps):
            latitude = -math.pi * .5 + math.pi * row / latitude_steps
            x, ring = rx * math.sin(latitude), math.cos(latitude)
            front = lid_front_degrees(upper, latitude) - polarity * chamfer
            for col in range(arc_steps + 1):
                lat, fraction = latitude, col / arc_steps
                if LID_JITTER_EDGE_COLUMNS <= col <= arc_steps - LID_JITTER_EDGE_COLUMNS:
                    # r8: interior vertices slide on the ellipsoid (see LID_JITTER_*).
                    lat += LID_JITTER_LATITUDE * (math.pi / latitude_steps) * _lid_jitter(upper, row, col, 0)
                    fraction += LID_JITTER_PHI / arc_steps * _lid_jitter(upper, row, col, 1)
                phi = math.radians(front + (back - front) * fraction)
                x, ring = rx * math.sin(lat), math.cos(lat)
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
            # r8: alternating diagonals (checkerboard) instead of flat quads.
            if (row + col) % 2 == 0:
                top += [(a, a + width, a + width + 1), (a, a + width + 1, a + 1)]
            else:
                top += [(a, a + width, a + 1), (a + width, a + width + 1, a + 1)]
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


def _strip_mesh(rings):
    """Closed loft through equal-size open-section rings with planar end caps."""
    n = len(rings[0])
    vertices = [v for ring in rings for v in ring]
    faces = [tuple(reversed(range(n)))]
    for row in range(len(rings) - 1):
        for j in range(n):
            a, b = row * n + j, row * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    return outward(vertices, faces)


def inside_white(point, grow=0.0):
    """Pivot-relative point inside the white ellipsoid grown by `grow` metres."""
    x, y, z = point
    return ((x / (EYE_RADII[0] + grow)) ** 2 + ((y + EYE_FORWARD_OFFSET) / (EYE_RADII[1] + grow)) ** 2
            + (z / (EYE_RADII[2] + grow)) ** 2) < 1


def _bisect(test, lo, hi, steps=40):
    """Last parameter in [lo, hi] for which test() still holds (test(lo) true)."""
    for _ in range(steps):
        mid = (lo + hi) * .5
        lo, hi = (mid, hi) if test(mid) else (lo, mid)
    return lo


def _last_inside(test, hi, step=.0005):
    """Exit parameter of a ray that may START outside the region (r8: with the
    white further ahead and shallower, the pivot axis no longer lies inside it
    near its lateral edge, and a plain bisection from 0 collapsed those cap
    rows onto the pivot). Scans for the last inside sample, then bisects."""
    last, t = None, 0.0
    while t <= hi:
        if test(t):
            last = t
        t += step
    if last is None:
        return 0.0
    return _bisect(test, last, min(hi, last + step))


def _rim_point(psi, t, behind=0.0):
    """Pivot-relative point at front-view angle psi, distance t from the pivot
    axis, on the shutters' rest-edge surface (upper edge above the axis, lower
    edge below it) pushed `behind` degrees away from the front, plus whether
    it belongs to the upper half."""
    c, s = math.cos(psi), math.sin(psi)
    x, h = t * c, t * s
    upper = h >= 0
    rx = lid_radii(upper, HOUSING_RECESS)[0] - LID_THICKNESS
    latitude = math.asin(max(-1.0, min(1.0, x / rx)))
    edge = math.radians(lid_front_degrees(upper, latitude) + (behind if upper else -behind))
    radius = h / math.sin(edge)
    return (x, -radius * math.cos(edge), h), upper, radius


def _inside_housing(psi, t, fill):
    point, upper, radius = _rim_point(psi, t, COLLAR_BEHIND_DEGREES)
    hx, hyz = (r - LID_THICKNESS for r in lid_radii(upper, HOUSING_RECESS))
    return (point[0] / hx) ** 2 + (radius / hyz) ** 2 < fill * fill


def collar_mesh(sign, part='EyeCollar'):
    """Fixed red rim face in the shutters' rest-edge surface (r6).

    Each section spans, at front-view angle psi, from 0.3 mm off the white to
    98.5% of its housing's inner layer, 0.6 deg behind the rest-edge surface
    (r7); it is 1.2 mm thick toward the back.
    Seen from the side it is edge-on (no second rim), seen from the front it
    is the red ring of the cup around the white. Closed strip with end caps
    (sphere topology), outward winding. part selects the arc
    (COLLAR_ARCS_DEGREES).
    """
    pivot = eye_center(sign)
    first, last = COLLAR_ARCS_DEGREES[part]
    rings = []
    for i in range(COLLAR_SEGMENTS + 1):
        psi = math.radians(first + (last - first) * i / COLLAR_SEGMENTS)
        outer = _bisect(lambda t: _inside_housing(psi, t, COLLAR_OUTER_FILL), 0.0, .07)
        inner = _bisect(lambda t: inside_white(_rim_point(psi, t, COLLAR_BEHIND_DEGREES)[0], COLLAR_INNER_CLEARANCE), 0.0, outer)
        inner = min(inner, outer - .0008)
        section = []
        for t, back in ((inner, 0.0), (outer, 0.0), (outer, COLLAR_THICKNESS), (inner, COLLAR_THICKNESS)):
            x, y, z = _rim_point(psi, t, COLLAR_BEHIND_DEGREES)[0]
            section.append((pivot[0] + sign * x, pivot[1] + y + back, pivot[2] + z))
        rings.append(section)
    return _strip_mesh(rings)


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def cap_mesh(sign):
    """Fixed eyelid wedge over the top of the white (r6, PER-03 NEUTRAL).

    Rows across the eye (pivot-relative x), columns by angle about the pivot
    axis. The inner layer follows the white 0.4 mm off it; the outer layer
    starts 1.2 mm above that at the lid's front edge and rises to 99% of the
    upper housing's inner layer at the upper shutter's rim (r6: 96.5% of the
    lower housing, which left a dark slot), then runs on under the shutter.
    Closed (two layers and four walls), sphere topology.
    """
    pivot = eye_center(sign)
    hx, hyz = (r - LID_THICKNESS for r in lid_radii(True, HOUSING_RECESS))
    shutter_x = lid_radii(True)[0]
    inner_layer, outer_layer = [], []
    for k in range(CAP_ROWS):
        x = -CAP_X_EXTENT + 2 * CAP_X_EXTENT * k / (CAP_ROWS - 1)
        section = hyz * math.sqrt(max(0.0, 1 - (x / hx) ** 2))
        ceiling = CAP_OUTER_FILL * section
        front = CAP_FRONT_DEGREES + (CAP_FRONT_SIDE_DEGREES - CAP_FRONT_DEGREES) * (abs(x) / CAP_X_EXTENT) ** CAP_FRONT_RAMP_POWER
        rim = lid_front_degrees(True, math.asin(min(1.0, abs(x) / shutter_x)))
        top = max(rim, front + CAP_MIN_RISE_DEGREES)
        columns_degrees = ([front + (top - front) * i / CAP_RISE_STEPS for i in range(CAP_RISE_STEPS + 1)]
                           + [top + (CAP_BACK_DEGREES - top) * i / CAP_BACK_STEPS for i in range(1, CAP_BACK_STEPS + 1)])
        previous = None
        for degrees in columns_degrees:
            phi = math.radians(degrees)
            direction = (0.0, -math.cos(phi), math.sin(phi))

            def at(t):
                return (x, t * direction[1], t * direction[2])
            t_in = _last_inside(lambda t: inside_white(at(t), CAP_INNER_CLEARANCE), .07)
            # r8: behind the (further forward) white the outer rows' rays miss
            # it; the inner layer then keeps the last radius it had (hidden
            # under the shutter) instead of collapsing onto the pivot.
            if previous is not None and t_in < previous * .5:
                t_in = previous
            previous = t_in
            rise = _smooth((degrees - front) / (top - front))
            settle = _smooth((degrees - top) / CAP_RIM_TAPER_DEGREES)
            limit = ceiling + (CAP_BACK_FILL * section - ceiling) * settle
            t_out = max(t_in + CAP_EDGE_THICKNESS * (1 - rise), t_in + (limit - t_in) * rise)
            t_out = min(max(t_out, t_in + .0006), max(limit, t_in + .0006))
            for layer, t in ((inner_layer, t_in), (outer_layer, t_out)):
                px, py, pz = at(t)
                layer.append((pivot[0] + sign * px, pivot[1] + py, pivot[2] + pz))
    columns = CAP_RISE_STEPS + CAP_BACK_STEPS + 1
    size = len(inner_layer)
    vertices = inner_layer + outer_layer
    sheet = []
    for k in range(CAP_ROWS - 1):
        for j in range(columns - 1):
            a = k * columns + j
            sheet.append((a, a + 1, a + columns + 1, a + columns))
    faces = sheet + [tuple(size + v for v in reversed(f)) for f in sheet]
    boundary = ([j for j in range(columns)] + [k * columns + columns - 1 for k in range(1, CAP_ROWS)]
                + [(CAP_ROWS - 1) * columns + j for j in reversed(range(columns - 1))]
                + [k * columns for k in reversed(range(1, CAP_ROWS - 1))])
    faces += [(b, a, a + size, b + size) for a, b in zip(boundary, boundary[1:] + boundary[:1])]
    return outward(vertices, faces)


# r8 (integration review r2: black wedges at the top of each cup, dotted dark
# lines on its lateral edge): from oblique views a few inward-facing surfaces
# of the cup show through the rim slots (the collar's inner wall, the inner
# layers of the shells near their edges). Facing the pivot, they only get
# ambient light and read black at game scale. Every shell-material face of the
# eye assembly (within the closed-shutter radius around a pivot) that faces
# the pivot gets a custom split normal pointing radially outward, so wherever
# it shows it is lit like the cup around it. Winding and geometry unchanged.
EYE_INTERIOR_RADII = (.030, .0495)
EYE_INTERIOR_FACING = -.25


def light_eye_interiors(obj):
    """Run AFTER the export's normal consistency pass on MosquitoSkin."""
    if obj.name != 'MosquitoSkin':
        return None
    from mathutils import Vector
    me = obj.data
    shells = {i for i, m in enumerate(me.materials) if m and m.name.startswith('Mosquito_Shell')}
    pivots = [Vector(eye_center(sign)) for sign in (1, -1)]
    normals = [(0.0, 0.0, 0.0)] * len(me.loops)
    changed = 0
    for polygon in me.polygons:
        if polygon.material_index not in shells:
            continue
        points = [me.vertices[i].co for i in polygon.vertices]
        for pivot in pivots:
            if not all(EYE_INTERIOR_RADII[0] <= (p - pivot).length <= EYE_INTERIOR_RADII[1] for p in points):
                continue
            radial = (polygon.center - pivot).normalized()
            if polygon.normal.dot(radial) < EYE_INTERIOR_FACING:
                for loop in polygon.loop_indices:
                    normals[loop] = tuple(radial)
                changed += 1
            break
    me.normals_split_custom_set(normals)
    me.update()
    return {'faces': changed, 'radii_m': list(EYE_INTERIOR_RADII), 'facing_threshold': EYE_INTERIOR_FACING}


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
                  'inward_yaw_degrees': PUPIL_INWARD_DEGREES, 'down_pitch_degrees': PUPIL_DOWN_DEGREES},
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
        'eye_collar': 'fixed red rim faces weighted to Head (EyeCollar.L/R lateral, EyeCollarInner.L/R medial) 0.6 deg behind the shutters rest-edge surface, from the white to 98.5% of the housing inner shell, behind the housing front chamfer; never touched by the shutters',
        'eye_cap': 'fixed eyelid wedge weighted to Head (EyeCap.L/R) from the top of the white (~12% of its front-view height) up under the upper shutter rim; inside the innermost shell',
        'rest_pose': 'upper shutter rests at ~66 deg over the eyelid cap, lower at ~-104 deg behind the bottom of the white; blink closure 0..1 starts from that bind pose',
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
            # A fixed rear housing prevents the white back of the eye becoming
            # exposed in profile when the articulated shutters rotate forward.
            vertices, faces = lid_mesh(sign, upper, recess=HOUSING_RECESS)
            mesh('EyeHousing.' + name, vertices, faces, shell, 'Head')
        # r6: one rim (the collar face in the rest-edge surface) and a fixed
        # eyelid cap over the top of the white; no rolled lip on the shutter.
        for part in COLLAR_ARCS_DEGREES:
            mesh(part + '.' + side, *collar_mesh(sign, part), shell, 'Head')
        mesh('EyeCap.' + side, *cap_mesh(sign), shell, 'Head')


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
