"""Mosquito-only sampled motion. Imported lazily by the shared orchestrator.

Root never moves. Surface support and Mouth remain separate explicit contracts.
Requires Blender; importing this module alone performs no work.
"""
import math
from author_mosquito_geometry import leg_points

STRIDE_SOURCE_M = .116
STANCE_DUTY = .58
SURFACE_CYCLE_FRAMES = 31
UNITY_SCALE = .5
SURFACE_DISTANCE_PER_CYCLE_M = round(STRIDE_SOURCE_M / STANCE_DUTY * UNITY_SCALE, 12)
SURFACE_SWING_LIFT_SOURCE_M = .020
SURFACE_NOMINAL_SPEED_MPS = .65
SURFACE_REVIEW_MAX_SPEED_MPS = .80
FLIGHT_FRAMES = 13
FLIGHT_WINGBEATS_PER_LOOP = 3
AIR_FLAP_RADIANS = .58
# v0.3.0 animation pass (chars.md, PER-03/PER-07): perched wings open in a wide
# V. Absolute left-wing directions in source axes (-Y forward): span 45 deg
# from the vertical seen from the front and ~38 deg above the horizontal in
# profile, membrane turned about the span so it shows ~80% of its width from
# the front, ~60% from the side and the far wing stays a leaf (not a needle)
# in the three-quarter view. The pose rotates the geometry's
# stance frame (author_mosquito_geometry WING_STANCE_*) onto these targets, so
# if the geometry adopts them the extra rotation becomes the identity.
WING_V_SPAN = (.52, .67, .52)
# Round 3 (anim-r3/chars-r10): the membrane faces more forward-up so the far wing of the three-quarter view
# is a leaf (~35% of its width) instead of an edge-on needle, the front view keeps ~75% of both wings.
WING_V_NORMAL = (.40, -.74, .54)
STUNNED_FRAMES = 37
# v0.3.0 review: knocked out BELLY UP (abdomen resting on the floor, legs kicking in the air, wings
# splayed flat) instead of sitting upright on the tail; Recover is a short 0.4 s roll back onto the six
# feet (the authority's Recovering window) instead of the 1.2 s tail played at 3x.
KO_ROLL = math.pi
KO_PITCH = .10
KO_ABDOMEN = -.55
KO_WING_FLAP = -.95
KO_WING_FOLD = .50
RECOVER_FRAMES = 13
# Round 3: Fall is a visible comic tumble: 1.5 turns about the long axis in the first .5 s (a flinch is
# folded into its first frames), then it lies belly up. Recover (0.4 s, the authority's Recovering window)
# rolls onto the belly, pushes up on the six legs and shakes the wings before standing like PerchIdle.
# Round 4 (director r4, item 3): the roll read as a 2-frame snap (a quintic ease packed it into ~0.1 s and
# the runtime fade ate its start). The roll now spans RECOVER_ROLL of the clip at a nearly even rate (~5
# frames at 30 fps, ~35-45 deg per frame, the legs reaching for the floor as the body turns) and the rest is
# a visible push-up: ~7 frames with all six feet planted, the belly starting low and the legs unfolding.
RECOVER_ROLL = .44
RECOVER_PUSH_DEPTH = .048
FALL_TUMBLE_SECONDS = .50
FALL_EXTRA_TURNS = 1.0
BITE_PUMPS_PER_LOOP = 2


def flight_channels(t, hover=False):
    """Authored R4 channels; clock/wingbeat frequency and Root stay unchanged.

    Broader wing excursion and secondary motion are a candidate for native
    legibility review. They do not establish the cause of the menu complaint.
    """
    wave = math.sin(math.tau * t)
    drive = 0 if hover else math.sin(math.pi * t) ** 2
    return {
        'thorax_x': -.110 * drive + .012 * wave,
        'abdomen01_x': .016 + .060 * drive + .021 * wave,
        'abdomen02_x': -.030 * wave,
        'leg_amount': .48 + .52 * drive,
        'leg_trail_source_m': .007 + .020 * drive,
        'flap': (AIR_FLAP_RADIANS + .12 * drive) * math.cos(math.tau * FLIGHT_WINGBEATS_PER_LOOP * t),
        'fold': .04,
    }


