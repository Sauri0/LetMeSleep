"""Deterministic editable mesh recipe. Stdlib only; Unity coordinates in metres.

Run with Python, then import generated_exterior.json in Unity through
AlfaQualityExterior.Build. Blender reconstruction is a separate slotted operation.
"""
import hashlib
import json
import math
from pathlib import Path
import random

HERE = Path(__file__).resolve().parent
TAU = math.tau
PALETTE = {
    'Bark': (.22, .145, .095), 'BarkLight': (.32, .225, .14),
    'NeedleDeep': (.105, .20, .16), 'Needle': (.20, .32, .245),
    'NeedleTip': (.29, .40, .28), 'Grass': (.25, .34, .19),
    'GrassTip': (.39, .44, .26), 'Leaf': (.26, .37, .25),
    'Stone': (.37, .40, .39), 'StoneLight': (.46, .47, .42),
    'Earth': (.265, .235, .18), 'SoilEdge': (.20, .18, .145),
    'Masonry': (.49, .46, .39), 'Timber': (.34, .23, .145),
    'Lawn': (.22, .295, .185), 'LawnSoft': (.235, .306, .195),
    'PathBed': (.29, .285, .245), 'Slate': (.39, .405, .375),
}


def add(a, b): return tuple(x + y for x, y in zip(a, b))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def mul(a, s): return tuple(x * s for x in a)
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def dot(a, b): return sum(x*y for x, y in zip(a, b))
def unit(a): return mul(a, 1/math.sqrt(dot(a, a)))
def vec(p): return dict(zip(('x', 'y', 'z'), (round(x, 6) for x in p)))


class Mesh:
    def __init__(self, name):
        self.name, self.vertices, self.groups = name, [], {}

    def face(self, points, material, outward=None, double=False):
        points = list(points)
        if outward is not None and dot(cross(sub(points[1], points[0]), sub(points[2], points[0])), outward) < 0:
            points.reverse()
        offset = len(self.vertices)
        self.vertices.extend(points)
        triangles = self.groups.setdefault(material, [])
        for i in range(1, len(points)-1):
            triangles.extend((offset, offset+i, offset+i+1))
        # Explicit backface triangles for leaves; no globally double-sided material.
        if double:
            self.face(points[::-1], material)

    def rings(self, rings, material):
        n = len(rings[0])
        for low, high in zip(rings, rings[1:]):
            center = mul(tuple(sum(v[a] for v in low+high) for a in range(3)), 1/(2*n))
            for i in range(n):
                j = (i+1) % n
                points = (low[i], low[j], high[j], high[i])
                outward = sub(mul(add(low[i], low[j]), .5), center)
                # Radial direction makes trunk, stone and crown facets consistently outward.
                self.face(points, material, (outward[0], 0, outward[2]))
        self.face(rings[0], material, (0, -1, 0))
        self.face(rings[-1], material, (0, 1, 0))

    def data(self):
        return dict(name=self.name, vertices=[vec(v) for v in self.vertices],
                    submeshes=[dict(material=m, triangles=t) for m, t in self.groups.items()])


def ring(center, rx, rz, n=9, phase=0, irregular=None):
    return [add(center, (math.cos(i*TAU/n+phase)*rx*(irregular[i] if irregular else 1), 0,
                         math.sin(i*TAU/n+phase)*rz*(irregular[i] if irregular else 1))) for i in range(n)]


def rock(mesh, center, scale, seed, material='Stone'):
    rng = random.Random(seed)
    n = 7 + seed % 3
    irregular = [rng.uniform(.77, 1.15) for _ in range(n)]
    rx, height, rz = scale
    rings = []
    for y, radius, shift in ((0, .72, 0), (.22, 1, -.07), (.76, .79, .12), (1, .39, .17)):
        rings.append(ring(add(center, (shift*rx, y*height, shift*rz)), rx*radius, rz*radius,
                          n, .17, irregular))
    mesh.rings(rings, material)


def stem(mesh, start, end, radius, material='Bark', sides=7):
    axis = unit(sub(end, start))
    tangent = unit(cross(axis, (0, 0, 1) if abs(axis[2]) < .9 else (1, 0, 0)))
    bitangent = cross(axis, tangent)
    rings = [[add(c, add(mul(tangent, math.cos(i*TAU/sides)*r), mul(bitangent, math.sin(i*TAU/sides)*r)))
              for i in range(sides)] for c, r in ((start, radius), (end, radius*.35))]
    for i in range(sides):
        j = (i+1) % sides
        mesh.face((rings[0][i], rings[0][j], rings[1][j], rings[1][i]), material,
                  sub(rings[0][i], start))
    mesh.face(rings[0], material, mul(axis, -1))
    mesh.face(rings[1], material, axis)


