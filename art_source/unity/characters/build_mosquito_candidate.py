"""Explicit mosquito-only build. Run only in a Director-authorized Blender slot.

blender --background --factory-startup --threads 2 --python-exit-code 1
        --python build_mosquito_candidate.py
Never exports Human/Flyswatter or writes the shared manifest.
"""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))


def main():
    import bpy
    import build_characters as common
    from author_mosquito_geometry import create_mosquito, REVISION
    from author_mosquito_motion import mosquito

    files = ('build_mosquito_candidate.py', 'author_mosquito_geometry.py',
             'author_mosquito_motion.py', 'author_mosquito_face.py', 'build_characters.py', 'author_motion.py')
    hashes = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in files}
    c = create_mosquito(**{name: getattr(common, name) for name in
                          ('Character', 'material', 'tube', 'ellipsoid', 'strip', 'mesh')})
    mosquito(c)
    audit = c.export()
    result = {'revision': REVISION, 'source_sha256': hashes, 'audit': audit,
              'blender': bpy.app.version_string, 'rendered': False,
              'unity_import_verified': False, 'art_accepted': False}
    result['outputs_sha256'] = {name: hashlib.sha256((ROOT / 'mosquito' / name).read_bytes()).hexdigest()
                                for name in ('LMS_Mosquito_alpha.blend', 'LMS_Mosquito_alpha.fbx', 'audit.json')}
    (ROOT / 'mosquito' / 'candidate.json').write_text(json.dumps(result, indent=2), encoding='utf8', newline='\n')
    print('LMS_MOSQUITO_CANDIDATE', json.dumps(result['outputs_sha256']), flush=True)


if __name__ == '__main__':
    main()
