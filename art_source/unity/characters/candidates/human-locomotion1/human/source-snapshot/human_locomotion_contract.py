"""Human flat-ground gait proposal; pure math shared by author and auditors.

Nominal cycle time differs from file duration. No runtime activation is implied.
"""
import math

REVISION = 'human-locomotion-v1-prepared'
BASELINE_BLEND_SHA256 = '918ddcf802ef761cf89bf20bedcec4019de8d8adc4bb765c2e5c7361e3f862a1'
FPS = 30
END_FRAME = 61
SOURCE_DURATION = (END_FRAME - 1) / FPS
PROFILES = [
    dict(clip='Human_WalkSlow', reference='Human_Walk', speed=1., contacts=2.4,
         duty=.60, hip=.695, rise=.025, lift=.09, ramp=.22, lean=.025, arm=.16, elbow=.2),
    dict(clip='Human_Walk', reference='Human_Walk', speed=1.55, contacts=3.2,
         duty=.56, hip=.685, rise=.035, lift=.10, ramp=.22, lean=.04, arm=.16, elbow=.2),
    dict(clip='Human_Trot', reference='Human_Run', speed=3.1, contacts=4.,
         duty=.32, hip=.69, compression=.030, lift=.15, ramp=.18, lean=.08, arm=.28, elbow=.6),
    dict(clip='Human_Run', reference='Human_Run', speed=5., contacts=4.6,
         duty=.24, hip=.68, compression=.035, lift=.20, ramp=.16, lean=.10, arm=.28, elbow=.6),
]
REPLACED = frozenset(('Human_Walk', 'Human_Run'))
GRIP_BONES = tuple(name + '.' + side for side in ('L', 'R') for name in
                   ['Hand', 'Socket.Grip'] + [f'{digit}{i:02d}' for digit in
                   ('Index', 'Middle', 'Ring', 'Little', 'Thumb') for i in range(1, 4)])


def nominal_duration(profile):
    return 2 / profile['contacts']


def distance(profile):
    return profile['speed'] * nominal_duration(profile)


def smooth(value):
    t = max(0., min(1., value))
    return t * t * t * (10 + t * (-15 + 6 * t))


def foot(phase, profile):
    """Return actor-forward ankle coordinate, source Z, and stance flag."""
    q = phase % 1
    duty = profile['duty']
    d = distance(profile)
    span = d * duty
    if q < duty:
        return span / 2 - d * q, .12, True
    u = (q - duty) / (1 - duty)
    forward = -span / 2 - d * (1 - duty) * u + d * smooth(u)
    z = .12 + profile['lift'] * smooth(u / profile['ramp']) * smooth((1 - u) / profile['ramp'])
    return forward, z, False


def hip_height(phase, profile):
    """UpperLeg origin height; Hips origin is 30 mm lower."""
    if profile['duty'] >= .5:
        return profile['hip'] + profile['rise'] * math.sin(2 * math.pi * phase) ** 2
    duration = nominal_duration(profile)
    local = phase % .5
    stance = profile['duty'] * duration
    flight = (.5 - profile['duty']) * duration
    if local <= profile['duty']:
        w = local / profile['duty']
        a = 9.81 * flight * stance / (2 * math.pi)
        return profile['hip'] - a * math.sin(math.pi * w) - (profile['compression'] - a) * math.sin(math.pi * w) ** 2
    t = (local - profile['duty']) * duration
    return profile['hip'] + .5 * 9.81 * t * (flight - t)


def sample_phases(steps=240):
    """Uniform subframes plus every stance boundary, including the loop seam."""
    return sorted({i / steps for i in range(steps + 1)} |
                  {q for p in PROFILES for q in (p['duty'], (p['duty'] + .5) % 1)})


def manifest():
    return dict(revision=REVISION, status='prepared-not-exported',
                baseline_blend_sha256=BASELINE_BLEND_SHA256,
                source_fps=FPS, source_start_frame=1, source_end_frame=END_FRAME,
                coordinate_system='source Z up / -Y forward; actor=(-sourceX, sourceZ, -sourceY)',
                profiles=[dict(p, nominal_duration_seconds=nominal_duration(p),
                               distance_per_cycle_m=distance(p), source_duration_seconds=SOURCE_DURATION,
                               contact_markers=[dict(foot='L', phase=0.), dict(foot='R', phase=.5)],
                               stance_duration_phase=p['duty'], loop=True, root_motion=False)
                          for p in PROFILES],
                playback='phase += presented_horizontal_distance / D; sample frac(phase) * actual imported Clip.length',
                gates_pending=['native source/FBX audit', 'Unity import and reference assignment',
                               'mesh/articulation and audio perception', 'blend transitions and starts/stops',
                               'crouched locomotion, turns and uneven terrain'])
