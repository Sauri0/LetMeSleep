"""Let me sleep v0.3.0 - catalogue of faceted low-poly props.

One function per prop, registered in authoring order. Units are metres,
Z up, front toward Blender -Y, base centred at the origin (Prop.build()
re-centres the base anyway). Style references: PRP-01, PRP-02, ENV-01,
ENV-03, ENV-04, ENV-05 and docs/v030/GUIA-ESTILO-BOCETOS.md. No weapons.
"""
import copy
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix
from props_lib import (C, M, Prop, look_matrix, g_box, g_cyl, g_ico, g_hull, rock_points, boulder_points, g_loft,
                       g_prism, g_poly_rings, circle, g_blade, g_fan, g_revolve, g_sheet,
                       g_solidify, merge_close, g_bevel)

REGISTRY = []


def register(fn):
    REGISTRY.append(fn)
    return fn


# local Z -> world X, local X -> world Y, local Y -> world Z (profiles in YZ extruded along X)
TO_X = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
# mirror of TO_X: local Z -> world -X (end caps facing -X)
TO_NX = Matrix(((0, 0, -1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def at(m, loc):
    m = m.copy()
    m.translation = Vector(loc)
    return m


def front(loc, tilt=0.0):
    """Local XY plane -> world XZ plane, local +Z -> world -Y (faces the front)."""
    return M(loc, (90, tilt, 0))


def g_disc(r, n, z=0.0, phase=0.0, up=True):
    pts = circle(r, n, phase)
    verts = [(x, y, z) for x, y in pts]
    face = tuple(range(n)) if up else tuple(reversed(range(n)))
    return verts, [face]


def top_split(verts, faces, part, top_part, limit=0.78):
    parts = []
    for f in faces:
        a, b, c = (Vector(verts[i]) for i in f[:3])
        n = (b - a).cross(c - a)
        parts.append(top_part if n.length > 0 and n.normalized().z > limit else part)
    return parts


def add_rock(p, loc, rx, ry, rz, yaw, part, top_part, n=14, flat_top=0.0, tilt=(0, 0)):
    """Rounded faceted boulder (PRP-02 'rock' shapes: chunky, no spikes)."""
    v, f = g_hull(boulder_points(p.rng, rx, ry, rz, n=n, flat_top=flat_top))
    p.add(top_split(v, f, part, top_part, limit=0.72), (v, f), M(loc, (tilt[0], tilt[1], yaw)))


def end_grain(p, poly, scales, ids, m):
    p.add(ids, g_poly_rings(poly, scales), m)


LANTERN_TOP = 0.392       # top of the handle ring at scale 1
LANTERN_GLASS_Z = 0.14    # centre of the glass at scale 1
FLAME_2D = [(0.0, -0.05), (0.02, -0.038), (0.029, -0.012), (0.024, 0.014), (0.011, 0.036), (0.0, 0.062),
            (-0.011, 0.036), (-0.024, 0.014), (-0.029, -0.012), (-0.02, -0.038)]


def sq(half):
    """g_cyl radius for an n=4, phase=pi/4 square of the given half side."""
    return half * math.sqrt(2)


def add_lantern(p, base, frame, glass, cap, flame):
    """Shared chunky square lantern (PRP-01 / ENV-03), ~0.39 m tall at scale 1, base at z=0.
    The amber glass box is opaque in Unity, so the flame is a bright emissive
    teardrop 1.5 mm proud of each pane (reads as the flame behind the glass)."""
    def A(part, geom, m):
        p.add(part, geom, base @ m)
    A(frame, g_box(0.18, 0.18, 0.03, chamfer=0.008), M((0, 0, 0.015)))
    A(frame, g_box(0.15, 0.15, 0.026, chamfer=0.005), M((0, 0, 0.043)))
    gh = 0.052
    A(glass, g_box(2 * gh, 2 * gh, 0.15), M((0, 0, LANTERN_GLASS_Z)))
    for rot in (0, 90, 180, 270):
        A(flame, g_prism(FLAME_2D, 0.002), M((0, 0, LANTERN_GLASS_Z - 0.006), (0, 0, rot)) @ front((0, -gh - 0.0012, 0)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            A(frame, g_box(0.026, 0.026, 0.16), M((sx * 0.058, sy * 0.058, LANTERN_GLASS_Z)))
    A(frame, g_box(0.16, 0.16, 0.024, chamfer=0.005), M((0, 0, 0.226)))
    A(cap, g_cyl(sq(0.092), sq(0.036), 0.066, n=4, phase=math.pi / 4), M((0, 0, 0.238)))
    A(cap, g_box(0.07, 0.07, 0.03, chamfer=0.006), M((0, 0, 0.318)))
    A(cap, g_cyl(sq(0.03), 0.0, 0.02, n=4, phase=math.pi / 4), M((0, 0, 0.333)))
    ring = [(0.036 * math.cos(math.radians(t)), 0.0, 0.352 + 0.034 * math.sin(math.radians(t)))
            for t in range(0, 181, 30)]
    A(frame, g_loft(ring, [0.007] * len(ring), n=5), M())


# =============================================================================
# interior / items
# =============================================================================

@register
def hand_lantern():
    p = Prop('HandLantern', 'Farol de mano', 'item',
             notes='Farol cuadrado con vidrio ámbar emisivo y llama visible en cada cara. '
                   'Luz sugerida: Point #FFB347 en el anchor "light", sin sombras.')
    p.mat('Frame', C['iron'], roughness=0.55, metallic=0.35)
    p.mat('Cap', C['metal_dark'], roughness=0.5, metallic=0.35)
    p.mat('Glass', C['amber'], emission=2.5, roughness=0.3)
    p.mat('Flame', '#FFE7A3', emission=4.0, roughness=0.3)
    add_lantern(p, Matrix.Identity(4), 'Frame', 'Glass', 'Cap', 'Flame')
    p.anchor('light', (0, 0, LANTERN_GLASS_Z), note='Centro del vidrio: Point #FFB347, rango 4-6 m, sin sombras.')
    p.anchor('handle', (0, 0, LANTERN_TOP), note='Tope del aro (para colgar o sostener).')
    return p


@register
def crate():
    p = Prop('Crate', 'Cajón de madera', 'interior')
    p.mat('Frame', C['wood'])
    p.mat('Plank', C['wood_light'])
    p.mat('Core', C['wood_core'], roughness=0.95)
    S, b, c = 0.7, 0.07, 0.61
    h = S / 2
    e = h - b / 2
    p.add('Core', g_box(c, c, c), M((0, 0, h)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add('Frame', g_box(b, b, S, chamfer=0.015), M((sx * e, sy * e, h)))
    for s1 in (-1, 1):
        for s2 in (-1, 1):
            p.add('Frame', g_box(S - 2 * b + 0.002, b, b, chamfer=0.015), M((0, s1 * e, h + s2 * e)))
            p.add('Frame', g_box(b, S - 2 * b + 0.002, b, chamfer=0.015), M((s1 * e, 0, h + s2 * e)))
    t = 0.024
    off = c / 2 + t / 2
    W = S - 2 * b
    ph = (W - 0.02) / 3
    for k in range(3):
        d = (k - 1) * (ph + 0.01)
        # PRP-01 edge glints: every plank keeps a 1 cm one-segment chamfer
        p.add('Plank', g_box(W, t, ph, chamfer=0.01), M((0, -off, h + d)))
        p.add('Plank', g_box(W, t, ph, chamfer=0.01), M((0, off, h + d)))
        p.add('Plank', g_box(t, W, ph, chamfer=0.01), M((-off, 0, h + d)))
        p.add('Plank', g_box(t, W, ph, chamfer=0.01), M((off, 0, h + d)))
        p.add('Plank', g_box(W, ph, t, chamfer=0.01), M((0, d, h + off)))
    dl = math.hypot(W, W) - 0.03
    o2 = off + t / 2 + 0.011
    p.add('Frame', g_box(dl, 0.022, 0.075, chamfer=0.009), M((0, -o2, h), (0, 45, 0)))
    p.add('Frame', g_box(dl, 0.022, 0.075, chamfer=0.009), M((0, o2, h), (0, -45, 0)))
    p.add('Frame', g_box(0.022, dl, 0.075, chamfer=0.009), M((-o2, 0, h), (45, 0, 0)))
    p.add('Frame', g_box(0.022, dl, 0.075, chamfer=0.009), M((o2, 0, h), (-45, 0, 0)))
    return p


@register
def chest():
    p = Prop('Chest', 'Cofre', 'interior')
    p.mat('Wood', C['wood'])
    p.mat('Lid', C['wood_light'])
    p.mat('Core', C['wood_core'], roughness=0.95)
    p.mat('Iron', C['iron'], roughness=0.5, metallic=0.5)
    p.mat('Lock', C['brass'], roughness=0.45, metallic=0.6)
    L, D = 0.9, 0.54
    p.add('Iron', g_box(L + 0.02, D + 0.02, 0.05, chamfer=0.008), M((0, 0, 0.025)))
    p.add('Core', g_box(L - 0.04, D - 0.04, 0.338), M((0, 0, 0.05 + 0.169)))
    cd, cw = (D - 0.04) / 2, (L - 0.04) / 2
    for k in range(3):
        z = 0.105 + k * 0.11
        for s in (-1, 1):
            p.add('Wood', g_box(L - 0.08, 0.022, 0.1, chamfer=0.01), M((0, s * (cd + 0.011), z)))
            p.add('Wood', g_box(0.022, D - 0.08, 0.1, chamfer=0.01), M((s * (cw + 0.011), 0, z)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add('Iron', g_box(0.05, 0.05, 0.33, chamfer=0.01), M((sx * (L / 2 - 0.025), sy * (D / 2 - 0.025), 0.215)))
    arc = [(D / 2 * math.cos(math.radians(a)), 0.2 * math.sin(math.radians(a))) for a in range(0, 181, 30)]
    p.add('Lid', g_bevel(g_prism(arc, L - 0.02), 0.015), at(TO_X, (0, 0, 0.388)))
    arc2 = [(0.285 * math.cos(math.radians(a)), 0.215 * math.sin(math.radians(a))) for a in range(0, 181, 30)]
    for sx in (-1, 1):
        p.add('Iron', g_bevel(g_prism(arc2, 0.06), 0.01), at(TO_X, (sx * 0.28, 0, 0.385)))
        p.add('Iron', g_box(0.06, D + 0.012, 0.33, chamfer=0.01), M((sx * 0.28, 0, 0.215)))
        p.add('Iron', g_box(0.025, 0.16, 0.03, chamfer=0.006), M((sx * (L / 2 + 0.012), 0, 0.3)))
    p.add('Lock', g_box(0.1, 0.03, 0.13, chamfer=0.006), M((0, -(D / 2 + 0.008), 0.38)))
    p.add('Iron', g_box(0.018, 0.01, 0.035), M((0, -(D / 2 + 0.026), 0.36)))
    return p


@register
def barrel():
    p = Prop('Barrel', 'Barril', 'interior')
    p.mat('Stave', C['wood'])
    p.mat('StaveLight', C['wood_light'])
    p.mat('Lid', C['wood_dark'])
    p.mat('Hoop', C['metal_dark'], roughness=0.5, metallic=0.45)
    n = 14
    body = [(0.285, 0.0), (0.312, 0.12), (0.336, 0.3), (0.345, 0.475), (0.336, 0.65), (0.312, 0.83), (0.285, 0.95)]
    # 1.5 cm chamfer on the bottom and top rims (PRP-01 barrel edge glints)
    prof = [(0.268, 0.0), (0.287, 0.014)] + body[1:-1] + [(0.287, 0.935), (0.27, 0.95), (0.255, 0.95), (0.255, 0.925)]
    nb = len(body) + 2                      # bands up to the top chamfer alternate staves

    def ids(i, j):
        if i < nb - 1:
            return j % 2
        return 0 if i == nb - 1 else 2
    p.add({0: 'Stave', 1: 'StaveLight', 2: 'Lid'}, g_revolve(prof, n=n, ids_fn=ids))
    p.add('Lid', g_disc(0.255, n, 0.925, math.pi / n))
    p.add('Stave', g_disc(0.268, n, 0.0, math.pi / n, up=False))

    def r_at(z):
        for (r0, z0), (r1, z1) in zip(body, body[1:]):
            if z0 <= z <= z1:
                return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
        return body[-1][0]
    for z0, z1 in ((0.15, 0.215), (0.735, 0.8)):
        rb, rt = r_at(z0), r_at(z1)
        hoop = [(rb, z0), (rb + 0.013, z0), (rt + 0.013, z1), (rt, z1)]
        p.add('Hoop', g_revolve(hoop, n=n))
    return p


def rug(p, W, H, t, xcuts, ycuts, part_fn, side_part):
    xs = sorted(set(round(x, 5) for x in xcuts))
    ys = sorted(set(round(y, 5) for y in ycuts))
    verts, faces, parts = [], [], []
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            x0, x1, y0, y1 = xs[i], xs[i + 1], ys[j], ys[j + 1]
            b = len(verts)
            verts += [(x0, y0, t), (x1, y0, t), (x1, y1, t), (x0, y1, t)]
            faces.append((b, b + 1, b + 2, b + 3))
            parts.append(part_fn((x0 + x1) / 2, (y0 + y1) / 2))
    p.add(parts, (verts, faces))
    rect = [(-W / 2, -H / 2), (W / 2, -H / 2), (W / 2, H / 2), (-W / 2, H / 2)]
    p.add(side_part, g_prism(rect, t, z0=0.0, caps=False))
    p.add(side_part, ([(x, y, 0.0) for x, y in rect], [(3, 2, 1, 0)]))


@register
def rug_red_striped():
    p = Prop('RugRedStriped', 'Alfombra roja con franjas', 'interior', preview={'elev': 55})
    p.mat('Field', C['red'], roughness=0.95)
    p.mat('Border', C['red_dark'], roughness=0.95)
    p.mat('Stripe', C['cream'], roughness=0.95)
    W, H = 2.0, 1.3

    def part(x, y):
        dx, dy = W / 2 - abs(x), H / 2 - abs(y)
        m = min(dx, dy)
        if m < 0.09:
            return 'Border'
        if m < 0.125:
            return 'Stripe'
        if 0.3 < dx < 0.34 or 0.42 < dx < 0.44:
            return 'Stripe'
        return 'Field'
    xc = [s * v for s in (-1, 1) for v in (W / 2, W / 2 - 0.09, W / 2 - 0.125, W / 2 - 0.3, W / 2 - 0.34,
                                            W / 2 - 0.42, W / 2 - 0.44)]
    yc = [s * v for s in (-1, 1) for v in (H / 2, H / 2 - 0.09, H / 2 - 0.125)]
    rug(p, W, H, 0.015, xc, yc, part, 'Border')
    return p


@register
def rug_blue():
    p = Prop('RugBlue', 'Alfombra azul', 'interior', preview={'elev': 55})
    p.mat('Field', C['blue'], roughness=0.95)
    p.mat('Stripe', C['blue_light'], roughness=0.95)
    p.mat('Border', C['navy'], roughness=0.95)
    # ENV-03 kitchen rug: broad light stripes running along the long side
    W, H = 1.8, 1.2
    centres = [-0.36, -0.12, 0.12, 0.36]
    hw = 0.05

    def part(x, y):
        dx, dy = W / 2 - abs(x), H / 2 - abs(y)
        if min(dx, dy) < 0.06:
            return 'Border'
        for c in centres:
            if abs(y - c) < hw:
                return 'Stripe'
        return 'Field'
    xc = [-W / 2, W / 2, -W / 2 + 0.06, W / 2 - 0.06]
    yc = [-H / 2, H / 2, -H / 2 + 0.06, H / 2 - 0.06] + [c + s * hw for c in centres for s in (-1, 1)]
    rug(p, W, H, 0.012, xc, yc, part, 'Border')
    return p


@register
def potted_plant():
    p = Prop('PottedPlant', 'Maceta con planta', 'interior')
    p.mat('Pot', C['terracotta'])
    p.mat('PotRim', C['terracotta_dark'])
    p.mat('Soil', C['soil'], roughness=1.0)
    p.mat('Leaf', C['green'])
    p.mat('LeafLight', C['green_light'])
    p.mat('LeafDark', C['green_dark'])
    p.add('Pot', g_cyl(0.11, 0.15, 0.23, n=8), M())
    p.add('PotRim', g_cyl(0.168, 0.168, 0.05, n=8), M((0, 0, 0.21)))
    p.add('Soil', g_cyl(0.15, 0.15, 0.01, n=8), M((0, 0, 0.245)))
    rng = p.rng
    n = 10
    for i in range(n):
        inner = i % 3 == 0
        yaw = i * 360 / n + rng.uniform(-12, 12)
        L = rng.uniform(0.34, 0.44) if inner else rng.uniform(0.3, 0.4)
        pitch = rng.uniform(62, 78) if inner else rng.uniform(35, 55)
        bend = rng.uniform(30, 50) if inner else rng.uniform(55, 85)
        s = rng.uniform(0.9, 1.1)
        hw = [w * s for w in (0.03, 0.048, 0.05, 0.04, 0.024)]
        p.add(['Leaf', 'LeafLight', 'LeafDark'][i % 3], g_blade(L, hw, pitch, bend, fold=0.35),
              M((0, 0, 0.25), (0, 0, yaw)))
    return p


@register
def hanging_plant():
    p = Prop('HangingPlant', 'Planta colgante', 'interior', mount='ceiling',
             notes='Cuelga del aro superior (~0,98 m sobre el pivote); el pivote es la punta más baja de las guías.')
    p.mat('Pot', '#E8DCC5')
    p.mat('PotBand', C['terracotta'])
    p.mat('Soil', C['soil'], roughness=1.0)
    p.mat('Rope', C['rope'], roughness=0.9)
    p.mat('Leaf', C['green'])
    p.mat('LeafLight', C['green_light'])
    p.mat('Vine', C['green_dark'])
    rng = p.rng
    z0 = 0.42
    p.add('Pot', g_cyl(0.085, 0.12, 0.15, n=8), M((0, 0, z0)))
    p.add('PotBand', g_cyl(0.124, 0.126, 0.03, n=8), M((0, 0, z0 + 0.122)))
    p.add('Soil', g_cyl(0.11, 0.11, 0.01, n=8), M((0, 0, z0 + 0.14)))
    top = (0.0, 0.0, 0.95)
    for k in range(3):
        a = math.radians(90 + 120 * k)
        rim = (0.118 * math.cos(a), 0.118 * math.sin(a), z0 + 0.15)
        p.add('Rope', g_loft([rim, top], [0.0045, 0.0045], n=4), M())
    ring = [(0.028 * math.sin(math.radians(t)), 0.0, 0.978 - 0.028 * math.cos(math.radians(t))) for t in range(0, 331, 45)]
    p.add('Rope', g_loft(ring, [0.0045] * len(ring), n=4), M())
    for k in range(6):
        a = math.radians(k * 60 + rng.uniform(-12, 12))
        out = Vector((math.cos(a), math.sin(a), 0))
        start = Vector((0.11 * math.cos(a), 0.11 * math.sin(a), z0 + 0.15))
        end_z = 0.0 if k == 0 else rng.uniform(0.02, 0.2)
        drop = start.z - end_z
        pts = [start, start + out * 0.045 + Vector((0, 0, 0.01)), start + out * 0.075 - Vector((0, 0, drop * 0.3)),
               start + out * 0.085 - Vector((0, 0, drop * 0.65)), start + out * 0.07 - Vector((0, 0, drop))]
        p.add('Vine', g_loft([tuple(q) for q in pts], [0.005, 0.005, 0.0045, 0.004, 0.003], n=4), M())
        for i in range(4):
            t = 0.25 + 0.75 * i / 3
            seg = t * (len(pts) - 1)
            i0 = min(int(seg), len(pts) - 2)
            q = pts[i0].lerp(pts[i0 + 1], seg - i0)
            yaw = math.degrees(a) - 90 + (55 if i % 2 else -55) + rng.uniform(-15, 15)
            p.add('Leaf' if i % 2 else 'LeafLight',
                  g_blade(0.095, [0.024, 0.03, 0.016], pitch=rng.uniform(-5, 25), bend=35, fold=0.3, thickness=0.003),
                  M(tuple(q), (0, 0, yaw)))
    for i in range(10):
        yaw = i * 36 + rng.uniform(-10, 10)
        p.add('Leaf' if i % 2 else 'LeafLight',
              g_blade(0.2, [0.034, 0.042, 0.024], pitch=rng.uniform(30, 60), bend=70, fold=0.3),
              M((0, 0, z0 + 0.15), (0, 0, yaw)))
    return p


@register
def painting_landscape():
    p = Prop('PaintingLandscape', 'Cuadro con paisaje', 'interior', mount='wall', preview={'elev': 8, 'yaw': 18},
             notes='Colgar en pared: el dorso queda en +Y (0,025 m detrás del pivote); el frente mira a -Y.')
    p.mat('Frame', C['wood'])
    p.mat('Sky', C['sky'], roughness=0.9)
    p.mat('Sun', '#FFD86B', roughness=0.9)
    p.mat('Mountain', '#7C8CA8', roughness=0.9)
    p.mat('Snow', C['white'], roughness=0.9)
    p.mat('Hill', C['pad_green'], roughness=0.9)
    p.mat('Lake', '#3D7CC9', roughness=0.9)
    p.mat('Pine', C['green_dark'], roughness=0.9)
    zc = 0.28
    for z in (0.03, 0.53):
        p.add('Frame', g_box(0.76, 0.05, 0.06, chamfer=0.008), M((0, 0, z)))
    for x in (-0.35, 0.35):
        p.add('Frame', g_box(0.06, 0.05, 0.44, chamfer=0.008), M((x, 0, zc)))
    p.add('Sky', g_box(0.66, 0.02, 0.46), M((0, 0.01, zc)))

    def layer(part, pts, y):
        p.add(part, g_prism(pts, 0.0015), front((0, y, zc)))
    layer('Sun', [(0.2 + x, 0.13 + y) for x, y in circle(0.035, 8)], -0.002)
    layer('Mountain', [(-0.32, -0.08), (0.32, -0.08), (0.32, 0.0), (0.22, 0.1), (0.14, 0.05), (0.02, 0.165),
                       (-0.1, 0.02), (-0.2, 0.085), (-0.32, -0.03)], -0.004)
    layer('Snow', [(-0.03, 0.11), (0.0, 0.125), (0.02, 0.105), (0.045, 0.125), (0.075, 0.105), (0.02, 0.165)], -0.006)
    layer('Snow', [(-0.235, 0.05), (-0.215, 0.058), (-0.2, 0.047), (-0.175, 0.06), (-0.2, 0.085)], -0.006)
    layer('Hill', [(-0.32, -0.22), (0.32, -0.22), (0.32, -0.03), (0.18, -0.055), (0.05, -0.015), (-0.1, -0.065),
                   (-0.25, -0.025), (-0.32, -0.05)], -0.008)
    layer('Lake', [(-0.02, -0.14), (0.26, -0.14), (0.3, -0.105), (0.06, -0.09)], -0.010)
    for i, (x, y0, s) in enumerate(((-0.24, -0.16, 1.0), (-0.16, -0.13, 0.8), (0.24, -0.2, 0.9))):
        y = -0.012 - 0.002 * i
        layer('Pine', [(x - 0.045 * s, y0), (x + 0.045 * s, y0), (x, y0 + 0.1 * s)], y)
        layer('Pine', [(x - 0.034 * s, y0 + 0.055 * s), (x + 0.034 * s, y0 + 0.055 * s), (x, y0 + 0.14 * s)], y - 0.001)
    return p


@register
def bookshelf():
    p = Prop('Bookshelf', 'Estante con libros', 'interior')
    p.mat('Wood', C['wood'])
    p.mat('WoodLight', C['wood_light'])
    p.mat('Back', C['wood_core'], roughness=0.95)
    books = ['BookRed', 'BookBlue', 'BookGreen', 'BookYellow', 'BookCream', 'BookTeal']
    for name, col in zip(books, (C['red'], C['blue_light'], C['green'], C['yellow'], C['cream'], '#2E8C8C')):
        p.mat(name, col, roughness=0.85)
    p.mat('Pot', C['terracotta'])
    p.mat('Leaf', C['green_light'])
    rng = p.rng
    for x in (-0.48, 0.48):
        p.add('Wood', g_box(0.04, 0.34, 1.8, chamfer=0.006), M((x, 0, 0.9)))
    p.add('WoodLight', g_box(1.02, 0.36, 0.04, chamfer=0.008), M((0, 0, 1.78)))
    p.add('Wood', g_box(0.92, 0.32, 0.08), M((0, 0, 0.04)))
    p.add('Back', g_box(0.92, 0.02, 1.72), M((0, 0.16, 0.9)))
    levels = [0.08, 0.52, 0.94, 1.36]
    for z in levels[1:]:
        p.add('WoodLight', g_box(0.92, 0.31, 0.03), M((0, -0.005, z - 0.015)))
    for li, base in enumerate(levels):
        clear = (levels[li + 1] - 0.03 if li + 1 < len(levels) else 1.76) - base
        x = -0.44 + rng.uniform(0.0, 0.02)
        limit = 0.44 if li == 0 else rng.uniform(0.12, 0.22)
        k = rng.randrange(len(books))
        while True:
            w = rng.uniform(0.036, 0.066)
            if x + w > limit:
                break
            h = min(clear - 0.04, rng.uniform(0.21, 0.33))
            d = rng.uniform(0.19, 0.24)
            k = (k + rng.choice((1, 2, 3))) % len(books)
            p.add(books[k], g_box(w, d, h), M((x + w / 2, -0.15 + d / 2, base + h / 2)))
            x += w + 0.002
        if li == 1:      # lying stack
            hh = 0.0
            for s in range(3):
                bw, bh = rng.uniform(0.2, 0.25), rng.uniform(0.035, 0.05)
                p.add(books[(k + s + 1) % len(books)], g_box(bw, 0.19, bh),
                      M((0.3 + rng.uniform(-0.01, 0.01), -0.04, base + hh + bh / 2), (0, 0, rng.uniform(-6, 6))))
                hh += bh
        elif li == 2:    # leaning book
            h = 0.27
            p.add(books[(k + 2) % len(books)], g_box(0.04, 0.21, h),
                  M((x + 0.02 + h / 2 * math.sin(math.radians(16)), -0.04, base + h / 2 * math.cos(math.radians(16))),
                    (0, 16, 0)))
        elif li == 3:    # small plant
            p.add('Pot', g_cyl(0.05, 0.065, 0.09, n=6), M((0.3, -0.02, base)))
            for i in range(5):
                p.add('Leaf', g_blade(0.13, [0.018, 0.024, 0.014], pitch=rng.uniform(40, 65), bend=60, fold=0.3),
                      M((0.3, -0.02, base + 0.085), (0, 0, i * 72 + rng.uniform(-10, 10))))
    return p


@register
def hanging_pans():
    p = Prop('HangingPans', 'Sartenes colgadas', 'kitchen', mount='wall', preview={'elev': 10, 'yaw': 20},
             notes='Barral con ganchos y 3 sartenes; el dorso del barral queda en +Y contra la pared.')
    p.mat('Board', C['wood'])
    p.mat('Hook', C['metal'], roughness=0.45, metallic=0.6)
    p.mat('Iron', C['iron'], roughness=0.55, metallic=0.4)
    p.mat('Rim', C['metal_light'], roughness=0.45, metallic=0.5)
    p.mat('Inside', C['metal'], roughness=0.6, metallic=0.3)
    p.mat('Handle', C['wood_dark'])
    p.mat('Copper', '#C8733A', roughness=0.45, metallic=0.5)
    p.add('Board', g_box(0.9, 0.03, 0.09, chamfer=0.008), M((0, 0.025, 0.58)))
    pans = [(-0.28, 0.10, 0.13, 0.045, 0.19, 'Iron', 'Iron'), (0.03, 0.075, 0.085, 0.08, 0.15, 'Copper', 'Copper'),
            (0.3, 0.08, 0.10, 0.04, 0.14, 'Iron', 'Handle')]
    for x, rb, rt, depth, hl, outer, handle_part in pans:
        n = 10
        top_z = 0.535 - hl
        zc = top_z - rt
        yb = 0.035
        prof = [(rb, 0.0), (rt, depth), (rt - 0.008, depth), (rb - 0.006, 0.008)]
        m = M((x, yb, zc), (90, 0, 0))
        p.add({0: outer, 1: 'Rim', 2: 'Inside'}, g_revolve(prof, n=n, phase=math.pi / 2, ids_fn=lambda i, j: i), m)
        p.add('Inside', g_disc(rb - 0.006, n, 0.008, math.pi / 2), m)
        p.add(outer, g_disc(rb, n, 0.0, math.pi / 2, up=False), m)
        yh = yb - depth + 0.008
        p.add(handle_part, g_box(0.03, 0.014, hl + 0.02, chamfer=0.004), M((x, yh, top_z + hl / 2 - 0.01)))
        p.add('Hook', g_box(0.012, 0.012, 0.05), M((x, 0.004, 0.53)))
        p.add('Hook', g_box(0.012, abs(yh - 0.004) + 0.012, 0.012), M((x, (yh + 0.004) / 2, 0.51)))
        p.add('Hook', g_box(0.012, 0.012, 0.03), M((x, yh - 0.006, 0.52)))
    return p


@register
def mug():
    p = Prop('Mug', 'Taza', 'item')
    p.mat('Enamel', C['blue_light'], roughness=0.35)
    p.mat('Rim', C['white'], roughness=0.35)
    p.mat('Inside', C['cream'], roughness=0.4)
    n = 12
    prof = [(0.04, 0.0), (0.043, 0.095), (0.037, 0.095), (0.037, 0.012)]
    p.add({0: 'Enamel', 1: 'Rim', 2: 'Inside'}, g_revolve(prof, n=n, ids_fn=lambda i, j: i))
    p.add('Inside', g_disc(0.037, n, 0.012, math.pi / n))
    p.add('Enamel', g_disc(0.04, n, 0.0, math.pi / n, up=False))
    handle = [(0.039 + 0.03 * math.cos(math.radians(t)), 0.0, 0.051 + 0.029 * math.sin(math.radians(t)))
              for t in (-90, -60, -30, 0, 30, 60, 90)]
    p.add('Enamel', g_loft(handle, [(0.0075, 0.009)] * len(handle), n=6))
    return p


@register
def toolbox():
    p = Prop('Toolbox', 'Caja de herramientas roja', 'item')
    p.mat('Body', C['red'], roughness=0.5)
    p.mat('Lid', C['red_light'], roughness=0.5)
    p.mat('Seam', C['iron'], roughness=0.6)
    p.mat('Handle', C['iron'], roughness=0.5, metallic=0.3)
    p.mat('Latch', C['metal_light'], roughness=0.35, metallic=0.7)
    p.add('Body', g_box(0.5, 0.24, 0.16, chamfer=0.012), M((0, 0, 0.08)))
    p.add('Seam', g_box(0.485, 0.225, 0.02), M((0, 0, 0.165)))
    p.add('Lid', g_box(0.505, 0.245, 0.07, chamfer=0.012), M((0, 0, 0.21)))
    p.add('Body', g_box(0.4, 0.18, 0.03, chamfer=0.008), M((0, 0, 0.255)))
    for s in (-1, 1):
        p.add('Handle', g_box(0.03, 0.03, 0.05), M((s * 0.12, 0, 0.29)))
        p.add('Latch', g_box(0.05, 0.016, 0.06, chamfer=0.004), M((s * 0.15, -0.124, 0.17)))
    p.add('Handle', g_box(0.3, 0.036, 0.032, chamfer=0.009), M((0, 0, 0.325)))
    return p


@register
def medkit():
    p = Prop('Medkit', 'Botiquín', 'item')
    p.mat('Case', C['red'], roughness=0.5)
    p.mat('Band', C['red_dark'], roughness=0.5)
    p.mat('Cross', C['white'], roughness=0.5)
    p.mat('Handle', C['iron'], roughness=0.5, metallic=0.3)
    p.add('Case', g_box(0.42, 0.15, 0.3, chamfer=0.035), M((0, 0, 0.15)))
    p.add('Band', g_box(0.425, 0.155, 0.018), M((0, 0, 0.15)))
    a, b = 0.03, 0.085
    plus = [(-a, -b), (a, -b), (a, -a), (b, -a), (b, a), (a, a), (a, b), (-a, b), (-a, a), (-b, a), (-b, -a), (-a, -a)]
    p.add('Cross', g_prism(plus, 0.008), front((0, -0.077, 0.15)))
    p.add('Cross', g_prism(plus, 0.008), M((0, 0.077, 0.15), (-90, 0, 0)))
    for s in (-1, 1):
        p.add('Handle', g_box(0.025, 0.025, 0.045), M((s * 0.09, 0, 0.315)))
    p.add('Handle', g_box(0.22, 0.036, 0.03, chamfer=0.009), M((0, 0, 0.345)))
    return p


@register
def rolled_map():
    p = Prop('RolledMap', 'Mapa enrollado', 'item', preview={'elev': 40})
    p.mat('Paper', C['parchment'], roughness=0.9)
    p.mat('PaperDark', C['parchment_dark'], roughness=0.9)
    p.mat('Hole', '#7A5A30', roughness=0.9)
    p.mat('Tie', C['red'], roughness=0.8)
    p.mat('Ink', '#3D7CC9', roughness=0.9)
    p.mat('Land', C['pad_green'], roughness=0.9)
    r, n, L, zc = 0.035, 10, 0.42, 0.035
    ph = math.pi / n
    poly = circle(r, n, ph)
    p.add('Paper', g_prism(poly, L, caps=False), at(TO_X, (0, 0, zc)))
    ids = {0: 'Paper', 1: 'PaperDark', 2: 'Paper', 3: 'Hole'}
    end_grain(p, poly, [1.0, 0.78, 0.6, 0.32], ids, at(TO_X, (L / 2, 0, zc)))
    end_grain(p, poly, [1.0, 0.78, 0.6, 0.32], ids, at(TO_NX, (-L / 2, 0, zc)))
    band = circle(r + 0.003, n, ph)
    p.add('Tie', g_prism(band, 0.016), at(TO_X, (0.06, 0, zc)))
    for s in (-1, 1):
        p.add('Tie', g_box(0.03, 0.006, 0.014), M((0.06 + s * 0.016, -0.012, zc + r + 0.004), (0, s * 35, 0)))
    ys = [-0.026, -0.06, -0.1, -0.14]
    zs = [0.012, 0.004, 0.004, 0.009]
    grid = [[(x, y, z) for x in (-0.19, 0.0, 0.19)] for y, z in zip(ys, zs)]
    grid = [[(x, y, z) for (x, y, z) in row] for row in grid]
    grid.reverse()  # rows run toward +Y so the top side faces up
    p.add('Paper', g_sheet(grid, 0.002, (0, 0, 1)))
    p.add('Land', g_prism([(x - 0.07, y - 0.1) for x, y in circle(0.028, 6, 0.3, 1.4, 0.8)], 0.001), M((0, 0, 0.0052)))
    p.add('Ink', g_box(0.1, 0.006, 0.001), M((0.02, -0.08, 0.0052), (0, 0, -15)))
    p.add('Tie', g_box(0.022, 0.004, 0.001), M((0.11, -0.085, 0.0052), (0, 0, 45)))
    p.add('Tie', g_box(0.022, 0.004, 0.001), M((0.11, -0.085, 0.0052), (0, 0, -45)))
    return p


@register
def soccer_ball():
    p = Prop('SoccerBall', 'Pelota de fútbol', 'item')
    p.mat('White', C['white'], roughness=0.6)
    p.mat('Black', C['black'], roughness=0.6)
    R = 0.11
    phi = (1 + 5 ** 0.5) / 2
    ico = []
    for a in (-1, 1):
        for b in (-1, 1):
            ico += [(0, a, b * phi), (a, b * phi, 0), (b * phi, 0, a)]
    ico = [Vector(v) for v in ico]
    edge = min((ico[i] - ico[j]).length for i in range(12) for j in range(i + 1, 12))
    nb = {i: [j for j in range(12) if j != i and abs((ico[i] - ico[j]).length - edge) < 1e-4] for i in range(12)}
    verts, faces, parts, key = [], [], [], {}

    def pt(i, j):
        k = (i, j)
        if k not in key:
            key[k] = len(verts)
            verts.append((ico[i] + (ico[j] - ico[i]) / 3).normalized() * R)
        return key[k]

    def add_poly(ids, part):
        cen = sum((verts[i] for i in ids), Vector()) / len(ids)
        nrm = (verts[ids[1]] - verts[ids[0]]).cross(verts[ids[2]] - verts[ids[0]])
        if nrm.dot(cen) < 0:
            ids = list(reversed(ids))
        c = len(verts)
        verts.append(cen.normalized() * R * 1.0)
        for a in range(len(ids)):
            faces.append((ids[a], ids[(a + 1) % len(ids)], c))
            parts.append(part)
    for i in range(12):
        ring = nb[i]
        cen = ico[i]
        ref = (ico[ring[0]] - cen)
        ax = cen.normalized()
        ref = (ref - ax * ref.dot(ax)).normalized()
        oth = ax.cross(ref)
        ring.sort(key=lambda j: math.atan2((ico[j] - cen).dot(oth), (ico[j] - cen).dot(ref)))
        add_poly([pt(i, j) for j in ring], 'Black')
    done = set()
    for i in range(12):
        for j in nb[i]:
            for k in nb[i]:
                if k > j and k in nb[j]:
                    tri = tuple(sorted((i, j, k)))
                    if tri in done:
                        continue
                    done.add(tri)
                    a, b, c = tri
                    add_poly([pt(a, b), pt(b, a), pt(b, c), pt(c, b), pt(c, a), pt(a, c)], 'White')
    p.add(parts, ([tuple(v) for v in verts], faces), M((0, 0, R)))
    return p


# =============================================================================
# bedroom / main-menu scene (UI-06: humano dormido, velador, reloj 03:27, ventana)
# =============================================================================

# UI-06 menu vignette recipe (Validation/V030/Props/vignette_menu.png), in bed-local Blender metres: bed pivot at
# the origin, headboard toward +Y, Z up. build_props.py adds the derived positions and Unity-local copies; the
# camera comes from render_props.py --search-menu-camera (head in the central third, nightstand + lamp + clock in
# the right third and >= 35 % of the frame height, camera at pillow height 1.2 m from the headboard).
MENU_LAYOUT = {
    'reference': 'UI-06 panel 1 (menú principal): durmiente en el tercio central, mesa de luz con velador y reloj '
                 'en el tercio derecho; el tercio izquierdo queda para el logo y los botones.',
    'resolution': [1600, 900],
    'wall_gap_m': 0.01,
    'nightstand_gap_m': 0.16,
    'wainscot_height_m': 1.55,
    'colors_srgb': {'wall': '#2A3560', 'wainscot': '#8B5A2B', 'wainscot_trim': '#A86F3A', 'floor': '#5A3A24',
                    'world': '#101830'},
    'window': {'x_m': -1.6, 'sill_z_m': 0.98},
    'moon': {'color_srgb': '#6A7BD0', 'strength': 2.0, 'direction': [0.45, 0.45, -0.77]},
    'lamp': {'color_rgb_linear': [1.0, 0.8, 0.45], 'watts': 27.0, 'radius_m': 0.06},
    'sleeper_fill': {'color_rgb_linear': [1.0, 0.72, 0.42], 'watts': 9.0, 'offset_from_head_m': [0.3, -0.35, 0.25]},
    'world_strength': 0.35,
    'glare_strength': 0.3,
    'camera': {'location': [-0.5071, -0.0726, 0.92], 'look_at': [-0.1387, 0.7025, 0.78], 'lens_mm': 20,
               'sensor_width_mm': 36.0, 'headboard_distance_m': 1.2, 'azimuth_deg': 25},
}

BED_W, BED_L = 0.86, 2.1          # single bed: 0.72 m mattress so the 0.62 m pillow covers 86 %
PILLOW = (0.62, 0.40, 0.16)


def g_cushion(size, sec=10, expo=2.8, stations=None):
    """Rounded rectangular cushion along X: superellipse sections (sec regular facets around)
    lofted through pinched ends. Convex, so faces are oriented against the centre."""
    PW, PD, PH = size
    if stations is None:
        stations = [(-PW / 2, 0.84, 0.34), (-PW / 2 + 0.05, 0.96, 0.82), (-PW / 2 + 0.17, 1.0, 1.0),
                    (PW / 2 - 0.17, 1.0, 1.0), (PW / 2 - 0.05, 0.96, 0.82), (PW / 2, 0.84, 0.34)]
    e = 2.0 / expo
    verts, faces = [], []
    for x, sy, sz in stations:
        for k in range(sec):
            a = 2 * math.pi * k / sec
            c, s_ = math.cos(a), math.sin(a)
            verts.append((x, math.copysign(abs(c) ** e, c) * PD / 2 * sy, math.copysign(abs(s_) ** e, s_) * PH / 2 * sz))
    for i in range(len(stations) - 1):
        for k in range(sec):
            a, b = i * sec + k, i * sec + (k + 1) % sec
            faces.append((a, b, b + sec, a + sec))
    last = (len(stations) - 1) * sec
    faces.append(tuple(range(sec)))
    faces.append(tuple(range(last, last + sec)))
    out = []
    for f in faces:
        a, b, c = (Vector(verts[i]) for i in f[:3])
        cen = sum((Vector(verts[i]) for i in f), Vector()) / len(f)
        out.append(f if (b - a).cross(c - a).dot(cen) >= 0 else tuple(reversed(f)))
    return verts, out


@register
def bed():
    p = Prop('Bed', 'Cama con acolchado rojo', 'bedroom', preview={'yaw': -38, 'elev': 28},
             notes='Cama de madera de una plaza (PRP-02 / ENV-03 / UI-06): cabecera en +Y, pie hacia -Y. Colchón grueso '
                   '(0,33-0,62 m) entre largueros laterales de 0,43 m; acolchado rojo con franja #8E2A2A cerca de los pies, '
                   'cara superior a ~0,66 m (0,06 m sobre el larguero de la piecera, 0,60 m) que cae 0,08 m por los lados. '
                   'Almohada: cojín rectangular redondeado 0,62 x 0,40 x 0,16 m (86 % del ancho del colchón, 10 facetas '
                   'regulares). Anchor "sleeper_head": nuca del humano dormido; "menu_layout": receta de la viñeta UI-06 '
                   '(cámara, mesa de luz y luces) en coordenadas de la cama.')
    p.mat('Wood', C['wood'])
    p.mat('WoodLight', C['wood_light'])
    p.mat('Mattress', '#E9E1D0', roughness=0.9)
    p.mat('Sheet', C['cream'], roughness=0.9)
    p.mat('Quilt', C['red'], roughness=0.9)
    p.mat('QuiltDark', C['red_dark'], roughness=0.9)
    p.mat('QuiltBand', '#8E2A2A', roughness=0.9)
    p.mat('Pillow', '#F2EEE6', roughness=0.9)
    rng = p.rng
    W, L, post = BED_W, BED_L, 0.1
    hx, hy = W / 2 - post / 2, L / 2 - post / 2
    w_in = W - 2 * post
    rail_z0, rail_z1 = 0.25, 0.43            # side rails (largueros laterales)
    foot_top = 0.60                          # top of the footboard rail (larguero de la piecera)
    m_z0, m_z1 = 0.33, 0.62                  # thick mattress (0.29 m)
    Wm, Lm = W - 0.14, L - 2 * post - 0.01   # 0.72 x 1.89
    q_top = foot_top + 0.06                  # quilt top face 0.66
    q_side = q_top - 0.08                    # quilt hem on the long sides 0.58
    for sx in (-1, 1):
        p.add('Wood', g_box(post, post, 1.1, chamfer=0.015), M((sx * hx, hy, 0.55)))
        p.add('WoodLight', g_box(post + 0.03, post + 0.03, 0.04, chamfer=0.012), M((sx * hx, hy, 1.12)))
        p.add('Wood', g_box(post, post, 0.76, chamfer=0.015), M((sx * hx, -hy, 0.38)))
        p.add('WoodLight', g_box(post + 0.03, post + 0.03, 0.04, chamfer=0.012), M((sx * hx, -hy, 0.78)))
        p.add('Wood', g_box(0.06, L - 2 * post + 0.004, rail_z1 - rail_z0, chamfer=0.015),
              M((sx * (W / 2 - 0.03), 0, (rail_z0 + rail_z1) / 2)))
    # headboard: panel + lower rail + arched top rail (PRP-02)
    p.add('Wood', g_box(w_in, 0.05, 0.5, chamfer=0.012), M((0, hy, 0.69)))
    p.add('WoodLight', g_box(w_in, 0.07, 0.06, chamfer=0.012), M((0, hy, 0.47)))
    xs = [w_in / 2 - w_in * i / 8 for i in range(9)]
    arch = [(-w_in / 2, 0.9), (w_in / 2, 0.9)] + [(x, 0.98 + 0.07 * math.cos(math.pi * x / w_in)) for x in xs]
    p.add('WoodLight', g_bevel(g_prism(arch, 0.08), 0.012), front((0, hy, 0)))
    # footboard: panel + top rail (the 'larguero' the quilt clears by 6 cm)
    p.add('Wood', g_box(w_in, 0.05, 0.3, chamfer=0.012), M((0, -hy, 0.42)))
    p.add('WoodLight', g_box(w_in + 0.004, 0.08, 0.06, chamfer=0.015), M((0, -hy, foot_top - 0.03)))
    p.add('Mattress', g_box(Wm, Lm, m_z1 - m_z0, chamfer=0.03), M((0, 0, (m_z0 + m_z1) / 2)))
    # quilt: faceted drape from the foot to a turned-down sheet fold; dark band near the feet (PRP-02)
    y_fold = L / 2 - post - 0.55
    y0 = -Lm / 2 - 0.012
    y1 = -Lm / 2 + 0.02
    yb0, yb1 = y1 + 0.14, y1 + 0.26
    y_end = y_fold - 0.16
    ys = [y0, y1, yb0, yb1] + [yb1 + (y_end - yb1) * k / 5 for k in range(1, 6)] + \
        [y_fold - 0.09, y_fold - 0.03, y_fold + 0.01]
    band = (2,)                                      # face row between yb0 and yb1
    xq = [-(Wm / 2 + 0.012), -(Wm / 2 - 0.02), -0.2, -0.1, 0.0, 0.1, 0.2, Wm / 2 - 0.02, Wm / 2 + 0.012]
    cols, nrow = len(xq), len(ys)
    grid = []
    for i, y in enumerate(ys):
        row = []
        for j, x in enumerate(xq):
            edge = j in (0, cols - 1)
            rim = j in (1, cols - 2)
            crown = 0.015 * math.cos(math.pi * x / (Wm + 0.03))
            if i == 0:
                z = 0.48
            elif i == nrow - 1:
                z = m_z1 - 0.03 if edge else m_z1 + 0.006
            elif edge:
                z = q_side
            elif i == nrow - 3:
                z = q_top + 0.03 + crown
            elif i == nrow - 2:
                z = q_top + 0.015 + crown
            elif rim or i == 1:
                z = q_top - 0.02
            else:
                # quilted puffs: checkerboard of stitch (low) and puff (high) vertices, mean ~ q_top
                z = q_top - 0.02 + crown + (0.026 if (i + j) % 2 else 0.0) + rng.uniform(-0.003, 0.003)
            row.append((x, y, z))
        grid.append(row)
    verts = [pt for row in grid for pt in row]
    faces, parts = [], []
    for i in range(nrow - 1):
        for j in range(cols - 1):
            a = i * cols + j
            faces.append((a, a + 1, a + cols + 1, a + cols))
            if i >= nrow - 3:
                parts.append('Sheet')
            elif i in band:
                parts.append('QuiltBand')
            elif i == 0 or j in (0, cols - 2):
                parts.append('QuiltDark')
            else:
                parts.append('Quilt')
    p.add(parts, (verts, faces))
    # pillow: rounded rectangular cushion 0.62 x 0.40 x 0.16, leaning 6 deg on the headboard
    PW, PD, PH = PILLOW
    py = hy - 0.04 - PD / 2 - 0.005
    pz = m_z1 + PH / 2 - 0.006
    p.add('Pillow', g_cushion(PILLOW), M((0, py, pz), (6, 0, 0)))
    p.anchor('sleeper_head', (0, py - 0.03, pz + PH / 2 - 0.012), normal=(0, 0, 1),
             note='Centro de la cara superior de la almohada, donde apoya la nuca el humano dormido; el cuerpo sigue '
                  'hacia -Y bajo el acolchado (cara superior ~0,66 m).')
    p.extra['bed_heights_m'] = {'side_rail_top': rail_z1, 'footboard_rail_top': foot_top, 'mattress': [m_z0, m_z1],
                                'quilt_top': round(q_top, 3), 'quilt_side_hem': round(q_side, 3),
                                'mattress_width': round(Wm, 3),
                                'pillow_size': list(PILLOW), 'pillow_to_mattress_width': round(PW / Wm, 3)}
    p.extra['menu_layout'] = copy.deepcopy(MENU_LAYOUT)
    return p


@register
def nightstand():
    p = Prop('Nightstand', 'Mesa de luz', 'bedroom',
             notes='Mesa de luz con cajón y estante abierto (PRP-02 / ENV-01), cantos con chaflán de 1-1,5 cm. Tapa a '
                   '0,59 m: anchors "lamp" y "clock" para apoyar la lámpara y el despertador.')
    p.mat('Wood', C['wood'])
    p.mat('WoodLight', C['wood_light'])
    p.mat('Drawer', C['wood_pale'])
    p.mat('Inside', C['wood_core'], roughness=0.95)
    p.mat('Handle', C['iron'], roughness=0.5, metallic=0.3)
    p.mat('BookRed', C['red'], roughness=0.85)
    p.mat('BookBlue', C['blue_light'], roughness=0.85)
    W, D = 0.5, 0.4
    z0, z1 = 0.1, 0.55
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add('Wood', g_box(0.06, 0.06, z0 + 0.01, chamfer=0.012),
                  M((sx * (W / 2 - 0.04), sy * (D / 2 - 0.04), (z0 + 0.01) / 2)))
        p.add('Wood', g_box(0.04, D - 0.02, z1 - z0, chamfer=0.012), M((sx * (W / 2 - 0.02), 0, (z0 + z1) / 2)))
    wi = W - 0.08
    p.add('Wood', g_box(wi, 0.025, z1 - z0, chamfer=0.01), M((0, D / 2 - 0.0225, (z0 + z1) / 2)))
    p.add('Wood', g_box(wi, D - 0.04, 0.03, chamfer=0.01), M((0, 0, z0 + 0.015)))
    p.add('Wood', g_box(wi, D - 0.04, 0.03, chamfer=0.01), M((0, 0, 0.34)))
    p.add('Inside', g_box(wi, 0.004, 0.2), M((0, D / 2 - 0.037, 0.23)))
    p.add('WoodLight', g_box(W + 0.04, D + 0.04, 0.04, chamfer=0.015), M((0, 0, z1 + 0.02)))
    p.add('Drawer', g_box(W - 0.1, 0.03, 0.17, chamfer=0.012), M((0, -D / 2 + 0.03, 0.445)))
    p.add('Handle', g_box(0.09, 0.022, 0.022, chamfer=0.005), M((0, -D / 2 + 0.006, 0.46)))
    p.add('BookBlue', g_box(0.22, 0.16, 0.035, chamfer=0.006), M((-0.04, 0.01, z0 + 0.03 + 0.0175), (0, 0, 4)))
    p.add('BookRed', g_box(0.19, 0.14, 0.03, chamfer=0.006), M((-0.03, 0.0, z0 + 0.03 + 0.035 + 0.015), (0, 0, -7)))
    topz = z1 + 0.04
    p.anchor('lamp', (0.1, 0.06, topz), note='Base de TableLamp.')
    p.anchor('clock', (-0.12, -0.09, topz), note='Base de AlarmClock (girarlo hacia la cámara, ver Bed.menu_layout).')
    return p


LAMP_SHADE_Z, LAMP_SHADE_H = 0.262, 0.2


@register
def table_lamp():
    p = Prop('TableLamp', 'Lámpara de mesa', 'bedroom',
             notes='Velador (UI-06): jarrón cerámico verde bronce #6B6A3A de 0,16 x 0,18 m con panza, cuello y anillo '
                   '#A86F3A; pantalla ámbar emisiva (#FFB347 costados, #FF8A2A cara inferior, emisión 1,8-2). Luz: Point '
                   '#FFB347 en el anchor "light" (tercio superior de la pantalla), rango 3-4 m, sin sombras. En Unity la '
                   'malla del velador va en un Rendering Layer propio que su luz excluye (Light.renderingLayerMask sin '
                   'esa capa) para que el jarrón no se queme.')
    p.mat('Ceramic', '#6B6A3A', roughness=0.45)
    p.mat('Ring', C['wood_light'], roughness=0.5)
    p.mat('Brass', C['brass'], roughness=0.4, metallic=0.6)
    p.mat('Shade', C['amber'], emission=1.8, roughness=0.8)
    p.mat('ShadeBottom', '#FF8A2A', emission=2.0, roughness=0.8)
    n = 10
    # ceramic jar: foot, belly, shoulder ring, neck and lip (Ø 0.16 m, 0.18 m tall)
    prof = [(0.05, 0.0), (0.064, 0.016), (0.078, 0.05), (0.08, 0.072), (0.079, 0.094), (0.07, 0.114), (0.056, 0.13),
            (0.036, 0.143), (0.027, 0.152), (0.027, 0.168), (0.035, 0.174), (0.035, 0.18)]
    ring_bands = (3, 10)
    p.add({0: 'Ceramic', 1: 'Ring'}, g_revolve(prof, n=n, ids_fn=lambda i, j: 1 if i in ring_bands else 0))
    p.add('Ceramic', g_disc(0.05, n, 0.0, math.pi / n, up=False))
    p.add('Ring', g_disc(0.035, n, 0.18, math.pi / n))
    p.add('Brass', g_cyl(0.024, 0.02, 0.02, n=8), M((0, 0, 0.18)))
    p.add('Brass', g_cyl(0.009, 0.009, 0.07, n=6), M((0, 0, 0.2)))
    sz, sh = LAMP_SHADE_Z, LAMP_SHADE_H
    p.add(['Shade'] * n + ['Shade', 'ShadeBottom'], g_cyl(0.15, 0.088, sh, n=n), M((0, 0, sz)))
    p.add('Brass', g_cyl(0.154, 0.153, 0.012, n=n, caps=(False, False)), M((0, 0, sz)))
    p.add('Brass', g_cyl(0.091, 0.09, 0.01, n=n, caps=(False, False)), M((0, 0, sz + sh - 0.01)))
    p.add('Brass', g_cyl(0.014, 0.0, 0.026, n=6), M((0, 0, sz + sh)))
    p.anchor('light', (0, 0, sz + sh * 0.8),
             note='Point #FFB347 en el tercio superior de la pantalla, sin sombras; excluir la malla del velador '
                  'de esta luz (Rendering Layers).')
    return p


CLOCK_SCREEN = (0.164, 0.056)


@register
def alarm_clock():
    p = Prop('AlarmClock', 'Reloj despertador digital', 'bedroom', preview={'yaw': 24, 'elev': 24},
             notes='Despertador de cuña (frente inclinado ~15°) con pantalla casi negra #140808 (emisión 0,12) SIN números: '
                   'Unity escribe "03:27" (TMP #FF3B30 con intensidad HDR x2,5, dígitos de 0,046 m = 82 % del alto de la '
                   'pantalla) 1 mm por delante del anchor "screen", con su normal. Contraste dígito/fondo >= 4:1.')
    p.mat('Body', '#2A2D35', roughness=0.45)
    p.mat('Bezel', '#17191E', roughness=0.4)
    p.mat('Screen', '#140808', emission=0.12, roughness=0.2)
    p.mat('Button', C['metal_light'], roughness=0.4, metallic=0.3)
    p.mat('Feet', C['black'], roughness=0.9)
    W, z0 = 0.2, 0.008
    yb, yt, H, c = -0.05, -0.03, 0.1, 0.012          # front foot, front top, height, corner cut
    back = 0.045
    side = [(yb + 0.002, 0.0), (back - c, 0.0), (back, c), (back, H - c), (back - c, H), (yt + c * 0.8, H),
            (yt, H - c), (yb, c)]
    # profile in (y, z) extruded along X: local x -> world y, local y -> world z, local z -> world x
    to_side = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    p.add('Body', g_prism(side, W), at(to_side, (0, 0, z0)))
    for y in (-0.025, 0.02):
        p.add('Feet', g_box(W - 0.04, 0.02, z0), M((0, y, z0 / 2)))
    tilt = math.degrees(math.atan2(yt - yb, H - 2 * c))
    zc = z0 + H / 2
    yc = (yb + yt) / 2
    frame = M((0, yc, zc), (-tilt, 0, 0))
    sw, sh = CLOCK_SCREEN
    p.add('Screen', g_box(sw, 0.006, sh), frame @ M((0, -0.001, 0)))
    for s_ in (-1, 1):
        p.add('Bezel', g_box(sw + 0.016, 0.008, 0.008), frame @ M((0, -0.002, s_ * (sh / 2 + 0.004))))
        p.add('Bezel', g_box(0.008, 0.008, sh), frame @ M((s_ * (sw / 2 + 0.004), -0.002, 0)))
    p.add('Button', g_box(0.07, 0.028, 0.012, chamfer=0.004), M((-0.03, 0.012, z0 + H + 0.004)))
    p.add('Button', g_box(0.022, 0.022, 0.01, chamfer=0.003), M((0.05, 0.012, z0 + H + 0.003)))
    t = math.radians(tilt)
    nrm = (0.0, -math.cos(t), math.sin(t))
    ctr = frame @ Vector((0, -0.004, 0))
    p.anchor('screen', tuple(ctr), normal=nrm, size=(sw, sh),
             note='Centro de la cara de la pantalla (inclinada ~15° hacia atrás); texto "03:27" centrado, sin tocar el bisel.')
    p.extra['clock_text'] = {'text': '03:27', 'color_srgb': '#FF3B30', 'hdr_intensity': 2.5, 'digit_height_m': 0.046,
                             'digit_height_ratio': round(0.046 / sh, 3), 'max_width_m': round(sw - 0.012, 3),
                             'offset_along_normal_m': 0.001, 'screen_color_srgb': '#140808', 'screen_emission': 0.12,
                             'min_contrast': 4.0}
    return p


@register
def window():
    p = Prop('Window', 'Ventana con parteluces', 'bedroom', mount='wall', preview={'elev': 6, 'yaw': 18},
             notes='Ventana de pared (PRP-02 / UI-06), 0,095 m de profundidad total, con vista nocturna detrás de los '
                   'parteluces: cielo #1A2A6A, colinas, pinos, estrellas y luna. Los paños de vista son emisivos (Emission 1) '
                   'para conservar su color con la sala a oscuras; se pueden reemplazar por un fondo real. Dorso en +Y '
                   'contra la pared; base = alféizar.')
    p.mat('Frame', C['wood'])
    p.mat('Trim', C['wood_light'])
    p.mat('Sash', C['wood_pale'])
    p.mat('Sky', '#1A2A6A', emission=1.0, roughness=0.9)
    p.mat('Moon', '#FFF2C4', emission=2.0, roughness=0.9)
    p.mat('HillFar', '#2A4A7C', emission=1.0, roughness=0.9)
    p.mat('HillNear', '#1E4A45', emission=1.0, roughness=0.9)
    W, H = 0.9, 1.25
    ox, oz0, oz1 = 0.35, 0.14, H - 0.14
    fd = 0.08                                   # frame depth: y in [-0.04, 0.04]
    fh = oz1 + 0.08 - 0.06
    for sx in (-1, 1):
        p.add('Frame', g_box(0.1, fd, fh, chamfer=0.012), M((sx * (ox + 0.05), 0, 0.06 + fh / 2)))
    p.add('Frame', g_box(2 * ox, fd, 0.08, chamfer=0.01), M((0, 0, oz0 - 0.04)))
    p.add('Frame', g_box(2 * ox, fd, 0.08, chamfer=0.01), M((0, 0, oz1 + 0.04)))
    p.add('Trim', g_box(W + 0.12, 0.095, 0.06, chamfer=0.012), M((0, -0.0075, 0.03)))       # sill: y -0.055..0.04
    p.add('Trim', g_box(W + 0.1, 0.09, 0.06, chamfer=0.012), M((0, -0.005, H - 0.03)))      # head: y -0.05..0.04
    oh = oz1 - oz0
    t, sd = 0.035, 0.04                          # sash bars: y in [-0.02, 0.02]
    for sx in (-1, 1):
        p.add('Sash', g_box(t, sd, oh), M((sx * (ox - t / 2), 0, oz0 + oh / 2)))
    for z in (oz0 + t / 2, oz1 - t / 2):
        p.add('Sash', g_box(2 * ox - 2 * t, sd, t), M((0, 0, z)))
    zm = oz0 + oh * 0.56
    p.add('Sash', g_box(t, sd, oh - 2 * t), M((0, 0, oz0 + oh / 2)))
    p.add('Sash', g_box(2 * ox - 2 * t, sd, t), M((0, 0, zm)))

    def layer(part, pts, y):
        p.add(part, g_prism(pts, 0.002), front((0, y, 0)))
    layer('Sky', [(-ox, oz0), (ox, oz0), (ox, oz1), (-ox, oz1)], 0.032)
    layer('Moon', [(0.17 + x, 0.93 + y) for x, y in circle(0.065, 10)], 0.029)
    for sx_, sz_ in ((-0.22, 1.02), (-0.08, 0.9), (0.28, 0.76), (-0.26, 0.78), (-0.14, 1.06)):
        layer('Moon', [(sx_, sz_ - 0.013), (sx_ + 0.008, sz_), (sx_, sz_ + 0.013), (sx_ - 0.008, sz_)], 0.029)
    far = [(-ox, oz0), (ox, oz0), (ox, 0.5), (0.2, 0.6), (0.06, 0.53), (-0.1, 0.64), (-0.26, 0.55), (-ox, 0.58)]
    layer('HillFar', far, 0.027)
    near = [(-ox, oz0), (ox, oz0), (ox, 0.4), (0.16, 0.33), (-0.04, 0.42), (-0.2, 0.36), (-ox, 0.4)]
    layer('HillNear', near, 0.025)
    for x, zb, s in ((-0.24, 0.33, 1.0), (-0.16, 0.36, 0.75), (0.24, 0.32, 0.9)):
        layer('HillNear', [(x - 0.04 * s, zb), (x + 0.04 * s, zb), (x, zb + 0.13 * s)], 0.023)
    p.anchor('view', (0, 0.032, (oz0 + oz1) / 2), normal=(0, -1, 0), size=(2 * ox, oh),
             note='Paño de vista (cielo). Para un fondo real, ocultar Sky/Moon/Hill* y poner el fondo detrás.')
    return p


# =============================================================================
# exterior / camp
# =============================================================================

@register
def dock_lamp_post():
    p = Prop('DockLampPost', 'Farol de poste de muelle', 'exterior',
             notes='Poste de muelle con brazo y farol colgante; vidrio emisivo. Luz sugerida: Point ámbar en el farol.')
    p.mat('Wood', C['wood'])
    p.mat('WoodDark', C['wood_dark'])
    p.mat('Rope', C['rope'], roughness=0.9)
    p.mat('Frame', C['iron'], roughness=0.55, metallic=0.35)
    p.mat('Cap', C['metal_dark'], roughness=0.5, metallic=0.35)
    p.mat('Glass', C['amber'], emission=2.5, roughness=0.3)
    p.mat('Flame', '#FFE7A3', emission=4.0, roughness=0.3)
    p.add('WoodDark', g_box(0.3, 0.3, 0.24, chamfer=0.02), M((0, 0, 0.12)))
    p.add('Wood', g_box(0.2, 0.2, 2.3, chamfer=0.02), M((0, 0, 1.15)))
    p.add('WoodDark', g_cyl(sq(0.115), 0.0, 0.1, n=4, phase=math.pi / 4), M((0, 0, 2.3)))
    p.add('Wood', g_box(0.13, 1.0, 0.13, chamfer=0.014), M((0, -0.5, 2.12)))
    p.add('WoodDark', g_box(0.15, 0.06, 0.15, chamfer=0.01), M((0, -0.99, 2.12)))
    a, b = (-0.1, 1.66), (-0.55, 2.06)
    Lb = math.hypot(b[0] - a[0], b[1] - a[1])
    ang = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    p.add('Wood', g_box(0.085, Lb + 0.06, 0.085), M((0, (a[0] + b[0]) / 2, (a[1] + b[1]) / 2), (ang, 0, 0)))
    hy = -0.86
    p.add('Frame', g_box(0.014, 0.014, 0.07), M((0, hy, 2.02)))
    for i, z in enumerate((1.975, 1.94)):
        p.add('Frame', g_box(0.012 if i % 2 == 0 else 0.034, 0.034 if i % 2 == 0 else 0.012, 0.045), M((0, hy, z)))
    s = 1.75
    lz = 1.925 - LANTERN_TOP * s
    add_lantern(p, M((0, hy, lz), (0, 0, 0), s), 'Frame', 'Glass', 'Cap', 'Flame')
    for z in (0.9, 0.96, 1.02):
        p.add('Rope', g_cyl(0.13, 0.13, 0.045, n=8), M((0, 0, z)))
    p.anchor('light', (0, hy, lz + LANTERN_GLASS_Z * s), note='Centro del vidrio: Point #FFB347, rango 5-7 m, sin sombras.')
    return p


@register
def fence_segment():
    p = Prop('FenceSegment', 'Segmento de cerca', 'exterior', preview={'elev': 18},
             notes='Tramo de 2 m: postes centrados en x=±1 m; encadenar tramos cada 2,0 m sobre X.')
    p.extra['tile_pitch_m'] = 2.0
    p.mat('Post', C['wood'])
    p.mat('Rail', C['wood_light'])
    p.mat('PostCap', C['wood_dark'])
    p.mat('Nail', C['iron'])
    # chunky PRP-02 / ENV-01 fence: thick posts, two fat rails nailed to the front
    for sx in (-1, 1):
        p.add('Post', g_box(0.16, 0.16, 1.0, chamfer=0.016), M((sx * 1.0, 0, 0.5)))
        p.add('PostCap', g_cyl(sq(0.085), 0.0, 0.07, n=4, phase=math.pi / 4), M((sx * 1.0, 0, 1.0)))
    for z, tilt in ((0.4, 0.7), (0.78, -0.5)):
        p.add('Rail', g_box(2.04, 0.07, 0.17, chamfer=0.014), M((0, -0.115, z), (0, tilt, 0)))
        for sx in (-1, 1):
            for dz in (-0.04, 0.04):
                p.add('Nail', g_box(0.018, 0.012, 0.018), M((sx * 0.985, -0.153, z + dz - sx * 0.012 * tilt)))
    return p


@register
def signpost():
    p = Prop('Signpost', 'Cartel con flechas', 'exterior',
             notes='Tablas en flecha SIN texto; si hace falta texto va como UI o decal.')
    p.mat('Post', C['wood'])
    p.mat('Board', C['wood_light'])
    p.mat('Groove', C['wood_core'], roughness=0.95)
    p.mat('Nail', C['iron'])
    # chunky PRP-01 / ENV-04 proportions: thick post, two fat arrow boards made of two planks each
    p.add('Post', g_box(0.15, 0.15, 1.8, chamfer=0.018), M((0, 0, 0.9)))
    p.add('Groove', g_cyl(sq(0.075), 0.0, 0.07, n=4, phase=math.pi / 4), M((0, 0, 1.8)))
    tipx, bodyx, hh, back = 0.56, 0.36, 0.15, -0.42
    xa = tipx - (tipx - bodyx) * 0.008 / hh
    top = [(back, 0.008), (xa, 0.008), (bodyx, hh), (back, hh)]
    bot = [(back, -hh), (bodyx, -hh), (xa, -0.008), (back, -0.008)]
    for z, direction, tilt in ((1.5, 1, 3.0), (1.13, -1, -4.0)):
        tp = [(direction * x, y) for x, y in top]
        bp = [(direction * x, y) for x, y in bot]
        m = front((0, -0.105, z), tilt)
        p.add('Board', g_bevel(g_prism(tp, 0.06), 0.012), m)
        p.add('Board', g_bevel(g_prism(bp, 0.06), 0.012), m)
        p.add('Groove', g_box(0.9, 0.016, 0.052), m @ M((direction * 0.06, 0, 0)))
        for y in (-0.075, 0.075):
            p.add('Nail', g_box(0.02, 0.02, 0.012), m @ M((0, y, 0.03)))
    return p


@register
def mailbox():
    p = Prop('Mailbox', 'Buzón rojo', 'exterior',
             notes='Buzón PRP-01: caja de túnel de 0,48 x 0,30 x 0,30 m con frente abierto sobre un poste corto de sección '
                   '0,10 m; bandera roja #C62E36 en un brazo rojo que sobresale 0,18 m sobre el techo (1,10 m en total).')
    p.mat('Body', C['red'], roughness=0.5)
    p.mat('Door', C['red_dark'], roughness=0.5)
    p.mat('Flag', '#C62E36', roughness=0.5)
    p.mat('Metal', C['metal_dark'], roughness=0.5, metallic=0.4)
    p.mat('Post', C['wood'])
    p.mat('Plate', C['wood_dark'])
    p.mat('Inside', '#3A1512', roughness=0.9)
    TOTAL, FLAG_UP = 1.10, 0.18
    W, L, Hb = 0.15, 0.48, 0.30                  # half width, length, height of the box
    hw = Hb - W                                  # straight wall under the arched roof
    z_roof = TOTAL - FLAG_UP                     # 0.92
    z0 = z_roof - Hb                             # 0.62 box bottom
    plate_t = 0.03
    post_h = z0 - plate_t
    p.add('Post', g_box(0.10, 0.10, post_h, chamfer=0.012), M((0, 0.03, post_h / 2)))
    p.add('Plate', g_box(0.2, L - 0.06, plate_t, chamfer=0.008), M((0, 0.0, post_h + plate_t / 2)))
    prof = [(-W, 0.0), (W, 0.0), (W, hw)] + \
        [(W * math.cos(math.radians(a)), hw + W * math.sin(math.radians(a))) for a in (30, 60, 90, 120, 150)] + \
        [(-W, hw)]
    n = len(prof)
    m = front((0, 0, z0))
    p.add('Body', g_prism(prof, L, caps=False), m)
    p.add('Body', ([(x, y, -L / 2) for x, y in prof], [tuple(reversed(range(n)))]), m)       # back cap (+Y)
    cen = (0.0, 0.14)
    ring_v, ring_f, ring_i = g_poly_rings(prof, [1.0, 0.78], z=L / 2, center=cen)
    p.add('Door', (ring_v, [f for f, i in zip(ring_f, ring_i) if i == 0]), m)                  # front rim
    inner = [(cen[0] + (x - cen[0]) * 0.78, cen[1] + (y - cen[1]) * 0.78) for x, y in prof]
    depth = 0.36
    tv, tf = g_prism(inner, depth, z0=L / 2 - depth, caps=False)
    p.add('Inside', (tv, [tuple(reversed(f)) for f in tf]), m)                                  # tunnel walls face inward
    p.add('Inside', ([(x, y, L / 2 - depth) for x, y in inner], [tuple(range(n))]), m)         # back of the tunnel
    for zz in (0.13, -0.13):                                                                    # two rolled bands
        band = [(x * 1.035, y * 1.03 - 0.004) for x, y in prof]
        p.add('Door', g_prism(band, 0.035), m @ M((0, 0, zz)))
    # raised flag: red arm on the right side, 0.18 m above the roof, flag plate fully above the silhouette
    xa, ya = W + 0.027, 0.13
    arm_z0 = z0 + 0.07
    p.add('Metal', g_box(0.03, 0.05, 0.05, chamfer=0.006), M((W + 0.012, ya, arm_z0 + 0.02)))
    p.add('Flag', g_box(0.024, 0.024, TOTAL - arm_z0, chamfer=0.005), M((xa, ya, (arm_z0 + TOTAL) / 2)))
    p.add('Flag', g_box(0.016, 0.15, 0.09, chamfer=0.004), M((xa, ya + 0.012 + 0.075, TOTAL - 0.045)))
    p.extra['flag'] = {'arm_top_above_roof_m': FLAG_UP, 'flag_bottom_above_roof_m': round(TOTAL - 0.09 - z_roof, 3)}
    return p


@register
def picnic_table():
    p = Prop('PicnicTable', 'Mesa de picnic', 'camp')
    p.mat('Top', C['wood_light'])
    p.mat('Leg', C['wood'])
    p.mat('Beam', C['wood_dark'])
    for i in range(5):
        y = -0.32 + i * 0.16
        p.add('Top', g_box(1.8, 0.145, 0.045, chamfer=0.013), M((0, y, 0.7275), (0, 0, p.rng.uniform(-0.4, 0.4))))
    for s in (-1, 1):
        for k in (-1, 1):
            p.add('Top', g_box(1.8, 0.125, 0.045, chamfer=0.013), M((0, s * (0.62 + k * 0.066), 0.4275)))
    for x in (-0.62, 0.62):
        p.add('Beam', g_box(0.07, 1.52, 0.07, chamfer=0.012), M((x, 0, 0.37)))
        p.add('Beam', g_box(0.07, 0.8, 0.05, chamfer=0.01), M((x, 0, 0.68)))
        for s in (-1, 1):
            y0, y1, z0, z1 = s * 0.66, s * 0.14, 0.0, 0.705
            Ll = math.hypot(y1 - y0, z1 - z0)
            ang = math.degrees(math.atan2(y1 - y0, z1 - z0))
            p.add('Leg', g_box(0.05, 0.09, Ll, chamfer=0.012), M((x + 0.06 * (1 if x < 0 else -1), (y0 + y1) / 2, Ll / 2 * math.cos(math.radians(ang))),
                                                                (-ang, 0, 0)))
    return p


@register
def folding_chair():
    p = Prop('FoldingChair', 'Silla plegable', 'camp')
    p.mat('Fabric', '#2F6FC0', roughness=0.85)
    p.mat('FabricDark', '#1F4E91', roughness=0.85)
    p.mat('Frame', C['metal_dark'], roughness=0.5, metallic=0.4)
    r = 0.012

    def tube(a, b, rad=r):
        p.add('Frame', g_loft([a, b], [rad, rad], n=5))
    for sx in (-0.25, 0.25):
        tube((sx, -0.22, 0.0), (sx, -0.22, 0.62))
        tube((sx, 0.22, 0.0), (sx, 0.27, 0.93))
        tube((sx, -0.24, 0.62), (sx, 0.245, 0.62))
        tube((sx, -0.22, 0.05), (sx, 0.22, 0.4))
        tube((sx, 0.22, 0.05), (sx, -0.22, 0.4))
        p.add('FabricDark', g_box(0.06, 0.42, 0.022, chamfer=0.008), M((sx, 0.0, 0.64)))
    for y in (-0.22, 0.22):
        tube((-0.25, y, 0.05), (0.25, y, 0.4))
        tube((0.25, y, 0.05), (-0.25, y, 0.4))
    tube((-0.25, 0.268, 0.92), (0.25, 0.268, 0.92))
    xs = (-0.25, 0.0, 0.25)
    seat = [[(x, y, 0.43 - (0.05 if (x == 0 and y == 0) else 0.025 if (x == 0 or y == 0) else 0.0)) for x in xs]
            for y in (-0.22, 0.0, 0.22)]
    p.add('Fabric', g_sheet(seat, 0.012, (0, 0, 1)))
    back = [[(x, 0.235 + (z - 0.5) * 0.08 + (0.03 if (x == 0 and z == 0.7) else 0.0), z) for x in xs]
            for z in (0.5, 0.7, 0.9)]
    p.add('Fabric', g_sheet(back, 0.012, (0, -1, 0.1)))
    return p


@register
def rowboat():
    p = Prop('Rowboat', 'Bote de remos', 'water', preview={'elev': 32},
             notes='Casco hueco con dos remos; el pivote queda bajo la quilla.')
    p.mat('Hull', C['wood'])
    p.mat('HullInside', C['wood_light'])
    p.mat('Trim', C['wood_dark'])
    p.mat('Seat', C['wood_pale'])
    p.mat('Oar', C['end_grain'])
    p.mat('Metal', C['metal_dark'], roughness=0.5, metallic=0.5)
    xs = [-1.5, -1.2, -0.7, -0.1, 0.5, 1.0, 1.35, 1.6]
    hw = [0.42, 0.52, 0.6, 0.62, 0.57, 0.43, 0.24, 0.0]
    zt = [0.56, 0.54, 0.52, 0.52, 0.53, 0.57, 0.62, 0.68]
    zb = [0.14, 0.08, 0.05, 0.05, 0.06, 0.1, 0.2, 0.42]
    yf = [-1.0, -0.93, -0.62, 0.0, 0.62, 0.93, 1.0]
    zf = [1.0, 0.55, 0.12, 0.0, 0.12, 0.55, 1.0]
    m = len(yf)
    verts = []
    for x, w, t, b in zip(xs, hw, zt, zb):
        for yy, zz in zip(yf, zf):
            verts.append((x, w * yy, b + (t - b) * zz))
    faces = []
    for i in range(len(xs) - 1):
        for j in range(m - 1):
            a, bb = i * m + j, (i + 1) * m + j
            faces.append((a, a + 1, bb + 1, bb))
    faces.append(tuple(reversed(range(m))))
    v, f = merge_close(verts, faces, 1e-5)
    v, f, ids = g_solidify(v, f, 0.035, offset=-1.0)
    p.add({0: 'Hull', 1: 'HullInside', 2: 'Trim'}, (v, f, ids))

    def hw_at(x):
        for (x0, w0), (x1, w1) in zip(zip(xs, hw), zip(xs[1:], hw[1:])):
            if x0 <= x <= x1:
                return w0 + (w1 - w0) * (x - x0) / (x1 - x0)
        return 0.0
    for x, z in ((-1.12, 0.4), (-0.1, 0.36), (0.78, 0.4)):
        w = 2 * hw_at(x) * 0.9
        p.add('Seat', g_box(0.2, w, 0.035, chamfer=0.006), M((x, 0, z)))
    keel = [(x, 0.0, b - 0.028) for x, b in zip(xs[:-1], zb[:-1])] + [(1.6, 0.0, 0.43)]
    p.add('Trim', g_loft(keel, [(0.03, 0.035)] * len(keel), n=4, ref=(0, 1, 0)))
    for s in (-1, 1):
        y = s * 0.22
        p.add('Oar', g_loft([(-0.95, y, 0.44), (0.85, y, 0.46)], [0.018, 0.018], n=6))
        p.add('Oar', g_box(0.42, 0.11, 0.016, chamfer=0.004), M((1.02, y, 0.465)))
        p.add('Metal', g_box(0.03, 0.03, 0.05), M((-0.1, s * (hw_at(-0.1) - 0.01), 0.54)))
    return p


@register
def log_bench():
    p = Prop('LogBench', 'Tronco-banco', 'camp')
    p.mat('Bark', C['bark'])
    p.mat('BarkDark', C['bark_dark'])
    p.mat('Seat', C['wood_pale'])
    p.mat('EndGrain', C['end_grain'])
    p.mat('Ring', C['end_ring'])
    p.mat('Chock', C['wood_dark'])
    R, n, cut, L, zc = 0.21, 10, 0.13, 1.8, 0.31
    poly = []
    for i in range(n):
        a = 2 * math.pi * i / n + math.pi / n
        poly.append((R * math.cos(a), min(R * math.sin(a), cut)))
    parts = []
    for i in range(n):
        z0, z1 = poly[i][1], poly[(i + 1) % n][1]
        parts.append('Seat' if abs(z0 - cut) < 1e-6 and abs(z1 - cut) < 1e-6 else ('Bark' if i % 2 == 0 else 'BarkDark'))
    p.add(parts, g_prism(poly, L, caps=False), at(TO_X, (0, 0, zc)))
    ids = {0: 'Bark', 1: 'EndGrain', 2: 'Ring', 3: 'EndGrain'}
    end_grain(p, poly, [1.0, 0.86, 0.5, 0.42], ids, at(TO_X, (L / 2, 0, zc)))
    end_grain(p, poly, [1.0, 0.86, 0.5, 0.42], ids, at(TO_NX, (-L / 2, 0, zc)))
    for x in (-0.55, 0.55):
        p.add('Chock', g_box(0.22, 0.38, 0.13, chamfer=0.012), M((x, 0, 0.065)))
    p.add('Bark', g_cyl(0.035, 0.022, 0.07, n=5), M((-0.25, -0.19, zc - 0.03), (90, 0, 0)))
    p.add('Bark', g_cyl(0.032, 0.02, 0.065, n=5), M((0.42, 0.19, zc - 0.02), (-90, 0, 0)))
    return p


@register
def firewood():
    p = Prop('Firewood', 'Leña apilada', 'camp', preview={'yaw': 66, 'elev': 22},
             notes='Pila de leña en pirámide 2+1 (LOGS de PRP-01): troncos gruesos de Ø 0,24 m con testas claras '
                   '#D9A566 y anillos concéntricos.')
    p.mat('Bark', C['bark'])
    p.mat('BarkDark', C['bark_dark'])
    p.mat('EndGrain', '#D9A566')
    p.mat('Ring', '#9A6333')
    rng = p.rng
    r = 0.12
    rise = r * math.sqrt(4 - 1.02 ** 2)                  # top log sits in the groove of the two below
    logs = [(-r * 1.02, r, 0.0, 0.74), (r * 1.02, r, -0.03, 0.72), (0.0, r + rise, 0.06, 0.7)]
    ids = {0: 'Bark', 1: 'EndGrain', 2: 'Ring', 3: 'EndGrain', 4: 'Ring', 5: 'EndGrain'}
    scales = [1.0, 0.88, 0.66, 0.57, 0.3, 0.2]
    n = 8
    for k, (y, z, xo, Lk) in enumerate(logs):
        rr = r * rng.uniform(0.97, 1.0)
        poly = circle(rr, n, rng.uniform(0, 6.28))
        parts = ['Bark' if (i + k) % 2 == 0 else 'BarkDark' for i in range(n)]
        p.add(parts, g_prism(poly, Lk, caps=False), at(TO_X, (xo, y, z)))
        end_grain(p, poly, scales, ids, at(TO_X, (xo + Lk / 2, y, z)))
        end_grain(p, poly, scales, ids, at(TO_NX, (xo - Lk / 2, y, z)))
    return p


@register
def campfire():
    p = Prop('Campfire', 'Fogón con piedras', 'camp',
             notes='Fogón PRP-01 / ENV-05: anillo de 8 piedras (Ø 1 m entre centros), tipi de 4 troncos de Ø 0,13-0,14 m '
                   'que sobresalen de las piedras y llama emisiva de 3 capas de 0,95 m: lenguas amarillas #FFD23F hasta el '
                   '55 % de la altura, cuerpo naranja #FF902A y puntas rojo anaranjado #E5482A; brasas. Luz sugerida Point '
                   '#FF8A2A con parpadeo, sin sombras.')
    p.mat('Stone', C['rock'])
    p.mat('StoneDark', C['rock_dark'])
    p.mat('StoneTop', C['rock_light'])
    p.mat('Ash', C['charcoal'], roughness=1.0)
    p.mat('Bark', C['bark'])
    p.mat('EndGrain', '#D9A566')
    p.mat('Char', '#2A201C', roughness=1.0)
    p.mat('FlameCore', '#FFD23F', emission=3.5, roughness=1.0)
    p.mat('Flame', '#FF902A', emission=3.0, roughness=1.0)
    p.mat('FlameTip', '#E5482A', emission=2.6, roughness=1.0)
    p.mat('Ember', C['ember'], emission=2.0, roughness=1.0)
    rng = p.rng
    for k in range(8):
        a = 2 * math.pi * k / 8 + rng.uniform(-0.08, 0.08)
        add_rock(p, (0.5 * math.cos(a), 0.5 * math.sin(a), 0), rng.uniform(0.17, 0.2), rng.uniform(0.14, 0.17),
                 rng.uniform(0.15, 0.19), math.degrees(a) + 90, 'Stone' if k % 2 else 'StoneDark', 'StoneTop', n=12,
                 flat_top=0.12)
    p.add('Ash', g_cyl(0.36, 0.34, 0.025, n=10), M())
    # tipi of 4 chunky logs (Ø 0.14 -> 0.128) resting on the stones: end grain sticks out past the ring,
    # charred tips meet inside the flame
    for k in range(4):
        a = math.radians(10 + 90 * k)
        o = (0.72 * math.cos(a), 0.72 * math.sin(a), 0.18)
        i = (0.06 * math.cos(a + 0.5), 0.06 * math.sin(a + 0.5), 0.47)
        p.add(['Bark'] * 6 + ['EndGrain', 'Char'], g_loft([o, i], [0.07, 0.064], n=6))
    for a in (0.9, 3.9):
        c, s_ = math.cos(a), math.sin(a)
        p.add(['Bark'] * 6 + ['EndGrain', 'EndGrain'],
              g_loft([(0.26 * c - 0.2 * s_, 0.26 * s_ + 0.2 * c, 0.06), (0.26 * c + 0.2 * s_, 0.26 * s_ - 0.2 * c, 0.06)],
                     [0.06, 0.06], n=6))

    def flame(parts, r0, a, H, radii, lean_k, z0):
        """Twisted 5-sided tongue. parts: one part for the whole tongue or [body, tip] (tip = top band)."""
        base = Vector((r0 * math.cos(a), r0 * math.sin(a), z0))
        lean = Vector((-math.cos(a), -math.sin(a), 0)) * lean_k
        pts = [base, base + Vector((0, 0, H * 0.3)) + lean * 0.3, base + Vector((0, 0, H * 0.62)) + lean * 0.8,
               base + Vector((0, 0, H * 0.82)) + lean * 1.1, base + Vector((0, 0, H)) + lean * 1.4]
        geom = g_loft([tuple(q) for q in pts], radii, n=5, twist=0.35, phase=a)
        if isinstance(parts, str):
            p.add(parts, geom)
        else:
            body, tip = parts
            p.add([tip if 15 <= f < 20 else body for f in range(len(geom[1]))], geom)
    # layer 1: yellow tongues own the bottom ring and reach 55 % of the height (0.52 m)
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.2
        flame('FlameCore', 0.14, a, rng.uniform(0.44, 0.48), [0.125, 0.115, 0.08, 0.04, 0.0], -0.06, z0=0.04)
    # layer 2: orange body (central tongue + four side tongues starting higher), red-orange tips
    flame(('Flame', 'FlameTip'), 0.0, 0.0, 0.83, [0.12, 0.14, 0.1, 0.06, 0.0], 0.0, z0=0.12)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.6 + rng.uniform(-0.15, 0.15)
        flame(('Flame', 'FlameTip'), 0.1, a, rng.uniform(0.55, 0.62), [0.09, 0.115, 0.08, 0.04, 0.0], -0.06, z0=0.18)
    # layer 3: red-orange licks above the body
    for k, a in enumerate((1.3, 4.2)):
        flame('FlameTip', 0.05, a, 0.46 - 0.04 * k, [0.035, 0.045, 0.03, 0.015, 0.0], 0.03, z0=0.46)
    for k in range(7):
        a = rng.uniform(0, 2 * math.pi)
        rr = rng.uniform(0.2, 0.32)
        p.add('Ember', g_ico(rng.uniform(0.02, 0.03), 1, scale=(1, 1, 0.6)), M((rr * math.cos(a), rr * math.sin(a), 0.03)))
    p.anchor('light', (0, 0, 0.4), note='Point #FF8A2A con parpadeo, rango 6-8 m, sin sombras.')
    p.extra['flame_layers'] = {'yellow_top_m': 0.52, 'total_height_m': 0.95, 'ring_diameter_m': 1.0,
                               'colors_srgb': {'core': '#FFD23F', 'body': '#FF902A', 'tips': '#E5482A'}}
    return p


# =============================================================================
# nature / water
# =============================================================================

@register
def bush():
    p = Prop('Bush', 'Arbusto', 'nature')
    p.mat('Leaf', C['green'])
    p.mat('LeafDark', C['green_dark'])
    p.mat('LeafLight', C['green_light'])
    puffs = [((0, 0, 0.45), 0.44, 'Leaf'), ((0.38, 0.08, 0.33), 0.33, 'LeafDark'), ((-0.36, 0.06, 0.32), 0.34, 'LeafDark'),
             ((0.1, 0.32, 0.36), 0.32, 'Leaf'), ((-0.06, -0.3, 0.33), 0.32, 'Leaf'), ((0.06, 0.02, 0.74), 0.3, 'LeafLight'),
             ((-0.2, -0.12, 0.6), 0.24, 'LeafLight')]
    for c, r, part in puffs:
        v, f = g_ico(r, 2, scale=(1, 1, 0.88), jitter=0.09, rng=p.rng)
        v = [(x + c[0], y + c[1], max(0.0, z + c[2])) for x, y, z in v]
        p.add(part, (v, f))
    return p


@register
def fern():
    p = Prop('Fern', 'Helecho', 'nature')
    p.mat('Frond', C['green'])
    p.mat('FrondLight', C['green_light'])
    p.mat('Young', C['green_yellow'])
    rng = p.rng
    n = 9
    for i in range(n):
        yaw = i * 360 / n + rng.uniform(-10, 10)
        L = rng.uniform(0.5, 0.62)
        s = rng.uniform(0.9, 1.1)
        hw = [w * s for w in (0.03, 0.055, 0.038, 0.065, 0.045, 0.06, 0.04, 0.045, 0.024)]
        p.add('Frond' if i % 2 else 'FrondLight',
              g_blade(L, hw, pitch=rng.uniform(58, 72), bend=rng.uniform(105, 130), fold=0.18),
              M((0, 0, 0.004), (0, 0, yaw)))
    for i in range(3):
        p.add('Young', g_blade(0.3, [0.02, 0.035, 0.03, 0.018], pitch=80, bend=70, fold=0.2),
              M((0, 0, 0.004), (0, 0, i * 120 + rng.uniform(0, 40))))
    return p


def flower_clump(p, n_blades, blade_len, blade_hw, flowers, head):
    rng = p.rng
    for i in range(n_blades):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.05)
        p.add('Blade', g_blade(rng.uniform(*blade_len), blade_hw, pitch=rng.uniform(55, 80), bend=rng.uniform(30, 60),
                               fold=0.25, thickness=0.003),
              M((r * math.cos(a), r * math.sin(a), 0.003), (0, 0, rng.uniform(0, 360))))
    for i in range(flowers):
        a = 2 * math.pi * i / flowers + rng.uniform(-0.4, 0.4)
        r = rng.uniform(0.01, 0.06)
        b = Vector((r * math.cos(a), r * math.sin(a), 0.0))
        out = Vector((math.cos(a), math.sin(a), 0.0))
        H = rng.uniform(*head['height'])
        top = b + out * rng.uniform(0.03, 0.08) + Vector((0, 0, H))
        mid = b.lerp(top, 0.5) + out * 0.01
        p.add('Stem', g_loft([tuple(b), tuple(mid), tuple(top)], [0.0045, 0.004, 0.0032], n=4))
        d = (out * rng.uniform(0.25, 0.55) + Vector((0, 0, 1))).normalized()
        head['fn'](p, top, d)


def g_petal_head(n, r_tip, r_in, half_ang, shoulder=0.78, cup=0.2, thickness=0.003):
    """Flower head facing +Z: n separate rounded petals (base, shoulder, tip, shoulder, base) fanned
    from the centre, double-sided. half_ang in radians at the shoulders."""
    top = [(0.0, 0.0, 0.0)]
    faces = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ring = [(r_in, a - half_ang * 0.55), (r_tip * shoulder, a - half_ang), (r_tip, a),
                (r_tip * shoulder, a + half_ang), (r_in, a + half_ang * 0.55)]
        idx = []
        for rr, aa in ring:
            top.append((rr * math.cos(aa), rr * math.sin(aa), cup * r_tip * (rr / r_tip) ** 2))
            idx.append(len(top) - 1)
        faces += [(0, idx[0], idx[1]), (0, idx[1], idx[2]), (0, idx[2], idx[3]), (0, idx[3], idx[4])]
    nv = len(top)
    under = [(x, y, z - thickness) for x, y, z in top]
    return top + under, faces + [tuple(reversed([i + nv for i in f])) for f in faces]


@register
def flowers_white():
    p = Prop('FlowersWhite', 'Flores blancas', 'nature',
             notes='Margaritas (PRP-02 / ENV-05): 7 cabezas de Ø 7,6 cm con 10 pétalos redondeados y centro amarillo, '
                   '18 hojas basales.')
    p.mat('Blade', C['green'])
    p.mat('Stem', C['green_dark'])
    p.mat('Petal', C['white'], roughness=0.7)
    p.mat('Center', C['yellow'], roughness=0.7)

    def head(p, top, d):
        m = look_matrix(top, d)
        p.add('Petal', g_petal_head(10, 0.038, 0.011, math.radians(13), cup=0.22), m)
        p.add('Center', g_ico(0.0135, 1, scale=(1, 1, 0.55)), m @ M((0, 0, 0.004)))
    flower_clump(p, 18, (0.12, 0.2), [0.014, 0.02, 0.012], 7, {'height': (0.2, 0.32), 'fn': head})
    return p


@register
def flowers_yellow():
    p = Prop('FlowersYellow', 'Flores amarillas', 'nature',
             notes='Girasoles bajos tipo ENV-05: 3 cabezas de Ø 0,10-0,13 m con 13 pétalos y centro #5A3A1A, hojas anchas '
                   'en el tallo y 16 hojas basales.')
    p.mat('Blade', C['green_light'])
    p.mat('Stem', C['green'])
    p.mat('Leaf', C['green_dark'])
    p.mat('Petal', C['yellow'], roughness=0.7)
    p.mat('Center', '#5A3A1A', roughness=0.85)
    rng = p.rng
    for i in range(16):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.06)
        p.add('Blade', g_blade(rng.uniform(0.15, 0.24), [0.02, 0.028, 0.017], pitch=rng.uniform(40, 70),
                               bend=rng.uniform(40, 70), fold=0.3, thickness=0.003),
              M((r * math.cos(a), r * math.sin(a), 0.003), (0, 0, rng.uniform(0, 360))))
    heads = [((0.0, 0.02), 0.5, 0.065, -0.2), ((0.1, -0.05), 0.4, 0.057, -0.9), ((-0.1, -0.04), 0.33, 0.05, 0.6)]
    for (x, y), H, rt, a in heads:
        b = Vector((x, y, 0.0))
        out = Vector((math.sin(a), -math.cos(a), 0.0))          # heads lean toward the front (-Y) and sideways
        top = b + out * 0.07 + Vector((0, 0, H))
        mid = b.lerp(top, 0.5) + out * 0.015
        p.add('Stem', g_loft([tuple(b), tuple(mid), tuple(top)], [0.008, 0.007, 0.0055], n=5))
        for k, (t, side) in enumerate(((0.35, 1), (0.6, -1))):
            q = b.lerp(top, t)
            yaw = math.degrees(math.atan2(out.y, out.x)) - 90 + side * 70
            p.add('Leaf', g_blade(0.13, [0.03, 0.04, 0.026], pitch=25, bend=45, fold=0.3, thickness=0.003),
                  M(tuple(q), (0, 0, yaw)))
        d = (out * 0.85 + Vector((0, 0, 0.55))).normalized()
        m = look_matrix(top, d)
        p.add('Petal', g_petal_head(13, rt, rt * 0.42, math.radians(10.5), shoulder=0.8, cup=0.12, thickness=0.004), m)
        p.add('Center', g_cyl(rt * 0.47, rt * 0.36, 0.016, n=10), m @ M((0, 0, -0.002)))
    return p


@register
def mushrooms():
    p = Prop('Mushrooms', 'Hongos', 'nature')
    p.mat('Stem', C['cream'])
    p.mat('Cap', C['red'], roughness=0.6)
    p.mat('Spot', C['white'], roughness=0.6)
    p.mat('Gill', '#D8C6A4')
    p.mat('Grass', C['green'])
    rng = p.rng
    specs = [((0.0, 0.0), 0.15, 0.085, 5, (-6, 4)), ((0.13, 0.05), 0.095, 0.06, 4, (8, -5)), ((-0.09, 0.08), 0.065, 0.045, 3, (-4, -9))]
    for (x, y), h, cr, spots, (tx, ty) in specs:
        m = M((x, y, 0), (tx, ty, rng.uniform(0, 360)))
        p.add('Stem', g_loft([(0, 0, 0), (0, 0, h * 0.55), (0, 0, h + 0.005)], [cr * 0.36, cr * 0.3, cr * 0.28], n=6), m)
        cz = [h, h + cr * 0.22, h + cr * 0.45, h + cr * 0.62, h + cr * 0.7]
        p.add('Cap', g_loft([(0, 0, z) for z in cz], [cr, cr * 0.96, cr * 0.78, cr * 0.45, 0.0], n=8, cap_start=False), m)
        p.add('Gill', g_cyl(cr * 0.97, cr * 0.3, 0.02, n=8, phase=math.pi / 2), m @ M((0, 0, h - 0.004)))
        for s in range(spots):
            th = math.radians(rng.uniform(18, 62))
            ph = 2 * math.pi * s / spots + rng.uniform(-0.3, 0.3)
            a, c = cr, cr * 0.7
            pt = Vector((a * math.sin(th) * math.cos(ph), a * math.sin(th) * math.sin(ph), h + c * math.cos(th)))
            nr = Vector((math.sin(th) * math.cos(ph) / a, math.sin(th) * math.sin(ph) / a, math.cos(th) / c)).normalized()
            p.add('Spot', g_cyl(cr * 0.16, cr * 0.1, cr * 0.07, n=6), m @ look_matrix(pt - nr * cr * 0.035, nr))
    for i in range(5):
        a = rng.uniform(0, 360)
        p.add('Grass', g_blade(rng.uniform(0.1, 0.16), [0.01, 0.012, 0.006], pitch=rng.uniform(55, 75), bend=40, fold=0.25,
                               thickness=0.003),
              M((rng.uniform(-0.1, 0.12), rng.uniform(-0.1, 0.1), 0.003), (0, 0, a)))
    return p


@register
def reeds():
    p = Prop('Reeds', 'Totoras', 'water', notes='Totoras/juncos de orilla; apoyar en el borde del agua.')
    p.mat('Stem', '#4E7A34')
    p.mat('Head', '#6B3F22')
    p.mat('Blade', C['reed_green'])
    p.mat('BladeDark', C['green_dark'])
    rng = p.rng
    for i in range(7):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.15)
        x, y = r * math.cos(a), r * math.sin(a)
        H = rng.uniform(0.95, 1.42)
        lx, ly = rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08)
        top = Vector((x + lx, y + ly, H))
        mid = Vector((x + lx * 0.4, y + ly * 0.4, H * 0.5))
        p.add('Stem', g_loft([(x, y, 0.0), tuple(mid), tuple(top)], [0.009, 0.008, 0.006], n=4))
        d = (top - mid).normalized()
        hb = top - d * 0.24
        pts = [hb + d * (0.17 * t) for t in (0.0, 0.15, 0.5, 0.85, 1.0)]
        p.add('Head', g_loft([tuple(q) for q in pts], [0.011, 0.026, 0.03, 0.026, 0.011], n=6))
        p.add('Stem', g_loft([tuple(top - d * 0.07), tuple(top + d * 0.06)], [0.004, 0.0], n=4))
    # dense clump of broad leaves around the stems (PRP-02 'reeds' / ENV-04 shore)
    for i in range(18):
        a = 2 * math.pi * i / 18 + rng.uniform(-0.15, 0.15)
        r = rng.uniform(0.02, 0.16)
        tall = i % 3 == 0
        p.add('Blade' if i % 3 else 'BladeDark',
              g_blade(rng.uniform(0.8, 1.1) if tall else rng.uniform(0.45, 0.75), [0.03, 0.036, 0.028, 0.014],
                      pitch=rng.uniform(72, 86) if tall else rng.uniform(58, 74), bend=rng.uniform(30, 70),
                      fold=0.3, thickness=0.003),
              M((r * math.cos(a), r * math.sin(a), 0.003), (0, 0, math.degrees(a) - 90 + rng.uniform(-30, 30))))
    return p


@register
def lily_pads():
    p = Prop('LilyPads', 'Nenúfares', 'water', preview={'elev': 50},
             notes='Hojas flotantes con flor; la cara inferior de las hojas es el nivel del agua (pivote).')
    p.mat('Pad', C['pad_green'], roughness=0.6)
    p.mat('PadLight', C['green_light'], roughness=0.6)
    p.mat('Petal', C['white'], roughness=0.7)
    p.mat('PetalPink', C['pink'], roughness=0.7)
    p.mat('Center', C['yellow'], roughness=0.7)
    pads = [((0, 0), 0.22, 20, 'Pad'), ((0.33, 0.1), 0.16, 160, 'PadLight'), ((-0.28, 0.2), 0.15, 250, 'Pad'),
            ((0.02, -0.34), 0.12, 300, 'PadLight'), ((-0.3, -0.2), 0.1, 40, 'Pad')]
    for (x, y), r, yaw, part in pads:
        notch = math.radians(34)
        n = 14
        pts = [(0.0, 0.0)] + [(r * math.cos(notch / 2 + (2 * math.pi - notch) * i / (n - 1)),
                               r * math.sin(notch / 2 + (2 * math.pi - notch) * i / (n - 1))) for i in range(n)]
        p.add(part, g_prism(pts, 0.012, z0=0.0), M((x, y, 0), (0, 0, yaw)))
    fx, fy, fz = 0.04, 0.03, 0.012
    p.add('Petal', g_fan(8, 0.075, 0.035, cup=0.45, thickness=0.004), M((fx, fy, fz + 0.004)))
    p.add('PetalPink', g_fan(8, 0.048, 0.02, cup=0.9, thickness=0.004), M((fx, fy, fz + 0.012), (0, 0, 22.5)))
    p.add('Center', g_ico(0.016, 1, scale=(1, 1, 0.7)), M((fx, fy, fz + 0.028)))
    return p


@register
def rock_cluster():
    p = Prop('RockCluster', 'Conjunto de rocas', 'nature')
    p.mat('Rock', C['rock'])
    p.mat('RockDark', C['rock_dark'])
    p.mat('RockTop', C['rock_light'])
    # PRP-02 'rock cluster': one big rounded boulder with a flattish crown, a medium and small ones
    add_rock(p, (0.0, 0.05, 0.0), 0.62, 0.52, 0.72, 10, 'Rock', 'RockTop', n=22, flat_top=0.16)
    add_rock(p, (0.66, -0.22, 0.0), 0.4, 0.34, 0.44, 40, 'RockDark', 'RockTop', n=16, flat_top=0.1)
    add_rock(p, (-0.6, -0.26, 0.0), 0.3, 0.26, 0.3, -20, 'Rock', 'RockTop', n=14)
    add_rock(p, (0.22, -0.56, 0.0), 0.18, 0.16, 0.15, 70, 'RockDark', 'RockTop', n=10)
    add_rock(p, (-0.26, 0.5, 0.0), 0.15, 0.13, 0.13, 15, 'Rock', 'RockTop', n=10)
    return p


@register
def tree_stump():
    p = Prop('TreeStump', 'Tocón', 'nature')
    p.mat('Bark', C['bark'])
    p.mat('BarkDark', C['bark_dark'])
    p.mat('Wood', C['end_grain'])
    p.mat('Ring', C['end_ring'])
    p.mat('Grass', C['green'])
    rng = p.rng
    n = 10
    prof = [(0.35, 0.0), (0.3, 0.08), (0.275, 0.22), (0.27, 0.42), (0.262, 0.5)]
    p.add({0: 'Bark', 1: 'BarkDark'}, g_revolve(prof, n=n, phase=0.0, ids_fn=lambda i, j: j % 2))
    top = circle(0.262, n, 0.0)
    end_grain(p, top, [1.0, 0.86, 0.6, 0.5], {0: 'Bark', 1: 'Wood', 2: 'Ring', 3: 'Wood'}, M((0, 0, 0.5)))
    p.add('BarkDark', g_disc(0.35, n, 0.0, 0.0, up=False))
    for k in range(5):
        a = math.radians(k * 72 + rng.uniform(-15, 15))
        c, s = math.cos(a), math.sin(a)
        p.add('Bark' if k % 2 else 'BarkDark',
              g_loft([(0.2 * c, 0.2 * s, 0.17), (0.36 * c, 0.36 * s, 0.06), (0.52 * c, 0.52 * s, 0.0)], [0.075, 0.05, 0.0], n=4))
    for i in range(4):
        a = rng.uniform(0, 2 * math.pi)
        p.add('Grass', g_blade(rng.uniform(0.14, 0.2), [0.012, 0.014, 0.008], pitch=rng.uniform(55, 75), bend=40, fold=0.25,
                               thickness=0.003),
              M((0.36 * math.cos(a), 0.36 * math.sin(a), 0.003), (0, 0, rng.uniform(0, 360))))
    return p


# =============================================================================
# v0.3.0 scenes (menu bedroom and lobby "sala")
# =============================================================================

# Side-lying sleeper under the BedSleeper quilt (bed coordinates, metres; head toward +Y, the sleeper faces -X).
# Height of the quilt mound above the mattress top along the bed, and its centre across it (knees forward, -X).
SLEEPER_MOUND = [  # (y, height above mattress, centre x, half width)
    (-0.95, 0.10, 0.02, 0.26), (-0.86, 0.19, 0.00, 0.27), (-0.72, 0.23, -0.02, 0.28), (-0.55, 0.25, -0.05, 0.29),
    (-0.38, 0.28, -0.08, 0.30), (-0.22, 0.30, -0.06, 0.31), (-0.06, 0.35, -0.01, 0.31), (0.08, 0.36, 0.01, 0.31),
    (0.20, 0.32, 0.02, 0.30), (0.32, 0.37, 0.02, 0.31), (0.42, 0.41, 0.02, 0.31), (0.50, 0.40, 0.02, 0.30),
]
SLEEPER_HEAD = (0.0, 0.70, 0.985)    # head centre resting on the pillow (bed coordinates)
PILLOW_SLEEPER = (0.64, 0.44, 0.24)  # fluffier than Bed's pillow: the propped head of UI-06
SLEEPER_FOLD_Y = 0.50                # quilt edge under the chin (turned-down sheet above it)
QUILT_CLEARANCE = 0.035              # quilt above the posed sleeper's highest point in each cell
SLEEP_ENVELOPE = Path(__file__).resolve().parents[2] / 'characters' / 'menu' / 'sleep_envelope.json'


def load_sleep_envelope():
    if not SLEEP_ENVELOPE.exists():
        return None
    data = json.loads(SLEEP_ENVELOPE.read_text(encoding='utf-8'))
    assert data.get('schema') == 'lms.menu.sleep-envelope/1', 'unexpected sleep envelope'
    return data


@register
def bed_sleeper():
    p = Prop('BedSleeper', 'Cama con durmiente (acolchado alto)', 'bedroom', preview={'yaw': -60, 'elev': 22},
             notes='Variante de la cama del menú (UI-06): mismo marco de una plaza que Bed, acolchado azul abullonado '
                   'que cubre a un durmiente acostado de costado (cabeza sobre la almohada hacia +Y, mirando a -X, '
                   'rodillas hacia -X) hasta los hombros, con la sábana doblada bajo el mentón. Anchors: "sleeper_head" '
                   '(centro de la cabeza apoyada), "quilt_edge" (borde del acolchado bajo el mentón).')
    p.mat('Wood', C['wood'])
    p.mat('WoodLight', C['wood_light'])
    p.mat('Mattress', '#E9E1D0', roughness=0.9)
    p.mat('Sheet', C['cream'], roughness=0.9)
    p.mat('Quilt', '#6C86D6', roughness=0.9)
    p.mat('QuiltDark', '#4A63B5', roughness=0.9)
    p.mat('QuiltBand', '#3F58A8', roughness=0.9)
    p.mat('Pillow', '#F2EEE6', roughness=0.9)
    rng = p.rng
    W, L, post = BED_W, BED_L, 0.1
    hx, hy = W / 2 - post / 2, L / 2 - post / 2
    w_in = W - 2 * post
    rail_z0, rail_z1 = 0.25, 0.43
    foot_top = 0.60
    m_z0, m_z1 = 0.33, 0.62
    Wm, Lm = W - 0.14, L - 2 * post - 0.01
    q_side = 0.58
    for sx in (-1, 1):
        p.add('Wood', g_box(post, post, 1.1, chamfer=0.015), M((sx * hx, hy, 0.55)))
        p.add('WoodLight', g_box(post + 0.03, post + 0.03, 0.04, chamfer=0.012), M((sx * hx, hy, 1.12)))
        p.add('Wood', g_box(post, post, 0.76, chamfer=0.015), M((sx * hx, -hy, 0.38)))
        p.add('WoodLight', g_box(post + 0.03, post + 0.03, 0.04, chamfer=0.012), M((sx * hx, -hy, 0.78)))
        p.add('Wood', g_box(0.06, L - 2 * post + 0.004, rail_z1 - rail_z0, chamfer=0.015),
              M((sx * (W / 2 - 0.03), 0, (rail_z0 + rail_z1) / 2)))
    p.add('Wood', g_box(w_in, 0.05, 0.5, chamfer=0.012), M((0, hy, 0.69)))
    p.add('WoodLight', g_box(w_in, 0.07, 0.06, chamfer=0.012), M((0, hy, 0.47)))
    xs = [w_in / 2 - w_in * i / 8 for i in range(9)]
    arch = [(-w_in / 2, 0.9), (w_in / 2, 0.9)] + [(x, 0.98 + 0.07 * math.cos(math.pi * x / w_in)) for x in xs]
    p.add('WoodLight', g_bevel(g_prism(arch, 0.08), 0.012), front((0, hy, 0)))
    p.add('Wood', g_box(w_in, 0.05, 0.3, chamfer=0.012), M((0, -hy, 0.42)))
    p.add('WoodLight', g_box(w_in + 0.004, 0.08, 0.06, chamfer=0.015), M((0, -hy, foot_top - 0.03)))
    p.add('Mattress', g_box(Wm, Lm, m_z1 - m_z0, chamfer=0.03), M((0, 0, (m_z0 + m_z1) / 2)))

    # quilt: a height field over the mattress. With menu/sleep_envelope.json (written by the menu human export) every
    # vertex clears the posed sleeper by QUILT_CLEARANCE; without it the default SLEEPER_MOUND profile is used.
    envelope = load_sleep_envelope()
    fold_y = envelope['fold_bed_y_m'] if envelope else SLEEPER_FOLD_Y

    def mound(y):
        pts = SLEEPER_MOUND
        if y <= pts[0][0]:
            return pts[0][1:]
        for (y0, h0, c0, w0), (y1, h1, c1, w1) in zip(pts, pts[1:]):
            if y <= y1:
                t = (y - y0) / (y1 - y0)
                return h0 + (h1 - h0) * t, c0 + (c1 - c0) * t, w0 + (w1 - w0) * t
        return pts[-1][1:]

    def required(x, y):
        if not envelope:
            return None
        best = None
        for yy, row in zip(envelope['rows_y'], envelope['max_z']):
            if abs(yy - y) > 0.085:
                continue
            for xx, z in zip(envelope['cols_x'], row):
                if z is not None and abs(xx - x) <= 0.09 and (best is None or z > best):
                    best = z
        return None if best is None else best + QUILT_CLEARANCE

    y_hem = -Lm / 2 - 0.012
    body_rows = [round(-0.95 + 0.08 * k, 3) for k in range(int((fold_y + 0.95) / 0.08) + 1)]
    rows = [y_hem] + body_rows + [fold_y + 0.03, fold_y + 0.06]
    xq = [-(Wm / 2 + 0.012), -(Wm / 2 - 0.02), -0.28, -0.2, -0.12, -0.04, 0.04, 0.12, 0.2, 0.28, Wm / 2 - 0.02, Wm / 2 + 0.012]
    cols, nrow = len(xq), len(rows)
    heights = []
    for i, y in enumerate(rows):
        h, cx, hw = mound(min(y, fold_y))
        row = []
        for j, x in enumerate(xq):
            edge = j in (0, cols - 1)
            if i == 0:
                z = 0.48 if edge else m_z1 + 0.06
            elif edge:
                z = q_side
            else:
                u = (x - cx) / hw
                z = m_z1 + 0.035 + (0.0 if envelope else h * max(0.0, 1 - u * u) ** 0.55)
                need = required(x, min(y, fold_y + 0.02))
                if need is not None:
                    z = max(z, need)
                z += rng.uniform(-0.005, 0.005)
            row.append(z)
        heights.append(row)
    # soften the lumps once (never below what the sleeper needs) so the quilt drapes instead of spiking
    smooth_h = [r[:] for r in heights]
    for i in range(1, nrow - 1):
        for j in range(2, cols - 2):
            avg = (heights[i][j] * 2 + heights[i - 1][j] + heights[i + 1][j] + heights[i][j - 1] + heights[i][j + 1]) / 6
            need = required(xq[j], min(rows[i], fold_y + 0.02))
            smooth_h[i][j] = max(avg, need if need is not None else 0.0)
    # rows next to the rails fall toward the drape so the quilt reads soft at the sides
    for i in range(1, nrow):
        for j in (1, cols - 2):
            smooth_h[i][j] = max(q_side + 0.04, min(smooth_h[i][j], smooth_h[i][j + (1 if j == 1 else -1)] - 0.05))
    grid = []
    for i, y in enumerate(rows):
        row = []
        for j, x in enumerate(xq):
            z = smooth_h[i][j]
            if i == nrow - 1 and 0 < j < cols - 1:
                z -= 0.035          # the turned-down sheet lip under the chin
            row.append((x, y, z))
        grid.append(row)
    verts = [pt for row in grid for pt in row]
    faces, parts = [], []
    for i in range(nrow - 1):
        for j in range(cols - 1):
            a = i * cols + j
            faces.append((a, a + 1, a + cols + 1, a + cols))
            if i >= nrow - 2:
                parts.append('Sheet')
            elif i == 1:
                parts.append('QuiltBand')
            elif i == 0 or j in (0, cols - 2):
                parts.append('QuiltDark')
            else:
                parts.append('Quilt')
    p.add(parts, (verts, faces))
    # hem under the lip so the sheet edge is not a paper-thin plane
    p.add('Sheet', ([(x, rows[-1], smooth_h[-1][j] - 0.035) for j, x in enumerate(xq)] +
                    [(x, rows[-1], q_side) for x in xq],
                    [(j, j + 1, cols + j + 1, cols + j) for j in range(cols - 1)]))
    PW, PD, PH = PILLOW_SLEEPER
    py = hy - 0.04 - PD / 2 - 0.005
    pz = m_z1 + PH / 2 - 0.006
    p.add('Pillow', g_cushion(PILLOW_SLEEPER), M((0, py, pz), (8, 0, 0)))
    p.anchor('sleeper_head', SLEEPER_HEAD, normal=(-1, 0, 0),
             note='Centro de la cabeza del durmiente apoyada en la almohada; la cara mira a -X (Blender) / +X (Unity).')
    edge = max(grid[-2][j][2] for j in range(cols))
    p.anchor('quilt_edge', (0.0, fold_y + 0.03, edge), normal=(0, 0, 1), note='Borde superior del acolchado bajo el mentón.')
    if envelope:
        p.anchor('sleeper_root', envelope['root_bed_m'], normal=envelope['forward_bed'], up=envelope['up_bed'],
                 note='Raíz del humano de menú acostado (menu/sleep_envelope.json): la normal es el frente del actor y '
                      '"up" su eje vertical de pie (hacia la cabecera). Rotación Unity = LookRotation(normal, up).')
    p.extra['sleeper_mound'] = {'envelope': 'art_source/unity/characters/menu/sleep_envelope.json' if envelope else None,
                                'envelope_contract': envelope['contract'] if envelope else None,
                                'clearance_m': QUILT_CLEARANCE, 'mattress_top': m_z1, 'fold_y': fold_y,
                                'head': envelope['head_centre_bed_m'] if envelope else SLEEPER_HEAD}
    return p


@register
def rug_striped_red_blue():
    p = Prop('RugStripedRedBlue', 'Alfombra rayada roja y azul', 'interior', preview={'elev': 55},
             notes='Alfombra grande de la sala (UI-06 / ENV-05): franjas anchas #8E2A2A y #2D4F9A a lo ancho, filete '
                   'crema y borde azul marino. 3,0 x 2,0 m; se puede escalar en X/Z (es plana).')
    p.mat('Red', '#8E2A2A', roughness=0.95)
    p.mat('Blue', '#2D4F9A', roughness=0.95)
    p.mat('Line', C['cream'], roughness=0.95)
    p.mat('Border', C['navy'], roughness=0.95)
    W, H = 3.0, 2.0
    border, line = 0.1, 0.03
    inner = W / 2 - border - line
    n = 9
    band = 2 * inner / n
    stripes = [-inner + band * k for k in range(n + 1)]

    def part(x, y):
        dx, dy = W / 2 - abs(x), H / 2 - abs(y)
        m = min(dx, dy)
        if m < border:
            return 'Border'
        if m < border + line:
            return 'Line'
        for k in range(n):
            if stripes[k] <= x <= stripes[k + 1]:
                return 'Red' if k % 2 == 0 else 'Blue'
        return 'Red'
    xc = [-W / 2, W / 2, -W / 2 + border, W / 2 - border] + stripes
    yc = [-H / 2, H / 2, -H / 2 + border, H / 2 - border, -H / 2 + border + line, H / 2 - border - line]
    rug(p, W, H, 0.015, xc, yc, part, 'Border')
    return p
