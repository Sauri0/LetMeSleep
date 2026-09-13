"""Offline mathematical regressions. Does not certify authored/imported animation."""
import argparse
import json
import math
from pathlib import Path
import unittest
from human_locomotion_contract import (PROFILES, GRIP_BONES, SOURCE_DURATION, distance,
                                        nominal_duration, foot, hip_height, manifest)


class ContractChecks(unittest.TestCase):
    def test_explicit_clip_duration_and_grip_contract(self):
        self.assertEqual([p['clip'] for p in PROFILES],
                         ['Human_WalkSlow', 'Human_Walk', 'Human_Trot', 'Human_Run'])
        self.assertEqual(len(set(GRIP_BONES)), 34)
        self.assertEqual(SOURCE_DURATION, 2.)
        self.assertEqual(manifest()['status'], 'prepared-not-exported')
        for p, expected in zip(PROFILES, (5 / 6, .96875, 1.55, 50 / 23)):
            self.assertAlmostEqual(distance(p), expected)
            self.assertNotAlmostEqual(nominal_duration(p), SOURCE_DURATION)

    def test_dense_full_swing_has_twenty_mm_extension_reserve(self):
        # These independently recorded extrema include swing overshoot, not just stance span.
        expected_max = (.638876, .636913, .633816, .636180)
        for p, expected in zip(PROFILES, expected_max):
            reaches = []
            for i in range(2401):
                phase = i / 2400
                for offset in (0., .5):
                    x, z, _ = foot(phase + offset, p)
                    reach = math.hypot(x, hip_height(phase, p) - z)
                    self.assertTrue(math.isfinite(reach))
                    self.assertGreater(reach, abs(.34 - .32) + .02)
                    self.assertLessEqual(reach, .64)
                    reaches.append(reach)
            self.assertAlmostEqual(max(reaches), expected, places=6)

    def test_stance_contacts_hold_world_position_and_height(self):
        for p in PROFILES:
            for offset in (0., .5):
                # A complete stance begins at global phase -offset.
                points = []
                for i in range(100):
                    phase = -offset + p['duty'] * i / 100
                    x, z, stance = foot(phase + offset, p)
                    self.assertTrue(stance)
                    self.assertEqual(z, .12)
                    points.append(x + distance(p) * phase)
                self.assertLess(max(points) - min(points), 1e-12)

    def test_foot_position_and_velocity_at_touchdown_and_liftoff(self):
        epsilon = 1e-6
        for p in PROFILES:
            for boundary in (0., p['duty'], 1.):
                before = foot(boundary - epsilon, p)
                at = foot(boundary, p)
                after = foot(boundary + epsilon, p)
                for axis in (0, 1):
                    left = (at[axis] - before[axis]) / epsilon
                    right = (after[axis] - at[axis]) / epsilon
                    self.assertAlmostEqual(left, right, delta=2e-5)
                    self.assertAlmostEqual(left, -distance(p) if axis == 0 else 0., delta=2e-5)

    def test_hip_height_and_velocity_at_each_stance_and_loop_boundary(self):
        epsilon = 1e-7
        for p in PROFILES:
            for boundary in (0., p['duty'], .5, (.5 + p['duty']) % 1, 1.):
                before = hip_height(boundary - epsilon, p)
                at = hip_height(boundary, p)
                after = hip_height(boundary + epsilon, p)
                self.assertAlmostEqual((at - before) / epsilon, (after - at) / epsilon, delta=2e-5)
            self.assertAlmostEqual(hip_height(0, p), hip_height(1, p))

    def test_contact_count_does_not_depend_on_file_seconds(self):
        # Check distance/phase interpretation against ten seconds of nominal travel.
        for p, expected in zip(PROFILES, (24, 32, 40, 46)):
            cycles = p['speed'] * 10 / distance(p)
            self.assertAlmostEqual(2 * cycles, expected)
            playback = SOURCE_DURATION * p['speed'] / distance(p)
            self.assertAlmostEqual(playback, p['contacts'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepared-json', type=Path)
    args = parser.parse_args()
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(ContractChecks)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if result.wasSuccessful() and args.prepared_json:
        args.prepared_json.parent.mkdir(parents=True, exist_ok=True)
        data = manifest()
        data['offline_validation'] = dict(tests_run=result.testsRun, status='passed',
                                          scope='Pure contract math only; bpy scripts syntax checked separately. Native export and audit have not run.')
        args.prepared_json.write_text(json.dumps(data, indent=2) + '\n', encoding='utf8', newline='\n')
    raise SystemExit(0 if result.wasSuccessful() else 1)
