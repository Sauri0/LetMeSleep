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
    # These are deliberately historical, failed reviews, not approved asset sheets.
    review_dir = args.output.parent / 'Alfa-VisualRecovery'
    evidence = [
        ('ui-menu-review', 'Menú principal · captura real 1080p', 'menu-1080.png',
         ['Personaje y mosquito demasiado oscuros.', 'Faroles sobreexpuestos.', 'Fondo del lobby vacío; corrección en curso.']),
        ('ui-customization-review', 'Personalizador · captura real 720p', 'customization-720.png',
         ['Proporción del visor por corregir.', 'Etiqueta Centrar truncada.', 'Icono de cabecera sin forma legible.']),
    ]
    for asset_id, name, filename, issues in evidence:
        source = review_dir / filename
        if source.is_file():
            shutil.copy2(source, media / filename)
            assets.append(dict(id=asset_id, name=name, category='UI', owner='Revisar interfaz visual',
                status='Revisado con fallas', pending=issues, variants=[], animations=[],
                evidence=[dict(path='media/' + filename, type='image',
                    sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                    note='Captura de integración 2026-09-12; anterior a correcciones siguientes. Personajes anteriores al nuevo lote M1.')]))
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=args.repository, text=True).strip()
    bundle = dict(inventory=inventory, indexGeneratedFromCommit=commit,
                  note='El commit del índice no acredita el contenido de cada captura. Consultar la evidencia individual.')
    (args.output / 'catalog.json').write_text(json.dumps(bundle, ensure_ascii=False, indent=2), encoding='utf-8')
    payload = json.dumps(bundle, ensure_ascii=False).replace('<', '\\u003c')
    template = Path(__file__).with_name('template.html').read_text(encoding='utf-8')
    (args.output / 'index.html').write_text(template.replace('@@CATALOG@@', payload), encoding='utf-8')
    print(json.dumps({'index': str(args.output / 'index.html'), 'entries': len(assets),
                      'sourceCommit': commit, 'images': len(list(media.glob('*.png')))}))


if __name__ == '__main__':
    main()
