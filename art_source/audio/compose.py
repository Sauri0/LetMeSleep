"""Let me sleep: original event-based score, CC0 acoustic sampler and Foley models.

Python + numpy + FFmpeg only. Reproducible seeds; no remote inference or MIDI
compositions from third parties. Source samples and their licenses are recorded
in sample-provenance.json. --sketches produces the listening checkpoint first.
"""
import argparse, functools, hashlib, json, math, pathlib, shutil, struct, subprocess, wave
import numpy as np

ROOT = pathlib.Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
SR = 44100
RNG = np.random.default_rng(6060719)
NOTE = {'C':0,'C#':1,'D':2,'D#':3,'E':4,'F':5,'F#':6,'G':7,'G#':8,'A':9,'A#':10,'B':11}
PROVENANCE = json.loads((ROOT/'sample-provenance.json').read_text(encoding='utf-8'))
BANK = {}
for entry in PROVENANCE['samples']:
    label = entry['note_label']
    # VSCO labels middle C as C3: its C4 sample measures about 523 Hz.
    entry['midi_root'] = (int(label[-1])+2)*12+NOTE[label[:-1]]
    BANK.setdefault(entry['instrument'], []).append(entry)

def write_wav(path, pcm):
    path = pathlib.Path(path); path.parent.mkdir(parents=True,exist_ok=True)
    stereo = pcm.ndim == 2
    with wave.open(str(path),'wb') as out:
        out.setparams((2 if stereo else 1,2,SR,0,'NONE','not compressed'))
        out.writeframes(np.round(np.clip(pcm,-.999,.999)*32767).astype('<i2').tobytes())

