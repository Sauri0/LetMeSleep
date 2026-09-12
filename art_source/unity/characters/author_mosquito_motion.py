"""Mosquito-only sampled motion. Imported lazily by the shared orchestrator.

Root never moves. Surface support and Mouth remain separate explicit contracts.
Requires Blender; importing this module alone performs no work.
"""
import math

STRIDE_SOURCE_M = .026
STANCE_DUTY = .65
SURFACE_CYCLE_FRAMES = 31
UNITY_SCALE = .5
SURFACE_DISTANCE_PER_CYCLE_M = STRIDE_SOURCE_M / STANCE_DUTY * UNITY_SCALE


def surface_step(time, leg, side):
    """Two alternating tripods. Return source-space offset and support state."""
    phase = (time + (.5 if (leg == 2) == (side == 'L') else 0)) % 1
    if phase < STANCE_DUTY:
        return (-STRIDE_SOURCE_M * .5 + STRIDE_SOURCE_M * phase / STANCE_DUTY, 0, True)
    u = (phase - STANCE_DUTY) / (1 - STANCE_DUTY)
    # Zero velocity/acceleration at liftoff and touchdown in the vertical axis.
    eased = u * u * u * (u * (u * 6 - 15) + 10)
    return (STRIDE_SOURCE_M * (.5 - eased), .014 * math.sin(math.pi * u) ** 2, False)


