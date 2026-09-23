#!/usr/bin/env python3
"""Objective listening harness for Let me sleep (analyzer half).

Subcommands
  clips     <clips_dir> --out DIR [--unity PROJECT] [--spectrograms]
            Static quality of audio assets: loudness (EBU R128), true/sample peak, noise floor,
            DC, abrupt edges, clicks, loop seams, spectrum; which cue/bed uses each clip.
  captures  <captures_dir> --out DIR [--spectrograms]
            Measures the WAV+JSON recordings written by the Unity AudioEvidence scenarios
            (unity/Assets/LetMeSleep/Tests/AudioEvidence): L/R balance, distance attenuation,
            occlusion, interior/exterior decay, pan tracking and doppler of moving sources,
            loudness per category, peaks/clipping, clicks and hard stops at transitions, loop
            seams, residual silence, voice counts, voice-chat proximity/occlusion/timbre/dropouts.
  report    --clips DIR --captures DIR --out DIR     Markdown summary with PASS/FAIL vs targets.json.

Only numpy + ffmpeg/ffprobe are required (no scipy). All numbers are objective measurements of
the recorded mix; they do not replace listening on real speakers/headphones.
"""
import argparse
import csv
import json
import math
import os
import re
import subprocess
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
EPS = 1e-12


# ----------------------------------------------------------------------------- io helpers
def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")


def probe(path):
    out = run(["ffprobe", "-v", "error", "-show_entries", "stream=codec_name,sample_rate,channels,duration",
               "-show_entries", "format=duration", "-of", "json", str(path)])
    data = json.loads(out.stdout or "{}")
    stream = (data.get("streams") or [{}])[0]
    duration = stream.get("duration") or data.get("format", {}).get("duration") or 0
    return {"codec": stream.get("codec_name"), "sampleRate": int(stream.get("sample_rate", 0) or 0),
            "channels": int(stream.get("channels", 0) or 0), "duration": float(duration)}


