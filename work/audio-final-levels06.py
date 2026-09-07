from pathlib import Path
import wave, json, math, sys
import numpy as np
root=Path(__file__).resolve().parent
suffix='-final' if '--final' in sys.argv else ''
with wave.open(str(root/('release06-gameplay-audiofinal.wav' if suffix else 'release06-gameplay-audio.wav')),'rb') as f:
    rate=f.getframerate()
    x=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(float)/32768
    x=x.reshape(-1,f.getnchannels())
def db(v): return round(20*math.log10(max(1e-12,float(v))),2)
windows={
    'menu_with_fade':(.2,1.7),
    'flight_background':(2.1,3.3),
    'concentration':(3.4,4.4),
    'bite_extract':(5.4,6.5),
    'pre_hit_background':(7.7,8.55),
    'palm_hit_and_stun':(8.65,9.2),
    'help_middle':(13,18),
    'recovery':(19.2,19.75),
}
report={'untouched_movie_audio':True,'saved_master':.59,'fresh_master':.6,'fresh_master_difference_db':db(.6/.59),'sample_rate':rate,'windows':{}}
for label,(start,end) in windows.items():
    y=x[int(start*rate):int(end*rate)]
    report['windows'][label]={'seconds':[start,end],'peak_dbfs':db(abs(y).max()),'rms_dbfs':db(np.sqrt(np.mean(y*y)))}
report['palm_peak_over_prior_background_db']=round(report['windows']['palm_hit_and_stun']['peak_dbfs']-report['windows']['pre_hit_background']['peak_dbfs'],2)
report['recovery_peak_over_help_middle_db']=round(report['windows']['recovery']['peak_dbfs']-report['windows']['help_middle']['peak_dbfs'],2)
report['scope']='Temporal mixed-audio windows, not isolated buses or a subjective speaker/headphone audibility certification; no normalization or preference changes.'
(root/('release06-audio-levels'+suffix+'.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
