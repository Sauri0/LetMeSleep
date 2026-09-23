"""Let me sleep v0.3.0: modular customization parts on the production rigs.

Run (Blender 5.2, one species per call):
  blender --background --factory-startup --python author_modular_parts.py -- --species Human
  blender --background --factory-startup --python author_modular_parts.py -- --species Mosquito

What it does
  * Builds the SAME authored character as build_characters.py (same generator, same armature,
    same bind pose) without its animation and without touching the production exports
    (human/, mosquito/ and menu/ are never written).
  * Splits the authored body into catalog parts (the defaults) and authors the new options
    (sketches PER-04/PER-05/PER-06/PER-07 and UI-06 screens 5-6) on that rig.
  * Exports one FBX per option to modular/<species>/<slot>__<option>.fbx (armature + the
    part's skinned meshes, no animation), a preview .blend with every part, and
    modular/<species>/parts.json (renderers, materials, palette, bones, rig signature).

Rig contract: every part is a SkinnedPart on the unchanged production bones (no new sockets).
Head wear, hair and glasses are rigid on Head, the backpack on Chest, sneakers on Foot, wings on
Wing.L/R, proboscis variants on Proboscis, markings follow Abdomen01/02 like the abdomen.
The authored face stays on the host: the human head (blink shape keys, hair sideburns, neck)
and hands are never part of a catalog option; the mosquito eyes are bone driven (Pupil/Lid
bones), so the mosquito base part carries the identical face geometry on the same bones.

Style: low-poly facetado (flat faces), saturated palette of docs/v030/GUIA-ESTILO-BOCETOS.md.
No firearms, no crafting. Deterministic: no unseeded randomness, so every run rebuilds the same
geometry.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import build_characters as bc  # noqa: E402  (same generator as production)
import author_motion  # noqa: E402
import author_mosquito_motion  # noqa: E402

OUT = HERE / 'modular'
RIG_IDS = {'Human': 'lms.human.v030', 'Mosquito': 'lms.mosquito.v030'}
REVISION = 'modular-parts-v030-r2'


# ----------------------------------------------------------------------------- helpers
def h(*values):
    """Deterministic pseudo random in [0, 1)."""
    return math.sin(sum(v * k for v, k in zip(values, (12.9898, 78.233, 37.719, 4.581, 9.151))) * 43758.5453) % 1


def srgb_to_linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


NEW_PALETTE = {}


def palette(name, hexcode, roughness=.85):
    """Shared palette material (the Unity builder maps it by name to Materials/<name>.mat).
    Existing production materials are reused as they are."""
    existing = bpy.data.materials.get(name)
    if existing is not None:
        return existing
    srgb = tuple(int(hexcode[i:i + 2], 16) / 255 for i in (1, 3, 5))
    m = bc.material(name, srgb, roughness)
    bc.principled(m).inputs['Base Color'].default_value = (*[srgb_to_linear(v) for v in srgb], 1)
    NEW_PALETTE[name] = {'hex': hexcode, 'roughness': roughness}
    return m


def make_mesh(name, verts, faces, materials, weights, face_materials=None):
    """Mesh object with explicit per-vertex weights ({bone: weight}, normalised, <= 4)."""
    data = bpy.data.meshes.new(name)
    data.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for m in materials:
        data.materials.append(m)
    if face_materials is not None:
        for poly, index in zip(data.polygons, face_materials):
            poly.material_index = index
    if callable(weights):
        weights = [weights(Vector(v)) for v in verts]
    elif isinstance(weights, str):
        weights = [{weights: 1.0}] * len(verts)
    groups = {}
    for i, row in enumerate(weights):
        best = sorted(((k, w) for k, w in row.items() if w > 1e-6), key=lambda kv: -kv[1])[:4]
        total = sum(w for _, w in best)
        assert total > 0, (name, i)
        for bone, w in best:
            if bone not in groups:
                groups[bone] = obj.vertex_groups.new(name=bone)
            groups[bone].add([i], w / total, 'REPLACE')
    for poly in data.polygons:
        poly.use_smooth = False
    return obj


def loft(rings, caps=(True, True)):
    """Faces of a closed loft through equal-size rings (optionally capped)."""
    n = len(rings[0])
    faces = []
    if caps[0]:
        faces.append(tuple(reversed(range(n))))
    for r in range(len(rings) - 1):
        faces += [(r * n + j, r * n + (j + 1) % n, (r + 1) * n + (j + 1) % n, (r + 1) * n + j) for j in range(n)]
    if caps[1]:
        faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    return [p for ring in rings for p in ring], faces


def tube_along(points, radii, sides=6, up_hint=Vector((0, 0, 1)), flat=1.0, caps=(True, True)):
    """Rings along a polyline with rotation-minimising frames. flat < 1 squashes the section
    along the frame's second axis (strap-like)."""
    points = [Vector(p) for p in points]
    tangents = []
    for i in range(len(points)):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        tangents.append((b - a).normalized())
    normal = up_hint - tangents[0] * up_hint.dot(tangents[0])
    if normal.length < 1e-6:
        normal = Vector((1, 0, 0)) - tangents[0] * tangents[0].x
    normal.normalize()
    rings = []
    for p, r, t in zip(points, radii, tangents):
        normal = (normal - t * normal.dot(t)).normalized()
        binormal = t.cross(normal)
        ring = []
        for j in range(sides):
            a = math.tau * (j + .5) / sides
            ring.append(p + r * math.cos(a) * normal + r * flat * math.sin(a) * binormal)
        rings.append(ring)
    return loft(rings, caps)


def icosphere(center, radius, subdivisions=1, jitter=.08, seed=0.0, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1)
    bm.verts.index_update()
    c = Vector(center)
    verts = []
    for v in bm.verts:
        n = v.co.normalized()
        k = 1 + jitter * (h(n.x, n.y, n.z, seed) - .5) * 2
        verts.append(c + Vector((n.x * scale[0], n.y * scale[1], n.z * scale[2])) * radius * k)
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return verts, faces


def merge(*parts):
    verts, faces = [], []
    for v, f in parts:
        base = len(verts)
        verts += list(v)
        faces += [tuple(i + base for i in face) for face in f]
    return verts, faces


def triangles_of(obj):
    obj.data.calc_loop_triangles()
    mw = obj.matrix_world
    return [tuple(mw @ obj.data.vertices[i].co for i in t.vertices) for t in obj.data.loop_triangles]


