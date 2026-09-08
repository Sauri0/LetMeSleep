"""Index actual capture batches, preserving missing and unreviewed coverage."""
from pathlib import Path
import argparse, hashlib, html, json

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--directory', type=Path, default=ROOT/'outputs/0.7-combinaciones')
parser.add_argument('--catalog', type=Path, default=ROOT/'work/cosmetics-catalog08.json')
parser.add_argument('--require-complete-captures', action='store_true')
args = parser.parse_args()
folder = args.directory.resolve()
folder.mkdir(parents=True, exist_ok=True)

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()

catalog = read(args.catalog)
ledger_path = folder/'visual-review.json'
ledger = read(ledger_path) if ledger_path.exists() else {'sheets': {}}
summary = {'schema': 1, 'catalog_sha256': digest(args.catalog), 'roles': {},
           'geometry_validation_status': 'separate_report_required',
           'continuous_turn_status': 'separate_capture_required',
           'scope': 'Captures and human review are counted separately. Enumeration alone does not certify geometry. Colors and body/head independence require the real mesh invariance gate.'}
cards = []
capture_exe_hashes = set()
capture_modes = set()
for role, expected in [('human', 2916), ('mosquito', 243)]:
    domain = catalog['roles'][role]
    head_keys = ['eyes', 'mouth', 'brows', 'hair', 'accessory'] + (['mustache', 'beard'] if role == 'human' else [])
    geometry_keys = domain['geometry_keys']
    mappings = []
    for item in domain['geometry_classes']:
        selectors = dict(zip(geometry_keys, item['indices'], strict=True))
        ordinal, place = 0, 1
        for key in head_keys:
            ordinal += selectors[key] * place
            place *= len(domain['domains'][key]['ids'])
        assert place == expected
        mappings.append({'geometry_class_id': item['id'], 'head_id': f'{"H" if role == "human" else "M"}-H{ordinal:04d}',
                         'head_ordinal': ordinal, 'page': ordinal//24,
                         'body_selectors': {k: v for k, v in selectors.items() if k not in head_keys}})
    captured, reviewed, sheets, invalid = set(), set(), [], []
    states_by_head = {}
    for page in range((expected+23)//24):
        label = f'{role}-page-{page:03d}'
        report_path, run_path = folder/f'{label}.json', folder/f'{label}.run.json'
        if not report_path.exists() or not run_path.exists():
            continue
        report, run = read(report_path), read(run_path)
        valid = report['capture_passed'] and report.get('actor_content_checked', False) and run['passed'] and report['role'] == role and report['page'] == page
        valid = valid and len(report.get('captured_views', [])) == report['captured_head_count']*8*len(report['states'])
        valid = valid and all(v['occupied_actor_samples'] >= 24 for v in report.get('captured_views', []))
        for relative, expected_hash in run['source_sha256'].items():
            valid = valid and digest(ROOT/relative) == expected_hash.upper()
        for sheet in report['sheets']:
            path = folder/sheet['file']
            valid = valid and path.exists() and digest(path) == run['sheet_sha256'][sheet['file']].upper()
        if not valid:
            invalid.append(label)
            continue
        capture_exe_hashes.add(run['exe_sha256'].upper())
        capture_modes.add('source' if run['source'] else 'exported_exe')
        for record in report['records']:
            ordinal = record['ordinal']
            assert 0 <= ordinal < expected
            states_by_head.setdefault(ordinal, set()).add(record['state'])
            if record['state'] == 'neutral':
                captured.add(ordinal)
        for sheet in report['sheets']:
            file = sheet['file']
            review = ledger['sheets'].get(file, {})
            # A review is tied to the exact image, a named reviewer and findings.
            is_reviewed = (review.get('status') in ['reviewed_clear', 'reviewed_findings']
                           and review.get('sha256', '').upper() == digest(folder/file)
                           and bool(review.get('reviewer')) and 'findings' in review)
            sheets.append({'file': file, 'state': sheet['state'], 'views': sheet['views'], 'reviewed': is_reviewed,
                           'review': review if is_reviewed else None})
            status = review.get('status', 'pending') if is_reviewed else 'pending'
            cards.append(f'<article data-role="{role}"><a href="{html.escape(file)}"><img loading="lazy" src="{html.escape(file)}" alt="{html.escape(file)}"></a><p>{html.escape(file)}<br>{html.escape(status)}</p></article>')
        neutral_sheets = [s for s in report['sheets'] if s['state'] == 'neutral']
        covered_angles = {v for s in neutral_sheets for v in s['views']}
        assert covered_angles == {'front', 'quarter-left', 'profile-left', 'profile-right', 'quarter-right', 'back', 'above-quarter', 'below-quarter'}
        reviewed_files = {s['file'] for s in sheets if s['reviewed']}
        if all(s['file'] in reviewed_files for s in neutral_sheets):
            reviewed.update(r['ordinal'] for r in report['records'] if r['state'] == 'neutral')
    summary['roles'][role] = {'expected_head_combinations': expected,
        'captured_neutral_eight_views': len(captured), 'visually_reviewed_neutral_eight_views': len(reviewed),
        'missing_head_ordinals': sorted(set(range(expected))-captured), 'invalid_batches': invalid,
        'valid_geometry_classes': domain['geometry_class_count'], 'valid_profiles_including_color': domain['valid_profile_count'],
        'source_geometry_to_head_mapping': mappings, 'sheets': sheets,
        'captured_states_by_head': {str(k): sorted(v) for k, v in states_by_head.items()}}
summary['all_neutral_heads_captured'] = all(not r['missing_head_ordinals'] and not r['invalid_batches'] for r in summary['roles'].values())
summary['all_neutral_heads_visually_reviewed'] = all(r['visually_reviewed_neutral_eight_views'] == r['expected_head_combinations'] for r in summary['roles'].values())
summary['capture_exe_sha256'] = sorted(capture_exe_hashes)
summary['capture_modes'] = sorted(capture_modes)
(folder/'coverage.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
counts = '; '.join(f'{role}: {r["captured_neutral_eight_views"]}/{r["expected_head_combinations"]} capturadas, {r["visually_reviewed_neutral_eight_views"]} revisadas' for role, r in summary['roles'].items())
document = f'''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Combinaciones de cabeza · 0.7</title><style>body{{background:#142733;color:#edf0e8;font:16px system-ui;margin:24px}}h1{{font-size:25px}}main{{display:grid;grid-template-columns:repeat(auto-fit,minmax(440px,1fr));gap:20px}}img{{width:100%;height:auto}}article{{background:#203e4b;padding:10px}}a{{color:#bddde5}}button{{font:inherit;padding:8px 16px;margin:0 8px 18px 0}}p{{line-height:1.5}}</style><h1>Combinaciones de cabeza</h1><p>{html.escape(counts)}</p><p>Cada hoja tiene 24 cabezas, dos vistas por cabeza e identificadores. Abrí la imagen para verla a tamaño completo. Capturada no significa revisada. <a href="coverage.json">Cobertura y equivalencias del catálogo</a>.</p><button onclick="show('all')">Todas</button><button onclick="show('human')">Humano</button><button onclick="show('mosquito')">Mosquito</button><main>{''.join(cards)}</main><script>function show(role){{document.querySelectorAll('article').forEach(a=>a.hidden=role!=='all'&&a.dataset.role!==role);}}</script></html>'''
(folder/'index.html').write_text(document, encoding='utf-8')
print(json.dumps({'counts': counts, 'all_captured': summary['all_neutral_heads_captured'], 'all_reviewed': summary['all_neutral_heads_visually_reviewed']}, ensure_ascii=False))
if args.require_complete_captures and not summary['all_neutral_heads_captured']:
    raise SystemExit('Incomplete capture coverage; see coverage.json.')
