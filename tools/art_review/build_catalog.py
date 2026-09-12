"""Build an offline, searchable index of real alpha assets and review evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--repository', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, action='append', default=[])
    args = parser.parse_args()
    inventory = json.loads(args.inventory.read_text(encoding='utf-8-sig'))
    assets = inventory['assets']
    if not isinstance(assets, list) or not all(isinstance(a, dict) and a.get('id') for a in assets):
        raise ValueError('Inventory requires assets with stable IDs')
    if len({a['id'] for a in assets}) != len(assets):
        raise ValueError('Duplicate asset IDs')
    args.output.mkdir(parents=True, exist_ok=True)
    media = args.output / 'media'
    media.mkdir(exist_ok=True)
    by_id = {a['id']: a for a in assets}
    for manifest in args.evidence:
        for item in json.loads(manifest.read_text(encoding='utf-8-sig')):
            if item['assetId'] not in by_id:
                raise ValueError('Unknown evidence asset: ' + item['assetId'])
            source = Path(item['path'])
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            if item.get('sha256', digest) != digest:
                raise ValueError('Evidence hash mismatch: ' + str(source))
            filename = digest[:12] + '-' + source.name
            shutil.copy2(source, media / filename)
            record = dict(item, path='media/' + filename, sha256=digest)
            asset = by_id[item['assetId']]
            existing = next((e for e in asset.get('evidence', [])
                             if e.get('sha256') == digest), None)
            if existing is None:
                asset.setdefault('evidence', []).append(record)
            else:
                existing.update(record)
            if item.get('current', False):
                asset['status'] = item.get('status', 'Capturado · revisión pendiente')
    # Embed native inventory images in their existing cards, preserving history.
    for asset in assets:
        for record in asset.get('evidence', []):
            if record.get('kind') != 'native_png' or record.get('path', '').startswith('media/'):
                continue
            source = Path(record['path'])
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            if record.get('sha256', digest) != digest:
                raise ValueError('Inventory evidence hash mismatch: ' + str(source))
            filename = digest[:12] + '-' + source.name
            shutil.copy2(source, media / filename)
            record.update(originalPath=str(source), path='media/' + filename,
                          type='image', sha256=digest)
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=args.repository, text=True).strip()
    bundle = dict(inventory=inventory, indexGeneratedFromCommit=commit,
                  note='El commit del índice no acredita el contenido de cada captura. Consultar la evidencia individual.')
    (args.output / 'catalog.json').write_text(json.dumps(bundle, ensure_ascii=False, indent=2), encoding='utf-8')
    payload = json.dumps(bundle, ensure_ascii=False).replace('<', '\\u003c')
    template = Path(__file__).with_name('template.html').read_text(encoding='utf-8')
    (args.output / 'index.html').write_text(template.replace('@@CATALOG@@', payload), encoding='utf-8')
    print(json.dumps({'index': str(args.output / 'index.html'), 'entries': len(assets),
                      'sourceCommit': commit, 'images': sum(
                          e.get('type') == 'image' for a in assets for e in a.get('evidence', []))}))


if __name__ == '__main__':
    main()