def encode_ogg(wav, ogg, loop=False):
    args = ['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(wav),'-c:a','libvorbis','-q:a','6']
    if loop: args += ['-metadata','LOOPSTART=0','-metadata',f'LOOPEND={wave.open(str(wav)).getnframes()}']
    subprocess.run(args+[str(ogg)],check=True)

@functools.lru_cache(maxsize=64)
def read_sample(file):
    raw = subprocess.check_output(['ffmpeg','-v','error','-i',str(ROOT/file),'-f','f32le','-ac','1','-ar',str(SR),'-'])
    x = np.frombuffer(raw,dtype='<f4').copy()
    peak = float(np.max(np.abs(x)))
    active = np.flatnonzero(np.abs(x) > peak*.025)
    if len(active): x = x[max(0,active[0]-80):]
    return x / max(peak,.02)

def acoustic(inst, midi, dur, velocity, variation=0):
    notes = BANK[inst]
    nearest = min(abs(midi-e['midi_root']) for e in notes)
    candidates = [e for e in notes if abs(midi-e['midi_root'])==nearest]
    entry = candidates[variation % len(candidates)]
    source = read_sample(entry['file'])
    ratio = 2**((midi-entry['midi_root'])/12)
    tail = .18 if inst=='clarinet' else .42
    count = min(int((dur+tail)*SR),int(len(source)/ratio)-1)
    x = np.interp(np.arange(count)*ratio,np.arange(len(source)),source).astype(np.float32)
    t = np.arange(count)/SR
    if inst=='clarinet':
        env = np.minimum(t/.018,1)*np.minimum(np.maximum(dur+.15-t,0)/.15,1)
    else:
        env = np.minimum(t/.003,1)*np.exp(-np.maximum(t-dur,0)*9)
    env *= np.minimum((count/SR-t)/.015,1)
    level = {'bass':.24,'pizz':.16,'clarinet':.18,'marimba':.17,'glock':.065}[inst]
    return x*env*level*velocity

def filtered_noise(seconds, color='warm', seed=0):
    gen = np.random.default_rng(60000+seed)
    x = gen.standard_normal(round(seconds*SR)).astype(np.float32)
    n = {'warm':35,'air':4,'cloth':12,'dark':90}.get(color,12)
    low = np.convolve(x,np.ones(n,dtype=np.float32)/n,mode='same')
    return low if color!='air' else x-low

def foley(kind, duration=None, seed=0):
    seconds = duration or {'brush':.22,'kick':.20,'rim':.12,'buzz':4,'clap':.28,'swish':.29,'impact':.32,'help':2,'recover':.70}.get(kind,.35)
    t = np.arange(round(seconds*SR))/SR
    if kind=='buzz':
        phase = 2*np.pi*(222*t + .40*np.sin(2*np.pi*6*t)+.14*np.sin(2*np.pi*11*t))
        x = sum(a*np.sin(k*phase+.13*k) for k,a in [(1,.18),(2,.10),(3,.05),(5,.022),(7,.008)])
        x *= .64+.09*np.sin(2*np.pi*13*t)+.045*np.sin(2*np.pi*17*t)
        x += filtered_noise(seconds,'air',seed)*.011
    elif kind=='brush':
        x = filtered_noise(seconds,'cloth',seed)*np.sin(np.pi*np.minimum(t/seconds,1))**1.3*.19
    elif kind=='kick':
        x = np.sin(2*np.pi*(62*t+1.2*(1-np.exp(-t*35))))*np.exp(-t*26)*.12
        x += filtered_noise(seconds,'dark',seed)*np.exp(-t*60)*.06
    elif kind=='rim':
        x = (np.sin(2*np.pi*920*t)*.035+np.sin(2*np.pi*1643*t)*.013)*np.exp(-t*95)
        x += filtered_noise(seconds,'air',seed)*np.exp(-t*150)*.028
    elif kind=='clap':
        noise=filtered_noise(seconds,'cloth',seed)
        burst=sum(np.exp(-np.maximum(t-offset,0)*rate)*(t>=offset) for offset,rate in [(0,95),(.008,130),(.018,65)])
        x = noise*burst*.5 + np.sin(2*np.pi*(180*t-.8*(1-np.exp(-t*35))))*np.exp(-t*32)*.11
    elif kind=='swish':
        x=filtered_noise(seconds,'air',seed)*np.sin(np.pi*t/seconds)**2*.095
    elif kind=='impact':
        x = filtered_noise(seconds,'warm',seed)*np.exp(-t*30)*.35+np.sin(2*np.pi*(98*t+1.8*(1-np.exp(-t*18))))*np.exp(-t*23)*.13
    elif kind=='help':
        x=filtered_noise(seconds,'cloth',seed)*(.025+.018*np.sin(2*np.pi*4*t))
        x += np.sin(2*np.pi*(328*t+.4*np.sin(2*np.pi*3*t)))*.019*(.5+.5*np.sin(2*np.pi*4*t))
    else:
        x = sum(np.sin(2*np.pi*f*t)*np.exp(-t*d)*a for f,d,a in [(588,7,.12),(880,10,.06),(1176,13,.02)])
    fade = min(.012,seconds/10)
    x *= np.minimum(t/fade,1)*np.minimum((seconds-t)/fade,1)
    return x.astype(np.float32)

def pan(x, position):
    angle=(position+1)*np.pi/4
    return np.column_stack((x*np.cos(angle),x*np.sin(angle))).astype(np.float32)

def room(x, wet=.10):
    out=x.copy()
    for delay,gain,swap in [(.021,.60,True),(.038,.48,False),(.067,.37,True),(.113,.28,False),(.179,.20,True),(.281,.12,False),(.419,.075,True)]:
        n=round(delay*SR)
        if len(x)>n: out[n:]+=x[:-n,::-1] * (wet*gain) if swap else x[:-n]*(wet*gain)
    return out

def place(track, x, second, position=0, loop=False):
    stereo=pan(x,position) if x.ndim==1 else x
    start=round(second*SR)
    if start<0: stereo=stereo[-start:]; start=0
    if start>=len(track): return
    count=min(len(stereo),len(track)-start)
    track[start:start+count]+=stereo[:count]
    if loop and count<len(stereo): track[:len(stereo)-count]+=stereo[count:]

# Each chord keeps extensions clear without dense block voicings.
HARMONY = [
    (38,[53,57,59,64],'Dm6/9'), (38,[53,57,59,64],'Dm6/9'),
    (43,[53,57,58,62],'Gm9'), (45,[55,58,61,64],'A7b9'),
    (38,[53,57,60,64],'Dm9'), (41,[57,60,64,67],'Fmaj9'),
    (43,[53,57,58,62],'Gm9'), (45,[55,58,61,64],'A7b9'),
]
# Original eight-bar call-and-response, beat positions expressed before swing.
MELODY = [
    [(0.5,69,.35),(1,74,.65),(2,77,.35),(2.5,76,.3),(3,74,.75)],
    [(0,71,.5),(1,69,.4),(2,65,.7),(3.5,69,.25)],
    [(0,70,.4),(1,74,.7),(2.5,77,.35),(3,76,.55)],
    [(0,73,.6),(1.5,70,.25),(2,69,1.15)],
    [(.5,69,.3),(1,74,.4),(1.5,77,.3),(2,81,.6),(3,79,.55)],
    [(0,77,.6),(1,76,.35),(2,72,.5),(3,69,.6)],
    [(.5,70,.4),(1,74,.4),(2,77,.4),(2.5,76,.25),(3,74,.5)],
    [(0,73,.5),(1,70,.3),(2,69,.5),(3,73,.45)],
]

def swing(beat, amount=.13):
    return beat+(amount if abs(beat%1-.5)<.02 else 0)

def score(kind, bars):
    tempo=104 if kind=='A' else 80
    events=[]
    gen=np.random.default_rng(60600+(0 if kind=='A' else 1))
    def note(inst, beat, midi, length, velocity, layer, p=0):
        events.append(dict(instrument=inst,beat=max(0,swing(beat)+float(gen.normal(0,.008))),midi=midi,duration=length,velocity=velocity*float(gen.uniform(.91,1.06)),layer=layer,pan=p))
    for bar in range(bars):
        phrase=(bar//8)%4; root,chord,label=HARMONY[bar%8]
        if kind=='B': root+=3; chord=[x+3 for x in chord]
        # Bass stays grounded; passing notes lead naturally into the next harmony.
        bass_pattern=[(0,root,.72),(1.5,root+7,.38),(2,root+12,.5),(3.5,root+11,.25)] if kind=='A' else [(0,root,.95),(2,root+7,.8),(3.5,root+12,.25)]
        for beat,pitch,duration in bass_pattern: note('bass',bar*4+beat,pitch,duration,.76 if beat==0 else .60,'base',-.08)
        for j,beat in enumerate([.5,2.5] if phrase!=2 else [.5,1.5,3]):
            for k,pitch in enumerate(chord[:3]): note('pizz',bar*4+beat+k*.018,pitch,.28,.44 if kind=='A' else .31,'base',-.28+k*.10)
        if kind=='A':
            for beat in [0,1,2,3]:
                events.append(dict(instrument='brush',beat=bar*4+beat+.01,midi=38,duration=.25,velocity=.53 if beat%2 else .38,layer='rhythm',pan=.27))
            for beat in [0,2]: events.append(dict(instrument='kick',beat=bar*4+beat,midi=36,duration=.2,velocity=.68,layer='rhythm',pan=0))
            for beat in [1,3]: events.append(dict(instrument='rim',beat=bar*4+beat,midi=37,duration=.12,velocity=.6,layer='rhythm',pan=.25))
        else:
            for beat in [1,3]: events.append(dict(instrument='brush',beat=bar*4+beat,midi=38,duration=.32,velocity=.45,layer='rhythm',pan=.2))
        for j,(beat,pitch,length) in enumerate(MELODY[bar%8]):
            if kind=='B': pitch+=3
            if phrase==1 and j==0: beat+=.5
            inst='marimba' if kind=='A' and bar%4<2 else 'clarinet'
            if phrase==2: inst='pizz'; pitch-=12
            note(inst,bar*4+beat,pitch,length,.72 if kind=='A' else .62,'melody',.17 if inst=='clarinet' else -.17)
        if bar%4==3:
            for beat,pitch in [(3.0,81),(3.5,77)]: note('glock',bar*4+beat,pitch+(3 if kind=='B' else 0),.3,.35,'melody',.45)
    return {'title':'Pasos de puntillas' if kind=='A' else 'La casa bosteza','tempo':tempo,'meter':'4/4','bars':bars,'composer':'Original composition created for Let me sleep','harmony':[x[2] for x in HARMONY], 'events':events}

def vlq(value):
    output=[value&127]; value >>=7
    while value: output.insert(0,(value&127)|128); value >>=7
    return bytes(output)

def midi_file(data,path):
    ppq=480; tempo=round(60_000_000/data['tempo'])
    tracks=[]
    instruments={'bass':(0,32),'pizz':(1,45),'clarinet':(2,71),'marimba':(3,12),'glock':(4,9),'brush':(9,0),'kick':(9,0),'rim':(9,0)}
    for inst,(channel,program) in instruments.items():
        commands=[(0,bytes([0xC0+channel,program]))]
        if inst=='bass': commands.append((0,b'\xff\x51\x03'+tempo.to_bytes(3,'big')))
        for e in data['events']:
            if e['instrument']!=inst:continue
            t=round(e['beat']*ppq); end=t+max(1,round(e['duration']*ppq))
            commands += [(t,bytes([0x90+channel,e['midi'],min(127,max(1,round(e['velocity']*105)))])),(end,bytes([0x80+channel,e['midi'],0]))]
        commands.sort(key=lambda c:c[0]); track=b''; previous=0
        for tick,command in commands: track+=vlq(tick-previous)+command; previous=tick
        track+=b'\x00\xff\x2f\x00'; tracks.append(b'MTrk'+len(track).to_bytes(4,'big')+track)
    path.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(tracks),ppq)+b''.join(tracks))

