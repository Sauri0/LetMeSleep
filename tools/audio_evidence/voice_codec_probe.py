#!/usr/bin/env python3
"""Offline probe of the voice DSP chain (no Unity): exact Python ports of
Online/VoiceImaAdpcmCodec.cs (240-sample independent frames, 12 kHz) and
Audio/Runtime/VoiceMosquitoTimbre.cs (dual delay pitch shifter, +4 semitones), fed with the
same speech-like synthetic signal as the Unity harness (SyntheticVoice). Reports codec SNR
(global and segmental), error spectrum by band, frame-boundary steps and the timbre's pitch
ratio/level change. Usage: python voice_codec_probe.py [--out result.json] [--wav-dir DIR]
"""
import argparse
import json
import math
import random
from pathlib import Path

import numpy as np

RATE = 12000
FRAME = 240
INDEX = [-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8]
STEP = [7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
        130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166,
        1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845,
        8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767]


def synthetic_voice(seconds, f0=130.0, seed=11, gain=0.5):
    """Port of SyntheticVoice.NextFrame (continuous, never silent)."""
    rnd = random.Random(seed)
    out = np.zeros(int(seconds * RATE))
    phase, noise_state = 0.0, 0.0
    for n in range(len(out)):
        t = n / RATE
        syllable = (t * 4.5) % 1.0
        envelope = 0.3 + 0.7 * math.sin(math.pi * syllable) ** 2
        f = f0 * (1 + 0.08 * math.sin(2 * math.pi * 0.7 * t) + 0.03 * math.sin(2 * math.pi * 5.5 * t))
        phase = (phase + f / RATE) % 1.0
        vowel = 0.5 + 0.5 * math.sin(2 * math.pi * 0.9 * t)
        f1, f2, f3 = 450 + 350 * vowel, 1100 + 900 * (1 - vowel), 2600
        voiced, k = 0.0, 1
        while k * f < RATE / 2 - 200:
            fr = k * f
            formant = (math.exp(-0.5 * ((fr - f1) / 90) ** 2) + 0.7 * math.exp(-0.5 * ((fr - f2) / 120) ** 2)
                       + 0.35 * math.exp(-0.5 * ((fr - f3) / 180) ** 2))
            voiced += math.sin(2 * math.pi * k * phase) * (1 / math.sqrt(k)) * (0.05 + formant)
            k += 1
        white = rnd.random() * 2 - 1
        hiss = white - noise_state
        noise_state = white
        fric = 0.08 * hiss * (0.5 - 0.5 * math.cos(2 * math.pi * syllable / 0.12)) if syllable < 0.12 else 0.0
        out[n] = max(-1, min(1, gain * (envelope * voiced * 0.22 + fric)))
    return out


