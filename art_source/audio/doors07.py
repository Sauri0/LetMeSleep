"""Original deterministic hinged-door Foley; no recordings or online services."""
import json
import pathlib
import subprocess
import wave
import numpy as np

ROOT = pathlib.Path(__file__).resolve().parent
ASSETS = ROOT.parents[1] / 'game/assets/audio/sfx'
RATE = 44100
rng = np.random.default_rng(707)

def write(name, pcm):
    pcm = np.asarray(pcm, dtype=float)
    pcm *= .38 / max(.001, np.max(np.abs(pcm)))
    pcm[:160] *= np.linspace(0, 1, 160)
    pcm[-400:] *= np.linspace(1, 0, 400)
    source = ROOT / 'masters/sfx' / (name + '.wav')
    with wave.open(str(source), 'wb') as output:
        output.setparams((1, 2, RATE, len(pcm), 'NONE', 'not compressed'))
        output.writeframes((pcm * 32767).astype('<i2').tobytes())
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-i', str(source), '-c:a', 'libvorbis', '-q:a', '5',
                    str(ASSETS / (name + '.ogg'))], check=True)
    return {'name': name, 'loop': False, 'seconds': len(pcm)/RATE,
            'peak': float(np.abs(pcm).max()), 'rms': float(np.sqrt(np.mean(pcm**2)))}

t = np.arange(int(.82 * RATE)) / RATE
phase = 2*np.pi*np.cumsum(180 + 28*np.sin(2*np.pi*2.7*t) + 16*t)/RATE
rub = sum(np.sin(phase*i)/i**1.65 for i in range(1, 7))
grain = np.convolve(rng.normal(size=len(t)), np.ones(23)/23, mode='same')
creak = (rub*.13 + grain*.2) * np.sin(np.pi*t/t[-1])**.7
events = [write('door_move', creak)]
for name, seconds, frequencies, decay in [
    ('door_latch', .32, [310, 680, 1130], 28),
    ('door_block', .28, [125, 205, 440], 22),
]:
    t = np.arange(int(seconds*RATE))/RATE
    body = sum(np.sin(2*np.pi*f*t)*np.exp(-t*(decay+i*12))/(i+1)
               for i,f in enumerate(frequencies))
    transient = rng.normal(size=len(t))*np.exp(-t*160)*.25
    if name == 'door_latch':
        offset = int(.055*RATE)
        body[offset:] += .45*body[:-offset]
    events.append(write(name, body+transient))
metrics = json.loads((ROOT/'sfx-metrics.json').read_text())
metrics = [item for item in metrics if item['name'] not in {e['name'] for e in events}]
(ROOT/'sfx-metrics.json').write_text(json.dumps(metrics+events, indent=2))
print(json.dumps({'generated': events}))
