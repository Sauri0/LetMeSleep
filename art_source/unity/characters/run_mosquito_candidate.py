"""Bounded CPU2 candidate batch; defaults to a native-free plan.

Only use --execute after Director explicitly grants a slot. Generation and all
outputs stay in a fresh work/mosquito-candidate/<run> snapshot, never canonical
assets. Stops at first error/timeout; promotion is a separate reviewed action.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parent
WORKTREE = ROOT.parents[2]
FILES = ('build_characters.py', 'author_motion.py', 'author_mosquito_geometry.py',
         'author_mosquito_motion.py', 'author_mosquito_face.py', 'build_mosquito_candidate.py',
         'audit_mosquito_candidate.py', 'audit_surface_support.py', 'render_mosquito_witness.py')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def now():
    return datetime.now(timezone.utc).isoformat()


def steps():
    result = [
        ('generate', 'build_mosquito_candidate.py', [], 90),
        ('audit', 'audit_mosquito_candidate.py', [], 90),
        ('support', 'audit_surface_support.py', [], 60),
    ]
    stills = (
        ('open', ['front', 'three_quarter', 'face_front', 'face_profile'], []),
        ('half', ['face_front'], ['--blink-left', '.5', '--blink-right', '.5']),
        ('closed', ['face_front', 'face_profile'], ['--blink-left', '1', '--blink-right', '1']),
        ('gaze_left', ['face_front'], ['--gaze-yaw', '12', '--gaze-pitch', '10']),
        ('gaze_right', ['face_front'], ['--gaze-yaw', '-12', '--gaze-pitch', '-10']),
        ('wink_left', ['face_front'], ['--blink-left', '1']),
        ('wink_right', ['face_front'], ['--blink-right', '1']),
    )
    for label, views, args in stills:
        result.append((label, 'render_mosquito_witness.py',
            ['--output-name', 'mosquito-r4-' + label, '--samples', '8', '--resolution', '640', '--views'] + views + args, 90))
    for clip in ('Fly', 'Hover'):
        result.append((clip.lower(), 'render_mosquito_witness.py',
            ['--output-name', 'mosquito-r4-' + clip.lower(), '--clip', clip,
             '--samples', '4', '--resolution', '384', '--views', 'three_quarter',
             '--sequence', '--loop-count', '2', '--playback', '1'], 180))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--run-name', default='r4-cpu2-01')
    parser.add_argument('--slot-note', default='')
    parser.add_argument('--blender', type=Path, default=Path('N:/Blender/blender.exe'))
    parser.add_argument('--total-timeout-seconds', type=int, default=720)
    args = parser.parse_args()
    if not re.fullmatch(r'r4-[a-z0-9-]{1,48}', args.run_name):
        raise ValueError('Use a unique r4- run name containing lowercase letters, numbers and hyphens')
    if not 60 <= args.total_timeout_seconds <= 720:
        raise ValueError('Total batch time must stay between 60 and 720 seconds')
    batch = (WORKTREE / 'work/mosquito-candidate' / args.run_name).resolve()
    if not batch.is_relative_to((WORKTREE / 'work/mosquito-candidate').resolve()):
        raise ValueError('Output escaped assigned work directory')
    plan = {'mode': 'execute' if args.execute else 'plan_only', 'output': str(batch),
            'native_processes_started': False, 'threads': 2, 'parallel_processes': 1, 'priority': 'BelowNormal',
            'total_timeout_seconds': args.total_timeout_seconds, 'canonical_writes': False,
            'expected_stills': 11, 'expected_sequence_frames': 48,
            'steps': [{'name': n, 'script': s, 'arguments': a, 'timeout_seconds': t} for n, s, a, t in steps()]}
    print(json.dumps(plan, indent=2), flush=True)
    if not args.execute:
        return
    if not args.slot_note.strip():
        raise ValueError('--slot-note must identify the explicit Director concession; this flag does not obtain permission')
    if not args.blender.is_file():
        raise FileNotFoundError(args.blender)
    batch.mkdir(parents=True, exist_ok=False)
    snapshot = batch / 'art_source/unity/characters'
    snapshot.mkdir(parents=True)
    for name in FILES:
        shutil.copy2(ROOT / name, snapshot / name)
    # Protect existing species, shared manifest and source exports by comparison.
    protected = [p for directory in ('mosquito', 'human', 'flyswatter')
                 for p in (ROOT / directory).glob('*') if p.suffix.lower() in ('.blend', '.fbx', '.json')]
    protected += [p for p in (ROOT / 'manifest.json', ROOT / 'surface_support_audit.json') if p.exists()]
    baseline = {str(p): sha(p) for p in protected}
    receipt = {**plan, 'start': now(), 'slot_note': args.slot_note,
               'source_sha256': {name: sha(snapshot / name) for name in FILES},
               'protected_before_sha256': baseline, 'processes': [], 'passed': False,
               'runtime_verified': False, 'art_accepted': False}
    receipt_path = batch / 'runner-receipt.json'
    def save():
        receipt_path.write_text(json.dumps(receipt, indent=2), encoding='utf8', newline='\n')
    deadline = time.monotonic() + args.total_timeout_seconds
    save()
    try:
        for label, script, extra, timeout in steps():
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError('Total batch deadline exhausted')
            command = [str(args.blender), '--background', '--factory-startup', '--threads', '2',
                       '--python-exit-code', '1', '--python', str(snapshot / script)]
            if extra:
                command += ['--'] + extra
            row = {'step': label, 'start': now(), 'command': command, 'timeout': min(timeout, remaining)}
            receipt['processes'].append(row)
            with (batch / (label + '.log')).open('wb') as log:
                process = subprocess.Popen(command, cwd=snapshot, stdout=log, stderr=subprocess.STDOUT,
                    creationflags=(subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS) if os.name == 'nt' else 0)
                row['pid'] = process.pid
                receipt['native_processes_started'] = True
                save()
                print(f'LMS_MOSQUITO_STEP_START {label} PID={process.pid}', flush=True)
                try:
                    row['exit_code'] = process.wait(timeout=row['timeout'])
                except BaseException:
                    process.kill()
                    process.wait(timeout=10)
                    row['exit_code'] = process.returncode
                    row['interrupted_or_timed_out'] = True
                    raise
                finally:
                    row['end'] = now()
                    save()
            print(f'LMS_MOSQUITO_STEP_DONE {label} exit={row["exit_code"]}', flush=True)
            if row['exit_code'] != 0:
                raise RuntimeError('Native step failed: ' + label + '; see its log before any retry')
        audit = json.loads((snapshot / 'mosquito/candidate_motion_audit.json').read_text())
        support = json.loads((snapshot / 'surface_support_audit.json').read_text())
        if not audit['passed'] or not support['passed']:
            raise RuntimeError('Native gates rejected the candidate')
        shutil.copy2(snapshot / 'surface_support_audit.json', snapshot / 'mosquito/candidate_surface_support_audit.json')
        receipt['candidate_outputs_sha256'] = {p.name: sha(p) for p in (snapshot / 'mosquito').iterdir() if p.is_file()}
        receipt['witness_png_count'] = len(list((snapshot / 'review').rglob('*.png')))
        if receipt['witness_png_count'] != 59:
            raise RuntimeError('Witness output count differs from the bounded plan')
        receipt['passed'] = True
    except BaseException as error:
        receipt['error'] = str(error)
        raise
    finally:
        receipt['end'] = now()
        receipt['protected_unchanged'] = all(p.is_file() and sha(p) == digest for path, digest in baseline.items() for p in (Path(path),))
        if not receipt['protected_unchanged']:
            receipt['passed'] = False
        save()
        print('LMS_MOSQUITO_BATCH_RECEIPT ' + str(receipt_path), flush=True)


if __name__ == '__main__':
    main()
