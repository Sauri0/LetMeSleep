"""Analytic source study of distance/cadence/reach; no bpy or native execution."""
import hashlib
import json
import math
from pathlib import Path

from author_mosquito_motion import surface_step, surface_leg_ranges, surface_contract

ROOT = Path(__file__).resolve().parent


def main():
    trials = []
    for distance in (.08, .10, .12):
        for duty in (.55, .58, .60):
            ranges = surface_leg_ranges(distance_per_cycle_unity_m=distance, stance_duty=duty)
            trials.append({'distance_per_cycle_unity_m': distance, 'duty': duty,
                           'stride_source_m': distance * duty / .5, 'cadence_at_065_mps_hz': .65 / distance,
                           'minimum_extension_margin_source_m': min(r['extension_margin_source_m'] for r in ranges),
                           'leg_ranges': ranges})
    contract = surface_contract()
    duty = contract['stance_duty']
    samples = []
    minimum_supports = 6
    max_world_stance_velocity = 0
    epsilon = 1e-7
    # Reconstruct actor distance from phase and verify stationary world support.
    for frame in range(1201):
        time = frame / 1200
        supported = []
        for side in ('L', 'R'):
            for leg in range(1, 4):
                y, z, contact = surface_step(time, leg, side)
                if contact:
                    supported.append(f'Leg{leg}03.{side}')
                    later = surface_step(time + epsilon, leg, side)
                    if later[2] and later[0] >= y:
                        y_speed_per_phase = (later[0] - y) / epsilon * .5
                        world_velocity = (y_speed_per_phase - contract['distance_per_cycle_unity_m']) * contract['nominal_cadence_hz']
                        max_world_stance_velocity = max(max_world_stance_velocity, abs(world_velocity))
        minimum_supports = min(minimum_supports, len(supported))
        if frame % 100 == 0:
            samples.append({'phase': time, 'supporting_targets': supported})
    boundary_rows = []
    for boundary in (duty, 1):
        before = surface_step(boundary - epsilon, 1, 'L')
        at = surface_step(boundary, 1, 'L')
        after = surface_step(boundary + epsilon, 1, 'L')
        before_velocity = tuple((at[i] - before[i]) / epsilon for i in range(2))
        after_velocity = tuple((after[i] - at[i]) / epsilon for i in range(2))
        boundary_rows.append({'phase': boundary, 'before_velocity_source_m_per_cycle': before_velocity,
                              'after_velocity_source_m_per_cycle': after_velocity,
                              'velocity_difference_source_m_per_cycle': math.dist(before_velocity, after_velocity)})
    errors = []
    if minimum_supports < 3:
        errors.append('support target count below three')
    if max_world_stance_velocity > 1e-6:
        errors.append('world support target slides analytically')
    if any(row['velocity_difference_source_m_per_cycle'] > 1e-4 for row in boundary_rows):
        errors.append('target velocity discontinuity at swing boundary')
    if min(row['extension_margin_source_m'] for row in contract['analytic_leg_ranges']) < .0002:
        errors.append('selected target exceeds unclamped IK range')
    hashes = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in (
        'author_mosquito_geometry.py', 'author_mosquito_motion.py', 'study_mosquito_surface.py')}
    report = {'schema': 'lms-mosquito-surface-study-v1', 'passed': not errors, 'errors': errors,
              'scope': '1201 analytic phases per leg per trial; unchanged rigid lengths, target reach, support count and target velocity only',
              'blender_executed': False, 'skin_contact_verified': False, 'runtime_verified': False,
              'source_sha256': hashes, 'geometry_unchanged_from_11a2cc9': hashes['author_mosquito_geometry.py'] ==
              'f10c538113b5b380ffcfa3ebfceaf231268e2fc2b89c1c80319e289f0ebf6203',
              'selected_contract': contract, 'trials': trials,
              'selection_reason': '.10 m/cycle gives 6.5 Hz at unchanged .65 m/s, more reach margin than .12, lower cadence than .08; .58 duty retains overlap and swing time',
              'minimum_support_targets': minimum_supports, 'support_samples': samples,
              'maximum_analytic_world_stance_velocity_mps': max_world_stance_velocity,
              'swing_boundary_velocity': boundary_rows,
              'pending': ['evaluated mesh contact/IK and self-intersection', 'FBX roundtrip',
                          'local/remote phase binding', '60fps clip legibility and real transition blends']}
    out = ROOT.parents[2] / 'docs/unity/mosquito/SURFACE-STUDY-20260912.json'
    out.write_text(json.dumps(report, indent=2), encoding='utf8', newline='\n')
    print(json.dumps({'passed': not errors, 'errors': errors, 'trial_count': len(trials),
                      'distance_per_cycle_unity_m': contract['distance_per_cycle_unity_m'],
                      'nominal_cadence_hz': contract['nominal_cadence_hz'],
                      'minimum_extension_margin_source_m': min(r['extension_margin_source_m'] for r in contract['analytic_leg_ranges']),
                      'minimum_support_targets': minimum_supports,
                      'maximum_analytic_world_stance_velocity_mps': max_world_stance_velocity,
                      'swing_boundary_velocity': boundary_rows}, indent=2))
    assert not errors, 'Surface study failed'


if __name__ == '__main__':
    main()
