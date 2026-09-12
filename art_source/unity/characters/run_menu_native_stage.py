"""Run one explicitly assigned Blender stage; no automatic retries or promotion."""
import argparse,datetime,json,subprocess,time
from pathlib import Path

ROOT=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--run-root',type=Path,required=True)
parser.add_argument('--stage',required=True)
parser.add_argument('--script',required=True)
parser.add_argument('--slot-note',required=True)
parser.add_argument('arguments',nargs=argparse.REMAINDER)
args=parser.parse_args();out=args.run_root.resolve()
assert out.is_relative_to((ROOT/'.validation').resolve()) and out.is_dir()
assert args.stage.replace('-','').isalnum()
script=(ROOT/args.script).resolve();assert script.parent==ROOT and script.suffix=='.py'
record_path=out/(args.stage+'-process.json');assert not record_path.exists()
tail=args.arguments[1:] if args.arguments[:1]==['--'] else args.arguments
argv=['N:/Blender/blender.exe','-b','-t','2','--python-exit-code','1','--python',str(script),'--',*tail]
record={'stage':args.stage,'argv':argv,'slot_note':args.slot_note,'started_utc':datetime.datetime.now(datetime.UTC).isoformat(),'threads':2,'priority':'BelowNormal'}
start=time.perf_counter()
with (out/(args.stage+'.log')).open('w',encoding='utf8') as stdout,(out/(args.stage+'.err.log')).open('w',encoding='utf8') as stderr:
    process=subprocess.Popen(argv,cwd=ROOT,stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
    record['pid']=process.pid
    record_path.write_text(json.dumps(record,indent=2)+'\n',encoding='utf8',newline='\n')
    print(json.dumps({'stage':args.stage,'pid':process.pid,'state':'started'}),flush=True)
    try:code=process.wait()
    except BaseException:
        process.terminate()
        try:process.wait(timeout=15)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=15)
        raise
record.update(exit_code=code,seconds=time.perf_counter()-start,finished_utc=datetime.datetime.now(datetime.UTC).isoformat())
record_path.write_text(json.dumps(record,indent=2)+'\n',encoding='utf8',newline='\n')
print(json.dumps(record),flush=True)
raise SystemExit(code)