def render(data, folder, basename, is_loop=False):
    folder.mkdir(parents=True,exist_ok=True)
    seconds=data['bars']*4*60/data['tempo']
    tracks={name:np.zeros((round(seconds*SR),2),np.float32) for name in ['base','rhythm','melody']}
    for i,e in enumerate(data['events']):
        dur=e['duration']*60/data['tempo']
        x=acoustic(e['instrument'],e['midi'],dur,e['velocity'],i) if e['instrument'] in BANK else foley(e['instrument'],dur,i)*e['velocity']
        place(tracks[e['layer']],x,e['beat']*60/data['tempo'],e['pan'],is_loop)
    for key,x in tracks.items(): tracks[key]=room(x,.12 if key!='rhythm' else .05)
    full=sum(tracks.values())
    # Fixed shared gain keeps stems additive; no independent stem normalization.
    gain=min(1.7,.62/max(.01,float(np.max(np.abs(full)))))
    for key in tracks: tracks[key]*=gain
    if is_loop:
        # A five-millisecond boundary taper removes discontinuities without
        # shortening the musical grid. All layers retain identical sample count.
        edge=round(.005*SR)
        for x in tracks.values():
            x[:edge]*=np.linspace(0,1,edge)[:,None]
            x[-edge:]*=np.linspace(1,0,edge)[:,None]
    full=sum(tracks.values())
    if not is_loop:
        fade=round(.32*SR); full[-fade:]*=np.linspace(1,0,fade)[:,None]
    wav=folder/(basename+'.wav'); write_wav(wav,full); encode_ogg(wav,folder/(basename+'.ogg'),is_loop)
    (folder/(basename+'.events.json')).write_text(json.dumps(data,indent=2),encoding='utf-8')
    midi_file(data,folder/(basename+'.mid'))
    report={'title':data['title'],'seconds':seconds,'sample_rate':SR,'peak_dbfs':20*math.log10(max(1e-9,float(np.max(np.abs(full))))),'rms_dbfs':20*math.log10(max(1e-9,float(np.sqrt(np.mean(full**2))))),'sample_count':len(full),'shared_stem_gain':gain}
    if is_loop:
        for name,x in tracks.items():
            stem=folder/(basename+'_'+name+'.wav'); write_wav(stem,x); encode_ogg(stem,stem.with_suffix('.ogg'),True)
    print(json.dumps(report))
    return report

