"""Merge a partial prop build into the committed library (plain Python, no Blender).

    N:/Blender/blender.exe --background --factory-startup --python build_props.py -- --only A,B --out DIR
    python merge_partial.py DIR

Partial builds never overwrite the library by themselves (build_props.py). This script copies the FBX of every
prop in DIR/manifest_partial.json beside this file and replaces or appends its entry in manifest.json, keeping every
other entry (and its FBX bytes) untouched, then refreshes the source hashes, totals and the pass flag. The editable
v030_props.blend is not rewritten: merged props live in DIR/v030_props_partial.blend and in props_catalog.py.
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    if len(sys.argv) != 2:
        raise SystemExit('usage: python merge_partial.py <partial build dir>')
    partial_dir = Path(sys.argv[1]).resolve()
    partial = json.loads((partial_dir / 'manifest_partial.json').read_text(encoding='utf-8'))
    library_path = HERE / 'manifest.json'
    library = json.loads(library_path.read_text(encoding='utf-8'))
    if not partial['passed']:
        raise SystemExit('partial build did not pass validation')
    by_name = {e['name']: i for i, e in enumerate(library['props'])}
    for entry in partial['props']:
        source = partial_dir / entry['fbx']
        if sha256(source) != entry['fbx_sha256']:
            raise SystemExit('hash mismatch ' + entry['fbx'])
        shutil.copyfile(source, HERE / entry['fbx'])
        if entry['name'] in by_name:
            library['props'][by_name[entry['name']]] = entry
        else:
            library['props'].append(entry)
        print('merged', entry['name'], entry['fbx_sha256'][:12])
    library['sources'] = {name: sha256(HERE / name) for name in ('build_props.py', 'props_catalog.py', 'props_lib.py')}
    library['partial_merges'] = sorted(set(library.get('partial_merges', [])) | {e['name'] for e in partial['props']})
    library['blend_note'] = ('v030_props.blend holds the props of the last full build; props listed in partial_merges were '
                             'built with --only and merged by merge_partial.py (their source is props_catalog.py).')
    entries = library['props']
    library['totals'] = {
        'props': len(entries),
        'triangles': sum(e['triangles'] for e in entries),
        'materials': sum(len(e['materials']) for e in entries),
        'emissive_props': [e['name'] for e in entries if any(m['emissive'] for m in e['materials'])],
    }
    library['passed'] = all(e['passed'] for e in entries)
    library_path.write_text(json.dumps(library, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('library props', len(entries), 'passed', library['passed'])


if __name__ == '__main__':
    main()