def bvh_of(*objects):
    verts, polys = [], []
    for obj in objects:
        base = len(verts)
        verts += [obj.matrix_world @ v.co for v in obj.data.vertices]
        polys += [[i + base for i in p.vertices] for p in obj.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def weights_from(source_objects, radius=.05):
    """Weight lookup copying the nearest source vertex group mix (data-transfer like)."""
    samples = []
    for obj in source_objects:
        names = {g.index: g.name for g in obj.vertex_groups}
        for v in obj.data.vertices:
            row = {names[g.group]: g.weight for g in v.groups if g.weight > 0}
            if row:
                samples.append((obj.matrix_world @ v.co, row))
    from mathutils.kdtree import KDTree
    tree = KDTree(len(samples))
    for i, (p, _) in enumerate(samples):
        tree.insert(p, i)
    tree.balance()

    def lookup(point):
        found = tree.find_n(Vector(point), 4)
        mix = {}
        total = 0.0
        for _, index, distance in found:
            w = 1.0 / max(distance, 1e-5) ** 2
            total += w
            for bone, value in samples[index][1].items():
                mix[bone] = mix.get(bone, 0) + value * w
        return {k: v / total for k, v in mix.items()}
    return lookup


# ----------------------------------------------------------------------------- part registry
class Part:
    def __init__(self, species, slot, option, renderers, colors=(), conditional=None, fp_head=False,
                 facial='None', note='', shown_only=None, anchors=()):
        self.species, self.slot, self.option = species, slot, option
        # renderer name -> list of scene objects
        self.renderers = renderers
        # (renderer, material name, colour slot, shade reference material or None, alpha override)
        self.colors = list(colors)
        # renderer -> slot id whose non-none selection hides that renderer (the full hairstyle under hats)
        self.conditional = conditional or {}
        # renderer -> slot id: the renderer shows ONLY while that slot has a non-none selection (hair under a hat)
        self.shown_only = shown_only or {}
        # (host anchor name, source bone, point in bind/armature space): the anchor follows this point of the part
        # instead of the bone head (a proboscis whose tip is not on Socket.Mouth keeps the bite on its real tip)
        self.anchors = list(anchors)
        self.fp_head = fp_head
        self.facial = facial
        self.note = note


PARTS = []


def add_part(*args, **kwargs):
    PARTS.append(Part(*args, **kwargs))


def copy_object(obj, name):
    dup = obj.copy()
    dup.data = obj.data.copy()
    dup.name = name
    bpy.context.collection.objects.link(dup)
    return dup


# ============================================================================= HUMAN
def build_human_parts():
    import author_human_geometry as ag
    import author_human_joints as aj
    from author_human_facial import EYE_CENTERS, EYE_RADIUS, EYE_HEIGHT_FACTOR
    O = bpy.data.objects
    skull = triangles_of(O['HeadAuthoredPlanes'])
    mats = bpy.data.materials

    # ---- defaults: the production pieces, split by option --------------------------------
    add_part('Human', 'human.base', 'base',
             {'HumanPartBase': [O[n] for n in ('Arm.L', 'Arm.R', 'FootSkin.L', 'FootSkin.R', 'Ankle.L', 'Ankle.R')]},
             colors=[('HumanPartBase', 'Human_Skin', 'human.skin', None, None)],
             note='Bare arms, feet and ankles (the authored head, neck and hands stay on the host).')
    add_part('Human', 'human.top', 'remera',
             {'HumanPartTop': [O[n] for n in ('Shirt', 'ShirtCollar', 'Sleeve.L', 'Sleeve.R')]},
             colors=[('HumanPartTop', 'Human_Shirt', 'human.top_color', None, None),
                     ('HumanPartTop', 'Human_ShirtShade', 'human.top_color', 'Human_Shirt', None)])
    pajama_objects = [O[n] for n in ('PajamaLeg.L', 'PajamaLeg.R', 'PajamaDots.L', 'PajamaDots.R',
                                     'TrouserCuff.L', 'TrouserCuff.R', 'TrouserSeat', 'Waistband')]
    add_part('Human', 'human.bottom', 'pijama', {'HumanPartBottom': pajama_objects},
             colors=[('HumanPartBottom', 'Human_Pajamas', 'human.pajama', None, None),
                     ('HumanPartBottom', 'Human_PantsShade', 'human.pajama', 'Human_Pajamas', None)])
    add_part('Human', 'human.footwear', 'pantuflas',
             {'HumanPartFeet': [O[n] for n in ('SlipperSole.L', 'SlipperSole.R', 'SlipperUpper.L', 'SlipperUpper.R')]})
    add_part('Human', 'human.headwear', 'gorro-dormir',
             {'HumanPartHeadwear': [O[n] for n in ('NightcapBand', 'NightcapCrown', 'NightcapTail', 'NightcapPom')]},
             fp_head=True)

    # ---- hair ------------------------------------------------------------------------------
    # r2 (modular review r1): under a hat the r1 hair showed the host's authored sideburns (10-14 mm off the skull,
    # a hard lower edge) plus the style's side tufts or curls: a black plate at the temple under the cap brim, a
    # studded disc under the beanie. Now every hat hides the host's authored hair (its Human_Hair channel on
    # HumanHead is alpha-clipped while a headwear is worn, see CharacterCustomizationContentBuilder) and each
    # hairstyle has two variants:
    #   * HumanPartHairTop - the full style (crown, the style's sideburns and nape over the host hair, locks or
    #     curls), hidden while any headwear is worn;
    #   * HumanPartHairUnderHat - the style under a hat: one flat slab <= 5 mm off the skull from under the hat
    #     edge to the hair line, its lower edge bevelled to 1 mm (plain for corto, flat points for despeinado,
    #     flat scallops for rulos), tucked at the nape; shown only while a headwear is worn.
    hair = mats['Human_Hair']
    COLS = ag.COLS
    HB = ag.HAIR_BOTTOM
    hb_keys = sorted(HB)
    UNDER_HAT_MAX = .0050
    # Under a hat: the host slab's shape at <= 5 mm, the last row 1 mm (a bevel that meets the skin).
    THIN_ROWS = [(0.0, .0040), (.17, .0045), (.34, .0046), (.51, .0046), (.68, .0045), (.84, .0036), (1.0, .0010)]
    THIN_SIDEBURN = 1.08
    # Without a hat: the style's sideburns and nape cover the host slab (ag.HAIR_ROWS, 10 mm, 14 mm sideburns)
    # by 1.8 mm, reach 6 mm below its hard lower edge and end in a bevel there.
    COVER = .0018
    FULL_ROWS = [(t, d + COVER) for t, d in ag.HAIR_ROWS if t < .9] + [(.97, .0100 + COVER), (1.0, .0022)]

    def authored_bottom(c):
        """Lower edge of the host's authored hair at a fractional column (sideburn 3.64 .. nape 7 .. 10.36)."""
        m = min(c % COLS, COLS - c % COLS)
        if m <= hb_keys[0]:
            return HB[hb_keys[0]]
        if m >= hb_keys[-1]:
            return HB[hb_keys[-1]]
        for a, b in zip(hb_keys, hb_keys[1:]):
            if a <= m <= b:
                return HB[a] + (HB[b] - HB[a]) * (m - a) / (b - a)

    def full_thickness(c):
        m = min(c % COLS, COLS - c % COLS)
        return ag.SIDEBURN_THICKNESS if ag.SIDEBURN_COLUMNS[0] <= m <= ag.SIDEBURN_COLUMNS[1] else 1.0

    def thin_thickness(c):
        m = min(c % COLS, COLS - c % COLS)
        return THIN_SIDEBURN if ag.SIDEBURN_COLUMNS[0] <= m <= ag.SIDEBURN_COLUMNS[1] else 1.0

    ARC = [3.60 + k * (10.40 - 3.60) / 68 for k in range(69)]

    def edge_slab(columns, top_fn, bottom_fn, profile, thick_fn=None, inner=-.002):
        """Closed shell hugging the skull from top_fn(c) to bottom_fn(c) over an open arc of columns.
        profile: [(t, distance)] from the top row (t 0) to the lower edge (t 1)."""
        outer_rows, inner_rows = [], []
        for t, d in profile:
            orow, irow = [], []
            for c in columns:
                z = top_fn(c) + (bottom_fn(c) - top_fn(c)) * t
                k = thick_fn(c) if thick_fn else 1.0
                orow.append(Vector(ag.radial_surface(skull, z, c, d * k)))
                irow.append(Vector(ag.radial_surface(skull, z, c, inner)))
            outer_rows.append(orow)
            inner_rows.append(irow)
        rows, cols = len(outer_rows), len(columns)
        verts = [p for row in outer_rows for p in row] + [p for row in inner_rows for p in row]
        base = rows * cols
        faces = []
        for r in range(rows - 1):
            for c in range(cols - 1):
                a = r * cols + c
                faces.append((a, a + 1, a + cols + 1, a + cols))
                faces.append((base + a + cols, base + a + cols + 1, base + a + 1, base + a))
        for c in range(cols - 1):
            faces.append((c + 1, c, base + c, base + c + 1))
            top = (rows - 1) * cols + c
            faces.append((top, top + 1, base + top + 1, base + top))
        for r in range(rows - 1):
            a = r * cols
            faces.append((a + cols, a, base + a, base + a + cols))
            b = r * cols + cols - 1
            faces.append((b, b + cols, base + b + cols, base + b))
        return verts, faces

    def hairline(c):
        """Bottom of the crown shell per skull column (0 front, 7 back)."""
        c = c % COLS
        d = min(c, COLS - c)
        return {0: 1.651, 1: 1.653, 2: 1.648, 3: 1.636}.get(int(round(d)), 1.630) if d < 3.5 else 1.630

    def shell_rings(distance, columns=28, bottom_offset=0.0, jitter=0.0, seed=1.0):
        rows = []
        for k, t in enumerate((0.0, .30, .58, .82, 1.0)):
            ring = []
            for j in range(columns):
                c = j * COLS / columns
                z0 = hairline(c) + bottom_offset
                z = z0 + (1.706 - z0) * t
                d = distance * (1.0 if k else 1.15)
                if jitter and 0 < k < 4:
                    d += jitter * (h(j, k, seed) - .5) * 2
                ring.append(Vector(ag.radial_surface(skull, z, c, d)))
            rows.append(ring)
        last = rows[-1]
        cx = sum(p.x for p in last) / len(last)
        cy = sum(p.y for p in last) / len(last)
        for z, s in ((1.717, .62), (1.724, .30)):
            rows.append([Vector((cx + (p.x - cx) * s, cy + (p.y - cy) * s, z + distance * .9)) for p in last])
        apex = Vector((cx, cy, 1.726 + distance))
        return rows, apex

    def closed_cap(outer_rows, outer_apex, inner_rows, inner_apex):
        n = len(outer_rows[0])
        verts = [p for r in outer_rows for p in r] + [outer_apex]
        ia = len(verts) - 1
        faces = []
        for r in range(len(outer_rows) - 1):
            faces += [(r * n + j, r * n + (j + 1) % n, (r + 1) * n + (j + 1) % n, (r + 1) * n + j) for j in range(n)]
        last = (len(outer_rows) - 1) * n
        faces += [(last + j, last + (j + 1) % n, ia) for j in range(n)]
        base = len(verts)
        verts += [p for r in inner_rows for p in r] + [inner_apex]
        ib = len(verts) - 1
        for r in range(len(inner_rows) - 1):
            faces += [(base + r * n + (j + 1) % n, base + r * n + j, base + (r + 1) * n + j, base + (r + 1) * n + (j + 1) % n)
                      for j in range(n)]
        last = base + (len(inner_rows) - 1) * n
        faces += [(last + (j + 1) % n, last + j, ib) for j in range(n)]
        faces += [(j, base + j, base + (j + 1) % n, (j + 1) % n) for j in range(n)]  # bottom rim
        return verts, faces

    def hair_top(distance=.0105, jitter=.0022, seed=1.0):
        outer, apex_o = shell_rings(distance, jitter=jitter, seed=seed)
        inner, apex_i = shell_rings(-.004)
        return closed_cap(outer, apex_o, inner, apex_i)

    def points_edge(c, tips):
        """Downward offset (negative) of a lower edge with flat pointed locks: tips = [(column, depth, half width)]."""
        m = min(c % COLS, COLS - c % COLS)
        return -max([0.0] + [depth * max(0.0, 1 - abs(m - col) / hw) ** 1.3 for col, depth, hw in tips])

    def scallop_edge(c, depth=.0085, lobe=.62):
        """Downward offset of a lower edge made of round lobes (curls lying flat)."""
        u = (c - 3.60) / lobe
        return -depth * abs(math.sin(math.pi * u)) ** .6

    def full_sides(bottom_offset_fn):
        """The style's sideburns and nape without a hat: over the host slab (1.8 mm proud), 6 mm below its edge
        plus the style's points or lobes, bevelled at the bottom."""
        return edge_slab(ARC, lambda c: ag.HAIR_TOP, lambda c: authored_bottom(c) - .006 + bottom_offset_fn(c), FULL_ROWS,
                         thick_fn=full_thickness)

    def under_hat_band(bottom_offset_fn):
        """The style under a hat: one slab <= 5 mm off the skull from under every hat edge (1.640) to the host hair
        line plus the style's flat points or lobes, bevelled to 1 mm at the bottom."""
        assert max(d * THIN_SIDEBURN for _, d in THIN_ROWS) <= UNDER_HAT_MAX
        return edge_slab(ARC, lambda c: 1.640, lambda c: authored_bottom(c) + bottom_offset_fn(c), THIN_ROWS,
                         thick_fn=thin_thickness)

    def head_out(p):
        return (Vector(p) - Vector((0, .005, 1.585))).normalized()

    def lock(path, widths, thick, sides=8):
        """A big rounded lock: a flat lofted strip (width across, thin outward) along a path near the scalp, its
        tip closed by a small cap (blunt, never a spike)."""
        pts = [Vector(p) for p in path]
        rings = []
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            n = head_out(p)
            n = (n - t * n.dot(t)).normalized()
            b = t.cross(n)
            ring = []
            for j in range(sides):
                a = math.tau * (j + .5) / sides
                ring.append(p + b * widths[i] * math.cos(a) + n * thick[i] * math.sin(a) + n * thick[i] * .35)
            rings.append(ring)
        return loft(rings)

    def surface_path(c0, z0, c1, z1, lift, samples=6, base=.010, bulge=.006):
        """Points from (c0, z0) to (c1, z1) over the skull, rising from `base` to `base + lift` off it."""
        out = []
        for k in range(samples):
            f = k / (samples - 1)
            c, z = c0 + (c1 - c0) * f, z0 + (z1 - z0) * f
            d = base + lift * f ** 1.6 + bulge * math.sin(math.pi * f)
            if z <= 1.712:
                out.append(ag.radial_surface(skull, z, c, d))
            else:
                out.append(tuple(Vector(ag.radial_surface(skull, 1.712, c, d)) + Vector((0, 0, (z - 1.712) * .8))))
        return out

    # corto: a short faceted crown over the host's sideburns and nape; under hats a plain thin slab.
    corto_top = make_mesh('HairCortoTop', *hair_top(seed=1.3), [hair], 'Head')
    corto_under = make_mesh('HairCortoUnderHat', *under_hat_band(lambda c: 0.0), [hair], 'Head')
    add_part('Human', 'human.hair', 'corto', {'HumanPartHairTop': [corto_top], 'HumanPartHairUnderHat': [corto_under]},
             colors=[('HumanPartHairTop', 'Human_Hair', 'human.hair_color', None, None),
                     ('HumanPartHairUnderHat', 'Human_Hair', 'human.hair_color', None, None)],
             conditional={'HumanPartHairTop': 'human.headwear'}, shown_only={'HumanPartHairUnderHat': 'human.headwear'},
             fp_head=True)

    def volume_shell(extra, base=.0100, hairline_fn=None, lean=None, columns=28, rows=8):
        """The hair mass as ONE closed faceted volume: the crown shell (hairline to apex) pushed out by extra(p)
        along the head's outward direction (and sideways by lean(p) for swept locks). Lumps merge into each
        other, so the style reads as grouped volumes, never as studs on a smooth cap."""
        hairline_fn = hairline_fn or hairline
        def ring(t):
            out = []
            for j in range(columns):
                c = j * COLS / columns
                z0 = hairline_fn(c)
                out.append(Vector(ag.radial_surface(skull, z0 + (1.706 - z0) * t, c, 0.0)))
            return out
        base_rows = [ring(k / (rows - 1)) for k in range(rows)]
        last = base_rows[-1]
        cx = sum(p.x for p in last) / len(last)
        cy = sum(p.y for p in last) / len(last)
        for z, s in ((1.713, .80), (1.719, .58), (1.723, .34)):
            base_rows.append([Vector((cx + (p.x - cx) * s, cy + (p.y - cy) * s, z)) for p in last])
        apex_base = Vector((cx, cy, 1.726))

        def push(p, distance):
            n = head_out(p)
            q = p + n * (distance + extra(p))
            return q + lean(p) if lean else q
        outer = [[push(p, base * (1.15 if k == 0 else 1.0)) for p in row] for k, row in enumerate(base_rows)]
        inner = [[p + head_out(p) * -.004 for p in row] for row in base_rows]
        return closed_cap(outer, push(apex_base, base), inner, apex_base + Vector((0, 0, -.004)))

    def bump(p, centre, radius, height, power=1.5):
        d = (Vector(p) - centre).length
        return height * max(0.0, 1 - (d / radius) ** 2) ** power

    def on_skull(c, z, d=0.0):
        return Vector(ag.radial_surface(skull, z, c, d)) if z <= 1.706 else Vector((0, .010, z))

    # despeinado (PER-05 MESSY): one chunky volume whose tufts rise into faceted locks leaning in different
    # directions (fringe forward over the forehead, crown up and back, sides out) and a jagged, lower fringe
    # line; sideburns and nape end in a few flat points. Under hats the same points lie flat (<= 5 mm).
    messy_tips_full = [(3.72, .016, .34), (4.95, .012, .40), (5.75, .018, .42), (6.55, .020, .38), (7.0, .013, .30)]
    messy_tips_under = [(3.72, .010, .30), (4.95, .008, .36), (5.75, .012, .40), (6.55, .013, .36), (7.0, .009, .28)]
    # (column, height, tuft height, radius, lean (outward, up, forward, left))
    tufts = [(0.0, 1.686, .034, .055, (0, -.3, 1.0, .25)), (1.5, 1.688, .032, .054, (0, -.2, .8, -.6)),
             (12.5, 1.688, .032, .054, (0, -.2, .8, .6)), (2.9, 1.706, .040, .058, (.3, .7, -.2, -.7)),
             (11.1, 1.706, .040, .058, (.3, .7, -.2, .7)), (5.2, 1.700, .038, .058, (.2, .6, -.8, -.3)),
             (8.8, 1.700, .038, .058, (.2, .6, -.8, .3)), (4.2, 1.660, .030, .050, (.6, -.2, -.3, -.8)),
             (9.8, 1.660, .030, .050, (.6, -.2, -.3, .8)), (7.0, 1.664, .032, .056, (.2, -.4, -.9, 0))]
    tuft_data = []
    for c, z, height, radius, (o, u, fw, lf) in tufts:
        centre = on_skull(c, z, .010) if z <= 1.706 else Vector((0, .010, 1.736))
        direction = head_out(centre) * o + Vector((0, 0, 1)) * u + Vector((0, -1, 0)) * fw + Vector((1, 0, 0)) * lf
        tuft_data.append((centre, radius, height, direction.normalized()))

    def messy_extra(p):
        return max([0.0] + [bump(p, centre, radius, height, 1.0) for centre, radius, height, _ in tuft_data]) + .003

    def messy_lean(p):
        total = Vector()
        for centre, radius, height, direction in tuft_data:
            total += direction * bump(p, centre, radius, height * .85, 1.6)
        return total

    def messy_hairline(c):
        m = min(c % COLS, COLS - c % COLS)
        if m > 2.6:
            return hairline(c)
        # a lower, jagged fringe: three chunks fall onto the forehead (stay above the brows, z >= 1.626)
        return max(1.626, hairline(c) - .014 - .010 * abs(math.sin(math.pi * (c + .5) / 1.3)))

    messy_top = make_mesh('HairDespeinadoTop', *merge(
        volume_shell(messy_extra, .0105, messy_hairline, messy_lean),
        full_sides(lambda c: points_edge(c, messy_tips_full))), [hair], 'Head')
    messy_under = make_mesh('HairDespeinadoUnderHat', *under_hat_band(lambda c: points_edge(c, messy_tips_under)),
                            [hair], 'Head')
    add_part('Human', 'human.hair', 'despeinado',
             {'HumanPartHairTop': [messy_top], 'HumanPartHairUnderHat': [messy_under]},
             colors=[('HumanPartHairTop', 'Human_Hair', 'human.hair_color', None, None),
                     ('HumanPartHairUnderHat', 'Human_Hair', 'human.hair_color', None, None)],
             conditional={'HumanPartHairTop': 'human.headwear'}, shown_only={'HumanPartHairUnderHat': 'human.headwear'},
             fp_head=True)

    # rulos (PER-05 CURLY): one volume of ten big rounded curl clusters that merge into each other (a lumpy
    # outline, no smooth cap between them) over scalloped sideburns and nape; under hats the scallops lie flat.
    cluster_data = [(on_skull(c, z, .010), radius, height) for c, z, radius, height in (
        (0.7, 1.690, .052, .046), (13.3, 1.690, .052, .046), (2.8, 1.688, .054, .050), (11.2, 1.688, .054, .050),
        (5.2, 1.684, .054, .050), (8.8, 1.684, .054, .050), (4.2, 1.636, .046, .036), (9.8, 1.636, .046, .036),
        (7.0, 1.638, .050, .042), (0.0, 1.762, .062, .040))]

    def curly_extra(p):
        # overlapping round lumps; the max keeps each cluster round where two meet (a crease between them)
        return max([0.0] + [bump(p, centre, radius, height, .60) for centre, radius, height in cluster_data])

    curly_top = make_mesh('HairRulosTop', *merge(
        volume_shell(curly_extra, .0070, rows=12, columns=40), full_sides(lambda c: scallop_edge(c, .011))),
        [hair], 'Head')
    curly_under = make_mesh('HairRulosUnderHat', *under_hat_band(lambda c: scallop_edge(c)), [hair], 'Head')
    add_part('Human', 'human.hair', 'rulos', {'HumanPartHairTop': [curly_top], 'HumanPartHairUnderHat': [curly_under]},
             colors=[('HumanPartHairTop', 'Human_Hair', 'human.hair_color', None, None),
                     ('HumanPartHairUnderHat', 'Human_Hair', 'human.hair_color', None, None)],
             conditional={'HumanPartHairTop': 'human.headwear'}, shown_only={'HumanPartHairUnderHat': 'human.headwear'},
             fp_head=True)

    # ---- headwear ------------------------------------------------------------------------
    red, red_dark = mats['Human_Nightcap'], mats['Human_NightcapBand']

    def crown(bottom, top_z, distances, columns=28, rib=0.0, jitter=0.0, seed=0.0, apex_z=1.76, dome=((.62, .014), (.30, .022))):
        rows = []
        steps = len(distances)
        for k, d in enumerate(distances):
            t = k / (steps - 1)
            ring = []
            for j in range(columns):
                c = j * COLS / columns
                z0 = bottom(c)
                z = z0 + (top_z - z0) * t
                dd = d + (rib if j % 2 == 0 else -rib * .5) + (jitter * (h(j, k, seed) - .5) * 2 if 0 < k < steps - 1 else 0)
                ring.append(Vector(ag.radial_surface(skull, z, c, dd)))
            rows.append(ring)
        last = rows[-1]
        cx = sum(p.x for p in last) / len(last)
        cy = sum(p.y for p in last) / len(last)
        for s, dz in dome:
            rows.append([Vector((cx + (p.x - cx) * s, cy + (p.y - cy) * s, p.z + dz)) for p in last])
        return rows, Vector((cx, cy, apex_z))

    def solid_cap(rows, apex, inner_rows):
        """Outer dome rows + apex, closed by an inner lip ring (inner_rows: bottom rings)."""
        n = len(rows[0])
        verts = [p for r in inner_rows for p in r] + [p for r in rows for p in r] + [apex]
        allrows = inner_rows + rows
        faces = []
        for r in range(len(allrows) - 1):
            faces += [(r * n + j, r * n + (j + 1) % n, (r + 1) * n + (j + 1) % n, (r + 1) * n + j) for j in range(n)]
        last = (len(allrows) - 1) * n
        faces += [(last + j, last + (j + 1) % n, len(verts) - 1) for j in range(n)]
        faces.append(tuple(reversed(range(n))))  # hidden inner floor under the lip
        return verts, faces

    # gorra roja: six-panel baseball cap, button on top, curved visor over the big eyes.
    cap_bottom = lambda c: 1.634 if min(c % COLS, COLS - c % COLS) < 2.2 else (1.618 if min(c % COLS, COLS - c % COLS) < 4.6 else 1.603)
    rows, apex = crown(cap_bottom, 1.705, (.0085, .0105, .0110, .0105), jitter=.0012, seed=4.4, apex_z=1.752,
                       dome=((.64, .012), (.32, .019)))
    lip = [[Vector(ag.radial_surface(skull, cap_bottom(j * COLS / 28), j * COLS / 28, .001)) for j in range(28)]]
    cap_crown = make_mesh('CapCrown', *solid_cap(rows, apex, lip), [red], 'Head')
    # visor: front arc of the crown's bottom ring, reaching forward and down. r2 (review r1): the flat, thin
    # visor made the cap read as a skull cap from the front; it now tilts VISOR_PITCH down at the centre
    # (~13 deg more than r1) and is VISOR_THICKNESS thick, so PER-04/05's brim shows from the front.
    VISOR_PITCH, VISOR_THICKNESS = math.radians(19.0), .0070
    bottom_ring = rows[0]
    arc = [j for j in range(28) if min(j * COLS / 28, COLS - j * COLS / 28) <= 3.4]
    arc = sorted(arc, key=lambda j: ((j * COLS / 28 + 7) % COLS))
    top_pts, bottom_pts = [], []
    for j in arc:
        c = j * COLS / 28
        d = min(c, COLS - c) / 3.4           # 0 centre .. 1 end
        inner = bottom_ring[j] + Vector((0, 0, .004))
        outward = Vector((inner.x, inner.y + .02, 0)).normalized()
        reach = .112 * (1 - d ** 2.2) + .012
        droop = reach * .75 * math.tan(VISOR_PITCH) + .016 * d ** 1.5
        outer = inner + outward * reach * .35 + Vector((0, -1, 0)) * reach * .75 + Vector((0, 0, -droop))
        top_pts.append((inner, outer))
    verts, faces = [], []
    for inner, outer in top_pts:
        verts += [inner, outer, outer + Vector((0, 0, -VISOR_THICKNESS)), inner + Vector((0, 0, -VISOR_THICKNESS))]
    n = len(top_pts)
    fm = []
    for i in range(n - 1):
        a, b = 4 * i, 4 * (i + 1)
        faces += [(a, a + 1, b + 1, b), (a + 3, b + 3, b + 2, a + 2), (a + 1, a + 2, b + 2, b + 1), (a, b, b + 3, a + 3)]
        fm += [0, 1, 0, 1]
    faces += [(0, 3, 2, 1), (4 * (n - 1), 4 * (n - 1) + 1, 4 * (n - 1) + 2, 4 * (n - 1) + 3)]
    fm += [0, 0]
    visor = make_mesh('CapVisor', verts, faces, [red, red_dark], 'Head', face_materials=fm)
    button = make_mesh('CapButton', *tube_along([(apex.x, apex.y, apex.z - .004), (apex.x, apex.y, apex.z + .007)],
                                                [.011, .009], sides=6), [red_dark], 'Head')
    seams = []
    for j in (0, 5, 9, 14, 19, 23):
        pts = []
        for k, row in enumerate(rows):
            p = row[j]
            radial = Vector((p.x, p.y - .01, 0))
            radial = radial.normalized() if radial.length > 1e-6 else Vector((0, 0, 0))
            pts.append(p + radial * .0012 + Vector((0, 0, .0012 if k >= 3 else 0)))
        pts.append(apex + Vector((0, 0, .0010)))
        seams.append(tube_along(pts, [.0019] * len(pts), sides=4))
    cap_seams = make_mesh('CapSeams', *merge(*seams), [red_dark], 'Head')
    add_part('Human', 'human.headwear', 'gorra', {'HumanPartHeadwear': [cap_crown, visor, button, cap_seams]}, fp_head=True)

    # gorro de lana (PER-04/05 BEANIE): r2 (review r1) charcoal grey knit, a rounded crown without the r1 drop
    # point (it read as an onion or a helmet) and a folded cuff one tone darker, 7 mm prouder than the crown.
    wool, wool_cuff = palette('Human_Beanie', '#4E525D', .95), palette('Human_BeanieCuff', '#34373F', .95)
    beanie_bottom = lambda c: 1.627 if min(c % COLS, COLS - c % COLS) < 2.4 else (1.607 if min(c % COLS, COLS - c % COLS) < 4.8 else 1.590)
    cuff_rows = []
    for k, (dz, d) in enumerate(((0, .004), (0.002, .0175), (.040, .0185), (.045, .0110))):
        cuff_rows.append([Vector(ag.radial_surface(skull, beanie_bottom(j * COLS / 28) + dz, j * COLS / 28,
                                                   d + (.0010 if (j % 2 == 0 and 0 < k < 3) else 0))) for j in range(28)])
    cv, cf = loft(cuff_rows, caps=(False, False))
    cf += [(3 * 28 + j, 3 * 28 + (j + 1) % 28, (j + 1) % 28, j) for j in range(28)]  # closed folded ring
    beanie_cuff = make_mesh('BeanieCuff', cv, cf, [wool_cuff], 'Head')
    rows, apex = crown(lambda c: beanie_bottom(c) + .040, 1.708, (.0100, .0140, .0160, .0165, .0160), rib=.0008,
                       apex_z=1.747, dome=((.86, .016), (.66, .028), (.40, .035)))
    lip = [[Vector(ag.radial_surface(skull, beanie_bottom(j * COLS / 28) + .040, j * COLS / 28, .004)) for j in range(28)]]
    beanie_crown = make_mesh('BeanieCrown', *solid_cap(rows, apex, lip), [wool], 'Head')
    add_part('Human', 'human.headwear', 'gorro-lana', {'HumanPartHeadwear': [beanie_cuff, beanie_crown]}, fp_head=True)

    # ---- glasses -------------------------------------------------------------------------
    frame = palette('Human_GlassesFrame', '#1E1C24', .45)
    lens = palette('Human_GlassesLens', '#1B2231', .80)  # r2: a glossy lens made a hexagonal highlight that read as a pupil
    rx, rz = EYE_RADIUS, EYE_RADIUS * EYE_HEIGHT_FACTOR

    def ring_frame(centre, ax, az, y, thick=.0055, depth=.0065, segments=18):
        pts = [Vector((centre[0] + ax * math.cos(a), y - .004 * abs(math.cos(a)) * 0, centre[2] + az * math.sin(a)))
               for a in (math.tau * i / segments for i in range(segments))]
        rings = []
        for p in pts:
            radial = Vector((p.x - centre[0], 0, p.z - centre[2])).normalized()
            rings.append([p - radial * thick * .5 + Vector((0, -depth * .5, 0)), p + radial * thick * .5 + Vector((0, -depth * .5, 0)),
                          p + radial * thick * .5 + Vector((0, depth * .5, 0)), p - radial * thick * .5 + Vector((0, depth * .5, 0))])
        verts = [q for r in rings for q in r]
        faces = []
        for i in range(segments):
            a, b = 4 * i, 4 * ((i + 1) % segments)
            faces += [(a + k, b + k, b + (k + 1) % 4, a + (k + 1) % 4) for k in range(4)]
        return verts, faces

    def temples(front, side):
        s = 1 if side == 'L' else -1
        pts = [(s * front[0], front[1], front[2]), (s * .178, -.128, 1.553), (s * .205, -.060, 1.558),
               (s * .214, .010, 1.560), (s * .214, .062, 1.552), (s * .204, .098, 1.522)]
        return tube_along(pts, [.0034] * len(pts), sides=5)

    # redondos: big round frames around the huge eyes (the eyes stay visible). r2 (review r1): in r1 the ring cut
    # the globe 28 mm behind its front, so from 3/4 the white poked out of the rim and seemed to pass through it.
    # The ring now stands in front of the globe (12 mm behind its tip, never touching it), 17 % taller and
    # centred 12 mm outward (the two globes almost touch, so it cannot grow toward the nose), with a 26 mm deep
    # rim that hides the globe's edge from +-45 deg.
    yf = -.186
    ax, az, shift = .078, rz * 1.17, .012
    pieces = []
    for side in ('L', 'R'):
        cx, _, cz = EYE_CENTERS[side]
        cx += math.copysign(shift, cx)
        pieces.append(ring_frame((cx, 0, cz), ax, az, yf + .0100, thick=.0060, depth=.0260))
        pieces.append(temples((abs(cx) + ax, yf + .016, cz + .012), side))
    pieces.append(tube_along([(-.014, yf - .004, 1.562), (0, yf - .007, 1.566), (.014, yf - .004, 1.562)], [.003] * 3, sides=5))
    round_glasses = make_mesh('GlassesRound', *merge(*pieces), [frame], 'Head')
    add_part('Human', 'human.glasses', 'redondos', {'HumanPartGlasses': [round_glasses]}, fp_head=True)

    # de sol: dark wayfarer-like lenses with a heavy top bar, in front of the eye lids. r2 (review r1): lenses
    # 7 % wider (they already meet at the nose) and 14 % taller, curved round the globes: each lens is a shell of
    # five rings whose depth follows the outer wrap but always stays 8 mm in front of the faceted eye globe, so
    # the white never pokes through (r1's flat fan let the globe's side through from 3/4) and the outer side still
    # wraps back round the globe from +-45 deg.
    yl = -.214
    ew, eh = EYE_RADIUS * 1.02, EYE_RADIUS * EYE_HEIGHT_FACTOR * 1.02  # faceted globe (vertices on the circumcircle)

    def eye_front(x, z, cx, cz):
        u = 1 - ((x - cx) / ew) ** 2 - ((z - cz) / eh) ** 2
        return EYE_CENTERS['L'][1] - ew * math.sqrt(u) if u > 0 else None

    pieces, fm = [], []
    for side in ('L', 'R'):
        cx, _, cz = EYE_CENTERS[side]
        s = 1 if cx > 0 else -1
        outline = []
        for i in range(16):
            a = math.tau * i / 16
            x = .080 * math.copysign(abs(math.cos(a)) ** .55, math.cos(a))
            z = .084 * math.copysign(abs(math.sin(a)) ** .70, math.sin(a))
            if z > 0:
                z *= 1.04
            if x * s > 0:
                x *= 1.10  # the outer side reaches further round the globe
            if abs(cx + x) < .004:
                x = math.copysign(.004, cx) - cx
            outline.append((cx + x, cz + .004 + z))

        def depth(x, z):
            # the outer side and the lower rim bend back round the globe, never closer than 8 mm to it
            y = yl + .066 * max(0.0, (abs(x) - .056) / .084) ** 1.5 + .020 * max(0.0, (cz - .02 - z) / .06) ** 2
            front_of_eye = eye_front(x, z, cx, cz)
            return y if front_of_eye is None else min(y, front_of_eye - .008)

        centre = (cx, cz + .004)
        rings = []
        for t in (.30, .55, .75, .90, 1.0):
            ring = []
            for x, z in outline:
                px, pz = centre[0] + (x - centre[0]) * t, centre[1] + (z - centre[1]) * t
                ring.append(Vector((px, depth(px, pz), pz)))
            rings.append(ring)
        n = len(outline)
        cen_f = Vector((centre[0], depth(*centre) - .0012, centre[1]))
        front = [p for ring in rings for p in ring] + [cen_f]
        back = [p + Vector((0, .0035, 0)) for p in front]
        verts = front + back
        c, m = len(front) - 1, len(front)
        faces = [(j, (j + 1) % n, c) for j in range(n)]
        for r in range(len(rings) - 1):
            faces += [(r * n + (j + 1) % n, r * n + j, (r + 1) * n + j, (r + 1) * n + (j + 1) % n) for j in range(n)]
        faces += [tuple(i + m for i in reversed(f)) for f in list(faces)]
        last = (len(rings) - 1) * n
        faces += [(last + (j + 1) % n, last + j, last + j + m, last + (j + 1) % n + m) for j in range(n)]
        pieces.append((verts, faces))
        fm += [1] * len(faces)
        edge = rings[-1]
        rim = tube_along([edge[j] + Vector((0, -.001, 0)) for j in range(n)] + [edge[0] + Vector((0, -.001, 0))],
                         [.0042 if edge[j].z > cz + .03 else .0030 for j in range(n)] + [.0042], sides=5)
        pieces.append(rim)
        fm += [0] * len(rim[1])
        t = temples((abs(cx) + .086, yl + .080, cz + .030), side)
        pieces.append(t)
        fm += [0] * len(t[1])
    bridge = tube_along([(-.012, yl - .002, 1.576), (0, yl - .004, 1.580), (.012, yl - .002, 1.576)], [.0035] * 3, sides=5)
    pieces.append(bridge)
    fm += [0] * len(bridge[1])
    sun = make_mesh('GlassesSun', *merge(*pieces), [frame, lens], 'Head', face_materials=fm)
    add_part('Human', 'human.glasses', 'sol', {'HumanPartGlasses': [sun]}, fp_head=True, facial='OccludesExistingFace')

    # ---- top: hoodie ---------------------------------------------------------------------
    shirt_m, shirt_shade = mats['Human_Shirt'], mats['Human_ShirtShade']
    piping = mats['Human_Piping']
    count = 12

    def body_weights(p):
        z = p.z
        wh = 1 - max(0, min(1, (z - .79) / .15))
        wc = max(0, min(1, (z - 1.02) / .13))
        return {k: v for k, v in {'Hips': wh, 'Spine': 1 - wh - wc, 'Chest': wc}.items() if v > 0}

    sections = [(.772, .198, .114), (.905, .200, .117), (1.035, .210, .128), (1.150, .218, .132),
                (1.188, .218, .129), (1.205, .214, .124), (1.2167, .204, .120), (1.2234, .186, .116),
                (1.2326, .160, .113), (1.243, .130, .111), (1.254, .118, .104)]
    rings = [aj.boxy(.186, .102, count, .760), aj.boxy(.198, .114, count, .730)]
    rings += [aj.boxy(a, b, count, z, 3.2 if z < 1.20 else 2.6) for z, a, b in sections]
    verts, faces = loft(rings)
    body = make_mesh('HoodieBody', verts, faces, [shirt_m, shirt_shade], body_weights)
    aj.facet(body, (.002, .0032), lambda co: (0, 0, co.z), lambda co: .80 < co.z < 1.19, 4.7)
    for poly in body.data.polygons:
        z = poly.center.z
        n = poly.normal
        if z < .776 or (abs(n.x) > .82 and .86 < z < 1.13):
            poly.material_index = 1
    # long sleeves with ribbed cuffs, weighted like the bare arm (so the elbow bends with it)
    sleeves = []
    for side, sign in (('L', 1), ('R', -1)):
        def section(x, a, b, dz=0.0, n=2.2):
            z0 = aj.arm_z(x) + dz
            pts = []
            for j in range(count):
                t = 2 * math.pi * (j + .5) / count
                c, s = math.cos(t), math.sin(t)
                pts.append(Vector((sign * x, a * math.copysign(abs(c) ** (2 / n), c), z0 + b * math.copysign(abs(s) ** (2 / n), s))))
            return pts
        stations = [(.190, .086, .042, -.020), (.222, .080, .056, -.010), (.250, .077, .066, -.004), (.300, .074, .072, -.001),
                    (.400, .071, .070, 0), (.500, .069, .068, 0), (.580, .066, .066, 0), (.640, .064, .064, 0),
                    (.648, .062, .062, 0), (.694, .061, .061, 0)]
        rings = [section(*row) for row in stations] + [section(.692, .053, .053), section(.676, .051, .051)]
        if sign < 0:
            rings = [list(reversed(r)) for r in rings]
        verts, faces = loft(rings)
        sleeve = make_mesh('HoodieSleeve.' + side, verts, faces, [shirt_m, shirt_shade],
                           lambda p, side=side: {k: v for k, v in aj.arm_weights(p, side).items()})
        aj.facet(sleeve, (.0015, .0025), lambda co: (co.x, 0, aj.arm_z(abs(co.x))), lambda co: .22 < abs(co.x) < .63, 8.1 + sign)
        for poly in sleeve.data.polygons:
            if abs(poly.center.x) > .644:
                poly.material_index = 1
        sleeves.append(sleeve)
    # hood lying down: a thick roll around the back of the neck and the hood bag on the back
    roll_path = [(.128, -.058, 1.250), (.142, .010, 1.262), (.128, .082, 1.266), (.078, .140, 1.252), (0, .160, 1.240)]
    roll_path = roll_path + [(-x, y, z) for x, y, z in reversed(roll_path[:-1])]
    radii = [.024, .031, .036, .038, .039, .038, .036, .031, .024]
    roll = make_mesh('HoodieHoodRoll', *tube_along(roll_path, radii, sides=8, up_hint=Vector((0, 0, 1)), flat=.85),
                     [shirt_m], 'Chest')
    bag_rings = []
    for z, yc, a, b in ((1.236, .150, .115, .020), (1.180, .152, .102, .019), (1.120, .148, .074, .016),
                        (1.075, .142, .040, .012)):
        bag_rings.append([Vector((a * math.cos(math.tau * (j + .5) / 8), yc + b * math.sin(math.tau * (j + .5) / 8), z))
                          for j in range(8)])
    bv, bf = loft(bag_rings)
    bag = make_mesh('HoodieHoodBag', bv, bf, [shirt_m, shirt_shade], 'Chest')
    for poly in bag.data.polygons:
        if poly.normal.y < -.3:
            poly.material_index = 1
    # kangaroo pocket projected on the front of the body
    bvh = bvh_of(body)
    pocket = []
    outline = [(-.118, .790), (.118, .790), (.098, .905), (-.098, .905)]
    grid = []
    for i in range(5):
        for k in range(3):
            u, v = i / 4, k / 2
            x0 = outline[0][0] + (outline[1][0] - outline[0][0]) * u
            x1 = outline[3][0] + (outline[2][0] - outline[3][0]) * u
            x = x0 + (x1 - x0) * v
            z = outline[0][1] + (outline[3][1] - outline[0][1]) * v
            hit = bvh.ray_cast(Vector((x, -.5, z)), Vector((0, 1, 0)))
            loc, normal = hit[0], hit[1]
            grid.append((loc + normal * .0055, loc - normal * .002))
    verts = [g[0] for g in grid] + [g[1] for g in grid]
    faces, fm = [], []
    for i in range(4):
        for k in range(2):
            a = i * 3 + k
            faces.append((a, a + 3, a + 4, a + 1))
            fm.append(0)
            faces.append((15 + a + 1, 15 + a + 4, 15 + a + 3, 15 + a))
            fm.append(0)
    rim = [0, 3, 6, 9, 12, 13, 14, 11, 8, 5, 2, 1]
    for a, b in zip(rim, rim[1:] + rim[:1]):
        faces.append((a, 15 + a, 15 + b, b))
        fm.append(1)
    pocket_obj = make_mesh('HoodiePocket', verts, faces, [shirt_m, shirt_shade], body_weights, face_materials=fm)
    strings = []
    for s in (1, -1):
        pts = [(s * .036, -.120, 1.238), (s * .040, -.136, 1.205), (s * .043, -.140, 1.160), (s * .046, -.138, 1.118)]
        strings.append(tube_along(pts, [.0042, .0042, .0040, .0036], sides=5))
        strings.append(icosphere((s * .046, -.138, 1.110), .0068, 0, .0, s))
    drawstrings = make_mesh('HoodieStrings', *merge(*strings), [piping], 'Chest')
    add_part('Human', 'human.top', 'buzo',
             {'HumanPartTop': [body, sleeves[0], sleeves[1], roll, bag, pocket_obj, drawstrings]},
             colors=[('HumanPartTop', 'Human_Shirt', 'human.top_color', None, None),
                     ('HumanPartTop', 'Human_ShirtShade', 'human.top_color', 'Human_Shirt', None)])

    # ---- bottom: jeans -------------------------------------------------------------------
    denim = palette('Human_Denim', '#3A5E9E', .92)
    denim_shade = palette('Human_DenimShade', '#2A477D', .92)
    denim_light = palette('Human_DenimLight', '#6C8FC8', .92)
    jeans = []
    for name in ('PajamaLeg.L', 'PajamaLeg.R'):
        leg = copy_object(O[name], 'Jeans' + name[6:])
        leg.data.materials.clear()
        leg.data.materials.append(denim)
        vs = leg.data.vertices
        sides = 12
        for r in range(len(vs) // sides):
            ring = [vs[r * sides + j] for j in range(sides)]
            centre = sum((v.co for v in ring), Vector()) / sides
            z = centre.z
            f = .93 if z < .45 else (1.0 if z > .60 else .93 + .07 * (z - .45) / .15)
            for v in ring:
                d = v.co - centre
                v.co = centre + Vector((d.x * f, d.y * f, d.z))
        aj.facet(leg, (.0012, .0022), lambda co: (co.x * 0 + (.122 if co.x > 0 else -.122), 0, co.z),
                 lambda co: .13 < co.z < .66, 5.9 + (1 if name.endswith('L') else -1))
        jeans.append(leg)
    for name in ('TrouserSeat', 'Waistband'):
        piece = copy_object(O[name], 'Jeans' + name)
        piece.data.materials.clear()
        piece.data.materials.append(denim)
        jeans.append(piece)
    cuffs = []
    for side, sign in (('L', 1), ('R', -1)):
        rings = []
        for z, a, b in ((.070, .089, .077), (.074, .096, .083), (.118, .096, .083), (.124, .087, .075)):
            rings.append([Vector((sign * .122 + a * math.cos(math.tau * (j + .5) / 12), b * math.sin(math.tau * (j + .5) / 12), z))
                          for j in range(12)])
        cuffs.append(loft(rings))
    jean_cuffs = make_mesh('JeansCuffs', *merge(*cuffs), [denim_light],
                           lambda p: {'LowerLeg.L' if p.x > 0 else 'LowerLeg.R': 1.0})
    # back pockets and side seams projected on the jeans
    bvh = bvh_of(*jeans[:2])
    extras, extra_weights = [], []
    for side, sign in (('L', 1), ('R', -1)):
        shape = [(-.036, .742), (.036, .742), (.033, .688), (0, .672), (-.033, .688)]
        front, back = [], []
        for dx, z in shape:
            x = sign * (.084 + dx)
            loc, normal, _, _ = bvh.ray_cast(Vector((x, .5, z)), Vector((0, -1, 0)))
            front.append(loc + normal * .0035)
            back.append(loc - normal * .0015)
        n = len(shape)
        verts = front + back
        faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))] + [(j, j + n, (j + 1) % n + n, (j + 1) % n) for j in range(n)]
        if sign < 0:
            faces = [tuple(reversed(f)) for f in faces]
        extras.append((verts, faces))
    pockets = make_mesh('JeansPockets', *merge(*extras), [denim_shade],
                        lambda p: aj.leg_weights(p, 'L' if p.x > 0 else 'R'))
    add_part('Human', 'human.bottom', 'jean', {'HumanPartBottom': jeans + [jean_cuffs, pockets]})

    # ---- footwear: sneakers --------------------------------------------------------------
    upper_m = palette('Human_SneakerUpper', '#C8322E', .88)
    white = palette('Human_SneakerSole', '#F1EEE6', .80)
    tread = palette('Human_SneakerTread', '#34343C', .95)
    lace = palette('Human_SneakerLace', '#FBF9F4', .80)
    shoes = []
    for side, sign in (('L', 1), ('R', -1)):
        cx = sign * (.125 + .012)
        half = [(0, -.214), (.042, -.208), (.070, -.190), (.086, -.160), (.090, -.118), (.086, -.074),
                (.078, -.030), (.074, .008), (.070, .040), (.056, .062), (.030, .074), (0, .077)]
        outline = half + [(-x, y) for x, y in reversed(half[1:-1])]
        m = len(outline)
        sole_rings = []
        for z, k in ((0.0, .97), (.006, 1.0), (.024, 1.0), (.030, .985)):
            sole_rings.append([Vector((cx + x * k, (y + .07) * k - .07, z)) for x, y in outline])
        sv, sf = loft(sole_rings)
        sole_fm = [1] + [1 if r == 0 else 0 for r in range(3) for _ in range(m)] + [0]
        sole = make_mesh('SneakerSole.' + side, sv, sf, [white, tread], 'Foot.' + side, face_materials=sole_fm)
        # upper: arches from sole edge to sole edge (toe cap white, collar opening padded)
        profile = [(-.206, .044), (-.190, .058), (-.165, .068), (-.132, .078), (-.098, .088), (-.062, .100),
                   (-.028, .112), (.004, .120), (.034, .124), (.056, .118), (.070, .096)]
        arch = 9
        verts, rows = [], []
        for y, top in profile:
            hw = max(abs(x) for x, yy in half if abs(yy - y) < .03) if any(abs(yy - y) < .03 for _, yy in half) else .06
            row = []
            for k in range(arch):
                a = math.pi * k / (arch - 1)
                c, s = math.cos(a), math.sin(a)
                x = hw * .99 * (1 + .05 * math.sin(min(math.pi, 2.2 * s))) * c
                z = .028 + (top - .028) * s ** .85
                row.append(len(verts))
                verts.append(Vector((cx + x, y, z)))
            rows.append(row)
        toe = len(verts)
        verts.append(Vector((cx, profile[0][0] - .006, .028)))
        heel = len(verts)
        verts.append(Vector((cx, profile[-1][0] + .006, .030)))
        faces, fm = [], []
        for r in range(len(rows) - 1):
            for k in range(arch - 1):
                faces.append((rows[r][k], rows[r][k + 1], rows[r + 1][k + 1], rows[r + 1][k]))
                fm.append(1 if r < 2 else 0)
        faces += [(rows[0][k + 1], rows[0][k], toe) for k in range(arch - 1)]
        fm += [1] * (arch - 1)
        faces += [(rows[-1][k], rows[-1][k + 1], heel) for k in range(arch - 1)]
        fm += [0] * (arch - 1)
        faces.append(tuple([toe] + [rows[r][0] for r in range(len(rows))] + [heel] +
                           [rows[len(rows) - 1 - r][arch - 1] for r in range(len(rows))]))
        fm.append(1)
        upper = make_mesh('SneakerUpper.' + side, verts, faces, [upper_m, white], 'Foot.' + side, face_materials=fm)
        # laces across the instep and a white side stripe
        bars = []
        for k, y in enumerate((-.118, -.094, -.070, -.046)):
            top = next(t for yy, t in profile if yy >= y - .02)
            zc = .028 + (top - .028) + .004
            bars.append(tube_along([(cx - .024, y, zc - .006), (cx, y + .002, zc), (cx + .024, y, zc - .006)], [.0040] * 3, sides=4))
        stripe = []
        for s in (1, -1):
            pts = []
            for y in (-.150, -.105, -.060, -.018, .018):
                hw = max(abs(x) for x, yy in half if abs(yy - y) < .03)
                pts.append((cx + s * (hw * 1.045 + .002), y, .050 + .010 * (y + .15) / .17))
            stripe.append(tube_along(pts, [.0055] * len(pts), sides=4, flat=.45, up_hint=Vector((s, 0, 0))))
        details = make_mesh('SneakerDetails.' + side, *merge(*(bars + stripe)), [lace], 'Foot.' + side)
        shoes += [sole, upper, details]
    add_part('Human', 'human.footwear', 'zapatillas', {'HumanPartFeet': shoes})

    # ---- back: backpack ------------------------------------------------------------------
    pack = palette('Human_Backpack', '#3F7F4C', .90)
    trim = palette('Human_BackpackTrim', '#28442F', .92)
    accent = palette('Human_BackpackAccent', '#E3B341', .70)
    sections = [(.860, .118, .046, .196), (.880, .140, .060, .198), (1.000, .150, .064, .200), (1.110, .148, .062, .199),
                (1.160, .138, .056, .196), (1.182, .112, .044, .193), (1.192, .070, .028, .190)]
    rings = [aj.boxy(a, b, 16, z, 3.0, yc) for z, a, b, yc in sections]
    body_v, body_f = loft(rings)
    pack_body = make_mesh('BackpackBody', body_v, body_f, [pack], 'Chest')
    aj.facet(pack_body, (.0015, .003), lambda co: (0, .198, co.z), lambda co: .87 < co.z < 1.17, 6.6)
    pocket_rings = [aj.boxy(a, b, 12, z, 3.2, yc) for z, a, b, yc in
                    ((.880, .090, .012, .262), (.890, .104, .022, .266), (.985, .104, .022, .266), (1.000, .092, .014, .262))]
    pv, pf = loft(pocket_rings)
    pack_pocket = make_mesh('BackpackPocket', pv, pf, [trim], 'Chest')
    flap_rings = [aj.boxy(a, b, 16, z, 3.0, yc) for z, a, b, yc in
                  ((1.100, .150, .066, .201), (1.170, .144, .062, .199), (1.190, .118, .050, .196), (1.199, .074, .032, .193))]
    fv, ff = loft(flap_rings)
    pack_flap = make_mesh('BackpackFlap', fv, ff, [trim], 'Chest')
    straps = []
    # r2 (review r1): the shoulder straps no longer stop mid-chest; each runs down the chest, turns under the
    # arm along the side of the ribs and ends on the pack's lower corner (PER-01 human 03).
    lower = []
    for s in (1, -1):
        pts = [(s * .098, .150, 1.150), (s * .112, .110, 1.236), (s * .120, .050, 1.268), (s * .124, -.030, 1.262),
               (s * .122, -.098, 1.222), (s * .120, -.142, 1.140), (s * .118, -.148, 1.060), (s * .122, -.144, 1.005)]
        straps.append(tube_along(pts, [.026] * len(pts), sides=4, flat=.20, up_hint=Vector((s * .2, 0, 1))))
        under = [(s * .122, -.144, 1.005), (s * .150, -.126, .972), (s * .184, -.082, .952), (s * .205, -.010, .942),
                 (s * .204, .064, .928), (s * .182, .128, .906), (s * .142, .176, .884)]
        lower.append(tube_along(under, [.024] * len(under), sides=4, flat=.22, up_hint=Vector((s, 0, .15))))
    strap_obj = make_mesh('BackpackStraps', *merge(*straps), [trim], 'Chest')
    lower_obj = make_mesh('BackpackLowerStraps', *merge(*lower), [trim], body_weights)
    buckles = []
    for s in (1, -1):
        buckles.append(tube_along([(s * .119, -.146, 1.100), (s * .119, -.147, 1.080)], [.014, .014], sides=4, flat=.4,
                                  up_hint=Vector((0, -1, 0))))
    buckles.append(tube_along([(-.070, .268, .990), (.070, .268, .990)], [.004, .004], sides=4))
    buckles.append(tube_along([(0, .262, 1.194), (0, .220, 1.222), (0, .178, 1.214)], [.007] * 3, sides=5))
    pack_accent = make_mesh('BackpackAccent', *merge(*buckles), [accent], 'Chest')
    add_part('Human', 'human.back', 'mochila',
             {'HumanPartBack': [pack_body, pack_pocket, pack_flap, strap_obj, lower_obj, pack_accent]})