def sketches():
    destination=PROJECT/'outputs'/'0.6-audio-bocetos'
    destination.mkdir(parents=True,exist_ok=True)
    reports=[render(score('A',12),destination,'01-pasos-de-puntillas'),render(score('B',8),destination,'02-la-casa-bosteza')]
    demo=np.zeros((22*SR,2),np.float32)
    buzz=foley('buzz',5,1); motion=np.linspace(-.85,.85,len(buzz)); envelope=np.sin(np.linspace(0,np.pi,len(buzz)))**.8
    place(demo,np.column_stack((buzz*envelope*np.sqrt((1-motion)/2),buzz*envelope*np.sqrt((1+motion)/2))),.6)
    place(demo,foley('swish',seed=2),7,-.1); place(demo,foley('clap',seed=3),7.2,-.1)
    place(demo,foley('swish',seed=4),9,.1); place(demo,foley('clap',seed=5),9.2,.1)
    place(demo,foley('impact',seed=6),11.3,0)
    for i in range(4):place(demo,foley('help',1.5,7+i),12+i*1.5,.1)
    place(demo,acoustic('marimba',74,.24,.72),18.2,-.12)
    place(demo,acoustic('marimba',77,.28,.62),18.38,.12)
    place(demo,acoustic('glock',81,.7,.40),18.63,.05)
    wav=destination/'03-zumbido-palmada-rescate.wav'; write_wav(wav,room(demo,.10)); encode_ogg(wav,wav.with_suffix('.ogg'))
    (destination/'ESCUCHA.md').write_text('# Let me sleep — bocetos de audio\n\n01: Pasos de puntillas, 104 BPM, jazz de travesura con contrabajo, pizzicatos, marimba y respuestas de clarinete.\n\n02: La casa bosteza, 80 BPM, más espacioso y somnoliento, misma familia de instrumentos.\n\n03: 0,6–5,6 s vuelo de izquierda a derecha; 7 y 9 s dos palmadas; 11,3 s caída, 12–18 s ayuda, 18,2 s recuperación.\n\nComposición original; instrumentos acústicos muestreados de VSCO 2 CE (CC0, Versilian Studios). Foley y zumbido sintetizados offline. No son grabaciones de mosquitos/personas reales. Estos archivos son bocetos para escuchar, no mezcla final ni validación de integración.\n',encoding='utf-8')
    (ROOT/'sketch-metrics.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')

