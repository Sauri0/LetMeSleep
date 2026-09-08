"""Run a bounded native continuous witness, then encode exactly its saved frames."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

ROOT=Path(__file__).resolve().parents[1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',required=True)
parser.add_argument('--smoke',action='store_true')
parser.add_argument('--no-video',action='store_true')
parser.add_argument('--native-timeout',type=float,default=55,help='bounded native child guard; parent-approved full recording may use 180 seconds')
args=parser.parse_args()
output=Path(args.output).resolve()
if output.exists() and any(output.iterdir()):parser.error('preserve previous evidence; output must be empty')
output.mkdir(parents=True,exist_ok=True)
godot=ROOT/'work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
fixture=ROOT/'game/tests/garment09_continuous_views.gd'
flags=getattr(subprocess,'CREATE_NO_WINDOW',0)
manifest={'fixture':str(fixture),'fixture_sha256':sha(fixture),'smoke':args.smoke,'processes':[],'complete':False}
def save():
    (output/'runner.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
def run(command,label,timeout=55):
    row={'label':label,'command':list(map(str,command)),'exit':None}
    manifest['processes'].append(row);save()
    start=time.monotonic()
    stdout=output/(label+'.stdout.log');stderr=output/(label+'.stderr.log')
    with stdout.open('w',encoding='utf8') as a,stderr.open('w',encoding='utf8') as b:
        process=subprocess.run(command,stdout=a,stderr=b,creationflags=flags,timeout=timeout,cwd=ROOT)
    row.update(exit=process.returncode,seconds=time.monotonic()-start,stdout=str(stdout),stderr=str(stderr))
    save()
    if process.returncode:raise RuntimeError(label+' failed; inspect logs')
    print('GARMENT09_PROCESS '+label+' seconds='+str(round(row['seconds'],2)),flush=True)
base=[str(godot),'--path',str(ROOT/'game'),'--audio-driver','Dummy']
try:
    run(base+['--headless','--script','res://tests/garment09_continuous_views.gd','--check-only'],'parse')
    run(base+['--rendering-method','gl_compatibility','--script','res://tests/garment09_continuous_views.gd','--',
              '--output='+str(output),'--limit='+('60' if args.smoke else '630')],'native',args.native_timeout)
    report=json.loads((output/'report.json').read_text())
    expected=60 if args.smoke else 630
    assert report['frame_count']==expected and not report['failures']
    assert report['sequence_complete']==(not args.smoke)
    assert (output/'native.stderr.log').stat().st_size==0
    frames=sorted((output/'frames').glob('*.png'))
    assert len(frames)==expected and [p.stem for p in frames]==[f'{n:04d}' for n in range(expected)]
    manifest.update(frame_count=expected,recording_fps=30,report_sha256=sha(output/'report.json'),
                    first_frame_sha256=sha(frames[0]),last_frame_sha256=sha(frames[-1]))
    if not args.no_video:
        ffmpeg=shutil.which('ffmpeg');ffprobe=shutil.which('ffprobe')
        assert ffmpeg and ffprobe,'FFmpeg and ffprobe are required for the video'
        movie=output/('continuous-smoke.mp4' if args.smoke else 'human-fit4-continuous.mp4')
        run([ffmpeg,'-hide_banner','-loglevel','error','-n','-framerate','30','-start_number','0',
             '-i',str(output/'frames/%04d.png'),'-frames:v',str(expected),'-an','-c:v','libx264',
             '-preset','fast','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(movie)],'encode')
        metadata=json.loads(subprocess.check_output([ffprobe,'-v','error','-show_streams','-show_format','-of','json',str(movie)],creationflags=flags,timeout=10))
        video=[s for s in metadata['streams'] if s['codec_type']=='video']
        assert len(video)==1 and int(video[0]['nb_frames'])==expected
        assert not any(s['codec_type']=='audio' for s in metadata['streams'])
        assert abs(float(metadata['format']['duration'])-expected/30)<.04
        manifest.update(video=str(movie),video_sha256=sha(movie),video_metadata=metadata)
    manifest['complete']=True;save()
    print('GARMENT09_RECORDING frames='+str(expected)+' seconds='+str(expected/30)+' smoke='+str(args.smoke))
except BaseException as error:
    manifest['error']=str(error);save()
    raise