def pine(variant, garden=True):
    mesh = Mesh(('Pine_' if garden else 'Pine_Wild_')+'%02d'%variant)
    drop=0 if garden else .55
    rng = random.Random(410 + variant)
    # One connected crown with drooping radial branch lobes. No individual pointed
    # gems. Lower skirt above eye height opens the fixed patio mosquito view.
    stem(mesh, (0, 0, 0), (.025, 3.30-drop, -.016), .105)
    n=18
    lobes=[1+.12*math.cos(i*TAU/6+.4*variant)+rng.uniform(-.045,.045) for i in range(n)]
    profile=((2.03,.16),(2.05,.91),(2.20,.78),(2.43,.57),(2.49,.39),
             (2.51,.72),(2.69,.59),(2.90,.36),(2.93,.47),(3.12,.32),(3.38,.018))
    rings=[]
    for j,(y,radius) in enumerate(profile):
        phase=variant*.29
        shift=.055*math.sin(y*2+variant)
        rings.append([(math.cos(i*TAU/n+phase)*radius*lobes[i]+shift,
                       y-drop+(.04*math.sin(i*TAU/6+variant) if radius>.3 else 0),
                       math.sin(i*TAU/n+phase)*radius*lobes[i]) for i in range(n)])
    for j,(lower,upper) in enumerate(zip(rings,rings[1:])):
        for i in range(n):
            next_i=(i+1)%n
            normal=(math.cos((i+.5)*TAU/n+variant*.29),0,math.sin((i+.5)*TAU/n+variant*.29))
            material='NeedleDeep' if j in (0,4,7) else ('NeedleTip' if i%6 in (1,2) else 'Needle')
            mesh.face((lower[i],lower[next_i],upper[next_i],upper[i]),material,normal)
    mesh.face(rings[0],'NeedleDeep',(0,-1,0))
    mesh.face(rings[-1],'NeedleTip',(0,1,0))
    for i in range(6):
        angle=i*TAU/6+variant*.29
        # Branches rise from the trunk into the crown; contact ends are hidden inside it.
        start=(.01,1.92-drop+(i%2)*.08,0)
        end=(math.cos(angle)*.69,2.14-drop,math.sin(angle)*.69)
        stem(mesh,start,end,.024,'BarkLight',6)
    return mesh


def grass(variant):
    mesh, rng = Mesh('Grass_%02d' % variant), random.Random(90+variant)
    for i in range(7+variant):
        angle = rng.uniform(0, TAU)
        base = (rng.uniform(-.11, .11), 0, rng.uniform(-.1, .1))
        height, bend, width = rng.uniform(.18, .34), rng.uniform(.05, .15), rng.uniform(.022, .036)
        side = (math.cos(angle)*width, 0, math.sin(angle)*width)
        direction = (-math.sin(angle), 0, math.cos(angle))
        mid = add(base, add(mul(direction, bend*.32), (0, height*.64, 0)))
        tip = add(base, add(mul(direction, bend), (0, height, 0)))
        ridge = add(mid, mul(direction, .009))
        mat = 'GrassTip' if i % 4 == 0 else 'Grass'
        mesh.face((sub(base, side), add(base, side), ridge), mat, double=True)
        mesh.face((add(base, side), add(mid, mul(side, .60)), ridge), mat, double=True)
        mesh.face((sub(base, side), ridge, sub(mid, mul(side, .60))), mat, double=True)
        mesh.face((sub(mid, mul(side, .60)), ridge, tip), mat, double=True)
        mesh.face((ridge, add(mid, mul(side, .60)), tip), mat, double=True)
    return mesh