def make_effect(name):
    seed=sum(ord(c)*(i+1) for i,c in enumerate(name))
    if name in ['buzz','buzz_perch','buzz_bite']:
        x=foley('buzz',4,seed)
        if name=='buzz_perch': x=filtered_noise(4,'cloth',seed)*.025
        elif name=='buzz_bite': x=x*.19+filtered_noise(4,'dark',seed)*.018
        return x,True
    if name in ['clap','swish','impact']:
        return foley(name,seed=seed),False
    durations={'bite':.23,'detach':.26,'perch':.17,'pickup':.28,'drop':.32,'stun':.68,'fall':.32,'recover':.75,'land':.3,'cloth':.26,'tool_swatter':.24,'tool_racket':.35,'tool_newspaper':.34,'tool_broom':.42,'task_start':.45,'task_done':.65,'task_fail':.40,'ui_select':.08,'ui_confirm':.24,'ui_error':.23,'step_wood':.24,'step_tile':.20,'step_cloth':.26,'focus_loop':3,'help_loop':2,'room_fan':8,'room_fridge':8,'night_air':8,'clock':4}
    seconds=durations[name]; t=np.arange(round(seconds*SR))/SR
    noise=filtered_noise(seconds,'cloth',seed)
    x=np.zeros_like(t)
    looping=name.endswith('_loop') or name in ['room_fan','room_fridge','night_air','clock']
    if name.startswith('step_') or name in ['land','drop','fall']:
        freq={'step_wood':155,'step_tile':220,'step_cloth':95,'land':80,'drop':180,'fall':120}[name]
        x=noise*np.exp(-t*32)*(.18 if name!='step_cloth' else .07)
        x+=np.sin(2*np.pi*freq*t)*np.exp(-t*48)*.09
        if name=='step_tile': x+=np.sin(2*np.pi*1273*t)*np.exp(-t*85)*.018
        if name=='step_wood': x+=np.sin(2*np.pi*413*t)*np.exp(-t*38)*.021
    elif name.startswith('tool_'):
        x=filtered_noise(seconds,'air',seed)*np.sin(np.pi*t/seconds)**1.7*.11
        if name=='tool_newspaper': x+=noise*(np.exp(-abs(t-.08)*85)+np.exp(-abs(t-.19)*60))*.14
        elif name=='tool_racket': x+=np.sin(2*np.pi*580*t)*np.exp(-np.maximum(t-.11,0)*45)*(t>.11)*.04
        elif name=='tool_broom': x+=filtered_noise(seconds,'warm',seed+2)*np.sin(np.pi*t/seconds)*.09
    elif name in ['cloth','perch','pickup','bite','detach']:
        x=noise*np.sin(np.pi*t/seconds)**1.1*np.exp(-t*7)*.18
        f={'cloth':90,'perch':470,'pickup':280,'bite':630,'detach':360}[name]
        x+=np.sin(2*np.pi*(f*t-.8*(1-np.exp(-t*15))))*np.exp(-t*25)*(.015 if name=='cloth' else .055)
        if name=='pickup': x+=np.sin(2*np.pi*1510*t)*np.exp(-t*45)*.018
    elif name=='stun':
        # A short flexible-wing/body resonance, not a siren or 35-second alarm.
        x=noise*np.exp(-t*28)*.18
        x+=sum(np.sin(2*np.pi*(f*t+d*(1-np.exp(-t*11))))*np.exp(-t*r)*a for f,d,r,a in [(136,3,11,.11),(371,1.5,17,.028),(589,.3,24,.016)])
    elif name=='help_loop':
        x=foley('help',seconds,seed)*.7
    elif name=='focus_loop':
        x=noise*(.011+.008*np.sin(2*np.pi*4*t))
        x+=np.sin(2*np.pi*(294*t+.21*np.sin(2*np.pi*2*t)))*.011
    elif name in ['room_fan','room_fridge','night_air']:
        x=filtered_noise(seconds,'dark',seed)*(.22 if name=='night_air' else .13)
        x*=.78+.12*np.sin(2*np.pi*.5*t)+.07*np.sin(2*np.pi*.125*t)
        if name=='room_fan': x+=np.sin(2*np.pi*74*t)*.023*(.8+.2*np.sin(2*np.pi*6*t))
        if name=='room_fridge': x+=np.sin(2*np.pi*100*t)*.009+np.sin(2*np.pi*151*t)*.004
        if name=='night_air':
            chirp=np.sin(2*np.pi*2860*t)*np.maximum(0,np.sin(2*np.pi*1.5*t))**12
            x+=chirp*.008*(.6+.4*np.sin(2*np.pi*.25*t))
    elif name=='clock':
        for beat in range(4):
            local=t-beat; gate=local>=0
            x+=(np.sin(2*np.pi*(940 if beat%2 else 740)*local)*.032+noise*.1)*np.exp(-np.maximum(local,0)*120)*gate
    elif name.startswith('ui_'):
        if name=='ui_select': x=noise*np.exp(-t*70)*.11+np.sin(2*np.pi*690*t)*np.exp(-t*85)*.027
        else:
            pitches=[74,77] if name=='ui_confirm' else [65,61]
            for i,pitch in enumerate(pitches):
                sound=acoustic('marimba',pitch,.09,.45)
                start=round(i*.08*SR); count=min(len(sound),len(x)-start); x[start:start+count]+=sound[:count]
    else:
        pitches={'task_start':[62,69],'task_done':[74,77,81],'task_fail':[70,69,61],'recover':[74,77,81]}[name]
        for i,pitch in enumerate(pitches):
            sound=acoustic('marimba',pitch,.14,.60)
            start=round(i*.13*SR); count=min(len(sound),len(x)-start); x[start:start+count]+=sound[:count]
    if looping:
        # Periodic crossfade preserves exact sample length for each independent loop.
        n=round(.12*SR); first=x[:n].copy(); last=x[-n:].copy(); ramp=np.linspace(0,1,n)
        seam=last*(1-ramp)+first*ramp
        x=np.concatenate([x[n:-n],seam])
        x[0]=x[-1]=(x[0]+x[-1])*.5
    else:
        x*=np.minimum(t/.003,1)*np.minimum((seconds-t)/.016,1)
    return x.astype(np.float32),looping

