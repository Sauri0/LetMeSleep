"""Read-only publication audit; never prints matched credential contents."""
from pathlib import Path
import json
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
git = lambda *args: subprocess.check_output(['git', '-C', str(ROOT), *args])
patterns = {
    'github_token': re.compile(rb'(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})'),
    'private_key': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'),
    'aws_access_key': re.compile(rb'AKIA[0-9A-Z]{16}'),
    'slack_token': re.compile(rb'xox[baprs]-[A-Za-z0-9-]{20,}'),
}
suspect_path = re.compile(r'(^|/)(?:\.env(?:\.|$)|\.godot/|\.git/|preferences\.cfg$|hosts\.yml$)|\.(?:log|err|pem|key|pfx|p12)$', re.I)
objects = []
for line in git('rev-list', '--objects', '--all').decode().splitlines():
    parts = line.split(' ', 1)
    if len(parts) == 2:
        objects.append(parts)
findings = []
scanned = 0
largest = {'bytes': 0, 'path': ''}
for oid, path in objects:
    if git('cat-file', '-t', oid).strip() != b'blob':
        continue
    size = int(git('cat-file', '-s', oid))
    if size > largest['bytes']:
        largest = {'bytes': size, 'path': path}
    if suspect_path.search(path):
        findings.append({'path': path, 'kind': 'sensitive_or_generated_filename'})
    if size > 2_000_000 or Path(path).suffix.lower() in {'.ttf', '.png', '.jpg', '.wav', '.ogg', '.exe', '.zip'}:
        continue
    content = git('cat-file', 'blob', oid)
    scanned += 1
    for name, pattern in patterns.items():
        if pattern.search(content):
            findings.append({'path': path, 'object': oid, 'kind': name})
archives = []
version = re.search(r'^config/version="([0-9.]+)"$', (ROOT / 'game/project.godot').read_text(encoding='utf-8'), re.M).group(1)
for filename in [f'Let-me-sleep-{version}-Windows.zip', f'Let-me-sleep-{version}-fuentes.zip']:
    with zipfile.ZipFile(ROOT / 'outputs' / filename) as archive:
        names = archive.namelist()
        archives.append({'file': filename, 'entries': len(names), 'suspect_names': [name for name in names if suspect_path.search(name)]})
report = {'commit': git('rev-parse', 'HEAD').decode().strip(), 'commits': int(git('rev-list', '--count', 'HEAD')),
          'text_blobs_scanned': scanned, 'largest_blob': largest, 'findings': findings, 'archives': archives,
          'scope': 'Known credential patterns and sensitive/generated filenames across reachable history; not a proof of absence of all secrets.'}
(Path(__file__).parent / 'history-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