# ============================================================================= MOSQUITO
def build_mosquito_parts():
    import author_mosquito_geometry as mg
    O = bpy.data.objects
    mats = bpy.data.materials
    shell_colors = []
    base_names = [o.name for o in bpy.context.scene.objects if o.type == 'MESH' and o.name != 'Proboscis'
                  and not o.name.startswith(('WingMembrane.', 'WingLeadingEdge.', 'WingVein.'))]
    shell = ('Mosquito_Shell', 'Mosquito_ShellShade', 'Mosquito_ShellDark', 'Mosquito_ShellDeep', 'Mosquito_Abdomen')

    def shell_bindings(renderer):
        return [(renderer, name, 'mosquito.color', None if name == 'Mosquito_Shell' else 'Mosquito_Shell', None)
                for name in shell]

    add_part('Mosquito', 'mosquito.base', 'base', {'MosquitoPartBody': [O[n] for n in base_names]},
             colors=shell_bindings('MosquitoPartBody'),
             note='Body, legs and the bone-driven eyes (Pupil/Lid bones) with the same geometry as the host.')

    # ---- wings -----------------------------------------------------------------------------
    wing, vein, edge = mats['Mosquito_Wing'], mats['Mosquito_WingVein'], mats['Mosquito_WingEdge']
    wing_colors = [('MosquitoPartWings', 'Mosquito_Wing', 'mosquito.wing_color', None, .50)]
    add_part('Mosquito', 'mosquito.wings', 'clasicas',
             {'MosquitoPartWings': [O['WingMembrane.L'], O['WingMembrane.R']],
              'MosquitoPartWingVeins': [O[n] for n in (f'{p}{s}' for p in ('WingLeadingEdge.', 'WingVein.Edge.', 'WingVein.',
                                                                            'WingVein.Branch.', 'WingVein.Branch2.', 'WingVein.Branch3.')
                                                       for s in ('L', 'R'))]},
             colors=wing_colors)
    def leaf_wing(tag, length, lead, trail, raise_=(.012, .006), stations=10):
        """A leaf membrane in the same stance frame as the production wing (author_mosquito_geometry._wing_frame:
        the idle aim, and the perched V of the animation, rotate these blades exactly like the clasicas), drawn
        from span stations: leading edge lead(f) (<= 0), trailing edge trail(f) (>= 0), f = u / length, a raised
        alternating midrib and real thickness. Veins: the leading edge and the midrib (thin, same materials)."""
        membranes, veins = [], []
        for side, sign in (('L', 1), ('R', -1)):
            root = Vector((sign * .017, 0, .082))
            span, chord, normal = (Vector(v) for v in mg._wing_frame(sign))
            # (u, v, raise) samples: leading edge, midrib, trailing edge per station; the tip is one point
            rows = []
            for k in range(stations + 1):
                f = k / stations
                u = length * f
                if k in (0, stations):
                    rows.append([(u, 0.0, 0.0)])  # the root (inside the thorax) and the tip are single points
                    continue
                vl, vt = lead(f), trail(f)
                mid = vl + (vt - vl) * .40
                rows.append([(u, vl, 0.0), (u, mid, raise_[k % 2] if 0 < k else 0.0), (u, vt, 0.0)])
            pts, faces = [], []
            index = []
            for row in rows:
                index.append([])
                for u, v, r in row:
                    index[-1].append(len(pts))
                    pts.append(root + (span * u + chord * v) * mg.WING_SCALE + normal * r * mg.WING_SCALE * .5)
            root_i, b = index[0][0], index[1]
            faces += [(root_i, b[0], b[1]), (root_i, b[1], b[2])]
            for k in range(1, stations - 1):
                a, b = index[k], index[k + 1]
                faces += [(a[0], b[0], b[1]), (a[0], b[1], a[1]), (a[1], b[1], b[2]), (a[1], b[2], a[2])]
            a, tip = index[stations - 1], index[stations][0]
            faces += [(a[0], tip, a[1]), (a[1], tip, a[2])]
            count = len(pts)
            thick = [p - normal * mg.WING_THICKNESS for p in pts]
            verts = pts + thick
            all_faces = faces + [tuple(i + count for i in reversed(f)) for f in faces]
            outline = [index[0][0]] + [row[0] for row in index[1:-1]] + [index[-1][0]] + [row[2] for row in reversed(index[1:-1])]
            all_faces += [(outline[(i + 1) % len(outline)], outline[i], outline[i] + count, outline[(i + 1) % len(outline)] + count)
                          for i in range(len(outline))]
            if sign < 0:
                all_faces = [tuple(reversed(f)) for f in all_faces]
            membranes.append(make_mesh(f'Wing{tag}Membrane.{side}', verts, all_faces, [wing], 'Wing.' + side))
            lead_pts = [verts[index[0][0]]] + [verts[row[0]] for row in index[1:-1]] + [verts[index[-1][0]]]
            mid_pts = [verts[row[1]] for row in index[1:-1]]
            veins.append(bc.strip(f'Wing{tag}LeadingEdge.{side}', lead_pts, .0004, edge, 'Wing.' + side))
            veins.append(bc.strip(f'Wing{tag}Vein.{side}', [verts[index[0][0]]] + mid_pts + [verts[index[-1][0]]], .00050,
                                  vein, 'Wing.' + side))
        return membranes, veins

    # redondas (PER-06/07 ROUNDED): r2 (review r1) a short broad oval, leaf proportion ~0.45 (width / length),
    # widest past the middle and a round tip; the r1 blade was too close to clasicas once seen in the idle.
    m, v = leaf_wing('Round', .206, lambda f: -.014 * math.sin(math.pi * f ** .85) ** .9,
                     lambda f: .079 * math.sin(math.pi * min(1.0, f ** 1.05)) ** .55 * (1 - .10 * f))
    add_part('Mosquito', 'mosquito.wings', 'redondas', {'MosquitoPartWings': m, 'MosquitoPartWingVeins': v},
             colors=[('MosquitoPartWings', 'Mosquito_Wing', 'mosquito.wing_color', None, .50)])
    # largas (PER-06/07 LONG): a long lance leaf, proportion ~0.23 (r1 was a 0.17 needle), widest at a third.
    m, v = leaf_wing('Long', .300, lambda f: -.010 * math.sin(math.pi * f ** .8) ** .8,
                     lambda f: .058 * math.sin(math.pi * f ** .62) ** .95)
    add_part('Mosquito', 'mosquito.wings', 'largas', {'MosquitoPartWings': m, 'MosquitoPartWingVeins': v},
             colors=[('MosquitoPartWings', 'Mosquito_Wing', 'mosquito.wing_color', None, .50)])

    # ---- proboscis ---------------------------------------------------------------------------
    shell_m, dark_m, shade_m = mats['Mosquito_Shell'], mats['Mosquito_ShellDark'], mats['Mosquito_ShellShade']
    add_part('Mosquito', 'mosquito.proboscis', 'estandar', {'MosquitoPartProboscis': [O['Proboscis']]},
             colors=shell_bindings('MosquitoPartProboscis')[:3])
    base_point = Vector(mg.PROBOSCIS_VISUAL_BASE)
    mouth = Vector(mg.MOUTH)

    def proboscis(tag, end, base_radius, tip_radius, control=None, stations=(-.08, 0, .15, .35, .55, .75, .88, 1.0)):
        def at(t):
            if control is None:
                return base_point.lerp(end, t)
            c = Vector(control)
            t = max(t, 0)
            return (1 - t) ** 2 * base_point + 2 * (1 - t) * t * c + t * t * end
        pts = [at(t) if t >= 0 else base_point + (base_point - at(.08)) for t in stations]
        radii = [base_radius + (tip_radius - base_radius) * max(0.0, t) for t in stations]
        verts, faces = tube_along(pts, radii, sides=6, up_hint=Vector((0, 0, 1)))
        fm = []
        n = 6
        # first cap, rows, last cap
        fm.append(0)
        for r in range(len(pts) - 1):
            fm += [1 if stations[r] >= .75 else 0] * n
        fm.append(1)
        obj = make_mesh('Proboscis' + tag, verts, faces, [shell_m, dark_m, shade_m], 'Proboscis', face_materials=fm)
        obj.data.update()
        for poly in obj.data.polygons:
            if poly.material_index == 0 and poly.normal.z < mg.NORMAL_SHADE_Z:
                poly.material_index = 2
        return obj

    # r2 (review r1): corta and larga end off Socket.Mouth, so the bite anchor ProboscisTip follows their real
    # tip (a per-option anchor offset applied by the assembler): the tip touches the skin in every bite.
    short_tip, long_tip = base_point.lerp(mouth, .66), base_point.lerp(mouth, 1.30)
    add_part('Mosquito', 'mosquito.proboscis', 'corta',
             {'MosquitoPartProboscis': [proboscis('Short', short_tip, .0118, .0016)]},
             colors=shell_bindings('MosquitoPartProboscis')[:3], anchors=[('ProboscisTip', 'Socket.Mouth', short_tip)],
             note='Tip 34% shorter than Socket.Mouth; ProboscisTip follows it.')
    add_part('Mosquito', 'mosquito.proboscis', 'larga',
             {'MosquitoPartProboscis': [proboscis('Long', long_tip, .0104, .0006)]},
             colors=shell_bindings('MosquitoPartProboscis')[:3], anchors=[('ProboscisTip', 'Socket.Mouth', long_tip)],
             note='Tip 30% past Socket.Mouth; ProboscisTip follows it.')
    add_part('Mosquito', 'mosquito.proboscis', 'curva',
             {'MosquitoPartProboscis': [proboscis('Curved', mouth, .0115, .0009, control=(0, -.176, .104))]},
             colors=shell_bindings('MosquitoPartProboscis')[:3],
             note='Hooked arc that still ends on Socket.Mouth.')

    # ---- markings ----------------------------------------------------------------------------
    accent = palette('Mosquito_Accent', '#F2EBDD', .88)
    shape = [(t, mg._abdomen_ring(t, s)) for t, s in mg.ABDOMEN_RINGS]

    def surface_ring(t):
        for (t0, r0), nxt in zip(shape, shape[1:] + [(1.0, None)]):
            t1 = nxt[0]
            if t0 - 1e-9 <= t <= t1 + 1e-9:
                f = (t - t0) / (t1 - t0)
                target = nxt[1] if nxt[1] is not None else [mg.ABDOMEN_TIP] * mg.ABDOMEN_SIDES
                return [Vector(p).lerp(Vector(q), f) for p, q in zip(r0, target)]
        raise ValueError(t)

    def abdomen_weights(p):
        t = max(0, min(1, (p.y - .088) / .045))
        t = t * t * (3 - 2 * t)
        return {k: v for k, v in {'Abdomen01': 1 - t, 'Abdomen02': t}.items() if v > 0}

    def lifted(points, t, lift):
        centre = Vector(mg._abdomen_point(t))
        return [p + (p - centre).normalized() * lift for p in points]

    breaks = sorted({t for t, _ in mg.ABDOMEN_RINGS})
    # anillos: four cream rings in the middle of the dark-banded rows
    bands = []
    for t0, t1 in ((.335, .372), (.505, .545), (.680, .720), (.848, .876)):
        ts = [t0] + [b for b in breaks if t0 < b < t1] + [t1]
        outer = [lifted(surface_ring(t), t, .0016) for t in ts]
        inner = [lifted(surface_ring(t), t, -.0010) for t in ts]
        n = mg.ABDOMEN_SIDES
        verts = [p for r in outer for p in r] + [p for r in inner for p in r]
        base = len(outer) * n
        faces = []
        for r in range(len(ts) - 1):
            faces += [(r * n + j, r * n + (j + 1) % n, (r + 1) * n + (j + 1) % n, (r + 1) * n + j) for j in range(n)]
            faces += [(base + r * n + (j + 1) % n, base + r * n + j, base + (r + 1) * n + j, base + (r + 1) * n + (j + 1) % n)
                      for j in range(n)]
        last = len(ts) - 1
        faces += [(base + j, base + (j + 1) % n, (j + 1) % n, j) for j in range(n)]
        faces += [(last * n + j, last * n + (j + 1) % n, base + last * n + (j + 1) % n, base + last * n + j) for j in range(n)]
        bands.append((verts, faces))
    rings_obj = make_mesh('MarkingRings', *merge(*bands), [accent], abdomen_weights)
    add_part('Mosquito', 'mosquito.markings', 'anillos', {'MosquitoPartMarkings': [rings_obj]},
             colors=[('MosquitoPartMarkings', 'Mosquito_Accent', 'mosquito.accent_color', None, None)])

    # rayas: two racing stripes along the flat top facet of the abdomen
    stripes = []
    top_a, top_b = 1, 2  # the flat top facet lies between columns 1 and 2 (half-side offset)
    for u0, u1 in ((.14, .36), (.64, .86)):
        ts = sorted(set([.115 + k * .04 for k in range(22) if .115 + k * .04 < .97] + [b for b in breaks if .115 < b < .97] + [.97]))
        outer, inner = [], []
        for t in ts:
            ring = surface_ring(t)
            a, b = ring[top_a], ring[top_b]
            centre = Vector(mg._abdomen_point(t))
            normal = ((a + b) * .5 - centre).normalized()
            row = [a.lerp(b, u0), a.lerp(b, u1)]
            outer.append([p + normal * .0015 for p in row])
            inner.append([p - normal * .0010 for p in row])
        verts, faces = [], []
        for r in range(len(ts)):
            verts += outer[r] + inner[r]
        for r in range(len(ts) - 1):
            a, b = 4 * r, 4 * (r + 1)
            faces += [(a, a + 1, b + 1, b), (a + 3, a + 2, b + 2, b + 3), (a + 2, a, b, b + 2), (a + 1, a + 3, b + 3, b + 1)]
        last = 4 * (len(ts) - 1)
        faces += [(0, 2, 3, 1), (last, last + 1, last + 3, last + 2)]
        stripes.append((verts, faces))
    # r2 (review r1): the r1 thorax chevrons were thin cream '^' wires with z-fighting on the crest facets; the
    # racing stripes stay on the abdomen only (PER-07 RACING).
    thorax = O['Thorax']
    tbvh = bvh_of(thorax)
    stripes_obj = make_mesh('MarkingStripes', *merge(*stripes), [accent], abdomen_weights)
    add_part('Mosquito', 'mosquito.markings', 'rayas', {'MosquitoPartMarkings': [stripes_obj]},
             colors=[('MosquitoPartMarkings', 'Mosquito_Accent', 'mosquito.accent_color', None, None)])

    # lunares: ladybug-like spots on the abdomen facets and the thorax
    spots = []
    rows = [(.15, .26), (.26, .42), (.42, .60), (.60, .78)]
    picks = [(1, 1), (1, 3), (2, 0), (2, 2), (2, 7), (2, 4), (3, 1), (3, 6), (3, 3), (0, 2)]
    for r, j in picks:
        t0, t1 = rows[r]
        tm = t0 + (t1 - t0) * (.62 if r else .55)
        ring0, ring1 = surface_ring(t0 + (t1 - t0) * .38), surface_ring(t0 + (t1 - t0) * .86)
        a0, b0 = ring0[j], ring0[(j + 1) % mg.ABDOMEN_SIDES]
        a1, b1 = ring1[j], ring1[(j + 1) % mg.ABDOMEN_SIDES]
        centre = (a0 + b0 + a1 + b1) / 4
        eu = (b0 - a0).normalized()
        ev = ((a1 + b1) / 2 - (a0 + b0) / 2)
        ev = (ev - eu * ev.dot(eu)).normalized()
        normal = eu.cross(ev)
        axis = Vector(mg._abdomen_point(tm))
        if normal.dot(centre - axis) < 0:
            normal = -normal
        radius = min((b0 - a0).length, (b1 - a1).length, ((a1 + b1) / 2 - (a0 + b0) / 2).length) * .30
        n = 6
        spin = h(r, j, 4.4) * math.tau
        front = [centre + normal * .0015 + (eu * math.cos(spin + math.tau * q / n) + ev * math.sin(spin + math.tau * q / n)) *
                 radius * (.85 + .3 * h(r, j, q)) for q in range(n)]
        back = [p - normal * .0028 for p in front]
        verts = front + back
        faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))] + [((q + 1) % n, q, q + n, (q + 1) % n + n) for q in range(n)]
        spots.append((verts, faces))
    thorax_spots = []
    for k, (x, y) in enumerate(((.030, -.005), (-.030, -.005), (0, .040), (.045, .045), (-.045, .045))):
        loc, normal, _, _ = tbvh.ray_cast(Vector((x, y, .40)), Vector((0, 0, -1)))
        eu = normal.cross(Vector((0, 1, 0))).normalized()
        ev = normal.cross(eu)
        n = 6
        radius = .0105 if k < 2 else .0090
        front = [loc + normal * .0016 + (eu * math.cos(math.tau * q / n + k) + ev * math.sin(math.tau * q / n + k)) * radius
                 for q in range(n)]
        back = [p - normal * .0030 for p in front]
        faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))] + [((q + 1) % n, q, q + n, (q + 1) % n + n) for q in range(n)]
        thorax_spots.append((front + back, faces))
    spots_obj = make_mesh('MarkingSpots', *merge(*spots), [accent], abdomen_weights)
    thorax_obj = make_mesh('MarkingThoraxSpots', *merge(*thorax_spots), [accent], 'Thorax')
    add_part('Mosquito', 'mosquito.markings', 'lunares', {'MosquitoPartMarkings': [spots_obj, thorax_obj]},
             colors=[('MosquitoPartMarkings', 'Mosquito_Accent', 'mosquito.accent_color', None, None)])

    # ---- accessories -------------------------------------------------------------------------
    leaf_m = palette('Mosquito_Leaf', '#6DBE45', .80)  # r2: brighter, the front view reads green, not a dark helmet
    leaf_vein = palette('Mosquito_LeafVein', '#3C7A2A', .85)
    petal_m = palette('Mosquito_Petal', '#F6F3EA', .75)
    # r2 (review r1): the undersides face the floor and only get ground ambient (a black 'helmet' under the leaf
    # from the front, grey-brown petals from behind): they get their own, lighter albedo.
    leaf_under = palette('Mosquito_LeafUnder', '#A6D77A', .85)
    petal_under = palette('Mosquito_PetalUnder', '#FFF7EC', .80)
    centre_m = palette('Mosquito_FlowerCentre', '#F2B92E', .80)
    # hoja: a leaf worn as a little hat across the tops of the eye cups, stem curling up behind
    LEAF = 1.55
    centre = Vector((0, -.046, .196))
    along = Vector((0, -1, .55)).normalized()
    across = Vector((1, 0, 0))
    up = along.cross(across).normalized() * -1
    stations = [LEAF * (-.050 + .106 * i / 8) for i in range(9)]
    top = []
    for i, u in enumerate(stations):
        f = i / 8
        w = max(.0025, LEAF * .029 * math.sin(math.pi * f) ** .75)
        lift = .006 * math.cos((f - .5) * math.pi)
        for v, rise in ((-w, -.014 * (w / (LEAF * .029)) ** 1.6), (0.0, .0075), (w, -.014 * (w / (LEAF * .029)) ** 1.6)):
            top.append(centre + along * u + across * v + up * (lift + rise))
    bottom = [p - up * .0022 for p in top]
    verts = top + bottom
    m2 = len(top)
    faces = []
    for i in range(8):
        a, b = 3 * i, 3 * (i + 1)
        faces += [(a, b, b + 1, a + 1), (a + 1, b + 1, b + 2, a + 2)]
        faces += [(m2 + a + 1, m2 + b + 1, m2 + b, m2 + a), (m2 + a + 2, m2 + b + 2, m2 + b + 1, m2 + a + 1)]
        faces += [(a, a + m2, b + m2, b), (a + 2, b + 2, b + 2 + m2, a + 2 + m2)]
    last = 3 * 8
    faces += [(0, 1, 2, 2 + m2, 1 + m2, m2), (last + 2, last + 1, last, last + m2, last + 1 + m2, last + 2 + m2)]
    # r2: tipped 6 deg further forward so the top face shows from the front; bottom faces use the underside tone
    leaf_fm = [1 if all(i >= m2 for i in f) else 0 for f in faces]
    leaf = make_mesh('AccessoryLeaf', verts, faces, [leaf_m, leaf_under], 'Head', face_materials=leaf_fm)
    stem_pts = [centre + along * (LEAF * -.046) + up * .004, centre + along * (LEAF * -.056) + up * .016,
                centre + along * (LEAF * -.058) + up * .032, centre + along * (LEAF * -.049) + up * .043]
    mid = [centre + along * (LEAF * u) + up * (.0090 + .006 * math.cos((u + .05) / .106 * math.pi - math.pi / 2))
           for u in (-.046, -.010, .020, .050)]
    stem = make_mesh('AccessoryLeafStem', *merge(tube_along(stem_pts, [.0032, .0030, .0026, .0022], sides=5),
                                                tube_along(mid, [.0016] * 4, sides=4)), [leaf_vein], 'Head')
    add_part('Mosquito', 'mosquito.accessory', 'hoja', {'MosquitoPartAccessory': [leaf, stem]})
    # flor: a daisy on top of the head, tilted toward the front
    FLOWER = 1.45
    fc = Vector((.0, -.050, .190))
    fn = Vector((0, -.45, 1)).normalized()
    fa = Vector((1, 0, 0))
    fb = fn.cross(fa).normalized()
    petals = []
    for k in range(7):
        ang = math.tau * k / 7 + .2
        d = fa * math.cos(ang) + fb * math.sin(ang)
        w = fn.cross(d).normalized()
        base = fc + d * .007 * FLOWER
        tip = fc + d * .030 * FLOWER + fn * .005
        mid_l = fc + d * .019 * FLOWER + w * .0072 * FLOWER + fn * .004
        mid_r = fc + d * .019 * FLOWER - w * .0072 * FLOWER + fn * .004
        top_v = [base, mid_l, tip, mid_r]
        verts = [p + fn * .0012 for p in top_v] + [p - fn * .0012 for p in top_v]
        faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        petals.append((verts, faces))
    petal_verts, petal_faces = merge(*petals)
    petal_fm = [1 if all(i % 8 >= 4 for i in f) else 0 for f in petal_faces]
    petals_obj = make_mesh('AccessoryPetals', petal_verts, petal_faces, [petal_m, petal_under], 'Head', face_materials=petal_fm)
    disc = icosphere(fc + fn * .004, .0085 * FLOWER, 1, .06, 7.7, scale=(1, 1, .55))
    stemf = tube_along([fc - fn * .002, fc - fn * .014], [.0026, .0026], sides=5)
    centre_obj = make_mesh('AccessoryFlowerCentre', *disc, [centre_m], 'Head')
    stem_obj = make_mesh('AccessoryFlowerStem', *stemf, [leaf_vein], 'Head')
    add_part('Mosquito', 'mosquito.accessory', 'flor', {'MosquitoPartAccessory': [petals_obj, centre_obj, stem_obj]})


