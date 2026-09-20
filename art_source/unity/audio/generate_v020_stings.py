"""Original three-second result motifs using the project's preserved CC0 bank.

Writes candidates only to an explicitly chosen output directory. Never modifies
the old score, source samples or Unity assets. No remote generation is required.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

import numpy as np


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    source = Path(__file__).resolve().parents[2] / 'audio' / 'compose.py'
    spec = importlib.util.spec_from_file_location('lms_acoustic_bank', source)
    bank = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(bank)
    for sample in bank.PROVENANCE['samples']:
        if sample['instrument'] not in ('glock', 'marimba', 'bass'):
            continue
        actual = hashlib.sha256((bank.ROOT / sample['file']).read_bytes()).hexdigest()
        if actual != sample['sha256']:
            raise RuntimeError('Source sample hash mismatch: ' + sample['file'])
    args.output.mkdir(parents=True, exist_ok=True)
    # Shared opening, different playful resolution; original notes and timing.
    scores = {
        'STG_V020_HumansWin': [(0, 74), (.24, 77), (.56, 81), (.96, 79), (1.28, 76), (1.72, 74)],
        'STG_V020_MosquitoesWin': [(0, 74), (.24, 77), (.56, 81), (.96, 84), (1.28, 81), (1.72, 77)],
    }
    receipt = {'duration_seconds': 3, 'sample_rate': bank.SR, 'channels': 2,
               'source_bank': str(bank.ROOT / 'sample-provenance.json'),
               'source_bank_sha256': hashlib.sha256((bank.ROOT / 'sample-provenance.json').read_bytes()).hexdigest(),
               'generator_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
               'listening_review': 'pending', 'assets': []}
    for name, score in scores.items():
        track = np.zeros((3 * bank.SR, 2), np.float32)
        for index, (start, pitch) in enumerate(score):
            duration = .8 if index == len(score) - 1 else .32
            bank.place(track, bank.acoustic('glock', pitch, duration, .65, index), start, .15)
            bank.place(track, bank.acoustic('marimba', pitch - 12, duration, .5, index), start + .012, -.15)
        for start, pitch in ((0, 38), (.96, 45), (1.72, 38)):
            bank.place(track, bank.acoustic('bass', pitch, .55, .55), start, 0)
        track = bank.room(track, .1)
        # A short natural tail ends within the requested three seconds.
        tail = round(.25 * bank.SR)
        track[-tail:] *= np.linspace(1, 0, tail, dtype=np.float32)[:, None]
        peak = float(np.max(np.abs(track)))
        if not np.isfinite(track).all() or peak <= 0:
            raise RuntimeError('Invalid generated signal')
        track *= (10 ** (-10 / 20)) / peak
        path = args.output / (name + '.wav')
        bank.write_wav(path, track)
        receipt['assets'].append({'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                                  'score': score, 'peak_dbfs': -10,
                                  'rms_dbfs': float(20 * np.log10(np.sqrt(np.mean(track ** 2)))),
                                  'last_sample': track[-1].tolist()})
    (args.output / 'receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    print(json.dumps(receipt, indent=2))


if __name__ == '__main__':
    main()