def sfx_library():
    names=['buzz','buzz_perch','buzz_bite','clap','swish','impact','bite','detach','perch','pickup','drop','stun','fall','help_loop','recover','focus_loop','task_start','task_done','task_fail','step_wood','step_tile','step_cloth','land','cloth','tool_swatter','tool_racket','tool_newspaper','tool_broom','ui_select','ui_confirm','ui_error','room_fan','room_fridge','night_air','clock']
    master=ROOT/'masters'/'sfx'; assets=PROJECT/'game'/'assets'/'audio'/'sfx'
    master.mkdir(parents=True,exist_ok=True); assets.mkdir(parents=True,exist_ok=True)
    metrics=[]
    for name in names:
        pcm,looping=make_effect(name)
        wav=master/(name+'.wav'); write_wav(wav,pcm); ogg=assets/(name+'.ogg'); encode_ogg(wav,ogg,looping)
        metrics.append({'name':name,'seconds':len(pcm)/SR,'channels':1,'loop':looping,'peak':float(abs(pcm).max()),'rms':float(np.sqrt(np.mean(pcm**2))),'sha256':hashlib.sha256(ogg.read_bytes()).hexdigest()})
    (ROOT/'sfx-metrics.json').write_text(json.dumps(metrics,indent=2),encoding='utf-8')
    print(f'Rendered {len(metrics)} original SFX, mono 44100 Hz; acoustic recovery/UI notes use CC0 samples')