def read_audio(path):
    """Decodes any file to float64 [n, ch]. WAV natively; other formats with one ffmpeg call."""
    if str(path).lower().endswith(".wav"):
        native = read_wav(path)
        if native is not None:
            return native
    out = subprocess.run(["ffmpeg", "-hide_banner", "-v", "info", "-i", str(path), "-f", "f32le", "-acodec", "pcm_f32le", "-"],
                         capture_output=True)
    err = out.stderr.decode("utf-8", "replace")
    m = re.search(r"Stream #\d+:\d+.*?: Audio: (\w+).*?, (\d+) Hz, (mono|stereo|(\d+) channels)", err)
    if not m:
        info = probe(path)
    else:
        ch = 1 if m.group(3) == "mono" else 2 if m.group(3) == "stereo" else int(m.group(4))
        info = {"codec": m.group(1), "sampleRate": int(m.group(2)), "channels": ch}
    data = np.frombuffer(out.stdout, dtype="<f4").astype(np.float64)
    ch = max(1, info["channels"])
    data = data[: len(data) // ch * ch].reshape(-1, ch)
    info["duration"] = len(data) / max(1, info["sampleRate"])
    return data, info["sampleRate"], info


def db(x):
    return 20.0 * math.log10(max(float(x), EPS))


def pdb(x):
    return 10.0 * math.log10(max(float(x), EPS))


def rms(x):
    return float(np.sqrt(np.mean(np.square(x)))) if x.size else 0.0


def mono(x):
    return x.mean(axis=1) if x.ndim == 2 else x


def moving_mean(x, n):
    n = max(1, int(n))
    c = np.cumsum(np.insert(np.asarray(x, dtype=np.float64), 0, 0.0))
    out = (c[n:] - c[:-n]) / n
    pad_l = (len(x) - len(out)) // 2
    return np.pad(out, (pad_l, len(x) - len(out) - pad_l), mode="edge")


def rnd(v, n=2):
    if v is None:
        return None
    if isinstance(v, float):
        if math.isnan(v) or math.isinf(v):
            return None
        return round(v, n)
    return v


# ----------------------------------------------------------------------------- wav io
def read_wav(path):
    """Native RIFF/WAVE reader (PCM 16/24/32, float32/64, extensible). Returns (x[n,ch], sr, info) or None."""
    import struct
    raw = Path(path).read_bytes()
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        return None
    pos, fmt, data = 12, None, None
    while pos + 8 <= len(raw):
        cid, size = raw[pos:pos + 4], struct.unpack("<I", raw[pos + 4:pos + 8])[0]
        body = raw[pos + 8:pos + 8 + size]
        if cid == b"fmt ":
            tag, ch, sr, _, align, bits = struct.unpack("<HHIIHH", body[:16])
            if tag == 0xFFFE and len(body) >= 26:
                tag = struct.unpack("<H", body[24:26])[0]
            fmt = (tag, ch, sr, bits)
        elif cid == b"data":
            data = body
        pos += 8 + size + (size & 1)
    if fmt is None or data is None:
        return None
    tag, ch, sr, bits = fmt
    if tag == 3 and bits == 32:
        x = np.frombuffer(data[: len(data) // 4 * 4], dtype="<f4").astype(np.float64)
    elif tag == 3 and bits == 64:
        x = np.frombuffer(data[: len(data) // 8 * 8], dtype="<f8").astype(np.float64)
    elif tag == 1 and bits == 16:
        x = np.frombuffer(data[: len(data) // 2 * 2], dtype="<i2").astype(np.float64) / 32768.0
    elif tag == 1 and bits == 24:
        b = np.frombuffer(data[: len(data) // 3 * 3], dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
        v = np.where(v & 0x800000, v - 0x1000000, v)
        x = v.astype(np.float64) / 8388608.0
    elif tag == 1 and bits == 32:
        x = np.frombuffer(data[: len(data) // 4 * 4], dtype="<i4").astype(np.float64) / 2147483648.0
    else:
        return None
    x = x[: len(x) // ch * ch].reshape(-1, ch)
    codec = {3: "pcm_f%d" % bits, 1: "pcm_s%dle" % bits}.get(tag, str(tag))
    return x, sr, {"codec": codec, "sampleRate": sr, "channels": ch, "duration": len(x) / sr}


# ----------------------------------------------------------------------------- loudness (ITU-R BS.1770-4 / EBU R128, numpy)
def _biquad_response(b, a, n_bins, n_fft):
    w = 2 * np.pi * np.arange(n_bins) / n_fft
    z1, z2 = np.exp(-1j * w), np.exp(-2j * w)
    return (b[0] + b[1] * z1 + b[2] * z2) / (a[0] + a[1] * z1 + a[2] * z2)


def k_weighting(sr):
    """K-weighting biquads for any rate (pre-filter shelf + RLB high-pass), as in BS.1770 / pyloudnorm."""
    g, q, fc = 3.999843853973347, 0.7071752369554196, 1681.974450955533
    A = 10 ** (g / 40)
    w0 = 2 * math.pi * fc / sr
    alpha = math.sin(w0) / (2 * q)
    c = math.cos(w0)
    shelf_b = [A * ((A + 1) + (A - 1) * c + 2 * math.sqrt(A) * alpha), -2 * A * ((A - 1) + (A + 1) * c),
               A * ((A + 1) + (A - 1) * c - 2 * math.sqrt(A) * alpha)]
    shelf_a = [(A + 1) - (A - 1) * c + 2 * math.sqrt(A) * alpha, 2 * ((A - 1) - (A + 1) * c),
               (A + 1) - (A - 1) * c - 2 * math.sqrt(A) * alpha]
    q, fc = 0.5003270373238773, 38.13547087602444
    w0 = 2 * math.pi * fc / sr
    alpha = math.sin(w0) / (2 * q)
    c = math.cos(w0)
    hp_b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
    hp_a = [1 + alpha, -2 * c, 1 - alpha]
    return (shelf_b, shelf_a), (hp_b, hp_a)


def k_filter(x, sr):
    """K-weighting applied in the frequency domain (zero padded; equivalent to the IIR for these short responses)."""
    n = len(x)
    n_fft = 1 << int(math.ceil(math.log2(n + sr)))
    (sb, sa), (hb, ha) = k_weighting(sr)
    X = np.fft.rfft(x, n_fft, axis=0)
    H = _biquad_response(sb, sa, X.shape[0], n_fft) * _biquad_response(hb, ha, X.shape[0], n_fft)
    return np.fft.irfft(X * H[:, None], n_fft, axis=0)[:n]


class Loudness:
    """Precomputes K-weighted 100 ms block energies once; answers any [start, end) query quickly."""

    def __init__(self, x, sr):
        self.sr = sr
        x = x if x.ndim == 2 else x[:, None]
        k = k_filter(x, sr)
        hop = int(sr * 0.1)
        e = np.square(k).sum(axis=1)  # channel weights 1.0 for L/R (and mono)
        nblocks = len(e) // hop
        self.blocks = e[: nblocks * hop].reshape(nblocks, hop).mean(axis=1) if nblocks else np.zeros(0)

    @staticmethod
    def _lufs(power):
        return -0.691 + 10 * np.log10(np.maximum(power, 1e-20))

    def _windows(self, b0, b1, length_blocks):
        blk = self.blocks[b0:b1]
        if len(blk) < length_blocks:
            return np.zeros(0)
        c = np.cumsum(np.insert(blk, 0, 0.0))
        return (c[length_blocks:] - c[:-length_blocks]) / length_blocks

    def stats(self, start=None, end=None):
        b0 = 0 if start is None else max(0, int(round(start / 0.1)))
        b1 = len(self.blocks) if end is None else min(len(self.blocks), int(round(end / 0.1)))
        res = {"I": None, "LRA": None, "Mmax": None, "Smax": None}
        m = self._windows(b0, b1, 4)
        if m.size:
            ml = self._lufs(m)
            res["Mmax"] = float(ml.max())
            gated = m[ml > -70]
            if gated.size:
                rel = self._lufs(gated.mean()) - 10
                g2 = gated[self._lufs(gated) > rel]
                if g2.size:
                    res["I"] = float(self._lufs(g2.mean()))
        s = self._windows(b0, b1, 30)
        if s.size:
            sl = self._lufs(s)
            res["Smax"] = float(sl.max())
            g = s[sl > -70]
            if g.size:
                rel = self._lufs(g.mean()) - 20
                g2 = self._lufs(g[self._lufs(g) > rel])
                if g2.size > 1:
                    res["LRA"] = float(np.percentile(g2, 95) - np.percentile(g2, 10))
        return res


def true_peak(x, sr, start=None, end=None, factor=4, chunk=1 << 16):
    """(dBTP, dBFS sample peak): 4x FFT oversampling in overlapping chunks (BS.1770 Annex 2 intent)."""
    x = x if x.ndim == 2 else x[:, None]
    a = 0 if start is None else max(0, int(start * sr))
    b = len(x) if end is None else min(len(x), int(end * sr))
    seg = x[a:b]
    if seg.size == 0:
        return None, None
    peak_sample = float(np.max(np.abs(seg)))
    best = peak_sample
    pad = 512
    for s in range(0, len(seg), chunk):
        lo, hi = max(0, s - pad), min(len(seg), s + chunk + pad)
        block = seg[lo:hi]
        n = len(block)
        X = np.fft.rfft(block, axis=0)
        Y = np.zeros((n * factor // 2 + 1, block.shape[1]), dtype=complex)
        Y[: X.shape[0]] = X
        y = np.fft.irfft(Y, n * factor, axis=0) * factor
        core = y[(s - lo) * factor: (s - lo + min(chunk, len(seg) - s)) * factor]
        if core.size:
            best = max(best, float(np.max(np.abs(core))))
    return db(best), db(peak_sample)


def ebur128_ffmpeg(path):
    """Reference measurement with ffmpeg's ebur128 filter; used by `selftest` to validate the numpy meter."""
    err = run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128=peak=true+sample", "-f", "null", "-"]).stderr
    summary = err.split("Summary:")[-1]
    res = {}
    m = re.search(r"I:\s+(-?[\d.]+) LUFS", summary)
    res["I"] = float(m.group(1)) if m else None
    m = re.search(r"LRA:\s+(-?[\d.]+) LU", summary)
    res["LRA"] = float(m.group(1)) if m else None
    for label, key in (("True peak", "TP"), ("Sample peak", "SP")):
        m = re.search(label + r":\s*\n\s*Peak:\s+(-?[\d.]+|-inf) dBFS", summary)
        res[key] = float(m.group(1)) if m and m.group(1) != "-inf" else None
    return res


# ----------------------------------------------------------------------------- spectral helpers
def spectrum(x, sr, nfft=8192):
    """Welch-averaged power spectrum of a mono signal."""
    x = np.asarray(x, dtype=np.float64)
    if len(x) < 64:
        return np.array([0.0]), np.array([EPS])
    nfft = int(min(nfft, 2 ** int(math.floor(math.log2(len(x))))))
    hop = nfft // 2
    win = np.hanning(nfft)
    acc = np.zeros(nfft // 2 + 1)
    count = 0
    for s in range(0, len(x) - nfft + 1, hop):
        acc += np.abs(np.fft.rfft(x[s:s + nfft] * win)) ** 2
        count += 1
    if count == 0:
        acc = np.abs(np.fft.rfft(np.pad(x, (0, nfft - len(x))) * win)) ** 2
        count = 1
    return np.fft.rfftfreq(nfft, 1.0 / sr), acc / count


def band_db(freqs, power, lo, hi):
    sel = (freqs >= lo) & (freqs < hi)
    return pdb(power[sel].sum()) if sel.any() else None


def centroid(freqs, power):
    total = power.sum()
    return float((freqs * power).sum() / total) if total > 0 else 0.0


OCTAVES = [63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000]


def octave_profile(freqs, power):
    out = []
    for f in OCTAVES:
        lo, hi = f / math.sqrt(2), f * math.sqrt(2)
        out.append(band_db(freqs, power, lo, hi) or -120.0)
    return out


def spectral_features(x, sr):
    f, p = spectrum(mono(x), sr)
    total = pdb(p.sum())
    hf4 = band_db(f, p, 4000, sr / 2)
    hf2 = band_db(f, p, 2000, sr / 2)
    hf35 = band_db(f, p, 3500, sr / 2)
    return {
        "centroidHz": rnd(centroid(f, p), 0),
        "hfAbove4kDb": rnd((hf4 - total) if hf4 is not None else None),
        "hfAbove2kDb": rnd((hf2 - total) if hf2 is not None else None),
        "hfAbove3500Db": rnd((hf35 - total) if hf35 is not None else None),
        "octaves": [rnd(v, 1) for v in octave_profile(f, p)],
    }


# ----------------------------------------------------------------------------- defects
def detect_clicks(x, sr, ratio=12.0, floor_db=-46.0, window_ms=10.0):
    """Discontinuities: a sample-to-sample step far outside the local step distribution.

    |x[n]-x[n-1]| > ratio * RMS(first difference over +-window/2, centre excluded) and above floor_db.
    Robust to band-limited/upsampled material (linear-interpolation kinks do not produce step outliers),
    sensitive to hard cuts, zero-fill gaps, non-zero clip starts and splices. Returns (times s, worst ratio).
    """
    x = x if x.ndim == 2 else x[:, None]
    floor = 10 ** (floor_db / 20)
    win = max(8, int(sr * window_ms / 1000))
    hits, worst = [], 0.0
    for c in range(x.shape[1]):
        d1 = np.abs(np.diff(x[:, c]))
        if d1.size < win * 2:
            continue
        e = np.square(d1)
        local = moving_mean(e, win) * win
        local = np.sqrt(np.maximum(local - e, 0) / max(1, win - 1)) + 1e-9
        r = d1 / local
        big = d1 > floor
        if big.any():
            worst = max(worst, float(np.max(r[big])))
        hits.extend(np.nonzero((r > ratio) & big)[0].tolist())
    hits = sorted(set(hits))
    events, last = [], -10 ** 9
    for h in hits:
        if h - last > sr * 0.005:
            events.append((h + 1) / sr)
        last = h
    return events, worst


def detect_hard_edges(x, sr, level_db=-45.0, silence_ms=1.0):
    """Hard stops (signal -> exact digital silence) and hard starts (silence -> signal), in seconds."""
    m = np.max(np.abs(x), axis=1) if x.ndim == 2 else np.abs(x)
    thr = 10 ** (level_db / 20)
    zero = m <= 1e-7
    n = max(1, int(sr * silence_ms / 1000))
    zrun = moving_mean(zero.astype(np.float64), n) >= 0.999
    stops, starts = [], []
    loud = m > thr
    idx = np.nonzero(loud[:-1] & zero[1:])[0]
    for i in idx:
        j = min(len(zrun) - 1, i + 1 + n // 2)
        if zrun[j]:
            stops.append((i + 1) / sr)
    idx = np.nonzero(zero[:-1] & loud[1:])[0]
    for i in idx:
        j = max(0, i - n // 2)
        if zrun[j]:
            starts.append((i + 1) / sr)
    return stops, starts


def dropouts(x, sr, min_ms=5.0):
    m = np.max(np.abs(x), axis=1) if x.ndim == 2 else np.abs(x)
    silent = m < 1e-5
    runs, total = 0, 0
    n = int(sr * min_ms / 1000)
    i = 0
    edges = np.diff(np.concatenate([[0], silent.astype(np.int8), [0]]))
    starts = np.nonzero(edges == 1)[0]
    ends = np.nonzero(edges == -1)[0]
    for s, e in zip(starts, ends):
        if e - s >= n:
            runs += 1
            total += e - s
    return runs, 1000.0 * total / sr


def schroeder_decay(x, sr):
    """EDT and T20 (s) from the energy decay after the loudest 5 ms."""
    e = np.square(mono(x))
    if e.size < sr * 0.05:
        return None, None
    peak = int(np.argmax(moving_mean(e, int(sr * 0.005))))
    tail = e[peak:]
    sch = np.cumsum(tail[::-1])[::-1]
    sch_db = 10 * np.log10(sch / (sch[0] + EPS) + EPS)

    def cross(level):
        idx = np.nonzero(sch_db <= level)[0]
        return idx[0] / sr if idx.size else None

    t0, t10, t5, t25 = cross(0.0), cross(-10.0), cross(-5.0), cross(-25.0)
    edt = 6 * t10 if t10 is not None else None
    t20 = 3 * (t25 - t5) if (t25 is not None and t5 is not None) else None
    return edt, t20


def f0_estimate(x, sr, lo=70.0, hi=450.0):
    """Median fundamental from autocorrelation over 40 ms voiced frames."""
    y = mono(x)
    n = int(sr * 0.04)
    hop = n // 2
    f0s = []
    lag_lo, lag_hi = int(sr / hi), int(sr / lo)
    for s in range(0, len(y) - n, hop):
        frame = y[s:s + n] * np.hanning(n)
        if rms(frame) < 1e-4:
            continue
        spec = np.fft.rfft(frame, 2 * n)
        ac = np.fft.irfft(np.abs(spec) ** 2)[: n]
        if ac[0] <= 0:
            continue
        seg = ac[lag_lo:lag_hi]
        if seg.size == 0:
            continue
        lag = lag_lo + int(np.argmax(seg))
        if ac[lag] / ac[0] > 0.3:
            f0s.append(sr / lag)
    return float(np.median(f0s)) if f0s else None


def pitch_track(x, sr, lo=150.0, hi=2500.0, win_s=0.08, hop_s=0.04):
    """Dominant spectral peak per window (parabolic interpolation), for doppler of loops."""
    y = mono(x)
    n = int(sr * win_s)
    nfft = 1 << (n - 1).bit_length()
    nfft *= 4
    hop = int(sr * hop_s)
    freqs = np.fft.rfftfreq(nfft, 1.0 / sr)
    sel = (freqs >= lo) & (freqs <= hi)
    idx_sel = np.nonzero(sel)[0]
    times, peaks = [], []
    win = np.hanning(n)
    for s in range(0, len(y) - n, hop):
        frame = y[s:s + n]
        if rms(frame) < 1e-5:
            continue
        mag = np.abs(np.fft.rfft(frame * win, nfft))
        k = idx_sel[int(np.argmax(mag[sel]))]
        if 0 < k < len(mag) - 1:
            a, b, c = np.log(mag[k - 1] + EPS), np.log(mag[k] + EPS), np.log(mag[k + 1] + EPS)
            off = 0.5 * (a - c) / (a - 2 * b + c + EPS)
        else:
            off = 0.0
        times.append((s + n / 2) / sr)
        peaks.append((k + off) * sr / nfft)
    return np.array(times), np.array(peaks)


def onsets(x, sr, threshold_db=-50.0, min_gap=0.12):
    env = moving_mean(np.max(np.abs(x), axis=1) if x.ndim == 2 else np.abs(x), int(sr * 0.003))
    thr = 10 ** (threshold_db / 20)
    above = env > thr
    idx = np.nonzero(above[1:] & ~above[:-1])[0]
    out, last = [], -1e9
    for i in idx:
        t = (i + 1) / sr
        if t - last >= min_gap:
            out.append(t)
            last = t
    return out


# ----------------------------------------------------------------------------- unity usage
def unity_usage(project):
    """Maps clip file name -> list of users (cues and beds) with their settings."""
    if not project:
        return {}
    project = Path(project)
    assets = project / "Assets" / "LetMeSleep" / "Audio"
    guid_to_clip = {}
    for meta in (assets / "Clips").glob("*.meta"):
        m = re.search(r"guid: (\w+)", meta.read_text(encoding="utf-8", errors="replace"))
        if m:
            guid_to_clip[m.group(1)] = meta.name[:-5]
    usage = {}
    for cue in (assets / "Generated" / "Cues").glob("*.asset"):
        text = cue.read_text(encoding="utf-8", errors="replace")
        cue_id = re.search(r"cueId: (.*)", text).group(1).strip()
        clips_block = text.split("clips:")[1].split("output:")[0]
        for g in re.findall(r"guid: (\w+)", clips_block):
            usage.setdefault(guid_to_clip.get(g, g), []).append("cue:" + cue_id)
    prefab = assets / "Generated" / "Prefabs" / "LMS_AlfaAudioRoot.prefab"
    if prefab.exists():
        text = prefab.read_text(encoding="utf-8", errors="replace")
        names = {}
        for block in text.split("--- !u!")[1:]:
            head = block.split("\n", 1)[0]
            fid = head.split("&")[-1].strip()
            if block.startswith("1 "):
                m = re.search(r"m_Name: (.*)", block)
                if m:
                    names[fid] = m.group(1).strip()
        for block in text.split("--- !u!")[1:]:
            if "clip: {fileID" in block and "fadeSeconds" in block:
                g = re.search(r"clip: \{fileID: \d+, guid: (\w+)", block)
                go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", block)
                vol = re.search(r"\n  volume: ([\d.]+)", block)
                if g:
                    usage.setdefault(guid_to_clip.get(g.group(1), g.group(1)), []).append(
                        "bed:" + names.get(go.group(1) if go else "", "?") + (f"(vol {vol.group(1)})" if vol else ""))
    return usage


# ----------------------------------------------------------------------------- spectrograms
def spectrogram(path, out_png, title=None, width=1600, height=520, start=None, duration=None):
    out_png.parent.mkdir(parents=True, exist_ok=True)
    cmd = ["ffmpeg", "-v", "error", "-y"]
    if start is not None:
        cmd += ["-ss", f"{start:.3f}"]
    if duration is not None:
        cmd += ["-t", f"{duration:.3f}"]
    cmd += ["-i", str(path), "-lavfi",
            f"showspectrumpic=s={width}x{height}:legend=1:scale=log:fscale=log:mode=separate:color=intensity:gain=1",
            "-frames:v", "1", str(out_png)]
    r = run(cmd)
    if r.returncode != 0:  # older ffmpeg without fscale
        cmd = [c.replace(":fscale=log", "") for c in cmd]
        run(cmd)
    return out_png.exists()


# ----------------------------------------------------------------------------- clips
LOOP_HINTS = ("Loop", "Buzz_", "AMB_", "MUS_")


def classify(name):
    for prefix, cat in (("MUS_", "music"), ("AMB_", "ambience"), ("STG_", "sting"), ("UI_", "ui"), ("SFX_", "sfx")):
        if name.startswith(prefix):
            return cat
    return "other"


def analyze_clip(path, targets, usage):
    x, sr, info = read_audio(path)
    name = path.name
    cat = classify(name)
    is_loop = any(h in name for h in LOOP_HINTS)
    steady_loop = is_loop and cat != "music"
    # Event loudness: the clip played once in silence (0.4 s padding so short one-shots get a momentary value).
    pad = np.zeros((int(sr * 0.4), x.shape[1]))
    meter = Loudness(np.concatenate([pad, x, pad]), sr)
    ebu = meter.stats()
    tp, sp = true_peak(x, sr)
    m = np.max(np.abs(x), axis=1)
    duration = len(x) / sr
    noise_floor = None
    if duration >= 1.5:
        frame = max(1, int(sr * 0.02))
        w = np.sqrt(np.mean(np.square(x[: len(x) // frame * frame]).reshape(-1, frame, x.shape[1]), axis=(1, 2)))
        w = w[w > 1e-7]
        noise_floor = db(np.percentile(w, 5)) if w.size else None
    thr = 10 ** (-60 / 20)
    loud = np.nonzero(m > thr)[0]
    lead = (loud[0] / sr * 1000) if loud.size else None
    trail = ((len(m) - 1 - loud[-1]) / sr * 1000) if loud.size else None
    first_db = db(m[0])
    last_db = db(m[-max(1, int(sr * 0.001)):].max())
    tail_db = db(rms(x[-max(1, int(sr * 0.01)):]))
    clicks, worst = detect_clicks(x, sr)
    dc = [float(np.mean(x[:, c])) for c in range(x.shape[1])]
    res = {
        "file": name, "category": cat, "loop": is_loop, "codec": info["codec"], "sampleRate": sr,
        "channels": info["channels"], "duration": rnd(duration, 3),
        "integratedLufs": rnd(ebu["I"]), "shortTermMaxLufs": rnd(ebu["Smax"]), "momentaryMaxLufs": rnd(ebu["Mmax"]),
        "lra": rnd(ebu["LRA"]), "truePeakDbtp": rnd(tp), "samplePeakDbfs": rnd(sp),
        "rmsDbfs": rnd(db(rms(x))), "dcOffset": rnd(max(dc, key=abs), 5), "noiseFloorDbfs": rnd(noise_floor),
        "leadingSilenceMs": rnd(lead, 1), "trailingSilenceMs": rnd(trail, 1),
        "firstSampleDbfs": rnd(first_db), "lastMsDbfs": rnd(last_db), "tail10msDbfs": rnd(tail_db),
        "clicks": len(clicks), "clickTimes": [rnd(t, 3) for t in clicks[:8]], "worstClickRatio": rnd(worst, 1),
        "usedBy": usage.get(name, []),
    }
    if x.shape[1] == 2:
        l, r = x[:, 0], x[:, 1]
        corr = float(np.dot(l, r) / (math.sqrt(np.dot(l, l) * np.dot(r, r)) + EPS))
        res["stereoCorrelation"] = rnd(corr, 3)
        res["lrBalanceDb"] = rnd(db(rms(l)) - db(rms(r)))
    res.update(spectral_features(x, sr))
    if is_loop:
        jump = float(np.max(np.abs(x[0] - x[-1])))
        p99 = float(np.percentile(np.abs(np.diff(x, axis=0)), 99.9)) + 1e-9
        joined = np.concatenate([x[-sr // 10:], x[: sr // 10]])
        seam_clicks, _ = detect_clicks(joined, sr)
        seam_clicks = [t for t in seam_clicks if abs(t - 0.1) < 0.01]
        n = int(sr * 0.05)
        res["loopSeam"] = {
            "jumpDbfs": rnd(db(jump)),
            "jumpVsP999Step": rnd(jump / p99, 2),
            "levelStepDb": rnd(db(rms(x[:n])) - db(rms(x[-n:]))) if steady_loop else None,
            "seamClicks": len(seam_clicks),
        }
    res["verdict"], res["issues"] = clip_verdict(res, targets)
    return res


def clip_verdict(c, targets):
    t = targets["asset"]
    fix, level, info = [], [], []
    if c["truePeakDbtp"] is not None and c["truePeakDbtp"] > t["sfx_true_peak_max_dbtp"]:
        level.append(f"true peak {c['truePeakDbtp']} dBTP > {t['sfx_true_peak_max_dbtp']}")
    if c["clicks"] > t["clicks_max"]:
        fix.append(f"{c['clicks']} click(s) at {c['clickTimes']} s")
    if not c["loop"]:
        if c["lastMsDbfs"] is not None and c["lastMsDbfs"] > t["abrupt_edge_dbfs"]:
            fix.append(f"abrupt end ({c['lastMsDbfs']} dBFS in last ms)")
        if c["firstSampleDbfs"] is not None and c["firstSampleDbfs"] > -40:
            fix.append(f"starts at non-zero sample ({c['firstSampleDbfs']} dBFS)")
    if c["loop"] and c.get("loopSeam"):
        s = c["loopSeam"]
        if s["jumpVsP999Step"] > t["loop_seam_jump_ratio_max"] and s["jumpDbfs"] > -50:
            fix.append(f"loop seam discontinuity ({s['jumpDbfs']} dBFS, x{s['jumpVsP999Step']} the 99.9% step)")
        if s["levelStepDb"] is not None and abs(s["levelStepDb"]) > t["loop_seam_level_step_max_db"]:
            fix.append(f"loop level step {s['levelStepDb']} dB (start vs end 50 ms)")
        if s["seamClicks"]:
            fix.append("click at loop seam")
    if c["category"] == "music" and c["integratedLufs"] is not None:
        if abs(c["integratedLufs"] - t["music_integrated_lufs"]) > t["music_tolerance_lu"]:
            level.append(f"music {c['integratedLufs']} LUFS (target {t['music_integrated_lufs']}±{t['music_tolerance_lu']})")
    if c["category"] == "ambience" and c["integratedLufs"] is not None:
        lo, hi = t["ambience_integrated_lufs"]
        if not lo <= c["integratedLufs"] <= hi:
            level.append(f"ambience {c['integratedLufs']} LUFS (target {lo}..{hi})")
    if c["category"] == "ui" and c["momentaryMaxLufs"] is not None:
        lo, hi = t["ui_short_term_lufs"]
        if not lo <= c["momentaryMaxLufs"] <= hi:
            level.append(f"UI momentary max {c['momentaryMaxLufs']} LUFS (target {lo}..{hi})")
    if abs(c["dcOffset"] or 0) > 0.005:
        fix.append(f"DC offset {c['dcOffset']}")
    if c["noiseFloorDbfs"] is not None and c["noiseFloorDbfs"] > t["noise_floor_max_dbfs"] and c["category"] not in ("music", "ambience") and not c["loop"]:
        info.append(f"floor {c['noiseFloorDbfs']} dBFS")
    if not c["usedBy"]:
        info.append("not referenced by any cue/bed (unused in game)")
    verdict = "fix" if fix else ("level" if level else "ok")
    if not c["usedBy"]:
        verdict = "unused-" + verdict
    return verdict, fix + level + info


def cmd_clips(args):
    targets = json.loads((HERE / "targets.json").read_text(encoding="utf-8"))
    usage = unity_usage(args.unity)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    files = sorted([p for p in Path(args.clips_dir).iterdir() if p.suffix.lower() in (".wav", ".ogg", ".flac", ".mp3")])
    results = []
    for p in files:
        r = analyze_clip(p, targets, usage)
        results.append(r)
        print(f"{r['file']:36s} {r['category']:8s} I={r['integratedLufs']} TP={r['truePeakDbtp']} clicks={r['clicks']} {r['verdict']}: {'; '.join(r['issues'])}")
        if args.spectrograms:
            spectrogram(p, out / "spectrograms" / "clips" / (p.stem + ".png"), width=1200, height=400)
    (out / "clips.json").write_text(json.dumps(results, indent=1, ensure_ascii=False), encoding="utf-8")
    keys = ["file", "category", "loop", "codec", "sampleRate", "channels", "duration", "integratedLufs", "shortTermMaxLufs",
            "momentaryMaxLufs", "lra", "truePeakDbtp", "samplePeakDbfs", "rmsDbfs", "noiseFloorDbfs", "dcOffset",
            "leadingSilenceMs", "trailingSilenceMs", "lastMsDbfs", "clicks", "centroidHz", "hfAbove4kDb", "verdict"]
    with open(out / "clips.csv", "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(keys + ["loopSeam", "usedBy", "issues"])
        for r in results:
            w.writerow([r.get(k) for k in keys] + [json.dumps(r.get("loopSeam")), "|".join(r["usedBy"]), "|".join(r["issues"])])
    print(f"clips: {len(results)} -> {out / 'clips.json'}")


# ----------------------------------------------------------------------------- captures
def load_capture(wav):
    meta = json.loads(Path(wav).with_suffix(".json").read_text(encoding="utf-8"))
    x, sr, _ = read_audio(wav)
    return x, sr, meta


def seg_slice(x, sr, s):
    a = max(0, int(round(s["start"] * sr)))
    b = min(len(x), int(round(s["end"] * sr)))
    return x[a:b]


def frames_in(meta, start, end):
    cols = meta.get("frameColumns", [])
    rows = [f for f in meta.get("frames", []) if start <= f[0] <= end]
    return [dict(zip(cols, r)) for r in rows]


def track_in(meta, name, start, end):
    cols = meta.get("trackColumns", [])
    return [dict(zip(cols, r)) for r in meta.get("tracks", {}).get(name, []) if start <= r[0] <= end]


def level_features(seg, sr):
    l = seg[:, 0]
    r = seg[:, 1] if seg.shape[1] > 1 else seg[:, 0]
    rl, rr = rms(l), rms(r)
    lr = db(rl) - db(rr)
    one_sided = max(rl, rr) > 1e-7 and min(rl, rr) < 1e-9
    return {
        "rmsDbfs": rnd(max(db(rms(seg)), -150.0)), "peakDbfs": rnd(max(db(np.max(np.abs(seg)) if seg.size else 0), -150.0)),
        "leftDbfs": rnd(max(db(rl), -150.0)), "rightDbfs": rnd(max(db(rr), -150.0)),
        "lrDb": rnd(max(-99.0, min(99.0, lr))), "oneChannelSilent": one_sided,
        "digitalSilence": bool(np.max(np.abs(seg)) <= 1e-9) if seg.size else True,
    }


def analyze_segment(x, sr, wav, meta, s, meter):
    seg = seg_slice(x, sr, s)
    out = {"label": s["label"], "kind": s["kind"], "start": rnd(s["start"], 3), "end": rnd(s["end"], 3),
           "params": s.get("params", {})}
    if seg.size == 0:
        out["empty"] = True
        return out
    out.update(level_features(seg, sr))
    kind = s["kind"]
    if kind in ("pan", "distance", "occlusion", "ambience", "music", "sting", "ui", "voice", "local_emitter", "footsteps", "stress", "reverb"):
        out.update(spectral_features(seg, sr))
    if kind in ("music", "ambience", "sting", "ui", "stress", "voice", "footsteps"):
        e = meter.stats(s["start"], s["end"])
        tp, _ = true_peak(x, sr, s["start"], s["end"])
        out.update({"integratedLufs": rnd(e["I"]), "shortTermMaxLufs": rnd(e["Smax"]), "momentaryMaxLufs": rnd(e["Mmax"]),
                    "lra": rnd(e["LRA"]), "truePeakDbtp": rnd(tp)})
    if kind in ("transition", "loop_seam", "swarm", "voice", "ui", "music", "stress", "orbit", "flyby", "approach", "sting"):
        clicks, worst = detect_clicks(seg, sr)
        stops, starts = detect_hard_edges(seg, sr)
        out["clicks"] = len(clicks)
        out["clickTimes"] = [rnd(s["start"] + t, 3) for t in clicks[:10]]
        out["worstClickRatio"] = rnd(worst, 1)
        out["hardStops"] = len(stops)
        out["hardStopTimes"] = [rnd(s["start"] + t, 3) for t in stops[:10]]
        out["hardStarts"] = len(starts)
    if kind == "transition":
        n = int(sr * 0.3)
        mid = len(seg) // 2
        out["levelBeforeDb"] = rnd(db(rms(seg[max(0, mid - n * 2):mid - n // 2])))
        out["levelAfterDb"] = rnd(db(rms(seg[mid + n // 2: mid + n * 2])))
        runs, ms = dropouts(seg, sr, 20)
        out["silentGapsOver20ms"] = runs
        out["silentGapMs"] = rnd(ms, 1)
    if kind == "loop_seam":
        mid = len(seg) // 2
        n = int(sr * 0.25)
        out["levelStepDb"] = rnd(db(rms(seg[mid:mid + n])) - db(rms(seg[mid - n:mid])))
        runs, ms = dropouts(seg, sr, 3)
        out["silentGaps"] = runs
        out["silentGapMs"] = rnd(ms, 1)
    if kind == "reverb":
        edt, t20 = schroeder_decay(seg, sr)
        out["edtMs"] = rnd(edt * 1000 if edt else None, 0)
        out["t20Ms"] = rnd(t20 * 1000 if t20 else None, 0)
        e = np.square(mono(seg))
        peak = int(np.argmax(moving_mean(e, int(sr * 0.005))))
        early = e[peak:peak + int(sr * 0.05)].sum()
        late = e[peak + int(sr * 0.05): peak + int(sr * 0.6)].sum()
        out["lateToEarlyDb"] = rnd(pdb(late) - pdb(early))
    if kind == "voice":
        runs, ms = dropouts(seg, sr, 5)
        out["dropouts"] = runs
        out["dropoutMs"] = rnd(ms, 1)
        w = float(s.get("params", {}).get("startupWindow", 0) or 0)
        if w > 0 and s.get("params", {}).get("audible"):
            k = int(w * sr)
            head, tail = seg[:k], seg[k:]
            m = np.max(np.abs(head), axis=1)
            voiced = np.nonzero(m > 1e-5)[0]
            # first time after which playout runs >= 0.5 s without any >= 5 ms digital silence
            first = None
            sil = (np.max(np.abs(seg), axis=1) < 1e-5).astype(np.int8)
            edges = np.diff(np.concatenate([[0], sil, [0]]))
            gaps = [(p0, p1) for p0, p1 in zip(np.nonzero(edges == 1)[0], np.nonzero(edges == -1)[0]) if p1 - p0 >= int(0.005 * sr)]
            cursor = 0
            for g0, g1 in gaps:
                if g0 - cursor >= int(0.5 * sr):
                    break
                cursor = g1
            if len(seg) - cursor >= int(0.5 * sr) or not gaps:
                first = cursor / sr
            out["firstSoundMs"] = rnd(voiced[0] / sr * 1000, 0) if voiced.size else None
            out["continuousFromMs"] = rnd(first * 1000, 0) if first is not None else None
            _, head_ms = dropouts(head, sr, 5)
            out["startupSilenceMs"] = rnd(head_ms, 0)
            truns, tms = dropouts(tail, sr, 5)
            out["steadyGaps"] = truns
            out["steadyGapMs"] = rnd(tms, 1)
            tclicks, _ = detect_clicks(tail, sr)
            out["steadyClicks"] = len(tclicks)
            out["steadySeconds"] = rnd(len(tail) / sr, 2)
        out["f0Hz"] = rnd(f0_estimate(seg, sr), 1)
        # level envelope over 250 ms windows (fluctuation while moving / network impairment)
        n = int(sr * 0.25)
        env = [db(rms(seg[i:i + n])) for i in range(0, len(seg) - n, n)]
        if env:
            out["levelWindowsDb"] = [rnd(v, 1) for v in env]
        if s.get("params", {}).get("track"):
            out.update(motion_features(seg, sr, meta, s))
            f = []
            m = int(sr * 0.2)
            for i in range(0, len(seg) - m, m):
                v = f0_estimate(seg[i:i + m], sr)
                if v:
                    f.append(v)
            if len(f) > 3:
                out["f0SpreadCents"] = rnd(float(1200 * np.log2(max(f) / min(f))), 1)
    if kind == "ui":
        out["onsets"] = len(onsets(seg, sr, threshold_db=-60, min_gap=0.02))
    if kind in ("orbit", "flyby", "approach"):
        out.update(motion_features(seg, sr, meta, s))
    if kind == "swarm" or kind == "stress":
        fr = frames_in(meta, s["start"], s["end"])
        if fr:
            out["maxPlayingSources"] = max(f["playingSources"] for f in fr)
            out["maxAudibleSources"] = max(f["audibleSources"] for f in fr)
            out["maxVirtualSources"] = max(f["virtualSources"] for f in fr)
            out["maxPoolPlaying"] = max(f["poolPlaying"] for f in fr)
        ev = [e for e in meta.get("events", []) if s["start"] <= e["t"] <= s["end"]]
        out["wingStarts"] = sum(1 for e in ev if e["label"] == "wing_start")
        out["wingStops"] = sum(1 for e in ev if e["label"] == "wing_stop")
        out["clippedSamples"] = int(np.sum(np.abs(seg) >= 0.999))
    if kind == "footsteps":
        out.update(step_features(seg, sr))
    return out


def motion_features(seg, sr, meta, s):
    name = s.get("params", {}).get("track")
    tr = track_in(meta, name, s["start"], s["end"]) if name else []
    res = {}
    if len(tr) < 10:
        return res
    hop = 0.05
    n = int(sr * hop)
    lr, lvl, az, dist, vr, times = [], [], [], [], [], []
    for row in tr[::3]:
        a = int((row["t"] - s["start"]) * sr) - n // 2
        if a < 0 or a + n > len(seg):
            continue
        w = seg[a:a + n]
        if rms(w) < 1e-6:
            continue
        lr.append(db(rms(w[:, 0])) - db(rms(w[:, 1])))
        lvl.append(db(rms(w)))
        az.append(row["azimuthDeg"])
        dist.append(max(0.05, row["distance"]))
        vr.append(row["radialVelocity"])
        times.append(row["t"] - s["start"])
    if len(lr) < 5:
        return res
    lr, lvl, az, dist, vr = map(np.array, (lr, lvl, az, dist, vr))
    expected = -np.sin(np.radians(az))  # +az = right -> L-R negative
    if np.std(lr) > 1e-6 and np.std(expected) > 1e-6:
        res["panTrackingR"] = rnd(float(np.corrcoef(lr, expected)[0, 1]), 3)
    res["lrSpanDb"] = rnd(float(np.max(lr) - np.min(lr)))
    res["lrAt90RightDb"] = rnd(float(np.median(lr[np.abs(az - 90) < 20]))) if np.any(np.abs(az - 90) < 20) else None
    res["lrAt90LeftDb"] = rnd(float(np.median(lr[np.abs(az + 90) < 20]))) if np.any(np.abs(az + 90) < 20) else None
    if np.std(np.log10(dist)) > 1e-3:
        slope = np.polyfit(np.log10(dist), lvl, 1)[0]
        res["levelVsDistanceSlopeDbPerDecade"] = rnd(float(slope))
        res["levelSpanDb"] = rnd(float(np.max(lvl) - np.min(lvl)))
    # doppler: pitch vs radial velocity
    t_p, f_p = pitch_track(seg, sr)
    if len(f_p) > 8 and np.max(np.abs(vr)) > 0.5:
        rv = np.interp(t_p, times, vr)
        base = np.median(f_p[np.abs(rv) < 0.3]) if np.any(np.abs(rv) < 0.3) else np.median(f_p)
        cents = 1200 * np.log2(np.maximum(f_p, 1) / max(base, 1))
        expected_c = 1200 * np.log2(343.0 / (343.0 + rv))
        approaching = cents[rv < -2.0]
        receding = cents[rv > 2.0]
        res["dopplerMeasuredCentsApproach"] = rnd(float(np.median(approaching)), 1) if approaching.size else None
        res["dopplerMeasuredCentsRecede"] = rnd(float(np.median(receding)), 1) if receding.size else None
        res["dopplerExpectedCentsApproach"] = rnd(float(np.median(expected_c[rv < -2.0])), 1) if approaching.size else None
        res["pitchTrackBaseHz"] = rnd(float(base), 1)
    return res


def step_features(seg, sr):
    ons = onsets(seg, sr, threshold_db=-55)
    levels, cents, snippets = [], [], []
    n = int(sr * 0.12)
    for t in ons:
        a = int(t * sr)
        w = seg[a:a + n]
        if len(w) < n // 2:
            continue
        levels.append(db(np.max(np.abs(w))))
        f, p = spectrum(mono(w), sr, 2048)
        cents.append(centroid(f, p))
        snippets.append(mono(w)[: n // 2])
    sims = []
    edges = [100 * 2 ** (i / 3) for i in range(0, 22)]  # 100 Hz .. ~12.7 kHz, 1/3 octave

    def profile(a):
        f = np.fft.rfftfreq(len(a), 1.0 / sr)
        p = np.abs(np.fft.rfft(a * np.hanning(len(a)))) ** 2
        return np.array([10 * np.log10(p[(f >= lo) & (f < hi)].sum() + 1e-20) for lo, hi in zip(edges[:-1], edges[1:])])

    for i in range(1, len(snippets)):
        a, b = snippets[i - 1], snippets[i]
        m = min(len(a), len(b))
        pa, pb = profile(a[:m]), profile(b[:m])
        pa, pb = pa - pa.mean(), pb - pb.mean()
        sims.append(float(np.dot(pa, pb) / (np.linalg.norm(pa) * np.linalg.norm(pb) + EPS)))
    return {"steps": len(ons), "stepPeakMeanDbfs": rnd(float(np.mean(levels))) if levels else None,
            "stepPeakSpreadDb": rnd(float(np.ptp(levels))) if levels else None,
            "stepCentroidHz": rnd(float(np.median(cents)), 0) if cents else None,
            "stepSimilarity": rnd(float(np.median(sims)), 3) if sims else None}


def analyze_capture(wav, out_dir, spectrograms):
    x, sr, meta = load_capture(wav)
    if x.shape[1] == 1:
        x = np.repeat(x, 2, axis=1)
    meter = Loudness(x, sr)
    tp, sp = true_peak(x, sr)
    whole = {"TP": tp, "SP": sp}
    clicks, worst = detect_clicks(x, sr)
    stops, _ = detect_hard_edges(x, sr)
    fr = meta.get("frames", [])
    cols = meta.get("frameColumns", [])
    fdicts = [dict(zip(cols, r)) for r in fr]
    res = {
        "scenario": meta.get("scenario"), "wav": str(wav), "captureMode": meta.get("captureMode"),
        "sampleRate": sr, "duration": rnd(len(x) / sr, 2), "unityVersion": meta.get("unityVersion"),
        "realVoices": meta.get("realVoices"), "dspBufferSize": meta.get("dspBufferSize"),
        "truePeakDbtp": rnd(whole["TP"]), "samplePeakDbfs": rnd(whole["SP"]),
        "clippedSamples": int(np.sum(np.abs(x) >= 0.999)),
        "clicks": len(clicks), "clickTimes": [rnd(t, 3) for t in clicks[:20]], "hardStops": len(stops),
        "hardStopTimes": [rnd(t, 3) for t in stops[:20]],
        "maxPlayingSources": max((f["playingSources"] for f in fdicts), default=None),
        "maxAudibleSources": max((f["audibleSources"] for f in fdicts), default=None),
        "maxVirtualSources": max((f["virtualSources"] for f in fdicts), default=None),
        "meta": meta.get("meta", {}),
        "events": meta.get("events", []),
        "segments": [analyze_segment(x, sr, wav, meta, s, meter) for s in meta.get("segments", [])],
    }
    if len(fdicts) > 2:
        dsp = np.array([f["dspTime"] for f in fdicts])
        t = np.array([f["t"] for f in fdicts])
        res["dspTimeAdvancesWithCapture"] = rnd(float(np.polyfit(t, dsp, 1)[0]), 3) if np.ptp(t) > 0 else None
    if spectrograms:
        spectrogram(wav, out_dir / "spectrograms" / "captures" / (Path(wav).stem + ".png"), width=2400, height=700)
    return res


def cmd_captures(args):
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    wavs = sorted(Path(args.captures_dir).glob("*.wav"))
    results = []
    for w in wavs:
        if not w.with_suffix(".json").exists():
            continue
        print("capture", w.name)
        results.append(analyze_capture(w, out, args.spectrograms))
    (out / "captures.json").write_text(json.dumps(results, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"captures: {len(results)} -> {out / 'captures.json'}")


# ----------------------------------------------------------------------------- report
def seg_by(cap, label):
    for s in cap["segments"]:
        if s["label"] == label:
            return s
    return None


def check(rows, name, ok, measured, target):
    rows.append((name, "PASS" if ok else ("INFO" if ok is None else "FAIL"), measured, target))


def fmt(v, unit=""):
    """Plain number formatting (no dB interpretation)."""
    if v is None:
        return "n/a"
    if isinstance(v, float):
        return f"{v:.1f}{unit}"
    return f"{v}{unit}"


def fmt_level(v, unit=" dBFS"):
    """dBFS level: the analyzer floors digital silence at -150."""
    if v is None:
        return "n/a"
    if v <= -149.9:
        return "silencio digital"
    return f"{v:.1f}{unit}"


def fmt_lr(v):
    """L−R balance: ±99 marks one channel in digital silence (hard pan)."""
    if v is None:
        return "n/a"
    if v >= 98.9:
        return "sólo L (R en silencio)"
    if v <= -98.9:
        return "sólo R (L en silencio)"
    return f"{v:+.1f} dB"


def evaluate(captures, targets):
    """Returns {scenario: [(check, status, measured, target)]} and derived tables."""
    tc, tv = targets["capture"], targets["voice"]
    by = {c["scenario"]: c for c in captures}
    out, tables = {}, {}

    for c in captures:
        rows = out.setdefault(c["scenario"], [])
        check(rows, "master true peak", c["truePeakDbtp"] is None or c["truePeakDbtp"] <= tc["master_true_peak_max_dbtp"],
              fmt(c["truePeakDbtp"], " dBTP"), f"<= {tc['master_true_peak_max_dbtp']} dBTP")
        check(rows, "clipped samples", c["clippedSamples"] <= tc["clipping_samples_max"], c["clippedSamples"], "0")
        if c.get("maxAudibleSources") is not None:
            check(rows, "max audible (real) sources", c["maxAudibleSources"] <= tc["max_simultaneous_audible_voices"],
                  f"{c['maxAudibleSources']} audible / {c['maxPlayingSources']} playing / {c['maxVirtualSources']} virtual",
                  f"<= {tc['max_simultaneous_audible_voices']}")

    cap = by.get("s01_spatial_pan")
    if cap:
        rows = out[cap["scenario"]]
        table = []
        for s in cap["segments"]:
            if s["kind"] != "pan":
                continue
            p = s["params"]
            table.append((p.get("cue"), p.get("azimuth"), s.get("lrDb"), s.get("rmsDbfs"), s.get("hfAbove4kDb")))
        tables["pan"] = table
        for cue in sorted({t[0] for t in table}):
            vals = {t[1]: t for t in table if t[0] == cue}
            if -90 in vals and 90 in vals:
                lr_l, lr_r = vals[-90][2], vals[90][2]
                ok = lr_l is not None and lr_r is not None and lr_l >= tc["pan_lr_min_db_at_90"] and -lr_r >= tc["pan_lr_min_db_at_90"]
                check(rows, f"L/R at ±90° ({cue})", ok, f"izq. {fmt_lr(lr_l)} / der. {fmt_lr(lr_r)}", f">= {tc['pan_lr_min_db_at_90']} dB")
            if 0 in vals and 180 in vals:
                d_level = (vals[0][3] or 0) - (vals[180][3] or 0)
                d_hf = (vals[0][4] or 0) - (vals[180][4] or 0)
                ok = abs(d_level) >= tc["front_back_min_difference_db"] or abs(d_hf) >= tc["front_back_min_difference_db"]
                check(rows, f"front/back cue ({cue})", ok, f"level {d_level:+.1f} dB, HF {d_hf:+.1f} dB",
                      f">= {tc['front_back_min_difference_db']} dB level or HF")

    cap = by.get("s02_distance")
    if cap:
        rows = out[cap["scenario"]]
        curves = {}
        for s in cap["segments"]:
            if s["kind"] != "distance":
                continue
            p = s["params"]
            curves.setdefault(p["cue"], []).append((p["distance"], s["rmsDbfs"], p.get("maxDistance")))
        floor = seg_by(cap, "noise_floor")
        tables["distance"] = curves
        for cue, pts in curves.items():
            pts.sort()
            ref = next((l for d, l, _ in pts if abs(d - 1) < 1e-3), None)
            at10 = next((l for d, l, _ in pts if abs(d - 10) < 1e-3), None)
            mono_ok = all(pts[i][1] >= pts[i + 1][1] - 0.3 for i in range(len(pts) - 1))
            drop = (ref - at10) if (ref is not None and at10 is not None) else None
            check(rows, f"distance drop 1→10 m ({cue})", drop is not None and drop >= tc["distance_drop_1_to_10m_min_db"],
                  fmt(drop, " dB"), f">= {tc['distance_drop_1_to_10m_min_db']} dB")
            check(rows, f"monotonic with distance ({cue})", mono_ok, "yes" if mono_ok else "no", "yes")

    cap = by.get("s03_mosquito_flight")
    if cap:
        rows = out[cap["scenario"]]
        orbit = seg_by(cap, "orbit_r1.5")
        if orbit:
            r = orbit.get("panTrackingR")
            check(rows, "buzz pan follows position (orbit)", r is not None and r >= tc["pan_tracking_min_r"], fmt(r), f">= {tc['pan_tracking_min_r']}")
            check(rows, "buzz L/R span in orbit", (orbit.get("lrSpanDb") or 0) >= 2 * tc["pan_lr_min_db_at_90"],
                  "total (un canal en silencio en los laterales)" if (orbit.get("lrSpanDb") or 0) >= 90 else fmt(orbit.get("lrSpanDb"), " dB"),
                  f">= {2 * tc['pan_lr_min_db_at_90']} dB")
        fly = seg_by(cap, "flyby_5mps")
        if fly:
            a, r_ = fly.get("dopplerMeasuredCentsApproach"), fly.get("dopplerMeasuredCentsRecede")
            span = (a - r_) if (a is not None and r_ is not None) else None
            check(rows, "doppler on 5 m/s fly-by (approach−recede)", span is not None and span >= 2 * tc["doppler_min_cents_at_5mps"],
                  fmt(span, " cents") + f" (expected ≈{fmt((fly.get('dopplerExpectedCentsApproach') or 0) * 2, ' cents')})",
                  f">= {2 * tc['doppler_min_cents_at_5mps']} cents")
        app = seg_by(cap, "approach_hover_retreat")
        if app:
            check(rows, "buzz level vs distance (approach)", (app.get("levelSpanDb") or 0) >= 20, fmt(app.get("levelSpanDb"), " dB span"),
                  ">= 20 dB span 0.4→12 m")
        sw = seg_by(cap, "swarm_9_policy")
        if sw:
            check(rows, "swarm: clicks while loops enter/leave", (sw.get("clicks") or 0) <= tc["transition_clicks_max"],
                  f"{sw.get('clicks')} clicks, {sw.get('hardStops')} hard stops, {sw.get('wingStarts')} starts/{sw.get('wingStops')} stops",
                  "0 clicks")
            check(rows, "swarm: max pool voices", None, sw.get("maxPoolPlaying"), "<= 6 wing loops (policy)")

    cap = by.get("s04_swatter")
    if cap:
        rows = out[cap["scenario"]]
        for side in ("3m", "8m"):
            l, r = seg_by(cap, f"other_human_left_{side}"), seg_by(cap, f"other_human_right_{side}")
            if l and r:
                ok = (l["lrDb"] or 0) >= tc["pan_lr_min_db_at_90"] and -(r["lrDb"] or 0) >= tc["pan_lr_min_db_at_90"]
                check(rows, f"swatter left/right at {side}", ok, f"izq. {fmt_lr(l['lrDb'])} / der. {fmt_lr(r['lrDb'])}", f">= {tc['pan_lr_min_db_at_90']} dB")
        n3, n8 = seg_by(cap, "other_human_left_3m"), seg_by(cap, "other_human_left_8m")
        if n3 and n8:
            check(rows, "swatter 3 m vs 8 m level", None, fmt((n3["rmsDbfs"] or 0) - (n8["rmsDbfs"] or 0), " dB"), "info")

    cap = by.get("s05_footsteps")
    if cap:
        rows = out[cap["scenario"]]
        cents = {}
        for s in cap["segments"]:
            if s["label"].startswith("own_walk_"):
                cents[s["params"]["material"]] = s.get("stepCentroidHz")
                sim = s.get("stepSimilarity")
                check(rows, f"step variation ({s['params']['material']})", sim is not None and sim <= tc["footstep_repetition_similarity_max"],
                      fmt(sim), f"<= {tc['footstep_repetition_similarity_max']} (1.0 = identical steps)")
        vals = [v for v in cents.values() if v]
        if len(vals) >= 2:
            ratio = max(vals) / max(1, min(vals))
            check(rows, "materials distinguishable (centroid ratio)", ratio >= tc["footstep_material_centroid_min_ratio"],
                  ", ".join(f"{k} {fmt(v, ' Hz')}" for k, v in cents.items()), f"max/min >= {tc['footstep_material_centroid_min_ratio']}")
        o = seg_by(cap, "other_walk_wood")
        if o:
            check(rows, "other walker pans across (L/R over segment)", None, fmt(o.get("lrDb"), " dB mean"), "info")

    cap = by.get("s06_door_occlusion")
    if cap:
        rows = out[cap["scenario"]]
        base = seg_by(cap, "probe_open")
        for cond in ("wall_between", "door_inside_closed_room", "control_lowpass_minus6db"):
            s = seg_by(cap, "probe_" + cond)
            if base and s:
                d = (base["rmsDbfs"] or 0) - (s["rmsDbfs"] or 0)
                dh = (base["hfAbove4kDb"] or 0) - (s["hfAbove4kDb"] or 0)
                ok = d >= tc["occlusion_min_db"] and dh >= tc["occlusion_extra_hf_cut_min_db"]
                check(rows, f"occlusion: {cond}", ok, f"-{d:.1f} dB, HF(>4k) -{dh:.1f} dB extra",
                      f">= {tc['occlusion_min_db']} dB and >= {tc['occlusion_extra_hf_cut_min_db']} dB HF")

    cap = by.get("s07_interior_exterior")
    if cap:
        rows = out[cap["scenario"]]
        for sig in ("impulse", "impact", "door"):
            ext = seg_by(cap, f"{sig}_exterior_open")
            conds = ["interior_closed_room", "control_reverbzone_bathroom"]
            if sig == "impulse":
                conds += ["control_reverbzone_plain_source", "control_reverbfilter_bathroom"]
            for cond in conds:
                s = seg_by(cap, f"{sig}_{cond}")
                if ext and s:
                    d = (s.get("edtMs") or 0) - (ext.get("edtMs") or 0)
                    lo, li = ext.get("lateToEarlyDb"), s.get("lateToEarlyDb")
                    dl = (max(li, -90.0) - max(lo, -90.0)) if (lo is not None and li is not None) else 0.0
                    ok = d >= tc["interior_edt_increase_min_ms"] or dl >= tc["interior_late_to_early_increase_min_db"]
                    check(rows, f"interior tail vs exterior: {sig} / {cond}", ok,
                          f"EDT {fmt(ext.get('edtMs'))}→{fmt(s.get('edtMs'))} ms; tail 50–600 ms vs first 50 ms: "
                          f"{fmt(lo)}→{fmt(li)} dB ({dl:+.1f} dB, floor −90)",
                          f"EDT +{tc['interior_edt_increase_min_ms']} ms or tail +{tc['interior_late_to_early_increase_min_db']} dB")

    cap = by.get("s08_map_ambience")
    if cap:
        rows = out[cap["scenario"]]
        amb = [s for s in cap["segments"] if s["kind"] == "ambience"]
        tables["maps"] = amb
        profiles = {s["params"]["map"]: np.array(s.get("octaves") or [0] * 9) for s in amb}
        names = list(profiles)
        sims = []
        for i in range(len(names)):
            for j in range(i + 1, len(names)):
                a = 10 ** (profiles[names[i]] / 10)
                b = 10 ** (profiles[names[j]] / 10)
                sims.append((names[i], names[j], float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b) + EPS))))
        tables["mapSimilarity"] = sims
        game = [s for s in sims if not s[0].startswith("menu") and not s[1].startswith("menu")]
        if game:
            mx = max(v for _, _, v in game)
            check(rows, "ambience differs between maps (max spectral similarity)", mx <= tc["map_ambience_similarity_max"],
                  f"{mx:.4f}", f"<= {tc['map_ambience_similarity_max']}")
        for s in amb:
            p = s["params"]
            check(rows, f"local emitters in {p['map']}", (p.get("audioSources") or 0) > 0,
                  f"{p.get('audioSources')} AudioSource, {p.get('reverbZones')} reverb zones, hints: {', '.join(p.get('emitterHints') or [])[:80]}",
                  ">= 1 local emitter")
            mats = p.get("groundMaterials") or {}
            check(rows, f"footstep materials resolved in {p['map']}", len(mats) > 1, json.dumps(mats), "> 1 material")
            check(rows, f"EnsureAudioZones cost in {p['map']}", (p.get("ensureZonesMsPerCall") or 0) < 0.5,
                  f"{fmt(p.get('ensureZonesMsPerCall'))} ms, {p.get('ensureZonesBytesPerCall')} B per call ({p.get('audioZoneNodes')} zone nodes)",
                  "< 0.5 ms (called per snapshot and per footstep)")
            near = seg_by(cap, "near_fire_" + p["map"])
            if near:
                rise = (near["rmsDbfs"] or 0) - (s["rmsDbfs"] or 0)
                check(rows, f"fire/hearth audible near {p.get('fireNode')} ({p['map']})", rise >= tc["local_emitter_min_rise_db"],
                      f"{rise:+.1f} dB vs map ambience", f">= +{tc['local_emitter_min_rise_db']} dB at 1.5 m")
        for s in cap["segments"]:
            if s["kind"] == "silence" and s["label"].startswith("after_stop_"):
                check(rows, f"residual after StopAll ({s['params'].get('map')})", (s["rmsDbfs"] or -200) <= tc["residual_silence_max_dbfs"],
                      fmt_level(s["rmsDbfs"]), f"<= {tc['residual_silence_max_dbfs']} dBFS")

    cap = by.get("s09_music_flow")
    if cap:
        rows = out[cap["scenario"]]
        for s in cap["segments"]:
            if s["kind"] == "music":
                check(rows, f"music loudness: {s['label']}", None, f"{fmt(s.get('integratedLufs'), ' LUFS')} (TP {fmt(s.get('truePeakDbtp'))})",
                      "consistent across contexts")
            if s["kind"] in ("transition",):
                ok = (s.get("clicks") or 0) <= tc["transition_clicks_max"] and (s.get("hardStops") or 0) <= tc["transition_hard_stops_max"]
                check(rows, f"transition {s['label']}", ok,
                      f"{s.get('clicks')} clicks, {s.get('hardStops')} hard stops, {fmt(s.get('levelBeforeDb'))}→{fmt(s.get('levelAfterDb'))} dB, gaps {s.get('silentGapsOver20ms')}",
                      "0 clicks / 0 hard stops")
            if s["kind"] == "loop_seam":
                ok = (s.get("clicks") or 0) == 0 and (s.get("silentGaps") or 0) == 0 and abs(s.get("levelStepDb") or 0) <= 1.5
                check(rows, "menu music loop seam in engine", ok,
                      f"{s.get('clicks')} clicks, {s.get('silentGaps')} gaps ({fmt(s.get('silentGapMs'))} ms), step {fmt(s.get('levelStepDb'))} dB",
                      "seamless")
            if s["kind"] == "silence" and s["label"] != "noise_floor":
                check(rows, f"residual: {s['label']}", (s["rmsDbfs"] or -200) <= tc["residual_silence_max_dbfs"], fmt_level(s["rmsDbfs"]),
                      f"<= {tc['residual_silence_max_dbfs']} dBFS")
        bed = seg_by(cap, "round_bed")
        for st in ("round_start_sting", "results_humans_win"):
            s = seg_by(cap, st)
            if s and bed:
                d = (s.get("momentaryMaxLufs") or -99) - (bed.get("momentaryMaxLufs") or -99)
                check(rows, f"{st} over round bed", d >= tc["sting_over_bed_min_db"], f"{d:+.1f} LU (momentary max)", f">= +{tc['sting_over_bed_min_db']} LU")

    cap = by.get("s10_ui")
    if cap:
        rows = out[cap["scenario"]]
        for s in cap["segments"]:
            if s["kind"] == "ui":
                check(rows, f"UI {s['label']}", (s.get("truePeakDbtp") or -99) <= tc["master_true_peak_max_dbtp"] and (s.get("clicks") or 0) == 0,
                      f"M max {fmt(s.get('momentaryMaxLufs'))} LUFS, TP {fmt(s.get('truePeakDbtp'))} dBTP, clicks {s.get('clicks')}",
                      "TP <= -1, no clicks")
        b = seg_by(cap, "ui_select_burst_30ms")
        if b:
            check(rows, "UI burst: clicks played / requested (throttle 65 ms)", None,
                  f"{b.get('onsets')} / {b['params'].get('requested')}", "info")
        mus, over = seg_by(cap, "menu_music_only"), seg_by(cap, "ui_over_menu_music")
        if mus and over:
            d = (over.get("momentaryMaxLufs") or -99) - (mus.get("momentaryMaxLufs") or -99)
            check(rows, "UI audible over menu music", d >= tc["ui_over_music_min_db"], f"{d:+.1f} LU", f">= +{tc['ui_over_music_min_db']} LU")

    cap = by.get("s11_voice")
    if cap:
        rows = out[cap["scenario"]]
        ref = seg_by(cap, "reference_nonspatial_waiting_room")
        vt = []
        for s in cap["segments"]:
            if s["kind"] != "voice":
                continue
            p = s["params"]
            vt.append((s["label"], p.get("distance"), p.get("policyGain"), p.get("policyLowPass"), s.get("rmsDbfs"), s.get("lrDb"),
                       s.get("hfAbove3500Db"), s.get("f0Hz"), s.get("continuousFromMs"), s.get("steadyGaps"), s.get("steadyGapMs"),
                       s.get("steadyClicks"), p.get("framesConcealed")))
            if p.get("audible"):
                check(rows, f"voice start latency: {s['label']}", (s.get("continuousFromMs") or 9999) <= tv["start_latency_max_ms"],
                      f"first sound {fmt(s.get('firstSoundMs'), ' ms')}, continuous from {fmt(s.get('continuousFromMs'), ' ms')}, "
                      f"{fmt(s.get('startupSilenceMs'), ' ms')} silent in the first 1 s", f"continuous <= {tv['start_latency_max_ms']} ms")
                check(rows, f"voice steady gaps/clicks: {s['label']}", (s.get("steadyGapMs") or 0) <= tv["dropout_ms_max"] and (s.get("steadyClicks") or 0) <= tv["clicks_max"],
                      f"{s.get('steadyGaps')} gaps ({fmt(s.get('steadyGapMs'), ' ms')}), {s.get('steadyClicks')} clicks in {fmt(s.get('steadySeconds'), ' s')}",
                      "0 gaps, 0 clicks")
        tables["voice"] = vt
        mv = seg_by(cap, "mosquito_circling_3m_5mps")
        if mv:
            check(rows, "moving mosquito voice: pan follows position", (mv.get("panTrackingR") or 0) >= tc["pan_tracking_min_r"],
                  f"r={fmt(mv.get('panTrackingR'))}, f0 spread {fmt(mv.get('f0SpreadCents'))} cents (static mosquito voice: see f0 table)",
                  f"r >= {tc['pan_tracking_min_r']}")
        wa = seg_by(cap, "human_walking_away_2_to_13m")
        if wa and wa.get("levelWindowsDb"):
            w = [v for v in wa["levelWindowsDb"] if v is not None]
            check(rows, "voice fades smoothly walking 2→13 m", None, f"{w[0]:.1f} → {w[-1]:.1f} dB over {len(w)} windows", "info")
        l, r = seg_by(cap, "human_left_3m"), seg_by(cap, "human_right_3m")
        if l and r:
            ok = (l["lrDb"] or 0) >= tv["lr_min_db_at_90"] and -(r["lrDb"] or 0) >= tv["lr_min_db_at_90"]
            check(rows, "voice L/R at ±90° 3 m", ok, f"izq. {fmt_lr(l['lrDb'])} / der. {fmt_lr(r['lrDb'])}", f">= {tv['lr_min_db_at_90']} dB")
        o, occ = seg_by(cap, "human_front_3m"), seg_by(cap, "human_front_3m_wall_occluded")
        if o and occ:
            d = (o["rmsDbfs"] or 0) - (occ["rmsDbfs"] or 0)
            dh = (o["hfAbove3500Db"] or 0) - (occ["hfAbove3500Db"] or 0)
            check(rows, "voice occlusion behind wall", d >= tv["occlusion_min_db"] and dh >= tv["occlusion_extra_hf_cut_min_db"],
                  f"-{d:.1f} dB, HF(>3.5k) -{dh:.1f} dB extra", f">= {tv['occlusion_min_db']} dB and HF cut")
        i = seg_by(cap, "human_front_3m_interior_room")
        if o and i:
            d = (i["rmsDbfs"] or 0) - (o["rmsDbfs"] or 0)
            check(rows, "voice interior vs exterior differs", abs(d) >= 1.0 or abs((i.get("hfAbove3500Db") or 0) - (o.get("hfAbove3500Db") or 0)) >= 1.0,
                  f"{d:+.1f} dB", "audible room character (>= 1 dB or HF change)")
        m = seg_by(cap, "mosquito_to_human_3m")
        if m and o and m.get("f0Hz") and o.get("f0Hz"):
            ratio = m["f0Hz"] / o["f0Hz"]
            lo, hi = tv["mosquito_f0_ratio"]
            check(rows, "mosquito timbre raises pitch", lo <= ratio <= hi, f"f0 {fmt(o['f0Hz'])}→{fmt(m['f0Hz'])} Hz (x{ratio:.2f})", f"x{lo}..x{hi}")
        near = seg_by(cap, "human_front_1m")
        if near and ref:
            d = (near["rmsDbfs"] or 0) - (ref["rmsDbfs"] or 0)
            check(rows, "voice 1 m vs non-spatial reference", None, f"{d:+.1f} dB", "info")
        f3, f10 = seg_by(cap, "human_front_3m"), seg_by(cap, "human_front_10m")
        if f3 and f10:
            check(rows, "voice 3 m → 10 m drop", None, f"{(f3['rmsDbfs'] or 0) - (f10['rmsDbfs'] or 0):.1f} dB", "info (policy smoothstep 4→12 m)")

    cap = by.get("s12_mix_stress")
    if cap:
        rows = out[cap["scenario"]]
        s = seg_by(cap, "dense_close_combat")
        if s:
            check(rows, "dense combat true peak", (s.get("truePeakDbtp") or -99) <= tc["master_true_peak_max_dbtp"], fmt(s.get("truePeakDbtp"), " dBTP"),
                  f"<= {tc['master_true_peak_max_dbtp']} dBTP")
            check(rows, "dense combat voices", (s.get("maxAudibleSources") or 0) <= tc["max_simultaneous_audible_voices"],
                  f"{s.get('maxAudibleSources')} audible / {s.get('maxPlayingSources')} playing / {s.get('maxVirtualSources')} virtual; pool accepted {s['params'].get('accepted')}/{s['params'].get('requested')}",
                  f"<= {tc['max_simultaneous_audible_voices']} audible")
        s = seg_by(cap, "residual_after_stopall")
        if s:
            check(rows, "residual after StopAll", (s["rmsDbfs"] or -200) <= tc["residual_silence_max_dbfs"], fmt_level(s["rmsDbfs"]),
                  f"<= {tc['residual_silence_max_dbfs']} dBFS")
    return out, tables


def cmd_report(args):
    targets = json.loads((HERE / "targets.json").read_text(encoding="utf-8"))
    out = Path(args.out)
    captures = json.loads((Path(args.captures) / "captures.json").read_text(encoding="utf-8")) if args.captures else []
    clips = json.loads((Path(args.clips) / "clips.json").read_text(encoding="utf-8")) if args.clips else []
    checks, tables = evaluate(captures, targets)
    lines = ["# Audio evidence report", ""]
    if captures:
        c0 = captures[0]
        lines += [f"Unity {c0.get('unityVersion')} · capture {c0.get('captureMode')} · {c0.get('sampleRate')} Hz · "
                  f"mixer {c0.get('meta', {}).get('mixerMode')} · commit {c0.get('meta', {}).get('gitCommit')}", ""]
    total = {"PASS": 0, "FAIL": 0, "INFO": 0}
    for scen, rows in checks.items():
        lines += [f"## {scen}", "", "| check | status | measured | target |", "|---|---|---|---|"]
        for name, status, measured, target in rows:
            total[status] += 1
            lines.append(f"| {name} | {status} | {measured} | {target} |")
        lines.append("")
    if "pan" in tables:
        lines += ["## Pan law (L−R dB at 2 m; + = left louder)", "", "| cue | azimuth | L−R | RMS dBFS | HF>4k rel dB |", "|---|---|---|---|---|"]
        for cue, az, lr, level, hf in tables["pan"]:
            lines.append(f"| {cue} | {az:g}° | {fmt_lr(lr)} | {fmt_level(level)} | {fmt(hf)} |")
        lines.append("")
    if "distance" in tables:
        lines += ["## Distance curves (dBFS at listener)", ""]
        dists = sorted({d for pts in tables["distance"].values() for d, _, _ in pts})
        lines.append("| cue | max m | " + " | ".join(f"{d:g} m" for d in dists) + " |")
        lines.append("|---|---|" + "---|" * len(dists))
        for cue, pts in tables["distance"].items():
            m = {d: l for d, l, _ in pts}
            mx = pts[0][2]
            lines.append(f"| {cue} | {mx} | " + " | ".join(fmt_level(m.get(d), "") for d in dists) + " |")
        lines.append("")
    if "voice" in tables:
        lines += ["## Voice chat measurements", "", "| segment | dist m | policy gain | LPF Hz | RMS | L−R | HF>3.5k dB | f0 Hz | continuous from ms | steady gaps | gap ms | steady clicks | concealed |",
                  "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
        for r in tables["voice"]:
            cells = [r[0], fmt(r[1]), fmt(r[2]), fmt(r[3]), fmt_level(r[4], ""), fmt_lr(r[5])] + [fmt(v) for v in r[6:]]
            lines.append("| " + " | ".join(cells) + " |")
        lines.append("")
    if clips:
        cues = {}
        for c in clips:
            for user in c["usedBy"]:
                if user.startswith("cue:") and c.get("momentaryMaxLufs") is not None:
                    cues.setdefault(user[4:], []).append((c["file"], c["momentaryMaxLufs"]))
        lines += ["## Level spread inside each cue (random variant selection)", "",
                  f"Target: <= {targets['asset']['cue_variant_spread_max_db']} dB between variants of one cue (event loudness, momentary max).", "",
                  "| cue | variants (M max LUFS) | spread dB | status |", "|---|---|---|---|"]
        for cue, items in sorted(cues.items(), key=lambda kv: -(max(v for _, v in kv[1]) - min(v for _, v in kv[1]))):
            spread = max(v for _, v in items) - min(v for _, v in items)
            status = "PASS" if spread <= targets["asset"]["cue_variant_spread_max_db"] else "FAIL"
            lines.append(f"| {cue} | " + ", ".join(f"{f} ({v:.1f})" for f, v in items) + f" | {spread:.1f} | {status} |")
        lines.append("")
        lines += ["## Clips", "", "| file | cat | dur s | I LUFS | M max | TP dBTP | clicks | verdict | issues |", "|---|---|---|---|---|---|---|---|---|"]
        for c in clips:
            lines.append(f"| {c['file']} | {c['category']} | {c['duration']} | {fmt(c['integratedLufs'])} | {fmt(c['momentaryMaxLufs'])} | "
                         f"{fmt(c['truePeakDbtp'])} | {c['clicks']} | {c['verdict']} | {'; '.join(c['issues'])} |")
        lines.append("")
    lines.insert(2, f"Checks: {total['PASS']} PASS · {total['FAIL']} FAIL · {total['INFO']} INFO")
    out.mkdir(parents=True, exist_ok=True)
    (out / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (out / "checks.json").write_text(json.dumps({k: [dict(zip(("check", "status", "measured", "target"), r)) for r in v] for k, v in checks.items()},
                                                indent=1, ensure_ascii=False, default=str), encoding="utf-8")
    print(f"report: {total} -> {out / 'summary.md'}")


def cmd_selftest(args):
    """Validates the numpy BS.1770 meter and true-peak against ffmpeg's ebur128 on given files."""
    worst = 0.0
    for f in args.files:
        x, sr, _ = read_audio(f)
        mine = Loudness(x, sr).stats()
        tp, sp = true_peak(x, sr)
        ref = ebur128_ffmpeg(f)
        d_i = (mine["I"] - ref["I"]) if (mine["I"] is not None and ref["I"] is not None) else None
        d_tp = (tp - ref["TP"]) if (tp is not None and ref["TP"] is not None) else None
        worst = max(worst, abs(d_i or 0), abs(d_tp or 0))
        print(f"{Path(f).name:34s} I {rnd(mine['I'])} vs {ref['I']} (d {rnd(d_i)}) | LRA {rnd(mine['LRA'])} vs {ref['LRA']} | "
              f"TP {rnd(tp)} vs {ref['TP']} (d {rnd(d_tp)}) | SP {rnd(sp)} vs {ref['SP']}")
    print("max |delta|", rnd(worst))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("clips")
    a.add_argument("clips_dir")
    a.add_argument("--out", required=True)
    a.add_argument("--unity", help="Unity project dir, to map clips to cues/beds")
    a.add_argument("--spectrograms", action="store_true")
    b = sub.add_parser("captures")
    b.add_argument("captures_dir")
    b.add_argument("--out", required=True)
    b.add_argument("--spectrograms", action="store_true")
    c = sub.add_parser("report")
    c.add_argument("--clips")
    c.add_argument("--captures")
    c.add_argument("--out", required=True)
    d = sub.add_parser("selftest")
    d.add_argument("files", nargs="+")
    args = ap.parse_args(argv)
    {"clips": cmd_clips, "captures": cmd_captures, "report": cmd_report, "selftest": cmd_selftest}[args.cmd](args)


if __name__ == "__main__":
    main()