# ============================================================================= export
def rig_signature(rig):
    rows = []
    for b in sorted(rig.data.bones, key=lambda b: b.name):
        rows.append('%s|%s|%s|%s' % (b.name, b.parent.name if b.parent else '',
                                     ','.join('%.5f' % v for v in b.head_local), ','.join('%.5f' % v for v in b.tail_local)))
    return hashlib.sha256('\n'.join(rows).encode('utf8')).hexdigest()


def join_copies(objects, name, rig):
    copies = [copy_object(o, name + '__tmp%d' % i) for i, o in enumerate(objects)]
    bpy.ops.object.select_all(action='DESELECT')
    for c in copies:
        c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    if len(copies) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    for mod in list(obj.modifiers):
        obj.modifiers.remove(mod)
    mod = obj.modifiers.new('Character skin', 'ARMATURE')
    mod.object = rig
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    return obj


def audit_mesh(obj):
    obj.data.calc_loop_triangles()
    errors = []
    unweighted = sum(1 for v in obj.data.vertices if not v.groups)
    bad = sum(1 for v in obj.data.vertices if abs(sum(g.weight for g in v.groups) - 1) > 1e-4)
    many = sum(1 for v in obj.data.vertices if len([g for g in v.groups if g.weight > 0]) > 4)
    degenerate = sum(1 for t in obj.data.loop_triangles if t.area < 1e-12)
    if unweighted: errors.append('unweighted_vertices=%d' % unweighted)
    if bad: errors.append('bad_weight_sums=%d' % bad)
    if many: errors.append('more_than_4_influences=%d' % many)
    if degenerate: errors.append('degenerate_triangles=%d' % degenerate)
    return {'triangles': len(obj.data.loop_triangles), 'vertices': len(obj.data.vertices),
            'materials': [m.name for m in obj.data.materials],
            'bones': sorted({obj.vertex_groups[g.group].name for v in obj.data.vertices for g in v.groups}),
            'errors': errors}


