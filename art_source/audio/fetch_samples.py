"""Fetch only the CC0 instrument notes used by the original score, not full banks."""
import concurrent.futures, hashlib, json, pathlib, re, urllib.request, urllib.parse

ROOT = pathlib.Path(__file__).resolve().parent
DEST = ROOT / 'instruments' / 'vsco2ce'
DEST.mkdir(parents=True, exist_ok=True)
HEADERS = {'User-Agent': 'Let-me-sleep-offline-audio-production'}
def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=60) as response:
        return response.read()

commit = '440300901dfe9275fd84e0b7763af1f8443ae62e'
paths = []
for note in ['C1','D1','A1','C#2']:
    for rr in [1,2]:
        paths.append(('bass', f'Strings/Solo Contrabass/Pizz/BKCtbss_Pizz_{note}_v1_rr{rr}.wav'))
for note in ['D3','F3','A3','C4']:
    for rr in [1,2]:
        paths.append(('pizz', f'Strings/Viola Section/pizz/ViolaEns_pizz_{note}_v1_rr{rr}.wav'))
for note in ['D3','F3','A#3','D4']:
    paths.append(('clarinet', f'Woodwinds/Clarinet/susLong/DCClar_susLong_{note}_v1_rr1_sum.wav'))
for note in ['C4','G4','F3']:
    paths.append(('marimba', f'Percussion/Marimba/Marimba_hit_Outrigger_{note}_loud_01.wav'))
paths.append(('glock','Percussion/Glock/glock_medium_C5.wav'))

def fetch(entry):
    instrument, upstream = entry
    url = f'https://raw.githubusercontent.com/sgossner/VSCO-2-CE/{commit}/'+urllib.parse.quote(upstream, safe='/')
    path = DEST / pathlib.Path(upstream).name
    if not path.exists():
        path.write_bytes(get(url))
    data = path.read_bytes()
    note = re.search(r'_([A-G]#?\d)(?:_|\.wav)', path.name).group(1)
    return {'instrument':instrument,'note_label':note,'file':path.relative_to(ROOT).as_posix(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'url':url,'license':'CC0-1.0'}

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
    samples = list(executor.map(fetch, paths))
license_url = f'https://raw.githubusercontent.com/sgossner/VSCO-2-CE/{commit}/LICENSE'
(DEST / 'LICENSE-CC0.txt').write_bytes(get(license_url))
(ROOT / 'sample-provenance.json').write_text(json.dumps({'library':'VSCO 2 Community Edition','recorded_by':'Sam Gossner and Simon Dalzell; sample cutting Elan Hickler/Soundemote','source':'https://versilian-studios.com/vsco-community/','repository_commit':commit,'license_url':license_url,'samples':samples}, indent=2),encoding='utf-8')
print(f'Downloaded {len(samples)} CC0 notes, {sum(s["bytes"] for s in samples):,} bytes; commit {commit}')