def herb(variant):
    mesh, rng = Mesh('Herb_%02d' % variant), random.Random(270+variant)
    for i in range(6):
        angle = i*2.399 + variant
        base = (rng.uniform(-.13, .13), 0, rng.uniform(-.13, .13))
        height = rng.uniform(.19, .43)
        top = add(base, (math.cos(angle)*.13, height, math.sin(angle)*.13))
        stem(mesh, base, top, .009, 'Leaf', 5)
        for level in (.55, 1):
            origin = add(base, mul(sub(top, base), level))
            direction = (math.cos(angle+level*4), .27, math.sin(angle+level*4))
            tip = add(origin, mul(direction, .18))
            mid = add(origin, mul(direction, .09))
            side = (-direction[2]*.047, -.011, direction[0]*.047)
            ridge = add(mid, (0, .019, 0))
            mesh.face((origin, sub(mid, side), tip, ridge), 'Leaf', double=True)
            mesh.face((origin, ridge, tip, add(mid, side)), 'NeedleTip', double=True)
    return mesh


def soil(name, rx, rz, seed):
    mesh = Mesh(name)
    rng = random.Random(seed)
    irr = [rng.uniform(.88, 1.08) for _ in range(17)]
    # Tops 6–18 mm above existing collider floor; soft irregular visual edge.
    mesh.rings([ring((0, .003, 0), rx, rz, 17, irregular=irr),
                ring((0, .012, 0), rx*.93, rz*.94, 17, irregular=irr),
                ring((0, .018, 0), rx*.66, rz*.64, 17, irregular=irr)], 'Earth')
    return mesh


def chamfer_box(name, sx, sy, sz, bevel=.014, material='Timber'):
    mesh = Mesh(name)
    # Octagonal section plus inset upper/lower ring: bevels on all twelve edges.
    contour = [(-sx/2+bevel,-sz/2),(sx/2-bevel,-sz/2),(sx/2,-sz/2+bevel),
               (sx/2,sz/2-bevel),(sx/2-bevel,sz/2),(-sx/2+bevel,sz/2),
               (-sx/2,sz/2-bevel),(-sx/2,-sz/2+bevel)]
    rings = [[(x-math.copysign(bevel*.6,x) if inset else x, y,
               z-math.copysign(bevel*.6,z) if inset else z) for x,z in contour]
             for y,inset in ((0,True),(bevel,False),(sy-bevel,False),(sy,True))]
    mesh.rings(rings, material)
    return mesh


def ground_patch(name, outline, center, material='Lawn'):
    mesh=Mesh(name)
    for i,a in enumerate(outline):
        b=outline[(i+1)%len(outline)]
        mesh.face((center,a,b),material,(0,1,0))
        mesh.face((a,(a[0],-.85,a[2]),(b[0],-.85,b[2]),b),'SoilEdge',
                  (a[0]+b[0]-2*center[0],0,a[2]+b[2]-2*center[2]))
    mesh.face([(p[0],-.85,p[2]) for p in outline],'SoilEdge',(0,-1,0))
    return mesh


def paving(variant):
    mesh=Mesh('Paving_%02d'%variant)
    rng=random.Random(2200+variant)
    points=[(-.42,-.27),(.23,-.29),(.43,-.18),(.40,.24),(.03,.29),(-.43,.19)]
    points=[(x+rng.uniform(-.02,.02),z+rng.uniform(-.014,.014)) for x,z in points]
    mesh.rings([[(x,.004,z) for x,z in points],[(x*.96,.014,z*.96) for x,z in points]],'Slate')
    return mesh


def garden_border(name,east=False):
    mesh=Mesh(name)
    sections=((11.70,1.02),(12.4,1.58),(13.3,1.40),(14.4,.82),(15.4,1.25),
              (16.45,2.38 if not east else 1.48),(17.1,1.98),(18.0,1.45 if not east else 2.30),(19.20,1.88))
    rows=[]
    for z,width in sections:
        row=[(.12,.006,z),(width*.50,.020,z),(width,.005,z)]
        rows.append([(12.8-x if east else x,y,z) for x,y,z in row])
    for previous,current in zip(rows,rows[1:]):
        for j in range(2):
            mesh.face((previous[j],current[j],current[j+1],previous[j+1]),'Earth',(0,1,0))
    return mesh


def fascia(name,start,end):
    axis=unit(sub(end,start))
    right=unit(cross(axis,(0,0,1)))
    forward=cross(right,axis)
    mesh=chamfer_box(name,.14,math.sqrt(dot(sub(end,start),sub(end,start))),.18,.018)
    mesh.vertices=[add(start,add(mul(right,x),add(mul(axis,y),mul(forward,z)))) for x,y,z in mesh.vertices]
    return mesh