def export(species, character):
    rig = character.rig
    folder = OUT / species.lower()
    folder.mkdir(parents=True, exist_ok=True)
    for stale in folder.glob('*.fbx'):
        stale.unlink()
    records = []
    preview = bpy.data.collections.new('ModularParts')
    bpy.context.scene.collection.children.link(preview)
    for part in PARTS:
        renderers = []
        for renderer, objects in part.renderers.items():
            obj = join_copies(objects, renderer, rig)
            if species == 'Mosquito' and part.slot == 'mosquito.base':
                from author_mosquito_face import light_eye_interiors
                obj.name = 'MosquitoSkin'
                light_eye_interiors(obj)
                obj.name = renderer
            renderers.append(obj)
        path = folder / f'{part.slot}__{part.option}.fbx'
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        for obj in renderers:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'ARMATURE', 'MESH'}, global_scale=1,
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                                 use_mesh_modifiers=True, add_leaf_bones=False, use_armature_deform_only=False,
                                 bake_anim=False, path_mode='AUTO', mesh_smooth_type='FACE')
        audits = {obj.name: audit_mesh(obj) for obj in renderers}
        errors = [f'{name}: {e}' for name, a in audits.items() for e in a['errors']]
        if errors:
            raise RuntimeError(f'{part.slot}/{part.option}: {errors}')
        records.append({
            'role': species, 'slot': part.slot, 'option': part.option, 'fbx': path.name,
            'renderers': [{'name': obj.name, **audits[obj.name],
                           'first_person_head': part.fp_head,
                           'hidden_when_slot_selected': part.conditional.get(obj.name, ''),
                           'shown_when_slot_selected': part.shown_only.get(obj.name, '')} for obj in renderers],
            'anchor_offsets': [{'anchor': a, 'bone': b, 'point_blender_m': [round(v, 6) for v in point]}
                               for a, b, point in part.anchors],
            'colors': [{'renderer': r, 'material': m, 'slot': s, 'shade_reference': ref or '', 'alpha': a if a is not None else -1}
                       for r, m, s, ref, a in part.colors if m in audits.get(r, {}).get('materials', [])],
            'facial_impact': part.facial, 'note': part.note,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        for obj in renderers:
            for c in list(obj.users_collection):
                c.objects.unlink(obj)
            preview.objects.link(obj)
            obj['lms_slot'] = part.slot
            obj['lms_option'] = part.option
            obj['lms_renderer'] = obj.name
            # free the renderer name for the next option (FBX object names are the Unity renderer names)
            obj.name = f'{part.slot}|{part.option}|{obj.name}'
            obj.hide_render = True
            obj.hide_viewport = True
    # palette of every material any part uses (production ones included, for reference)
    used = sorted({m for r in records for x in r['renderers'] for m in x['materials']})
    materials = []
    for name in used:
        m = bpy.data.materials[name]
        materials.append({'name': name, 'color': dict(zip(('r', 'g', 'b', 'a'), m.diffuse_color)),
                          'roughness': bc.principled(m).inputs['Roughness'].default_value,
                          'new': name in NEW_PALETTE})
    sources = {}
    for name in ('author_modular_parts.py', 'build_characters.py', 'author_human_geometry.py', 'author_human_joints.py',
                 'author_human_facial.py', 'author_mosquito_geometry.py', 'author_mosquito_face.py'):
        sources[name] = hashlib.sha256((HERE / name).read_bytes()).hexdigest()
    manifest = {
        'schema': 1, 'revision': REVISION, 'species': species, 'rig_id': RIG_IDS[species],
        'rig_signature_sha256': rig_signature(rig), 'blender': bpy.app.version_string,
        'bones': [b.name for b in rig.data.bones],
        'bind_bones': [{'name': b.name, 'parent': b.parent.name if b.parent else '',
                        'head_blender_m': [round(v, 6) for v in b.head_local],
                        'tail_blender_m': [round(v, 6) for v in b.tail_local]} for b in rig.data.bones],
        'axes': {'source_up': '+Z', 'source_forward': '-Y', 'fbx_axis_forward': '-Z', 'fbx_axis_up': 'Y'},
        'parts': records, 'material_palette': materials, 'source_sha256': sources,
        'provenance': 'Original procedural geometry for Let me sleep, authored from the user sketches PER-04..07 '
                      'and UI-06; no external meshes or textures.'}
    (folder / 'parts.json').write_text(json.dumps(manifest, indent=2), encoding='utf8', newline='\n')
    # preview blend: the parts (hidden) plus everything else the host keeps visible
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / f'LMS_{species}_modular_preview.blend'))
    print('LMS_MODULAR_PARTS_EXPORTED', species, len(records), flush=True)
    return manifest


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument('--species', choices=['Human', 'Mosquito'], required=True)
    options = parser.parse_args(args)
    captured = {}
    # Same generator, no animation: the parts only need the bind pose.
    author_motion.human = lambda c: None
    author_mosquito_motion.mosquito = lambda c: None
    bc.Character.export = lambda self: captured.setdefault('c', self)
    if options.species == 'Human':
        bc.human()
        build_human_parts()
    else:
        bc.mosquito()
        build_mosquito_parts()
    export(options.species, captured['c'])


if __name__ == '__main__':
    main()