def flight_contract():
    duration = (FLIGHT_FRAMES - 1) / 30
    return {
        'clips': ['Mosquito_Fly', 'Mosquito_Hover'], 'frames': FLIGHT_FRAMES,
        'fps': 30, 'duration_seconds': duration,
        'wingbeats_per_loop': FLIGHT_WINGBEATS_PER_LOOP,
        'wingbeat_frequency_hz_at_speed_one': FLIGHT_WINGBEATS_PER_LOOP / duration,
        'air_endpoint_flap_radians': AIR_FLAP_RADIANS,
        'fly_peak_flap_envelope_radians': AIR_FLAP_RADIANS + .12,
        'wing_source_axes': 'mirror around each bind origin: Rz(sign*fold) @ Ry(-sign*flap)',
        'secondary_channels': ['Thorax', 'Abdomen01', 'Abdomen02', 'six leg IK chains'],
        'root_motion': False,
        'air_endpoint_dependents': ['Fly', 'Hover', 'PerchEnter', 'Land', 'Brake', 'Detach'],
        'acceptance': 'candidate source only; normal-speed final-scale wings/body/legs and blends pending',
    }


def surface_step(time, leg, side, *, distance_per_cycle_unity_m=SURFACE_DISTANCE_PER_CYCLE_M,
                 stance_duty=STANCE_DUTY):
    """Offset from bind tarsus, centered under the coxa; phase convention unchanged.

    Study-only keyword overrides let the same trajectory evaluate candidate D/duty.
    Positive source Y during support cancels actor movement toward source -Y.
    """
    stride = distance_per_cycle_unity_m * stance_duty / UNITY_SCALE
    points = leg_points(1 if side == 'L' else -1, leg)
    center_offset = points[0][1] - points[2][1]
    phase = (time + (.5 if (leg == 2) == (side == 'L') else 0)) % 1
    if phase < stance_duty:
        return (center_offset - stride * .5 + stride * phase / stance_duty, 0, True)
    u = (phase - stance_duty) / (1 - stance_duty)
    eased = u * u * u * (u * (u * 6 - 15) + 10)
    # Quintic Hermite swing keeps the support velocity at both boundaries. A
    # zero-velocity ease would produce a horizontal jerk at every footfall.
    tangent = stride / stance_duty * (1 - stance_duty)
    y = center_offset + stride * (.5 - eased) + tangent * (u - eased)
    return (y, SURFACE_SWING_LIFT_SOURCE_M * math.sin(math.pi * u) ** 2, False)


def surface_leg_ranges(*, distance_per_cycle_unity_m=SURFACE_DISTANCE_PER_CYCLE_M,
                       stance_duty=STANCE_DUTY, samples=1201):
    """Analytic target reach in source metres; skin/contact still needs Blender."""
    rows = []
    for side, sign in (('L', 1), ('R', -1)):
        for leg in range(1, 4):
            hip, knee, ankle, _ = leg_points(sign, leg)
            upper, lower = math.dist(hip, knee), math.dist(knee, ankle)
            distances = []
            for frame in range(samples):
                dy, dz, _ = surface_step(frame / (samples - 1), leg, side,
                    distance_per_cycle_unity_m=distance_per_cycle_unity_m, stance_duty=stance_duty)
                distances.append(math.dist(hip, (ankle[0], ankle[1] + dy, ankle[2] + dz)))
            minimum, maximum = min(distances), max(distances)
            rows.append({'leg': leg, 'side': side, 'upper_length_source_m': upper,
                         'lower_length_source_m': lower, 'reach_min_source_m': abs(upper - lower),
                         'reach_max_source_m': upper + lower, 'target_min_source_m': minimum,
                         'target_max_source_m': maximum, 'extension_margin_source_m': upper + lower - maximum,
                         'fold_margin_source_m': minimum - abs(upper - lower)})
    return rows


