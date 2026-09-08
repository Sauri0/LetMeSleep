"""Decode the actual deliverable assets, audit peaks/loops and write provenance."""
import hashlib,json,pathlib,shutil,subprocess,wave
import numpy as np
ROOT=pathlib.Path(__file__).resolve().parent
PROJECT=ROOT.parents[1]
ASSETS=PROJECT/'game/assets/audio'
sample_data=json.loads((ROOT/'sample-provenance.json').read_text(encoding='utf-8'))
assert len(sample_data['samples'])==24
for sample in sample_data['samples']:
    assert hashlib.sha256((ROOT/sample['file']).read_bytes()).hexdigest()==sample['sha256'], sample['file']
assert (ROOT/'instruments/vsco2ce/LICENSE-CC0.txt').read_text().startswith('CC0 1.0 Universal')
effects={e['name']:e for e in json.loads((ROOT/'sfx-metrics.json').read_text())}
tool_effects=json.loads((ROOT/'tools07-metrics.json').read_text())
assert len(tool_effects)==16
for effect in tool_effects:
    asset=ASSETS/'sfx'/(effect['cue']+'.ogg')
    assert hashlib.sha256(asset.read_bytes()).hexdigest()==effect['sha256'],asset
    effects[effect['cue']]={'loop':False}
items=[]
for path in sorted(ASSETS.rglob('*.ogg')):
    info=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration:stream=sample_rate,channels,codec_name','-of','json',str(path)]))
    stream=info['streams'][0]; channels=int(stream['channels'])
    raw=subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-f','f32le','-'])
    samples=np.frombuffer(raw,dtype='<f4').reshape(-1,channels)
    peak=float(abs(samples).max()); rms=float(np.sqrt(np.mean(samples**2)))
    assert np.isfinite(samples).all() and peak < .98 and rms>.00001,path
    assert stream['codec_name']=='vorbis' and stream['sample_rate']=='44100',path
    assert channels==(1 if path.parent.name=='sfx' else 2),path
    is_loop=effects[path.stem]['loop'] if path.parent.name=='sfx' else not path.stem.startswith('accent_')
    item={'file':path.relative_to(ASSETS).as_posix(),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'codec':'Vorbis','sample_rate':44100,'channels':channels,'seconds':float(info['format']['duration']),'decoded_frames':len(samples),'decoded_peak':peak,'decoded_rms':rms,'loop':is_loop}
    items.append(item)
for theme in ['menu','gameplay']:
    group=[e for e in items if e['file'].startswith('music/'+theme+'_')]
    assert len(group)==3 and len(set(e['decoded_frames'] for e in group))==1
    assert max(e['seconds'] for e in group)-min(e['seconds'] for e in group)<1/44100
    for layer in ['base','rhythm','melody']:
        with wave.open(str(ROOT/'masters/music'/f'{theme}_{layer}.wav')) as w:
            pcm=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2')
            assert w.getnframes()==3256615 and abs(int(pcm[0])-int(pcm[-1]))<=1
assert len(items)==67
shutil.copy2(ROOT/'instruments/vsco2ce/LICENSE-CC0.txt',ASSETS/'LICENSE-VSCO2CE-CC0.txt')
credits='''Let me sleep — audio 0.7

Original composition, arrangement, MIDI/event sources, sampling renderer,
Foley synthesis and mix were created for this project, offline in Python.
No external generative music service was used.

Acoustic instrument samples: VSCO 2 Community Edition, CC0 1.0 Universal.
Recorded by Sam Gossner and Simon Dalzell; sample cutting by Elan Hickler /
Soundemote. Source: https://versilian-studios.com/vsco-community/
Repository: https://github.com/sgossner/VSCO-2-CE
Pinned commit: 440300901dfe9275fd84e0b7763af1f8443ae62e
Only 24 instrument-note recordings were used. See sample-provenance.json in
art_source/audio for exact URLs and hashes; LICENSE-VSCO2CE-CC0.txt included.

Buzzes, cloth, brushes, impacts, footfalls and domestic ambience are modeled
synthetically. They are not recordings of insects, people or household rooms.
Music/recovery/task/UI mallets combine the original score with CC0 notes.
The 16 tool equip/hit/throw/landing variations are edited and layered from
the original project Foley masters by tools07.py; no new outside samples.

Tools: Python 3.14 + NumPy; FFmpeg/Vorbis encoding; Godot 4.5.2 playback.
FFmpeg and Python are production tools, not runtime game dependencies.
'''
(ASSETS/'CREDITS.txt').write_text(credits,encoding='utf-8')
manifest={'version':'0.7 audio production','sample_library':{'name':sample_data['library'],'license':'CC0-1.0','source':sample_data['source'],'commit':sample_data['repository_commit']},'asset_count':len(items),'asset_bytes':sum(e['bytes'] for e in items),'items':items}
(ASSETS/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(ROOT/'asset-validation.json').write_text(json.dumps({'checks':'67 assets decoded; sample/tool provenance hashes; mono effects/stereo music; finite nonzero PCM; decoded peak<0.98; stems exact equal duration/frame count; WAV loop seams <=1 PCM unit','pass':True,'assets':len(items),'bytes':manifest['asset_bytes'],'max_decoded_peak':max(e['decoded_peak'] for e in items)},indent=2),encoding='utf-8')
print(json.dumps({'pass':True,'assets':len(items),'bytes':manifest['asset_bytes'],'max_peak':max(e['decoded_peak'] for e in items)}))