def music_library():
    master=ROOT/'masters'/'music'; assets=PROJECT/'game'/'assets'/'audio'/'music'
    previews=PROJECT/'outputs'/'0.6-audio-final'
    for folder in [master,assets,previews]: folder.mkdir(parents=True,exist_ok=True)
    menu=score('A',32)
    menu['title']='Pasos de puntillas — tema principal'
    game=json.loads(json.dumps(menu)); game['title']='Pasos de puntillas — tres capas de partida'
    for e in game['events']:
        if e['layer']=='melody': e['velocity']*=.76
        if e['layer']=='base': e['velocity']*=.88
    # Restrained repeated-note motion belongs to the optional rhythm layer.
    for bar in range(32):
        if bar%8>=4:
            for beat in [.5,1.5,2.5,3.5]:
                game['events'].append(dict(instrument='brush',beat=bar*4+swing(beat),midi=38,duration=.13,velocity=.23,layer='rhythm',pan=-.27))
    quiet=score('B',24); quiet['title']='La casa bosteza — variante tranquila'
    metrics=[]
    for data,name in [(menu,'menu'),(game,'gameplay'),(quiet,'quiet')]:
        metrics.append(render(data,master,name,True))
        shutil.copy2(master/(name+'.ogg'),previews/(name+'.ogg'))
        if name in ['menu','gameplay']:
            for layer in ['base','rhythm','melody']:
                shutil.copy2(master/(name+'_'+layer+'.ogg'),assets/(name+'_'+layer+'.ogg'))
        else: shutil.copy2(master/'quiet.ogg',assets/'quiet.ogg')
    accents={
        'start':[(0,69,.14),(.16,74,.15),(.32,77,.19),(.55,81,.36)],
        'stun':[(0,74,.09),(.11,71,.10),(.23,70,.09),(.38,69,.14)],
        'recover':[(0,74,.13),(.16,77,.16),(.35,81,.19),(.59,86,.3)],
        'task':[(0,77,.13),(.14,81,.16),(.35,86,.30)],
        'win':[(0,69,.2),(.24,74,.2),(.47,77,.2),(.71,81,.2),(1.0,83,.3),(1.38,86,.6)],
        'lose':[(0,69,.23),(.31,67,.21),(.62,64,.28),(1.0,62,.55)],
    }
    accent_events={}
    for name,notes in accents.items():
        seconds=2.7 if name in ['win','lose'] else 1.6
        pcm=np.zeros((round(seconds*SR),2),np.float32)
        for index,(start,pitch,dur) in enumerate(notes):
            inst='pizz' if name=='stun' else 'marimba'
            place(pcm,acoustic(inst,pitch,dur,.72,index),start,-.18+index*.06)
        place(pcm,acoustic('bass',38,.65,.65),0,-.05)
        if name in ['win','recover','task']:
            place(pcm,acoustic('glock',notes[-1][1],.5,.38),notes[-1][0],.28)
        pcm=room(pcm,.14)*1.6
        wav=master/('accent_'+name+'.wav'); write_wav(wav,pcm); encode_ogg(wav,assets/('accent_'+name+'.ogg'))
        accent_events[name]=notes
    (ROOT/'accent-events.json').write_text(json.dumps(accents,indent=2),encoding='utf-8')
    (ROOT/'music-metrics.json').write_text(json.dumps(metrics,indent=2),encoding='utf-8')
    print('Rendered primary/menu, gameplay 3 stems, calm variation and 6 original accents')

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--sketches',action='store_true'); parser.add_argument('--sfx',action='store_true'); parser.add_argument('--music',action='store_true'); args=parser.parse_args()
    if args.sfx: sfx_library()
    elif args.music: music_library()
    else: sketches()