def encode_frame(samples):
    pcm = [int(round(max(-1.0, min(1.0, v)) * 32767)) for v in samples]
    predictor = pcm[0]
    total = sum(abs(pcm[i] - pcm[i - 1]) for i in range(1, len(pcm)))
    target = max(7, (total // max(1, len(pcm) - 1)) // 2)
    index = 0
    while index < 88 and STEP[index] < target:
        index += 1
    nibbles = []
    p, idx = predictor, index
    for s in pcm[1:]:
        step = STEP[idx]
        diff = s - p
        nib = 0
        if diff < 0:
            nib, diff = 8, -diff
        delta = step >> 3
        if diff >= step:
            nib |= 4; diff -= step; delta += step
        if diff >= step >> 1:
            nib |= 2; diff -= step >> 1; delta += step >> 1
        if diff >= step >> 2:
            nib |= 1; delta += step >> 2
        p += -delta if nib & 8 else delta
        p = max(-32768, min(32767, p))
        idx = max(0, min(88, idx + INDEX[nib]))
        nibbles.append(nib)
    return predictor, index, nibbles


def decode_frame(predictor, index, nibbles):
    out = [predictor / 32768.0]
    p, idx = predictor, index
    for nib in nibbles:
        step = STEP[idx]
        delta = step >> 3
        if nib & 4: delta += step
        if nib & 2: delta += step >> 1
        if nib & 1: delta += step >> 2
        p += -delta if nib & 8 else delta
        p = max(-32768, min(32767, p))
        idx = max(0, min(88, idx + INDEX[nib]))
        out.append(p / 32768.0)
    return np.array(out)


def codec_roundtrip(x):
    y = np.zeros_like(x)
    for s in range(0, len(x) - FRAME + 1, FRAME):
        y[s:s + FRAME] = decode_frame(*encode_frame(x[s:s + FRAME]))
    return y


class MosquitoTimbre:
    """Port of VoiceMosquitoTimbre (BufferSize 1024, MinimumDelay 96, DelaySweep 384)."""

    def __init__(self):
        self.delay = np.zeros(1024)
        self.write = 0
        self.phase = 0.5
        self.initialized = False

    def _read(self, head):
        back = 96 + (1 - head) * 384
        pos = self.write - back
        while pos < 0:
            pos += 1024
        left = int(pos) & 1023
        right = (left + 1) & 1023
        frac = pos - math.floor(pos)
        return self.delay[left] + (self.delay[right] - self.delay[left]) * frac

    def process(self, frame, semitones=4.0):
        out = np.zeros(len(frame))
        if not self.initialized:
            self.delay[:] = max(-1, min(1, frame[0]))
            self.initialized = True
        step = (2 ** (semitones / 12) - 1) / 384
        for i, v in enumerate(frame):
            self.delay[self.write] = max(-1, min(1, v))
            second = self.phase + 0.5
            if second >= 1:
                second -= 1
            w = 0.5 - 0.5 * math.cos(2 * math.pi * self.phase)
            out[i] = max(-1, min(1, self._read(self.phase) * w + self._read(second) * (1 - w)))
            self.write = (self.write + 1) & 1023
            self.phase = (self.phase + step) % 1.0
        return out


def f0_track(x, lo=70, hi=450):
    n = int(RATE * 0.04)
    res = []
    for s in range(0, len(x) - n, n // 2):
        fr = x[s:s + n] * np.hanning(n)
        if np.sqrt(np.mean(fr ** 2)) < 1e-4:
            continue
        ac = np.fft.irfft(np.abs(np.fft.rfft(fr, 2 * n)) ** 2)[:n]
        a, b = int(RATE / hi), int(RATE / lo)
        lag = a + int(np.argmax(ac[a:b]))
        if ac[0] > 0 and ac[lag] / ac[0] > 0.3:
            res.append(RATE / lag)
    return float(np.median(res)) if res else None


def band_snr(x, y):
    e = x - y
    X = np.abs(np.fft.rfft(x)) ** 2
    E = np.abs(np.fft.rfft(e)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    out = {}
    for lo, hi in ((80, 500), (500, 1500), (1500, 3000), (3000, 6000)):
        sel = (f >= lo) & (f < hi)
        out[f"{lo}-{hi}Hz"] = round(10 * math.log10(X[sel].sum() / max(E[sel].sum(), 1e-20)), 2)
    return out


def write_wav(path, x):
    import struct
    data = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    with open(path, "wb") as fh:
        fh.write(b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, RATE, RATE * 2, 2, 16)
                 + b"data" + struct.pack("<I", len(data)) + data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out")
    ap.add_argument("--wav-dir")
    ap.add_argument("--seconds", type=float, default=4.0)
    a = ap.parse_args()
    x = synthetic_voice(a.seconds)
    y = codec_roundtrip(x)
    n = len(y)
    x = x[:n]
    err = x - y
    seg = []
    for s in range(0, n - FRAME + 1, FRAME):
        p = np.mean(x[s:s + FRAME] ** 2)
        q = np.mean(err[s:s + FRAME] ** 2)
        if p > 1e-8:
            seg.append(10 * math.log10(p / max(q, 1e-20)))
    steps = [abs(y[s] - y[s - 1]) for s in range(FRAME, n, FRAME)]
    inner = np.abs(np.diff(y))
    timbre = MosquitoTimbre()
    z = np.concatenate([timbre.process(y[s:s + FRAME]) for s in range(0, n - FRAME + 1, FRAME)])
    f_in, f_out = f0_track(y), f0_track(z)
    res = {
        "signal": "SyntheticVoice f0=130 Hz, 12 kHz, %.1f s" % a.seconds,
        "codec": "IMA ADPCM 4 bit, independent 240-sample frames (126 B, 50.4 kbit/s payload)",
        "snrDb": round(10 * math.log10(np.mean(x ** 2) / np.mean(err ** 2)), 2),
        "segmentalSnrDb": round(float(np.mean(seg)), 2),
        "worstFrameSnrDb": round(float(np.min(seg)), 2),
        "bandSnrDb": band_snr(x, y),
        "frameBoundaryStepP99": round(float(np.percentile(steps, 99)), 5),
        "innerStepP99": round(float(np.percentile(inner, 99)), 5),
        "mosquito": {
            "f0InHz": round(f_in, 1) if f_in else None, "f0OutHz": round(f_out, 1) if f_out else None,
            "ratio": round(f_out / f_in, 3) if f_in and f_out else None, "expectedRatio": round(2 ** (4 / 12), 3),
            "levelChangeDb": round(10 * math.log10(np.mean(z ** 2) / np.mean(y ** 2)), 2),
        },
    }
    print(json.dumps(res, indent=1))
    if a.out:
        Path(a.out).write_text(json.dumps(res, indent=1), encoding="utf-8")
    if a.wav_dir:
        d = Path(a.wav_dir)
        d.mkdir(parents=True, exist_ok=True)
        write_wav(d / "voice_source.wav", x)
        write_wav(d / "voice_ima_adpcm.wav", y)
        write_wav(d / "voice_ima_adpcm_mosquito.wav", z)


if __name__ == "__main__":
    main()
