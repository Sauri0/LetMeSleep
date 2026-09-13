"""Bounded pure-data checks. Never reads an export or writes a recipe."""
import unittest
import struct
from unittest.mock import patch
from pathlib import Path
import prepare_map_recipe as tool


def config():
    return dict(sourceFinal=True, mapId='hf-synthetic-v1', fbx='none.fbx', glb='none.glb', audit='none.json', report='none-report.json')


def empty(name, x):
    return dict(name=name, type='EMPTY', location=[999, 999, 999],
                matrix_world=[[1, 0, 0, x], [0, 1, 0, 2], [0, 0, 1, 3], [0, 0, 0, 1]])


class Checks(unittest.TestCase):
    def test_fbx_blender_scalar_bool_char_and_signed_byte(self):
        payload = b'B\x01B\x00CQZ' + struct.pack('<b', -7) + b'I' + struct.pack('<i', 123456)
        for version, layout, header in ((7400, '<IIIB', 13), (7500, '<QQQB', 25)):
            with self.subTest(version=version):
                prefix = b'Kaydara FBX Binary  \x00\x1a\x00' + struct.pack('<I', version)
                end = len(prefix) + header + 1 + len(payload) + header
                encoded = prefix + struct.pack(layout, end, 5, len(payload), 1) + b'P' + payload + bytes(header * 2)
                actual_version, nodes = tool.fbx_tree(encoded)
                self.assertEqual(actual_version, version)
                self.assertEqual(nodes[0]['props'], [True, False, b'Q', -7, 123456])

    def test_nonfinal_rejected_before_source_io(self):
        c = config(); c['sourceFinal'] = False
        with patch.object(Path, 'read_bytes', side_effect=AssertionError('Source access forbidden')):
            with self.assertRaisesRegex(ValueError, 'not confirmed final'):
                tool.prepare(c, Path('.'))

    def test_map_and_cup_parameters(self):
        for map_id in ('hf-isla-del-laguito-v1', 'hf-casa-del-patio-v1', 'hf-campamento-pinar-v1', 'hf-yate-a-la-deriva-v1', 'hf-puerto-del-faro-v1'):
            c = config(); c.update(mapId=map_id, humanCount=5, mosquitoCount=16)
            tool.validate_config(c)
        c['mapId'] = '../bad'
        with self.assertRaises(ValueError): tool.validate_config(c)
        c = config(); c['humanCount'] = 0
        with self.assertRaises(ValueError): tool.validate_config(c)

    def test_audit_role_required_and_conflict_rejected(self):
        o = dict(name='Trunk', properties={})
        with self.assertRaises(ValueError): tool.classify(o, {'extras': {'collision_role': 'static_solid'}}, 'Trunk', {})
        o['properties']['collision_role'] = 'static_solid'
        with self.assertRaises(ValueError): tool.classify(o, {'extras': {'collision_role': 'non_solid'}}, 'Trunk', {})
        self.assertEqual(tool.classify(o, {}, 'Trunk', {}), ('static_solid', 'solid'))

    def test_split_canopy_stays_nonsolid(self):
        o = dict(name='Crown', properties={'collision_role': 'non_solid'})
        self.assertEqual(tool.classify(o, {}, 'Tree/Crown', {}), ('non_solid', 'decoration'))
        self.assertEqual(tool.classify(o, {}, 'Tree/Crown', {'Tree/Crown': 'foliage'}), ('non_solid', 'foliage'))
        with self.assertRaises(ValueError): tool.classify(o, {}, 'Tree/Crown', {'Tree/Crown': 'solid'})

    def test_water_uses_explicit_role(self):
        o = dict(name='Water_Stream', properties={'collision_role': 'non_solid', 'wave_loop_seconds': 8})
        self.assertEqual(tool.classify(o, {}, o['name'], {})[1], 'water')

    def test_spawns_use_world_matrices_and_requested_minimum(self):
        objects = {f'Spawn_Human_{i:02}': empty(f'Spawn_Human_{i:02}', i) for i in range(1, 6)}
        paths = {name: 'Markers/' + name for name in objects}
        selected = tool.select_spawns(objects, paths, 'Spawn_Human_', 5)
        self.assertEqual(len(selected), 5)
        self.assertEqual(selected[0], ('Markers/Spawn_Human_01', [1, 2, 3]))
        with self.assertRaises(ValueError): tool.select_spawns(objects, paths, 'Spawn_Human_', 6)
        objects['Spawn_Human_01']['type'] = 'MESH'
        with self.assertRaises(ValueError): tool.select_spawns(objects, paths, 'Spawn_Human_', 5)


if __name__ == '__main__':
    unittest.main()
