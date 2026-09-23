"""Let me sleep v0.3.0 - helpers for the faceted low-poly prop library.

Pure bpy/bmesh geometry helpers used by props_catalog.py and build_props.py.
Every generator returns (verts, faces[, face_parts]) in local metres, Z up,
front of a prop authored toward Blender -Y. Nothing here touches the scene
except Prop.build() and the solidify helper (temporary object, removed).
Blender 5.2 LTS.
"""
import math
import random
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

# ----------------------------------------------------------------------------
# colour helpers (manifest keeps sRGB hex; Blender materials get linear values)
# ----------------------------------------------------------------------------

def hex_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lin(hexcol):
    return tuple(srgb_to_linear(c) for c in hex_rgb(hexcol))


# Shared warm palette (sRGB). Derived from PRP-01/PRP-02 swatches and
# docs/v030/GUIA-ESTILO-BOCETOS.md section 4.
C = dict(
    wood_dark='#6E4424', wood='#8B5A2B', wood_light='#A86F3A', wood_pale='#C8955A',
    end_grain='#DDB06C', end_ring='#B7844A', bark='#6B4226', bark_dark='#553318',
    wood_core='#4A2E17',
    iron='#2B2D33', metal_dark='#3A3D46', metal='#5E636E', metal_light='#A3A9B3',
    brass='#D9A441',
    red='#C8322E', red_dark='#9E2422', red_light='#E0463C',
    amber='#FFB347', flame_orange='#FF7A1A', flame_yellow='#FFD24A', ember='#FF5A1F',
    green='#3E8E3A', green_light='#5DB04A', green_dark='#2C6B2F', green_yellow='#86C24F',
    reed_green='#5E8F3A', pad_green='#4E9A3E',
    rock='#8C919B', rock_dark='#6C717B', rock_light='#AEB3BB',
    cream='#EFE6D2', white='#F4F4F0', yellow='#FFC93C', orange='#F08A24',
    blue='#2D4F9A', blue_light='#4F78C4', navy='#1E3570', sky='#8CCBF2',
    terracotta='#C4683A', terracotta_dark='#A5532C', soil='#4A3222',
    parchment='#E6CE98', parchment_dark='#C9AD72', rope='#C9A66B', charcoal='#2E2724',
    black='#1E1E24', pink='#F2C4D6',
)

# ----------------------------------------------------------------------------
# transforms
# ----------------------------------------------------------------------------