def surface_contract():
    """Single source of truth consumed by generation and the light study report."""
    duration = (SURFACE_CYCLE_FRAMES - 1) / 30
    return {
        'clip': 'Mosquito_SurfaceWalk', 'source_units': 'metres, Blender',
        'stride_source_m': STRIDE_SOURCE_M, 'stance_duty': STANCE_DUTY,
        'unity_scale_applied_once': UNITY_SCALE,
        'distance_per_cycle_unity_m': SURFACE_DISTANCE_PER_CYCLE_M,
        'frames': SURFACE_CYCLE_FRAMES, 'start_frame': 1, 'end_frame': SURFACE_CYCLE_FRAMES,
        'fps': 30, 'duration_seconds': duration,
        'support_center': 'each coxa Y; tarsal bind X/Z retained',
        'phase_zero': 'L1/L3/R2 begin support; L2/R1/R3 phase +0.5',
        'phase_direction': 'positive phase moves supported feet along source +Y; actor forward is source -Y',
        'authority_phase_distance_m': .3,
        'authority_unwrapped_phase_multiplier': round(.3 / SURFACE_DISTANCE_PER_CYCLE_M, 12),
        'phase_formula': 'frac(authorityMotionPhase * (0.3 / distance_per_cycle_unity_m)); convert before modulo, once',
        'animator_speed_formula': 'duration_seconds * actual_tangential_speed_mps / distance_per_cycle_unity_m',
        'nominal_game_speed_mps_unchanged': SURFACE_NOMINAL_SPEED_MPS,
        'nominal_cadence_hz': SURFACE_NOMINAL_SPEED_MPS / SURFACE_DISTANCE_PER_CYCLE_M,
        'review_speed_range_mps': [.08, SURFACE_REVIEW_MAX_SPEED_MPS],
        'review_cadence_range_hz': [.08 / SURFACE_DISTANCE_PER_CYCLE_M, SURFACE_REVIEW_MAX_SPEED_MPS / SURFACE_DISTANCE_PER_CYCLE_M],
        'proposed_animator_speed_limits': [0, duration * SURFACE_REVIEW_MAX_SPEED_MPS / SURFACE_DISTANCE_PER_CYCLE_M],
        'nominal_60fps_samples_per_cycle': 60 * SURFACE_DISTANCE_PER_CYCLE_M / SURFACE_NOMINAL_SPEED_MPS,
        'swing_lift_source_m': SURFACE_SWING_LIFT_SOURCE_M,
        'range_status': 'analytic source contract; readable cadence/contact requires native review',
        'analytic_leg_ranges': surface_leg_ranges(),
        'runtime_phase_sync_verified': False,
    }


def wing_v_rotation():
    """Armature-space rotation carrying the stance wing frame onto the perched V frame (left wing)."""
    from mathutils import Matrix, Vector
    from author_mosquito_geometry import WING_STANCE_SPAN, WING_STANCE_NORMAL

    def frame(span, hint):
        span = Vector(span).normalized()
        normal = (Vector(hint) - span * Vector(hint).dot(span)).normalized()
        return Matrix((span, normal, span.cross(normal))).transposed()

    return (frame(WING_V_SPAN, WING_V_NORMAL) @ frame(WING_STANCE_SPAN, WING_STANCE_NORMAL).transposed()).to_quaternion()


