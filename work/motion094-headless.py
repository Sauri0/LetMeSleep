"""Run owned CPU-only tests without project autoloads, imports or a renderer."""
from pathlib import Path
import argparse
import json
import re
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('test', choices=['bounds', 'house', 'practice'])
args = parser.parse_args()
repo = Path(__file__).resolve().parent.parent
game = repo / 'game'
engine = repo.parent / 'dejame-dormir/work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe'
test = f'tests/motion094_{args.test}_test.gd'
dependencies = set()

with tempfile.TemporaryDirectory(prefix='lms-motion094-') as temporary:
    isolated = Path(temporary)
    (isolated / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Motion094 CPU checks"\n', encoding='utf-8')

    def copy_dependency(relative):
        if relative in dependencies:
            return
        dependencies.add(relative)
        source = game / relative
        text = source.read_text(encoding='utf-8')
        target = isolated / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding='utf-8')
        for child in re.findall(r'(?:preload|load)\("res://([^"\n]+\.gd)"\)', text):
            copy_dependency(child)

    copy_dependency(test)
    run = subprocess.run([str(engine), '--headless', '--audio-driver', 'Dummy', '--path', str(isolated), '--script', 'res://' + test], capture_output=True, text=True, timeout=55, creationflags=subprocess.CREATE_NO_WINDOW)
    prefix = repo / 'work' / f'motion094-{args.test}'
    prefix.with_suffix('.stdout.log').write_text(run.stdout, encoding='utf-8')
    prefix.with_suffix('.stderr.log').write_text(run.stderr, encoding='utf-8')
    result = {'test':test,'exit_code':run.returncode,'stderr_bytes':len(run.stderr.encode()),'headless':True,'autoloads':False,'results':[line for line in run.stdout.splitlines() if line.startswith('MOTION094_')],'dependencies':sorted(dependencies)}
    prefix.with_suffix('.run.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(run.stdout, end='')
    print(run.stderr, end='')
    print(json.dumps(result))
    raise SystemExit(run.returncode or (1 if run.stderr else 0))
