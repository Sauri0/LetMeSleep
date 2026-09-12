"""Regression checks for the audit's stationary-head rotation blind spot; no bpy."""
import math
import unittest

from audit_mosquito_candidate import compare_poses, quaternion_angle_degrees, threshold_errors


def pose(rotation=(1, 0, 0, 0), marker=(.1, 0, 0)):
    return {'heads': {'Wing.L': (0, 0, 0)},
            'rotations': {'Wing.L': rotation},
            'markers': {'Wing.L/0': marker}}


class RotationAuditRegression(unittest.TestCase):
    def test_quaternion_sign_is_not_a_rotation_difference(self):
        q = (math.cos(.3), 0, math.sin(.3), 0)
        self.assertAlmostEqual(quaternion_angle_degrees(q, tuple(-v for v in q)), 0, places=5)

    def test_stationary_wing_root_does_not_hide_rotation(self):
        rotated = (math.sqrt(.5), 0, math.sqrt(.5), 0)
        result = compare_poses(pose(), pose(rotated))
        self.assertEqual(result['max_head_difference_source_m'], 0)
        self.assertAlmostEqual(result['max_deformation_rotation_difference_degrees'], 90)
        self.assertIn('max_deformation_rotation_difference_degrees', threshold_errors(result))

    def test_mesh_marker_detects_change_even_when_bone_snapshot_matches(self):
        result = compare_poses(pose(), pose(marker=(.105, 0, 0)))
        self.assertEqual(result['max_head_difference_source_m'], 0)
        self.assertEqual(result['max_deformation_rotation_difference_degrees'], 0)
        self.assertAlmostEqual(result['max_mesh_marker_difference_source_m'], .005)
        self.assertIn('max_mesh_marker_difference_source_m', threshold_errors(result))

    def test_invalid_quaternion_cannot_silently_pass(self):
        with self.assertRaises(ValueError):
            quaternion_angle_degrees((0, 0, 0, 0), (1, 0, 0, 0))


if __name__ == '__main__':
    unittest.main(verbosity=2)