def mosquito(c):
    import bpy
    from mathutils import Vector, Matrix, Quaternion
    from author_motion import Pose, sampled, smooth, TAU
    from author_mosquito_geometry import SUPPORT_Z

    p = Pose(c)
    p.reset()
    rest = {b.name: b.head.copy() for b in p.rig.pose.bones}
    wing_rest = {s: p.rest['Wing.' + s].copy() for s in ('L', 'R')}
    v_rotation = wing_v_rotation()

    def wings(flap=.12, fold=.24, v=1.0):
        # Mirror in armature space. Identical local Euler values are not a reliable
        # mirror for opposite bone axes. Parent motion is then applied once.
        # v blends the perched V opening (1 = PER-03/PER-07 perched wings, 0 = flight axes).
        p.update()
        parent = p.rig.pose.bones['Thorax'].matrix @ p.rest['Thorax'].inverted()
        opening = Quaternion().slerp(v_rotation, max(0., min(1., v))).to_matrix().to_4x4()
        mirror = Matrix.Diagonal((-1, 1, 1, 1))
        for side, sign in (('L', 1), ('R', -1)):
            origin = wing_rest[side].translation
            spread = opening if sign > 0 else mirror @ opening @ mirror
            rotation = spread @ Matrix.Rotation(sign * fold, 4, 'Z') @ Matrix.Rotation(-sign * flap, 4, 'Y')
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
        # Both air loops meet the same departure/approach pose at phase zero.
        # A smooth envelope preserves Fly's tuck/lean inside the loop without a
        # 48 mm marker jump at Detach->Fly or Fly->PerchEnter.
        channels = flight_channels(t, hover)
        p.rotate('Thorax', (channels['thorax_x'], 0, 0))
        p.rotate('Abdomen01', (channels['abdomen01_x'], 0, 0))
        p.rotate('Abdomen02', (channels['abdomen02_x'], 0, 0))
        p.update()
        legs_air(channels['leg_amount'], trail=channels['leg_trail_source_m'])
        wings(channels['flap'], channels['fold'], 0)
        return p.snapshot()

    sampled(c, 'Fly', FLIGHT_FRAMES, lambda t: flight(t, False))
    sampled(c, 'Hover', FLIGHT_FRAMES, lambda t: flight(t, True))

    def settle_leg(leg, side, sign, t, start, length=.24, lift=.016):
        # One foot lifts, shuffles and is put down again on its own support spot (A18 attentive idle).
        u = (t - start) / length
        if not 0 < u < 1:
            return
        dz = lift * math.sin(math.pi * u) ** 2
        dy = -.008 * math.sin(math.pi * u)
        target = rest[f'Leg{leg}03.{side}'] + Vector((sign * .004 * math.sin(math.pi * u), dy, dz))
        pole = rest[f'Leg{leg}02.{side}'] + Vector((sign * .03, -.02, .02))
        p.chain(f'Leg{leg}01.{side}', f'Leg{leg}02.{side}', target, pole, f'Leg{leg}03.{side}')

    def idle(t):
        # Round 3 (anim-r3): never frozen - four foot tics per 2 s loop (front feet lift higher, a back
        # foot rubs), a quick double wing flick, a curious head tilt and a breathing abdomen.
        p.reset()
        flick = math.sin(math.pi * min(1., max(0., (t - .70) / .12))) ** 2 + .6 * math.sin(math.pi * min(1., max(0., (t - .84) / .10))) ** 2
        wings(.12 + .30 * flick, .24 - .10 * flick)
        p.rotate('Abdomen01', (.035 * math.sin(TAU * t) + .015 * math.sin(TAU * 3 * t), 0, 0))
        p.rotate('Abdomen02', (-.025 * math.sin(TAU * t), 0, 0))
        tilt = math.sin(math.pi * min(1., max(0., (t - .30) / .40))) ** 2
        p.rotate('Head', (.06 * tilt, .10 * tilt, .22 * tilt))
        p.update()
        settle_leg(1, 'L', 1, t, .06, .20, .030)
        settle_leg(2, 'R', -1, t, .30, .18, .020)
        settle_leg(1, 'R', -1, t, .52, .20, .030)
        settle_leg(3, 'L', 1, t, .76, .18, .020)
        return p.snapshot()

    sampled(c, 'Idle', 61, idle)
    sampled(c, 'PerchIdle', 61, idle)

    def perch(t):
        stance()
        u = smooth(t)
        p.rotate('Abdomen01', (.016 * (1 - u), 0, 0))
        # Extend all six legs before the wings settle; exact bind support at end.
        legs_air(.48 * (1 - smooth(min(1, t / .72))), trail=.007)
        wings(AIR_FLAP_RADIANS * (1 - u) * math.cos(TAU * 3 * t) + .12 * u,
              .04 + .20 * u, u)
        return p.snapshot()

    sampled(c, 'PerchEnter', 25, perch)
    sampled(c, 'Land', 25, perch)

    def brake(t):
        stance()
        u = smooth(t)
        effort = math.sin(math.pi * u) ** 2
        p.rotate('Thorax', (.065 * effort, 0, 0))
        p.rotate('Abdomen01', (.016 + .030 * effort, 0, 0))
        legs_air(.48 + .25 * effort, trail=.007)
        wings((AIR_FLAP_RADIANS + .06 * effort) * math.cos(TAU * 3 * t), .04, 0)
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

    previous_reach_margin = p.minimum_reach_margin
    p.minimum_reach_margin = math.inf
    sampled(c, 'SurfaceWalk', SURFACE_CYCLE_FRAMES, surface)
    surface_reach_margin = p.minimum_reach_margin
    p.minimum_reach_margin = min(previous_reach_margin, surface_reach_margin)

    def bite(t, amount=1):
        stance()
        # No canceling Head/Proboscis Euler rotations: their different pivots moved
        # Mouth in the old clip. Feed motion is isolated behind the fixed thorax.
        pulse = math.sin(TAU * BITE_PUMPS_PER_LOOP * t)
        p.rotate('Abdomen01', ((.050 + .075 * pulse) * amount, 0, 0))
        p.rotate('Abdomen02', ((-.020 - .060 * pulse) * amount, 0, 0))
        # The body pumps toward the fixed proboscis tip (ActorVisualBinding re-anchors the tip every frame).
        p.rotate('Thorax', (.035 * pulse * amount, 0, 0))
        wings(.12 - .025 * amount + .05 * amount * max(0., pulse), .24 + .04 * amount)
        return p.snapshot()

    sampled(c, 'BiteStart', 19, lambda t: bite(0, smooth(t)))
    sampled(c, 'BiteLoop', 31, bite)
    sampled(c, 'Bite', 37, lambda t: bite(t, math.sin(math.pi * t) ** 2))

    def detach(t):
        u = smooth(t)
        bite(0, 1 - u)
        p.rotate('Abdomen01', (.040 * (1 - u) + .016 * u, 0, 0))
        # Start wingbeats before the tarsi leave; Root displacement belongs to game.
        lift = .48 * smooth(max(0, (t - .22) / .78))
        legs_air(lift, trail=.007)
        wings(AIR_FLAP_RADIANS * u * math.cos(TAU * 3 * t) + .095 * (1 - u), .28 - .24 * u, 1 - u)
        return p.snapshot()

    sampled(c, 'Detach', 19, detach)

    def settle_to_support(bodies_only=False):
        # Mesh contact, not a guessed pelvis height; move Thorax, never Root. bodies_only ignores the wings: a
        # rolling body must not be held up in the air by the wing tip that points at the floor (round 4).
        p.update()
        graph = bpy.context.evaluated_depsgraph_get()
        lowest = math.inf
        for obj in bpy.context.scene.objects:
            if obj.type != 'MESH' or (bodies_only and obj.name.startswith('Wing')):
                continue
            evaluated = obj.evaluated_get(graph)
            data = evaluated.to_mesh()
            lowest = min(lowest, min((evaluated.matrix_world @ v.co).z for v in data.vertices))
            evaluated.to_mesh_clear()
        # Additive: the knockout roll already moved the thorax about its centre.
        thorax = p.rig.pose.bones['Thorax']
        thorax.location += p.rest['Thorax'].to_3x3().inverted() @ Vector((0, 0, SUPPORT_Z - lowest))
        p.update()

    def hit(t):
        stance()
        u = smooth(t)
        p.rotate('Thorax', (.25 * u, .10 * math.sin(math.pi * t), .40 * u))
        legs_air(.48 + .25 * u)
        wings(.25 * (1 - u) * math.cos(TAU * 3 * t) + .12 * u, .24, u)
        return p.snapshot()

    sampled(c, 'Hit', 19, hit)

    def body_roll(roll, pitch=0.):
        # Whole body about its own long axis (source Y) through the thorax centre; Root never moves.
        from author_mosquito_geometry import THORAX_CENTER
        p.update()
        centre = Vector(THORAX_CENTER)
        turn = (Matrix.Translation(centre) @ Matrix.Rotation(pitch, 4, 'X') @ Matrix.Rotation(roll, 4, 'Y')
                @ Matrix.Translation(-centre))
        p.rig.pose.bones['Thorax'].matrix = turn @ p.rest['Thorax']
        p.update()

    def knocked_out(u, t=0., wobble=0., spin=0., flail=0., lift=0., legs=None, bodies_only=False):
        """u=0 upright stance -> u=1 belly up; t/wobble drive the dizzy loop; spin adds whole turns of the
        tumble, flail waves the legs and wings while falling, lift raises the body on its legs (get-up);
        legs overrides how far the legs are tucked into the air (default .73-1 with u)."""
        stance()
        body_roll(KO_ROLL * min(u, 1.) + spin, KO_PITCH * u)
        p.rotate('Abdomen01', ((KO_ABDOMEN + .06 * wobble * math.sin(TAU * 2 * t)) * u, 0, 0))
        p.rotate('Abdomen02', (-.10 * u, 0, 0))
        p.rotate('Head', (.22 * wobble * math.sin(TAU * t) * u, .10 * wobble * math.sin(TAU * t + 1.1) * u,
                          .34 * wobble * math.sin(TAU * 2 * t + .6) * u))
        legs_air(.73 + .27 * u if legs is None else legs)
        if wobble:
            for side, sign in (('L', 1), ('R', -1)):
                for i in range(1, 4):
                    knee = p.rig.pose.bones[f'Leg{i}02.{side}']
                    knee.rotation_euler.x += .55 * wobble * math.sin(TAU * (2 * t + .17 * i + (.5 if sign < 0 else 0)))
        # Wings splay flat on the floor on either side of the flipped body, with weak twitches.
        twitch = .10 * wobble * math.sin(TAU * 3 * t) ** 2
        if flail:
            for side, sign in (('L', 1), ('R', -1)):
                for i in range(1, 4):
                    knee = p.rig.pose.bones[f'Leg{i}02.{side}']
                    knee.rotation_euler.x += .8 * flail * math.sin(TAU * (3 * u + .21 * i + (.5 if sign < 0 else 0)))
            twitch += .6 * flail * math.sin(TAU * 4 * u) ** 2
        wings(.12 + (KO_WING_FLAP - .12) * min(u, 1.) + twitch, .24 + (KO_WING_FOLD - .24) * min(u, 1.), 1 - min(u, 1.))
        settle_to_support(bodies_only)
        if lift:
            thorax = p.rig.pose.bones['Thorax']
            thorax.location += p.rest['Thorax'].to_3x3().inverted() @ Vector((0, 0, lift))
            p.update()

    def fall_pose(t):
        # Comic tumble (round 3): a whole extra turn before landing belly up, in FALL_TUMBLE_SECONDS with a
        # cubic ease (<=55 deg per 1/30 s key), legs and wings flailing; the rest of the clip holds the pose.
        u = min(1., t / FALL_TUMBLE_SECONDS)
        eased = u * u * (3 - 2 * u)
        knocked_out(min(1., eased * 1.15), spin=TAU * FALL_EXTRA_TURNS * eased, flail=1 - u)
        return p.snapshot()

    sampled(c, 'Fall', 31, fall_pose)

    def recover(t):
        # 0.4 s get-up (round 4): roll over onto the belly in the first RECOVER_ROLL of the clip (~5 frames, a
        # nearly even turn: <= ~45 deg per 1/30 s) with the legs reaching down, then a push-up on the six planted
        # feet (~7 frames): the belly starts on the floor with the legs folded and splayed, the body rises, the
        # head shakes it off and the wings buzz, ending exactly on the PerchIdle pose.
        if t >= 1 - 1e-8:
            stance()
            return p.snapshot()
        if t < RECOVER_ROLL:
            a = t / RECOVER_ROLL
            roll = 1 - (.55 * a + .45 * a * a * (3 - 2 * a))  # half linear: every frame turns visibly
            knocked_out(roll, legs=.25 + .75 * roll, bodies_only=True)
            return p.snapshot()
        v = (t - RECOVER_ROLL) / (1 - RECOVER_ROLL)
        stance()
        # Legs start folded under the lowered body (belly on the floor) and unfold; the head shakes it off.
        crouch = 1 - smooth(min(1., v / .85))
        p.rotate('Thorax', (.12 * crouch, 0, .10 * math.sin(TAU * 2 * v) * crouch))
        thorax = p.rig.pose.bones['Thorax']
        thorax.location += p.rest['Thorax'].to_3x3().inverted() @ Vector((0, 0, -RECOVER_PUSH_DEPTH * crouch))
        p.rotate('Head', (-.10 * crouch, .25 * math.sin(TAU * 2.5 * v) * crouch, .30 * math.sin(TAU * 2.5 * v + .5) * crouch))
        p.rotate('Abdomen01', (-.24 * crouch, 0, 0))
        p.update()
        # The six feet stay on (slightly splayed) support spots while the lowered body pushes up.
        for side, sign in (('L', 1), ('R', -1)):
            for i in range(1, 4):
                target = rest[f'Leg{i}03.{side}'] + Vector((sign * .012 * crouch, 0, 0))
                pole = rest[f'Leg{i}02.{side}'] + Vector((sign * .03, -.02, .02 + .02 * crouch))
                p.chain(f'Leg{i}01.{side}', f'Leg{i}02.{side}', target, pole, f'Leg{i}03.{side}')
        wings(.12 + .45 * math.sin(math.pi * v) * abs(math.sin(TAU * 3 * v)), .24, 1.)
        return p.snapshot()

    sampled(c, 'Recover', RECOVER_FRAMES, recover)

    def stunned(t):
        # Dizzy belly up (loop): legs kicking, wing twitches, the head wobbling.
        knocked_out(1, t, 1)
        return p.snapshot()

    sampled(c, 'StunnedLoop', STUNNED_FRAMES, stunned)
    c.contact['minimum_surface_leg_reach_margin_m'] = surface_reach_margin
    c.contact['minimum_any_pose_leg_reach_margin_m'] = p.minimum_reach_margin
    c.contact['surface_walk'] = surface_contract()
    c.contact['flight'] = flight_contract()
    c.contact['bite_tip_policy'] = 'Head/Thorax/Proboscis bind transforms retained throughout BiteStart/BiteLoop/Bite'
    c.contact['fall_contact_policy'] = 'evaluated mesh minimum at authored support plane; native landing/transition review pending'
