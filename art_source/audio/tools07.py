"""Distinct tool Foley, assembled from this project's original acoustic masters.

No downloaded samples or runtime synthesis. Retains editable WAV masters and
records duration, RMS, peak and hash for each exported Vorbis asset.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
MASTERS = ROOT / 'art_source/audio/masters/sfx'
OUT = ROOT / 'game/assets/audio/sfx'
SR = 44100

def source(name, duration, rate=1.0):
    with wave.open(str(MASTERS / (name+'.wav')), 'rb') as f:
        assert f.getsampwidth() == 2 and f.getframerate() == SR
        x = np.frombuffer(f.readframes(f.getnframes()), '<i2').astype(float)/32768
        x = x.reshape(-1, f.getnchannels()).mean(axis=1)
    n = round(duration*SR)
    y = np.interp(np.arange(n)*rate, np.arange(len(x)), x, right=0)
    fade = np.minimum(1, np.arange(n)/(SR*.004))*np.minimum(1, np.arange(n)[::-1]/(SR*.018))
    return y*fade

def mix(duration, *parts):
    return sum(source(name,duration,rate)*gain for name,gain,rate in parts)

events = {}
for tool, material, dur, rate in [('swatter','pickup',.13,1.3),('racket','pickup',.17,.8),
                                ('newspaper','cloth',.22,1.5),('broom','step_wood',.20,.9),
                                ('slipper','cloth',.19,.7)]:
    sweep = 'tool_'+tool if tool!='slipper' else 'step_cloth'
    events['equip_'+tool] = mix(dur,(material,.75,rate),(sweep,.30,1.6))
    events['hit_'+tool] = mix(dur,(sweep,.70,.8),(('step_cloth' if tool=='slipper' else 'clap'),.45,rate))
events['tool_slipper'] = mix(.29,('swish',.55,.75),('cloth',.75,1.0))
events['throw_newspaper'] = mix(.32,('tool_newspaper',.75,1.1),('swish',.4,1.35))
events['throw_slipper'] = mix(.38,('cloth',.8,.95),('swish',.65,.75))
events['land_wood'] = mix(.18,('step_wood',.9,1.3),('drop',.15,1.0))
events['land_tile'] = mix(.16,('step_tile',.8,1.55),('pickup',.15,1.4))
events['land_cloth'] = mix(.22,('step_cloth',.85,.8),('cloth',.2,.8))
records = []
for name,x in events.items():
    # Fixed event headroom; no limiter/normalizer is applied during gameplay.
    peak = max(float(np.abs(x).max()),1e-9)
    x *= min(1.0,.62/peak)
    path = MASTERS / (name+'.wav')
    pcm = np.rint(x*32767).astype('<i2')
    with wave.open(str(path),'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(SR); f.writeframes(pcm.tobytes())
    target = OUT / (name+'.ogg')
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(path),'-c:a','libvorbis','-q:a','5',str(target)],check=True)
    records.append({'cue':name,'duration':len(x)/SR,'peak':float(np.abs(x).max()),
                    'rms':float(np.sqrt(np.mean(x*x))),'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
(ROOT/'art_source/audio/tools07-metrics.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
print('TOOLS07_AUDIO',len(records),'distinct events; original project masters')