def M(loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    """Location (m), XYZ Euler rotation (degrees), uniform or xyz scale."""
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    return Matrix.LocRotScale(Vector(loc), Euler([math.radians(a) for a in rot], 'XYZ'), Vector(scale))


def look_matrix(origin, direction, up=(0, 0, 1)):
    """Matrix whose local +Z points along direction (for Z-built parts)."""
    d = Vector(direction).normalized()
    up = Vector(up)
    if abs(d.dot(up)) > 0.98:
        up = Vector((1, 0, 0))
    x = up.cross(d).normalized()
    y = d.cross(x).normalized()
    m = Matrix((x, y, d)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


# ----------------------------------------------------------------------------
# geometry generators
# ----------------------------------------------------------------------------

def bm_geom(bm):
    bm.verts.index_update()
    v = [tuple(x.co) for x in bm.verts]
    f = [tuple(x.index for x in face.verts) for face in bm.faces]
    bm.free()
    return v, f


def g_box(sx, sy, sz, chamfer=0.0):
    """Box centred on the origin. chamfer>0 gives a one-segment bevel (44 tris)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= sx
        v.co.y *= sy
        v.co.z *= sz
    if chamfer > 0:
        c = min(chamfer, 0.45 * min(sx, sy, sz))
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=c, offset_type='OFFSET',
                        segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
    return bm_geom(bm)


def g_bevel(geom, offset, min_angle=40.0):
    """One-segment chamfer on the sharp edges (dihedral > min_angle degrees) of a closed
    mesh - the 'brillo de canto' of PRP-01/PRP-02 wood. Used for prisms (arrow boards,
    arched lids) that g_box(chamfer=) does not cover."""
    verts, faces = geom[0], geom[1]
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in verts]
    for f in faces:
        try:
            bm.faces.new([vs[i] for i in f])
        except ValueError:
            pass
    bm.normal_update()
    lim = math.radians(min_angle)
    sharp = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > lim]
    if sharp and offset > 0:
        bmesh.ops.bevel(bm, geom=sharp, offset=offset, offset_type='OFFSET', segments=1, profile=0.5,
                        affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm_geom(bm)


def g_cyl(r1, r2, h, n=8, caps=(True, True), phase=None):
    """Cylinder/frustum/cone with base at z=0 and top at z=h (r2=0 gives an apex)."""
    if phase is None:
        phase = math.pi / n
    verts, faces = [], []
    for i in range(n):
        a = 2 * math.pi * i / n + phase
        verts.append((r1 * math.cos(a), r1 * math.sin(a), 0.0))
    if r2 <= 1e-9:
        verts.append((0.0, 0.0, h))
        for i in range(n):
            faces.append((i, (i + 1) % n, n))
    else:
        for i in range(n):
            a = 2 * math.pi * i / n + phase
            verts.append((r2 * math.cos(a), r2 * math.sin(a), h))
        for i in range(n):
            j = (i + 1) % n
            faces.append((i, j, n + j, n + i))
        if caps[1]:
            faces.append(tuple(range(n, 2 * n)))
    if caps[0]:
        faces.append(tuple(reversed(range(n))))
    return verts, faces


def g_ico(r, subdiv=1, scale=(1, 1, 1), jitter=0.0, rng=None):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
    for v in bm.verts:
        if jitter and rng:
            v.co *= 1.0 + rng.uniform(-jitter, jitter)
        v.co.x *= scale[0]
        v.co.y *= scale[1]
        v.co.z *= scale[2]
    return bm_geom(bm)


def g_hull(points):
    """Convex hull (outward normals) - the faceted rock/pebble primitive."""
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    res = bmesh.ops.convex_hull(bm, input=bm.verts[:], use_existing_faces=False)
    kill = list({g for g in res['geom_interior'] + res['geom_unused'] if isinstance(g, bmesh.types.BMVert)})
    if kill:
        bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm_geom(bm)


def rock_points(rng, rx, ry, rz, n=14, base_n=7, base_scale=0.82):
    """Random points on a squashed ellipsoid with a flat base ring at z=0."""
    pts = []
    for i in range(base_n):
        a = 2 * math.pi * i / base_n + rng.uniform(-0.25, 0.25)
        k = base_scale * rng.uniform(0.85, 1.0)
        pts.append((rx * k * math.cos(a), ry * k * math.sin(a), 0.0))
    for i in range(n):
        u = rng.uniform(0, 2 * math.pi)
        z = rng.uniform(0.15, 1.0)
        s = math.sqrt(max(0.0, 1 - z * z)) * rng.uniform(0.88, 1.08)
        pts.append((rx * s * math.cos(u), ry * s * math.sin(u), rz * (0.35 + 0.65 * z) * rng.uniform(0.9, 1.05)))
    pts.append((rng.uniform(-.15, .15) * rx, rng.uniform(-.15, .15) * ry, rz))
    return pts


def boulder_points(rng, rx, ry, rz, n=16, base_n=8, base_scale=0.86, flat_top=0.0):
    """Rounded boulder: golden-spiral points on the upper half of an ellipsoid,
    radial jitter, a flat base ring at z=0 and an optional flattened crown."""
    pts = []
    for i in range(base_n):
        a = 2 * math.pi * i / base_n + rng.uniform(-0.2, 0.2)
        k = base_scale * rng.uniform(0.9, 1.0)
        pts.append((rx * k * math.cos(a), ry * k * math.sin(a), 0.0))
    ga = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        t = (i + 0.5) / n
        z = 0.18 + 0.82 * t
        s = math.sqrt(max(0.0, 1 - z * z))
        u = i * ga + rng.uniform(-0.25, 0.25)
        j = rng.uniform(0.9, 1.06)
        zz = min(z, 1.0 - flat_top) if flat_top else z
        pts.append((rx * s * j * math.cos(u), ry * s * j * math.sin(u), rz * zz * rng.uniform(0.95, 1.03)))
    return pts


def _outward_fix(verts, faces, probe_face, probe_center):
    """Flip every face if probe_face points toward probe_center."""
    f = faces[probe_face]
    a, b, c = (Vector(verts[i]) for i in f[:3])
    n = (b - a).cross(c - a)
    centroid = sum((Vector(verts[i]) for i in f), Vector()) / len(f)
    if n.dot(centroid - Vector(probe_center)) < 0:
        faces[:] = [tuple(reversed(x)) for x in faces]


def g_loft(centers, radii, n=8, cap_start=True, cap_end=True, phase=0.0, twist=0.0, ref=None):
    """Elliptical sections normal to a path. radius 0 at an end gives an apex."""
    Cn = [Vector(c) for c in centers]
    t0 = (Cn[1] - Cn[0]).normalized()
    if ref is None:
        ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    ref = Vector(ref)
    verts, rings = [], []
    for i, c in enumerate(Cn):
        t = (Cn[min(i + 1, len(Cn) - 1)] - Cn[max(i - 1, 0)]).normalized()
        u = t.cross(ref)
        if u.length < 1e-6:
            u = t.cross(Vector((0, 1, 0)))
        u.normalize()
        v = t.cross(u).normalized()
        r = radii[i]
        rx, ry = (r, r) if isinstance(r, (int, float)) else r
        if rx <= 1e-9 and ry <= 1e-9:
            rings.append([len(verts)])
            verts.append(tuple(c))
            continue
        ring = []
        for j in range(n):
            a = 2 * math.pi * j / n + phase + twist * i
            ring.append(len(verts))
            verts.append(tuple(c + rx * math.cos(a) * u + ry * math.sin(a) * v))
        rings.append(ring)
    faces = []
    for i in range(len(rings) - 1):
        A, B = rings[i], rings[i + 1]
        if len(A) == 1 and len(B) == 1:
            continue
        for j in range(n):
            k = (j + 1) % n
            if len(B) == 1:
                faces.append((A[j], A[k], B[0]))
            elif len(A) == 1:
                faces.append((A[0], B[k], B[j]))
            else:
                faces.append((A[j], A[k], B[k], B[j]))
    if cap_start and len(rings[0]) > 1:
        faces.append(tuple(reversed(rings[0])))
    if cap_end and len(rings[-1]) > 1:
        faces.append(tuple(rings[-1]))
    # orientation probe: first side face vs its section centre
    probe = 0
    _outward_fix(verts, faces, probe, Cn[0] if len(rings[0]) > 1 else Cn[1])
    return verts, faces


def signed_area(pts):
    return 0.5 * sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
                     for i in range(len(pts)))


def g_prism(pts, depth, z0=None, caps=True):
    """Extrude a 2-D polygon (XY) along Z, centred on z=0 unless z0 is given.
    Side face i joins polygon point i to point i+1 (after CCW normalisation);
    with caps the two caps come first."""
    pts = list(pts)
    if signed_area(pts) < 0:
        pts.reverse()
    n = len(pts)
    lo = -depth / 2 if z0 is None else z0
    hi = lo + depth
    verts = [(x, y, lo) for x, y in pts] + [(x, y, hi) for x, y in pts]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))] if caps else []
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return verts, faces


def g_poly_rings(poly, scales, z=0.0, center=(0.0, 0.0)):
    """Planar concentric partition of a polygon (for end grain / rug frames).
    scales descending, e.g. [1, .85, .5]. Returns verts, faces, ring index per face
    (0 = outermost band, len(scales)-1 = centre fan)."""
    if signed_area(poly) < 0:
        poly = list(reversed(poly))
    n = len(poly)
    cx, cy = center
    verts, rings = [], []
    for s in scales:
        ring = []
        for x, y in poly:
            ring.append(len(verts))
            verts.append((cx + (x - cx) * s, cy + (y - cy) * s, z))
        rings.append(ring)
    verts.append((cx, cy, z))
    ci = len(verts) - 1
    faces, idx = [], []
    for r in range(len(rings) - 1):
        A, B = rings[r], rings[r + 1]
        for j in range(n):
            k = (j + 1) % n
            faces.append((A[j], A[k], B[k], B[j]))
            idx.append(r)
    last = rings[-1]
    for j in range(n):
        faces.append((last[j], last[(j + 1) % n], ci))
        idx.append(len(rings) - 1)
    return verts, faces, idx


def circle(r, n, phase=0.0, sx=1.0, sy=1.0):
    return [(r * sx * math.cos(2 * math.pi * i / n + phase), r * sy * math.sin(2 * math.pi * i / n + phase))
            for i in range(n)]


def g_blade(length, halfwidths, pitch=30.0, bend=0.0, fold=0.3, thickness=0.004):
    """Faceted double-sided leaf/blade. Base at the origin, grows along +Y,
    rising with pitch (deg above horizontal) and bending down by `bend` degrees
    over its length. halfwidths: one value per interior station (base and tip
    are points). The upper surface faces away from the bend; the underside is
    offset by `thickness` along the local surface normal."""
    k = len(halfwidths)
    steps = k + 1
    ds = length / steps
    a = math.radians(pitch)
    db = math.radians(bend) / steps
    pos = Vector((0, 0, 0))
    path = [(pos.copy(), a)]
    for i in range(steps):
        pos = pos + Vector((0, math.cos(a), math.sin(a))) * ds
        a -= db
        path.append((pos.copy(), a))

    def nrm_at(ang):
        return Vector((0, -math.sin(ang), math.cos(ang)))

    top, offs = [], []
    top.append(tuple(path[0][0]))
    offs.append(nrm_at(path[0][1]))
    rows = []
    for i in range(1, k + 1):
        p, ang = path[i]
        nrm = nrm_at(ang)
        w = halfwidths[i - 1]
        rib = p + nrm * (fold * w)
        drop = nrm * (-0.15 * w)
        L = p + Vector((-w, 0, 0)) + drop
        R = p + Vector((w, 0, 0)) + drop
        rows.append((len(top), len(top) + 1, len(top) + 2))
        top += [tuple(L), tuple(rib), tuple(R)]
        offs += [nrm, nrm, nrm]
    tip = len(top)
    top.append(tuple(path[-1][0]))
    offs.append(nrm_at(path[-1][1]))
    faces = []
    Li, Mi, Ri = rows[0]
    faces += [(0, Ri, Mi), (0, Mi, Li)]
    for (aL, aM, aR), (bL, bM, bR) in zip(rows, rows[1:]):
        faces += [(aM, aR, bR, bM), (aL, aM, bM, bL)]
    Li, Mi, Ri = rows[-1]
    faces += [(Mi, Ri, tip), (Li, Mi, tip)]
    # orientation: the upper surface must face along the station normal
    p1, a1 = path[1]
    _outward_fix(top, faces, 0, tuple(Vector(p1) - nrm_at(a1)))
    nv = len(top)
    under = [tuple(Vector(v) - o * thickness) for v, o in zip(top, offs)]
    faces = faces + [tuple(reversed([i + nv for i in f])) for f in faces]
    return top + under, faces


def g_fan(n, r_tip, r_notch, cup=0.2, thickness=0.004, center_drop=0.0):
    """Flower head facing +Z: alternating petal tips and notches around a centre."""
    top = [(0.0, 0.0, -center_drop)]
    for i in range(2 * n):
        a = math.pi * i / n
        r = r_tip if i % 2 == 0 else r_notch
        z = cup * r_tip if i % 2 == 0 else cup * r_tip * 0.35
        top.append((r * math.cos(a), r * math.sin(a), z))
    faces = [(0, 1 + i, 1 + (i + 1) % (2 * n)) for i in range(2 * n)]
    nv = len(top)
    under = [(x, y, z - thickness) for x, y, z in top]
    return top + under, faces + [tuple(reversed([i + nv for i in f])) for f in faces]


def g_revolve(profile, n=10, phase=None, ids_fn=None):
    """Surface of revolution around Z from (radius, z) pairs, open ends.
    Faces keep the outward side when the profile climbs; a profile that turns
    inward (rims, recesses) faces up/toward the axis as a real lathe would.
    ids_fn(i, j) -> id for the band between profile rows i and i+1, column j."""
    if phase is None:
        phase = math.pi / n
    verts, rings = [], []
    for r, z in profile:
        ring = []
        for j in range(n):
            a = 2 * math.pi * j / n + phase
            ring.append(len(verts))
            verts.append((r * math.cos(a), r * math.sin(a), z))
        rings.append(ring)
    faces, ids = [], []
    for i in range(len(rings) - 1):
        A, B = rings[i], rings[i + 1]
        for j in range(n):
            k = (j + 1) % n
            faces.append((A[j], A[k], B[k], B[j]))
            ids.append(ids_fn(i, j) if ids_fn else 0)
    return verts, faces, ids


def g_sheet(grid, thickness, offset_dir):
    """Double-sided sheet from a rows x cols grid of 3-D points (fabric, paper).
    Front faces follow (col+, row+) orientation; the back is offset along
    -offset_dir by `thickness` and reversed."""
    rows, cols = len(grid), len(grid[0])
    verts = [tuple(p) for row in grid for p in row]
    faces = []
    for i in range(rows - 1):
        for j in range(cols - 1):
            a = i * cols + j
            faces.append((a, a + 1, a + cols + 1, a + cols))
    nv = len(verts)
    d = Vector(offset_dir).normalized() * thickness
    back = [tuple(Vector(v) - d) for v in verts]
    return verts + back, faces + [tuple(reversed([i + nv for i in f])) for f in faces]


def g_solidify(verts, faces, thickness, offset=-1.0):
    """Solidify an open shell. Returns verts, faces, shell id per face
    (0 = original surface, 1 = offset shell, 2 = rim)."""
    me = bpy.data.meshes.new('_lms_tmp_solid')
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new('_lms_tmp_solid', me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new('solid', 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = offset
    mod.use_rim = True
    mod.use_even_offset = True
    mod.material_offset = 1
    mod.material_offset_rim = 2
    for _ in range(3):
        me.materials.append(None)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    m2 = ev.to_mesh()
    v = [tuple(x.co) for x in m2.vertices]
    f = [tuple(p.vertices) for p in m2.polygons]
    ids = [min(p.material_index, 2) for p in m2.polygons]
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return v, f, ids


def merge_close(verts, faces, dist=1e-5):
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in verts]
    for f in faces:
        try:
            bm.faces.new([vs[i] for i in f])
        except ValueError:
            pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=dist)
    return bm_geom(bm)


# ----------------------------------------------------------------------------
# Prop accumulator
# ----------------------------------------------------------------------------

class Prop:
    def __init__(self, name, es, category, mount='floor', notes='', preview=None, seed=0):
        self.name = name
        self.es = es
        self.category = category
        self.mount = mount
        self.notes = notes
        self.preview = preview or {}
        self.extra = {}
        # anchors: name -> dict(center=(x, y, z) in authoring space, normal=(x, y, z), size=(w, h), note='')
        # build() moves them with the pivot; build_props.py writes Blender and Unity-local copies.
        self.anchors = {}
        self.pivot_offset = Vector((0, 0, 0))
        self.V, self.F, self.FM = [], [], []
        self.mats = {}
        self.order = []
        self.rng = random.Random(seed or sum(map(ord, name)))

    def mat(self, part, hexcol, emission=0.0, roughness=0.8, metallic=0.0):
        assert part not in self.mats, part
        self.mats[part] = dict(hex=hexcol, emission=float(emission), roughness=roughness, metallic=metallic)
        self.order.append(part)
        return part

    def anchor(self, name, center, normal=(0, 0, 1), size=None, note=''):
        self.anchors[name] = dict(center=tuple(center), normal=tuple(normal), size=size, note=note)

    def material_name(self, part):
        return 'Prop_%s_%s%s' % (self.name, part, '_Emissive' if self.mats[part]['emission'] > 0 else '')

    def add(self, part, geom, m=None):
        """part: a material part name, or a list of part names (one per face),
        or a dict {id: part} used with geom[2] ids."""
        verts, faces = geom[0], geom[1]
        ids = geom[2] if len(geom) > 2 else None
        mm = m if m is not None else Matrix.Identity(4)
        flip = mm.to_3x3().determinant() < 0
        base = len(self.V)
        for v in verts:
            self.V.append(mm @ Vector(v))
        for k, f in enumerate(faces):
            f = tuple(base + i for i in f)
            if flip:
                f = tuple(reversed(f))
            self.F.append(f)
            if isinstance(part, dict):
                p = part[ids[k]]
            elif isinstance(part, (list, tuple)):
                p = part[k]
            else:
                p = part
            self.FM.append(self.order.index(p))
        return self

    # ------------------------------------------------------------------
    def build(self, collection, make_material):
        import bmesh as _bm
        name = 'Prop_' + self.name
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in self.V], [], self.F)
        me.update()
        me.polygons.foreach_set('material_index', self.FM)
        for part in self.order:
            me.materials.append(make_material(self, part))
        bm = _bm.new()
        bm.from_mesh(me)
        _bm.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges[:])
        _bm.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            _bm.ops.delete(bm, geom=loose, context='VERTS')
        # pivot: centred base
        xs = [v.co.x for v in bm.verts]
        ys = [v.co.y for v in bm.verts]
        zs = [v.co.z for v in bm.verts]
        off = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
        for v in bm.verts:
            v.co -= off
        self.pivot_offset = off.copy()
        # simple box-projected UVs (1 unit = 1 m) so Unity can build tangents
        uv = bm.loops.layers.uv.new('UVMap')
        bm.normal_update()
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                co = l.vert.co
                if ax == 0:
                    l[uv].uv = (co.y, co.z)
                elif ax == 1:
                    l[uv].uv = (co.x, co.z)
                else:
                    l[uv].uv = (co.x, co.y)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        me.update()
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        ob['lms_prop'] = self.name
        return ob