def mosquito(c):
    import bpy
    from mathutils import Vector, Matrix
    from author_motion import Pose, sampled, smooth, TAU
    from author_mosquito_geometry import SUPPORT_Z

    p = Pose(c)
    p.reset()
    rest = {b.name: b.head.copy() for b in p.rig.pose.bones}
    wing_rest = {s: p.rest['Wing.' + s].copy() for s in ('L', 'R')}

    def wings(flap=.12, fold=.24):
        # Mirror in armature space. Identical local Euler values are not a reliable
        # mirror for opposite bone axes. Parent motion is then applied once.
        p.update()
        parent = p.rig.pose.bones['Thorax'].matrix @ p.rest['Thorax'].inverted()
        for side, sign in (('L', 1), ('R', -1)):
            origin = wing_rest[side].translation
            rotation = Matrix.Rotation(sign * fold, 4, 'Z') @ Matrix.Rotation(-sign * flap, 4, 'Y')
            matrix = Matrix.Translation(origin) @ rotation @ Matrix.Translation(-origin) @ wing_rest[side]
            p.rig.pose.bones['Wing.' + side].matrix = parent @ matrix
        p.update()

    def stance():
        p.reset()
        wings()

    def legs_air(amount, trail=.014):
        if abs(amount) < 1e-10:
            return
        p.update()
        parent = p.rig.pose.bones['Thorax'].matrix @ p.rest['Thorax'].inverted()
        for side, sign in (('L', 1), ('R', -1)):
            for i in range(1, 4):
                upper, lower, foot = (f'Leg{i}{j:02d}.{side}' for j in (1, 2, 3))
                offset = Vector((-sign * .010 * amount, trail * amount, .041 * amount))
                target = parent @ (rest[foot] + offset)
                pole = parent @ (rest[lower] + Vector((sign * .035, -.016, .015)) * amount)
                orientation = parent @ p.rest[foot]
                p.chain(upper, lower, target, pole, foot, orientation)

    def flight(t, hover=False):
        stance()
        wave = math.sin(TAU * t)
        p.rotate('Thorax', ((.008 * wave if hover else -.085 + .008 * wave), 0, 0))
        p.rotate('Abdomen01', ((.016 if hover else .065) + .014 * wave, 0, 0))
        p.rotate('Abdomen02', (-.022 * wave, 0, 0))
        p.update()
        legs_air(.48 if hover else 1, trail=.007 if hover else .024)
        wings((.43 if hover else .53) * math.cos(TAU * 3 * t), .04)
        return p.snapshot()

    sampled(c, 'Fly', 13, lambda t: flight(t, False))
    sampled(c, 'Hover', 13, lambda t: flight(t, True))

    def idle(t):
        stance()
        p.rotate('Abdomen01', (.020 * math.sin(TAU * t), 0, 0))
        p.rotate('Abdomen02', (-.012 * math.sin(TAU * t), 0, 0))
        return p.snapshot()

    sampled(c, 'Idle', 61, idle)
    sampled(c, 'PerchIdle', 61, idle)

    def perch(t):
        stance()
        u = smooth(t)
        # Extend all six legs before the wings settle; exact bind support at end.
        legs_air(.48 * (1 - smooth(min(1, t / .72))), trail=.007)
        wings(.43 * (1 - u) * math.cos(TAU * 3 * t) + .12 * u,
              .04 + .20 * u)
        return p.snapshot()

    sampled(c, 'PerchEnter', 25, perch)
    sampled(c, 'Land', 25, perch)

    def brake(t):
        stance()
        u = smooth(t)
        p.rotate('Thorax', (-.085 * (1 - u), 0, 0))
        p.rotate('Abdomen01', (.065 * (1 - u), 0, 0))
        legs_air(1 - .52 * u, trail=.024 - .017 * u)
        wings((.53 - .10 * u) * math.cos(TAU * 3 * t), .04)
        return p.snapshot()

    sampled(c, 'Brake', 25, brake)

    def surface(t):
        stance()
        for side, sign in (('L', 1), ('R', -1)):
            for i in range(1, 4):
                dy, dz, _ = surface_step(t, i, side)
                target = rest[f'Leg{i}03.{side}'] + Vector((0, dy, dz))
                pole = rest[f'Leg{i}02.{side}'] + Vector((sign * .03, -.02, .02))
                p.chain(f'Leg{i}01.{side}', f'Leg{i}02.{side}', target, pole, f'Leg{i}03.{side}')
        p.rotate('Abdomen01', (.012 * math.sin(TAU * 2 * t), 0, 0))
        return p.snapshot()

    sampled(c, 'SurfaceWalk', SURFACE_CYCLE_FRAMES, surface)

    def bite(t, amount=1):
        stance()
        # No canceling Head/Proboscis Euler rotations: their different pivots moved
        # Mouth in the old clip. Feed motion is isolated behind the fixed thorax.
        pulse = math.sin(TAU * t)
        p.rotate('Abdomen01', ((.040 + .040 * pulse) * amount, 0, 0))
        p.rotate('Abdomen02', ((-.018 - .030 * pulse) * amount, 0, 0))
        wings(.12 - .025 * amount, .24 + .04 * amount)
        return p.snapshot()

    sampled(c, 'BiteStart', 19, lambda t: bite(0, smooth(t)))
    sampled(c, 'BiteLoop', 31, bite)
    sampled(c, 'Bite', 37, lambda t: bite(t, math.sin(math.pi * t) ** 2))

    def detach(t):
        u = smooth(t)
        bite(0, 1 - u)
        # Start wingbeats before the tarsi leave; Root displacement belongs to game.
        lift = .48 * smooth(max(0, (t - .22) / .78))
        legs_air(lift, trail=.007)
        wings(.43 * u * math.cos(TAU * 3 * t) + .095 * (1 - u), .28 - .24 * u)
        return p.snapshot()

    sampled(c, 'Detach', 19, detach)

    def settle_to_support():
        # Mesh contact, not a guessed pelvis height; move Thorax, never Root.
        p.update()
        graph = bpy.context.evaluated_depsgraph_get()
        lowest = math.inf
        for obj in bpy.context.scene.objects:
            if obj.type != 'MESH':
                continue
            evaluated = obj.evaluated_get(graph)
            data = evaluated.to_mesh()
            lowest = min(lowest, min((evaluated.matrix_world @ v.co).z for v in data.vertices))
            evaluated.to_mesh_clear()
        p.translate('Thorax', (0, 0, SUPPORT_Z - lowest))
        p.update()

    def hit(t):
        stance()
        u = smooth(t)
        p.rotate('Thorax', (.25 * u, .10 * math.sin(math.pi * t), .40 * u))
        legs_air(.48 + .25 * u)
        wings(.25 * (1 - u) * math.cos(TAU * 3 * t) + .12 * u, .24)
        return p.snapshot()

    sampled(c, 'Hit', 19, hit)

    def fall_pose(u):
        stance()
        p.rotate('Thorax', (.25 + .90 * u, .12 * math.sin(math.pi * u), .40 + .85 * u))
        p.rotate('Abdomen01', (-.12 * u, 0, 0))
        legs_air(.73 + .27 * u)
        wings(.12 - .20 * u, .24 + .32 * u)
        settle_to_support()
        return p.snapshot()

    sampled(c, 'Fall', 31, lambda t: fall_pose(smooth(t)))

    def recover(t):
        # Unroll and establish six-foot support, with final pose equal to PerchIdle.
        u = 1 - smooth(t)
        stance()
        p.rotate('Thorax', (1.15 * u, .12 * math.sin(math.pi * u), 1.25 * u))
        p.rotate('Abdomen01', (-.12 * u, 0, 0))
        legs_air(u)
        wings(.12 - .20 * u, .24 + .32 * u)
        if u > 1e-8:
            settle_to_support()
        return p.snapshot()

    sampled(c, 'Recover', 37, recover)
    c.contact['minimum_surface_leg_reach_margin_m'] = p.minimum_reach_margin
    c.contact['surface_walk'] = {
        'clip': 'Mosquito_SurfaceWalk', 'source_units': 'metres, Blender',
        'stride_source_m': STRIDE_SOURCE_M, 'stance_duty': STANCE_DUTY,
        'unity_scale_applied_once': UNITY_SCALE,
        'distance_per_cycle_unity_m': SURFACE_DISTANCE_PER_CYCLE_M,
        'frames': SURFACE_CYCLE_FRAMES, 'fps': 30, 'duration_seconds': 1.0,
        'runtime_phase_sync_verified': False,
    }
    c.contact['bite_tip_policy'] = 'Head/Thorax/Proboscis bind transforms retained throughout BiteStart/BiteLoop/Bite'
    c.contact['fall_contact_policy'] = 'evaluated mesh minimum at authored support plane; native landing/transition review pending'
