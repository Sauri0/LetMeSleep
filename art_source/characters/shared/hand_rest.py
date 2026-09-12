"""Reauthor selected-A distal hand rest without reflecting the surface.

The hand/wrist frame, palm center, forearm and gameplay contact stay fixed.
Digits retain their IDs, parents and exact lengths, with a palmar rest bend.
Mesh coordinates use a smooth shear and local thickness compression normal
to the palm, never a reflection. Skin weights blend across the wrist.
"""
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector

RIG_VERSION = 'LMS092.palm1'


def smooth(a, b, value):
    t = max(0.0, min(1.0, (value - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def profile(value, knots):
    if value <= knots[0][0]:
        return knots[0][1]
    for (a, va), (b, vb) in zip(knots, knots[1:]):
        if value <= b:
            return va + (vb - va) * smooth(a, b, value)
    return knots[-1][1]


def finger_lift(x, longitudinal):
    centers = [-.041, -.014, .013, .040]
    amounts = [profile(longitudinal, [(.052, 0.0), (.072, .018),
               (.084 if index in [0, 3] else .092, .040)]) for index in range(4)]
    if x <= centers[0]:
        return amounts[0]
    for i in range(3):
        if x <= centers[i + 1]:
            weight = smooth(centers[i], centers[i + 1], x)
            return amounts[i] * (1.0 - weight) + amounts[i + 1] * weight
    return amounts[-1]


def repair_human_hand_rest(rig, bones):
    core = bpy.data.objects['human_core']
    assert not core.data.shape_keys
    matrices = {}
    for side, sign in [('l', -1), ('r', 1)]:
        wrist = Vector(bones['hand_' + side]['from'])
        axis = (Vector(bones['hand_' + side]['to']) - wrist).normalized()
        align = Vector((0, -1, 0)).rotation_difference(axis)
        matrices[side] = (sign, wrist, align)
    changed = []
    groups = {g.index: g.name for g in core.vertex_groups}
    weights_changed = 0
    for vertex in core.data.vertices:
        # No positions at or above the wrist enter this edit. The frozen core
        # has only arms/hands; source-side sign selects the appropriate frame.
        source = Vector((vertex.co.x, vertex.co.z, -vertex.co.y))
        side = 'l' if source.x < 0 else 'r'
        sign, wrist, align = matrices[side]
        q = align.inverted() @ (source - wrist)
        longitudinal = -q.y
        existing = {groups[g.group]: g.weight for g in vertex.groups}
        weights = dict(existing)
        pool = weights.get('hand_' + side, 0.0) + weights.get('forearm_' + side, 0.0)
        if -.09 < longitudinal < .05 and pool > 0:
            hand_share = smooth(-.09, .04, longitudinal)
            blend = smooth(-.09,-.055,longitudinal)
            amount = weights.get('hand_' + side,0.0)*(1.0-blend)+pool*hand_share*blend
            weights['hand_' + side] = amount
            weights['forearm_' + side] = pool-amount
        if .020 < longitudinal < .105 and -sign * q.x > .034:
            def distance(name):
                a, b = Vector(bones[name]['from']), Vector(bones[name]['to'])
                d = b-a
                return (source-a-d*max(0.0,min(1.0,(source-a).dot(d)/d.length_squared))).length
            dt = min(distance('thumb_a_' + side), distance('thumb_b_' + side))
            df = min(distance('finger%d_%s_%s' % (i, section, side)) for i in range(4) for section in ['a','b'])
            desired = smooth(-.018, .018, df-dt)
            previous = weights.get('thumb_a_' + side, 0.0) + weights.get('thumb_b_' + side, 0.0)
            strength = smooth(.020,.030,longitudinal)
            desired = previous * (1-strength) + desired * strength
            other = sum(v for k,v in weights.items() if not k.startswith('thumb'))
            if other < 1e-6:
                weights = {'hand_' + side: 1.0-desired}
            else:
                weights = {k:v*(1.0-desired)/other for k,v in weights.items() if not k.startswith('thumb')}
            distal = smooth(.016,.085,longitudinal)
            weights['thumb_a_' + side] = desired*(1.0-distal)
            weights['thumb_b_' + side] = desired*distal
        for digit in range(4):
            a,b='finger%d_a_%s'%(digit,side),'finger%d_b_%s'%(digit,side)
            total=weights.get(a,0.0)+weights.get(b,0.0)
            if total>0:
                second=smooth(.058,.092,longitudinal)
                weights[a]=total*(1-second);weights[b]=total*second
        if any(abs(weights.get(k,0.0)-existing.get(k,0.0))>1e-6 for k in set(weights)|set(existing)):
            for group in core.vertex_groups: group.remove([vertex.index])
            for name, weight in weights.items():
                if weight>1e-8: core.vertex_groups[name].add([vertex.index],weight,'REPLACE')
            weights_changed += 1
        if longitudinal <= .012 or longitudinal > .125 or abs(q.x) > .105:
            continue
        thumb = smooth(.046, .057, -sign * q.x)
        thumb_lift = profile(longitudinal, [(.012, 0.0), (.038, .012), (.069, .042)])
        lift = finger_lift(sign * q.x, longitudinal) * (1.0 - thumb) + thumb_lift * thumb
        seat = smooth(.018, .030, longitudinal) * (1.0 - smooth(.060, .080, longitudinal))
        seat *= 1.0 - smooth(.038, .057, abs(q.x))
        compression = 1.0 - .30 * seat
        displacement = q.z * (compression - 1.0) + lift
        if abs(displacement) <= 1e-10:
            continue
        # z' = s(x,longitudinal)*z + f(x,longitudinal), with s >= .70.
        # x and longitudinal are unchanged, retaining widths and palm location.
        q.z = q.z * compression + lift
        result = wrist + align @ q
        vertex.co = Vector((result.x, -result.z, result.y))
        changed.append((vertex.index, abs(displacement)))
    core.data.update()
    for polygon in core.data.polygons:
        polygon.use_smooth = True
    # Relax only local source triangles that folded after the normal shear.
    # Motion stays on the hand normal: widths and longitudinal stations stay fixed.
    neighbors = {v.index:set() for v in core.data.vertices}
    for edge in core.data.edges:
        a,b=edge.vertices;neighbors[a].add(b);neighbors[b].add(a)
    relaxed=set()
    for iteration in range(12):
        core.data.update();core.data.calc_loop_triangles()
        bad={i for tri in core.data.loop_triangles
             if tri.normal.dot(sum((core.data.vertices[j].normal for j in tri.vertices),Vector()))<-.001
             for i in tri.vertices}
        if not bad:break
        updates={}
        for index in bad:
            vertex=core.data.vertices[index]
            side='l' if vertex.co.x<0 else 'r'
            _,wrist,align=matrices[side]
            normal=align@Vector((0,0,1));normal=Vector((normal.x,-normal.z,normal.y))
            mean=sum((core.data.vertices[j].co for j in neighbors[index]),Vector())/len(neighbors[index])
            updates[index]=vertex.co+normal*(mean-vertex.co).dot(normal)*.35
        for index,co in updates.items():core.data.vertices[index].co=co
        relaxed.update(bad)
    core.data.update()
    # Fifteen local weight corrections keep the palm web and wrist triangles
    # oriented across the authored native poses. Positions and bone IDs stay fixed.
    corrections = json.loads(Path(__file__).with_name('hand_weight_corrections.json').read_text())['vertices']
    source_points = [Vector((v.co.x, v.co.z, -v.co.y)) for v in core.data.vertices]
    for correction in corrections:
        target = Vector(correction['position'])
        index = min(range(len(source_points)), key=lambda i: (source_points[i]-target).length_squared)
        assert (source_points[index]-target).length < .000003, 'Hand correction source vertex moved'
        for group in core.vertex_groups: group.remove([index])
        for name, weight in correction['weights'].items():
            core.vertex_groups[name].add([index], weight, 'REPLACE')
    # Match a relaxed thumb rest to the near surface of the handle. Reversing
    # the entire old dorsal bend made the thumb penetrate the shaft at rest.
    for vertex in core.data.vertices:
        source = Vector((vertex.co.x, vertex.co.z, -vertex.co.y))
        side = 'l' if source.x < 0 else 'r'
        sign, wrist, align = matrices[side]
        q = align.inverted() @ (source-wrist)
        longitudinal = -q.y
        if .012 < longitudinal < .125 and abs(q.x) < .105:
            thumb = smooth(.046, .057, -sign*q.x)
            q.z -= .425*thumb*profile(longitudinal, [(.012, 0.0), (.038, .012), (.069, .042)])
            q.z *= 1.0-.30*thumb
            contour = thumb*profile(longitudinal, [(.012, 0.0), (.035, 1.0), (.080, 1.0), (.125, 0.0)])
            contour += (1.0-thumb)*profile(longitudinal, [(.012, 0.0), (.025, 1.0), (.038, 1.0), (.050, 0.0)])
            q.z -= .0025*contour
            result = wrist+align@q
            vertex.co = Vector((result.x, -result.z, result.y))
    core.data.update()
    final_corrections = json.loads(Path(__file__).with_name('hand_weight_corrections_final.json').read_text())['vertices']
    source_points = [Vector((v.co.x, v.co.z, -v.co.y)) for v in core.data.vertices]
    for correction in final_corrections:
        target = Vector(correction['position'])
        index = min(range(len(source_points)), key=lambda i: (source_points[i]-target).length_squared)
        assert (source_points[index]-target).length < .000003, 'Final hand correction vertex moved'
        for group in core.vertex_groups: group.remove([index])
        for name, weight in correction['weights'].items():
            core.vertex_groups[name].add([index], weight, 'REPLACE')
    modifications = {}
    maximum_length_error = 0.0
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for side in ['l', 'r']:
        _, wrist, align = matrices[side]
        for digit in ['finger0', 'finger1', 'finger2', 'finger3', 'thumb']:
            base = Vector(bones[digit + '_a_' + side]['from'])
            for section in ['a', 'b']:
                name = digit + '_' + section + '_' + side
                definition = bones[name]
                old_vector = Vector(definition['to']) - Vector(definition['from'])
                local = align.inverted() @ old_vector
                local.z = abs(local.z)
                if digit == 'thumb':
                    local.z *= .15
                    local.y = -math.sqrt(max(0.0, old_vector.length_squared-local.x*local.x-local.z*local.z))
                end = base + align @ local
                maximum_length_error = max(maximum_length_error, abs((end - base).length - old_vector.length))
                bone = rig.data.edit_bones[name]
                bone.head = Vector((base.x, -base.z, base.y))
                bone.tail = Vector((end.x, -end.z, end.y))
                modifications[name] = {'from': list(base), 'to': list(end), 'parent': definition['parent']}
                base = end
    bpy.ops.object.mode_set(mode='OBJECT')
    report = {'version': RIG_VERSION, 'changed_core_vertices': len(changed),
              'maximum_distal_lift_m': max((lift for _, lift in changed), default=0.0),
              'maximum_bone_length_error_m': maximum_length_error,
              'digit_bones': sorted(modifications), 'reweighted_vertices': weights_changed,
              'source_normal_relaxed_vertices':len(relaxed),
              'local_weight_corrections': len(corrections),
              'acquisition_weight_corrections': len(final_corrections),
              'maximum_local_contour_clearance_m': .0025,
              'thumb_palmar_rest_fraction': .15,
              'hand_wrist_forearm_bones_unchanged': True,
              'palm_seat_normal_scale_minimum': .70,
              'palm_center_and_width_unchanged': True,
              'deformation': 'Smooth normal compression/shear z=s(x,long)z+f(x,long), Jacobian >= .70; palmar bind, no negative-scale reflection',
              'scope': 'Selected A human_core hands and wrist weights; head, garment, body scale, contacts and hand frames unchanged'}
    core['hand_rest092'] = json.dumps(report)
    return modifications, report