def build():
    meshes = [pine(i) for i in range(3)] + [pine(i,False) for i in range(3)] + [grass(i) for i in range(4)] + [herb(i) for i in range(2)]
    meshes.extend(paving(i) for i in range(3))
    for i in range(4):
        mesh = Mesh('Rock_%02d' % i)
        rock(mesh, (0, 0, 0), (.25+i*.045, .14+i*.032, .20+i*.025), 34+i,
             'StoneLight' if i % 2 else 'Stone')
        meshes.append(mesh)
    meshes.extend((soil('Soil_Wide', 1.30, .48, 71), soil('Soil_Round', .72, .64, 82),
                   chamfer_box('FoundationStone', .44, .19, .045, .012, 'Masonry'),
                   chamfer_box('WindowSill', 1.36, .085, .20, .015),
                   chamfer_box('WindowApron', 1.20, .12, .04, .008),
                   chamfer_box('FenceCap', .15, .07, .15, .025),
                   chamfer_box('WindowJamb',.095,1.19,.085,.012),
                   chamfer_box('WindowLintel',1.42,.11,.10,.014),
                   chamfer_box('CornerBoard',.14,5.72,.08,.01),
                   chamfer_box('FloorBand',12.8,.12,.085,.012),
                   fascia('GableFasciaLeft',(-.25,5.965,0),(6.4,8.165,0)),
                   fascia('GableFasciaRight',(6.4,8.165,0),(13.05,5.965,0))))
    instances = []
    def place(name, mesh, pos, yaw=0, scale=(1,1,1), zone='Garden'):
        instances.append(dict(name=name, mesh=mesh, position=vec(pos), yaw=yaw, scale=vec(scale), zone=zone))

    # Broad connected lawn/soil boundaries replace the rigid material strip visually.
    # Traversable surface offsets stay <=14 mm; original support colliders stay put.
    for label,outline,center,material in (
        ('LawnWest',[(0,.002,11.4),(5.18,.002,11.4),(5.03,.002,14.3),(5.16,.002,17),(5.12,.002,19.39),(0,.002,19.39)],(2.2,.003,15.4),'Lawn'),
        ('LawnEast',[(6.99,.002,11.4),(12.8,.002,11.4),(12.8,.002,19.39),(7.08,.002,19.39),(7.14,.002,16.7),(7.02,.002,13.7)],(10,.003,15.6),'LawnSoft'),
        ('PathBed',[(5.03,.004,11.4),(7.12,.004,11.4),(7.12,.004,13.8),(7.24,.004,16.5),(7.18,.004,19.39),(4.99,.004,19.39),(5.06,.004,16.8),(4.93,.004,14.1)],(6.06,.004,15.5),'PathBed')):
        meshes.append(ground_patch(label,outline,center,material))
        place(label,label,(0,0,0),zone='PatioSurface')
    for row in range(13):
        for column in range(2):
            x=5.59+column*.91+.025*math.sin(row*1.8)
            z=11.73+row*.60
            place('WalkSlab_%02d_%d'%(row,column),'Paving_%02d'%((row+column)%3),(x,0,z),
                  (-1 if column else 1)*(row%3)*2,zone='PatioSurface')
    for label,east in (('GardenBorderWest',False),('GardenBorderEast',True)):
        meshes.append(garden_border(label,east))
        place(label,label,(0,0,0),zone='PatioSurface')
    # Bed edges follow the fence in loose groups instead of isolated plant coasters.
    for side,x in (('West',.42),('East',12.33)):
        for i,z in enumerate((12.2,12.6,13.2,14.8,15.2,16.0,17.8,18.3,18.8)):
            place('Edge_%s_%02d'%(side,i),'Grass_%02d'%(i%4),(x+.11*math.sin(i*2.3),.005,z),i*73,
                  (1.15,.8+(i%3)*.1,1.15))
        for i,z in enumerate((13.6,16.1,18.6)):
            place('EdgeStone_%s_%d'%(side,i),'Rock_%02d'%i,(x,.002,z),i*81,(1,.72,.85))

    # Composition islands. Front entry x4.8..7.3 and patio centre x4.8..7.3 stay empty.
    islands = [('LivingBed',1.85,-.88,'Soil_Wide'), ('DiningBed',10.5,-.80,'Soil_Wide'),
               ('PatioWestNear',.92,12.9,'Soil_Round'), ('PatioWestBack',1.32,18.48,'Soil_Round'),
               ('PatioEastBack',11.63,18.12,'Soil_Round'), ('PatioEastNear',11.86,13.25,'Soil_Round')]
    for k, (label,x,z,patch) in enumerate(islands):
        if patch=='Soil_Wide': place(label+'_Earth', patch, (x,0,z))
        rng = random.Random(720+k)
        rx = 1.10 if patch == 'Soil_Wide' else .52
        for i in range(9):
            angle = i*2.399
            radius = rng.uniform(.40, .9)
            dx, dz = math.cos(angle)*rx*radius, math.sin(angle)*.36*radius
            kind = 'Herb_%02d' % (i%2) if i in (1,4,7) else 'Grass_%02d' % (i%4)
            place(label+'_Plant_%02d'%i, kind, (x+dx,.018,z+dz), rng.uniform(0,360),
                  (1,rng.uniform(.73,1.06),1))
        for i in range(3):
            # Low edge stones stay within planted island, never a path obstacle.
            place(label+'_Stone_%d'%i, 'Rock_%02d'%((i+k)%4),
                  (x+(-.7+i*.65)*rx,.005,z+.20+(i%2)*.05), 31+i*63,
                  (.66,.62,.66))

    # Trees inherit original trunk collision and world position. New foliage is visual only.
    place('Patio_Pine_W_Replacement','Pine_00',(2,0,16.5),285,zone='Replacement')
    place('Patio_Pine_E_Replacement','Pine_01',(10.8,0,18.3),137,zone='Replacement')

    # Exterior stone course, interrupted well clear of front/rear doors.
    for label,z in (('Front',-.020),('Back',11.420)):
        for row in range(2):
            for index in range(28):
                x=.23+index*.455+(row%2)*.21
                if 4.85 < x < 7.22 or x > 12.55: continue
                place('Foundation_%s_%d_%02d'%(label,row,index),'FoundationStone',(x,.016+row*.198,z))
    for floor in (0,3):
        for label,x in (('Living',1.38),('Dining',10.6)):
            place('Sill_%s_%d'%(label,floor),'WindowSill',(x,floor+1.105,-.075),zone='Facade')
            place('Apron_%s_%d'%(label,floor),'WindowApron',(x,floor+.984,-.018),zone='Facade')
    windows=[('FrontA',1.38,1.70,-.026,0),('FrontB',10.6,1.70,-.026,0),
             ('FrontUpperA',1.38,4.70,-.026,0),('FrontUpperB',10.6,4.70,-.026,0),
             ('Back',10.4,1.70,11.426,0),('BackUpper',8.25,4.70,11.426,0),
             ('East',12.826,1.70,8.8,90),('EastUpper',12.826,4.70,8.8,90)]
    for label,x,y,z,yaw in windows:
        a=math.radians(yaw)
        for side in (-1,1):
            place(label+'_Jamb_'+str(side),'WindowJamb',
                  (x+side*.638*math.cos(a),y-.595,z-side*.638*math.sin(a)),yaw,zone='Facade')
        place(label+'_Lintel','WindowLintel',(x,y+.55,z),yaw,zone='Facade')
        if not label.startswith('Front'):
            place(label+'_Sill','WindowSill',(x+.045*math.sin(a),y-.595,z+.045*math.cos(a)),yaw,zone='Facade')
    for label,z in (('Front',-.035),('Back',11.435)):
        for x in (.07,12.73):
            place(label+'_Corner_'+str(x),'CornerBoard',(x,.05,z),zone='Facade')
        for y in (2.86,5.76):
            place(label+'_Band_'+str(y),'FloorBand',(6.4,y,z),zone='Facade')
        for slope in ('Left','Right'):
            place(label+'_Gable_'+slope,'GableFascia'+slope,(0,0,-.29 if label=='Front' else 11.69),zone='Facade')
    for x in (0,12.8):
        for z in (12,14,16,18,19.3):
            place('FenceCap_%s_%s'%(x,z),'FenceCap',(x,1.4,z),zone='Facade')
    for x in (2,4,6,8,10,12):
        place('FenceBackCap_%d'%x,'FenceCap',(x,1.4,19.3),zone='Facade')

    # Director-approved nonplayable depth, all actual meshes, beyond z=-2.2 boundary.
    # Camera ray through Living window heads left as it travels toward -Z.
    for i,(x,z,s) in enumerate(((-4.8,-6,1.55),(3.8,-8.4,1.35),(-9.8,-13,1.90),
                               (-.9,-14,1.60),(7.8,-19,1.90),(-13,-22,2.10),(-4.5,-23,2.0),
                               (-3.3,14.6,1.44),(-6.0,20.6,1.85),(2.0,24.2,1.64),(9.4,27.5,1.91),
                               (16.4,15.2,1.60),(19.8,21.5,1.84),(22.3,31.1,2.12),(-8.5,33.4,2.31))):
        place('Depth_Pine_%02d'%i,'Pine_Wild_%02d'%(i%3),(x,-.10,z),i*47,(s,s,s),zone='BeyondBoundary')
    # Irregular low relief, away from house. Closed volume, no vertical backdrop plane.
    outline = [(-17,-.14,-2.23),(17,-.14,-2.23),(20,.17,-12),(18,.72,-26),
               (7,1.28,-29),(-5,.98,-28),(-18,.42,-24),(-20,.12,-10)]
    center = (0,.10,-13)
    terrains=[('DistantGround',outline,center),
              ('RearGround',[(-16,-.07,19.62),(30,-.07,19.62),(36,.35,30),(29,1.1,47),(5,1.3,49),(-20,.6,39)],(8,.18,30)),
              ('WestGround',[(-.72,-.07,-2.23),(-.72,-.07,19.62),(-17,.2,19.62),(-23,.5,12),(-18,-.14,-2.23)],(-9,.10,10)),
              ('EastGround',[(13.52,-.07,-2.23),(30,-.14,-2.23),(35,.5,12),(30,.2,19.62),(13.52,-.07,19.62)],(22,.10,10))]
    for label,contour,origin in terrains:
        meshes.append(ground_patch(label,contour,origin))
        place('Depth_'+label,label,(0,0,0),zone='BeyondBoundary')
    for i,(x,z) in enumerate(((-2.7,-4.2),(-6.3,-8.4),(1.2,-8.7),(4.5,-15.1),(-9.8,-16),
                              (-2.3,16.8),(-3.6,17.1),(3.0,23.2),(4.4,23.6),(15.3,14.4),(16.8,15.0),(20.1,28))):
        place('Depth_Rock_%02d'%i,'Rock_%02d'%(i%4),(x,-.08,z),i*41,(2.3,2.6,2.1),zone='BeyondBoundary')
    for i,(x,z) in enumerate(((-3,15.7),(-3.7,16),(-2.1,18),(2.5,22.2),(4.2,24),(5,24.2),
                              (15.4,14),(16.2,14.3),(16,16.8),(-3.3,-4.7),(-5.5,-7.9),(3.3,-9.0))):
        place('Depth_Understory_%02d'%i,'Herb_%02d'%(i%2),(x,0,z),i*97,(2.2,1.5,2.2),zone='BeyondBoundary')

    # Ground contact follows the actual piecewise-planar relief at every remote root.
    for instance in instances:
        if instance['zone']!='BeyondBoundary' or instance['mesh'].endswith('Ground'): continue
        x,z=instance['position']['x'],instance['position']['z']
        heights=[]
        for _,contour,origin in terrains:
            for i in range(len(contour)):
                a,b,c=origin,contour[i],contour[(i+1)%len(contour)]
                denominator=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
                u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/denominator
                v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/denominator
                if u>=-1e-6 and v>=-1e-6 and u+v<=1.000001:
                    heights.append(u*a[1]+v*b[1]+(1-u-v)*c[1])
        assert heights, ('Remote instance outside its terrain',instance['name'])
        instance['position']['y']=round(max(heights)-.025,6)

    result=dict(schemaVersion=1, mapId='house-patio-v1', units='metres', axes='Unity +Y up +Z forward',
                materials=[dict(name=n,color=dict(r=c[0],g=c[1],b=c[2],a=1)) for n,c in PALETTE.items()],
                meshes=[m.data() for m in meshes], instances=instances,
                replaceRendererRoots=['Patio_Pine_W','Patio_Pine_E'])
    path=HERE/'generated_exterior.json'
    report=validate(result)
    path.write_text(json.dumps(result,separators=(',',':'))+'\n',encoding='utf-8')
    report['sourceSha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    report['geometrySha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    (HERE/'geometry_validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))


def validate(data):
    names=[m['name'] for m in data['meshes']]
    assert len(set(names))==len(names)
    assert len(set(i['name'] for i in data['instances']))==len(data['instances'])
    triangles=0
    for mesh in data['meshes']:
        vertices=[tuple(v[k] for k in ('x','y','z')) for v in mesh['vertices']]
        assert all(math.isfinite(c) for v in vertices for c in v)
        for submesh in mesh['submeshes']:
            indices=submesh['triangles']
            assert len(indices)%3==0
            triangles+=len(indices)//3
            for i in range(0,len(indices),3):
                a,b,c=(vertices[j] for j in indices[i:i+3])
                normal=cross(sub(b,a),sub(c,a))
                assert dot(normal,normal)>1e-16, (mesh['name'],i,'degenerate')
    assert all(i['mesh'] in names for i in data['instances'])
    # Geometry clearance, beyond the unchanged physics contract. Both authored patio
    # mosquito spawns are sphere centres, not feet. Check the actual new triangles.
    mesh_lookup={m['name']:m for m in data['meshes']}
    clearances=[]
    eye_rays=[]
    for spawn in ((2,1.8,17),(10,1.8,17)):
        nearest=999
        nearest_ray=999
        eye_direction=unit(sub((6,1.4,14),spawn))
        for instance in data['instances']:
            p,s=instance['position'],instance['scale']
            if abs(p['x']-spawn[0])>3 or abs(p['z']-spawn[2])>3: continue
            a=math.radians(instance['yaw']);co,si=math.cos(a),math.sin(a)
            points=[]
            for v in mesh_lookup[instance['mesh']]['vertices']:
                x,y,z=(v[k]*s[k] for k in ('x','y','z'))
                points.append((p['x']+x*co+z*si,p['y']+y,p['z']-x*si+z*co))
            for group in mesh_lookup[instance['mesh']]['submeshes']:
                ids=group['triangles']
                for i in range(0,len(ids),3):
                    triangle=[points[j] for j in ids[i:i+3]]
                    nearest=min(nearest,point_triangle_distance(spawn,*triangle))
                    hit=ray_triangle_distance(spawn,eye_direction,*triangle)
                    if hit is not None: nearest_ray=min(nearest_ray,hit)
        clearances.append(round(nearest,6))
        eye_rays.append(round(nearest_ray,6) if nearest_ray<999 else None)
        assert nearest>.075, ('patio mosquito spawn intersects exterior art',spawn,nearest)
        assert nearest_ray>.65, ('mosquito central eye ray blocked within 65cm',spawn,nearest_ray)
    return dict(status='PASS_OFFLINE_GEOMETRY_ONLY',uniqueMeshes=len(names),
                instances=len(data['instances']),uniqueMeshTriangles=triangles,
                patioMosquitoSpawnMinimumDistance=clearances,
                patioMosquitoCentralEyeRayFirstHit=eye_rays,
                checks=['finite coordinates','unique identifiers','nondegenerate indexed triangles','instance mesh references'],
                pending=['Unity compile/import','native living and patio views','collider/ID invariance in Unity',
                         'human/mosquito traversal','Blender editable source reconstruction','visual approval'])


def point_triangle_distance(p,a,b,c):
    ab,ac,ap=sub(b,a),sub(c,a),sub(p,a)
    normal=unit(cross(ab,ac))
    projection=sub(p,mul(normal,dot(ap,normal)))
    q=sub(projection,a)
    aa,bb,cc=dot(ab,ab),dot(ac,ac),dot(ab,ac)
    denominator=aa*bb-cc*cc
    u=(dot(q,ab)*bb-dot(q,ac)*cc)/denominator
    v=(dot(q,ac)*aa-dot(q,ab)*cc)/denominator
    if u>=0 and v>=0 and u+v<=1: return abs(dot(ap,normal))
    closest=999
    for start,end in ((a,b),(b,c),(c,a)):
        edge=sub(end,start)
        t=max(0,min(1,dot(sub(p,start),edge)/dot(edge,edge)))
        delta=sub(p,add(start,mul(edge,t)))
        closest=min(closest,math.sqrt(dot(delta,delta)))
    return closest


def ray_triangle_distance(origin,direction,a,b,c):
    edge1,edge2=sub(b,a),sub(c,a)
    p=cross(direction,edge2)
    determinant=dot(edge1,p)
    if abs(determinant)<1e-10: return None
    offset=sub(origin,a)
    u=dot(offset,p)/determinant
    if u<0 or u>1: return None
    q=cross(offset,edge1)
    v=dot(direction,q)/determinant
    if v<0 or u+v>1: return None
    distance=dot(edge2,q)/determinant
    return distance if distance>1e-6 else None


if __name__=='__main__': build()
