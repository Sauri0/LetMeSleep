#!/usr/bin/env python3
"""Deterministically synthesize the original Let me sleep alfa audio set.

No samples, models, or external sound libraries are used. Musical notes,
envelopes, synthesis voices, and the seeded ambience are editable below.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import shutil
import struct
import wave
from array import array
from pathlib import Path


RATE = 48_000
TAU = math.tau
TEMPO = 96
BEAT = 60.0 / TEMPO
BAR = BEAT * 4
ROOT = Path(__file__).resolve().parent

NOTE = {
    "C2": 65.406, "D2": 73.416, "E2": 82.407, "F2": 87.307,
    "G2": 97.999, "A2": 110.000, "Bb2": 116.541,
    "C3": 130.813, "D3": 146.832, "E3": 164.814, "F3": 174.614,
    "G3": 195.998, "A3": 220.000, "Bb3": 233.082, "B3": 246.942,
    "C4": 261.626, "D4": 293.665, "E4": 329.628, "F4": 349.228,
    "G4": 391.995, "A4": 440.000, "Bb4": 466.164,
}

SCORE = {
    "title": "Night Mischief",
    "author": "Let me sleep project contributors",
    "tempo_bpm": TEMPO,
    "meter": "4/4",
    "bars": 8,
    "chords": [
        ["D3", "F3", "A3", "C4"],
        ["G2", "B3", "D4", "F4"],
        ["C3", "E3", "G3", "B3"],
        ["A2", "C3", "E3", "G3"],
    ] * 2,
    "bass": ["D2", "G2", "C2", "A2"] * 2,
    "melody": [
        "A4", "F4", "D4", "E4", "F4", "A4", "G4", "F4",
        "D4", "F4", "G4", "B3", "E4", "D4", "C4", "A3",
        "G4", "E4", "D4", "C4", "E4", "G4", "A4", "G4",
        "E4", "C4", "A3", "C4", "D4", "E4", "G4", "A4",
    ],
}


def buffer(seconds: float, channels: int = 1) -> list[array]:
    frames = int(round(seconds * RATE))
    return [array("f", [0.0]) * frames for _ in range(channels)]


def envelope(t: float, duration: float, attack: float, release: float) -> float:
    attack_gain = min(1.0, t / max(attack, 1e-6))
    release_gain = min(1.0, (duration - t) / max(release, 1e-6))
    return max(0.0, min(attack_gain, release_gain))


def add_voice(
    channels: list[array], start: float, duration: float, frequency: float,
    amplitude: float, pan: float = 0.0, kind: str = "pluck", wrap: bool = False,
) -> None:
    total = len(channels[0])
    start_sample = int(round(start * RATE))
    count = int(round(duration * RATE))
    left_gain = math.sqrt((1.0 - max(-1.0, min(1.0, pan))) * 0.5)
    right_gain = math.sqrt((1.0 + max(-1.0, min(1.0, pan))) * 0.5)
    for n in range(count):
        index = start_sample + n
        if wrap:
            index %= total
        elif index < 0 or index >= total:
            continue
        t = n / RATE
        if kind == "pluck":
            env = envelope(t, duration, 0.003, 0.08) * math.exp(-5.4 * t)
            value = (math.sin(TAU * frequency * t)
                     + 0.34 * math.sin(TAU * frequency * 2.0 * t)
                     + 0.12 * math.sin(TAU * frequency * 3.01 * t)) / 1.46
        elif kind == "bass":
            env = envelope(t, duration, 0.008, 0.08) * math.exp(-2.6 * t)
            value = math.sin(TAU * frequency * t) + 0.18 * math.sin(TAU * frequency * 2 * t)
        elif kind == "reed":
            env = envelope(t, duration, 0.025, 0.10)
            value = (math.sin(TAU * frequency * t)
                     + 0.22 * math.sin(TAU * frequency * 3 * t)
                     + 0.08 * math.sin(TAU * frequency * 5 * t)) / 1.30
        else:
            raise ValueError(f"Unknown voice kind: {kind}")
        sample = amplitude * env * value
        channels[0][index] += sample * left_gain
        if len(channels) > 1:
            channels[1][index] += sample * right_gain


def add_brush(channels: list[array], start: float, amplitude: float, seed: int) -> None:
    rng = random.Random(seed)
    duration = 0.12
    count = int(duration * RATE)
    state = 0.0
    for n in range(count):
        index = (int(start * RATE) + n) % len(channels[0])
        t = n / RATE
        white = rng.uniform(-1.0, 1.0)
        state = 0.78 * state + 0.22 * white
        value = (white - state) * math.exp(-28.0 * t) * amplitude
        channels[0][index] += value * 0.68
        channels[1][index] += value * 0.74


def make_loop_seamless(channels: list[array]) -> None:
    """Remove the PCM value discontinuity without adding an edge slope."""
    count = len(channels[0])
    if count < 4:
        return
    for channel in channels:
        first_step = channel[1] - channel[0]
        target_last = channel[0] - first_step
        value_delta = target_last - channel[-1]
        for index in range(count):
            t = index / (count - 1)
            t2 = t * t
            t3 = t2 * t
            channel[index] += (-2.0 * t3 + 3.0 * t2) * value_delta


def menu_music() -> list[array]:
    channels = buffer(SCORE["bars"] * BAR, 2)
    for bar_index, chord in enumerate(SCORE["chords"]):
        bar_start = bar_index * BAR
        for note_index, note in enumerate(chord):
            add_voice(channels, bar_start + note_index * 0.035, 1.10,
                      NOTE[note], 0.085, -0.42 + note_index * 0.28, "pluck", True)
        for beat_index in range(4):
            bass_note = SCORE["bass"][bar_index]
            fifth = NOTE[bass_note] * 1.5
            frequency = NOTE[bass_note] if beat_index in (0, 2) else fifth
            add_voice(channels, bar_start + beat_index * BEAT, 0.46,
                      frequency, 0.115, -0.18, "bass", True)
            add_brush(channels, bar_start + beat_index * BEAT, 0.020,
                      1100 + bar_index * 4 + beat_index)
    swing = (BEAT * 0.66, BEAT * 0.34)
    cursor = 0.0
    for index, note in enumerate(SCORE["melody"]):
        duration = 0.22 if index % 4 else 0.34
        add_voice(channels, cursor, duration, NOTE[note], 0.072,
                  0.24 + 0.12 * math.sin(index), "reed", True)
        cursor += swing[index % 2]
    make_loop_seamless(channels)
    return channels


def night_ambience() -> list[array]:
    duration = 12.0
    channels = buffer(duration, 2)
    rng = random.Random(94311)
    low_left = low_right = 0.0
    for n in range(len(channels[0])):
        t = n / RATE
        low_left = 0.9982 * low_left + 0.0018 * rng.uniform(-1.0, 1.0)
        low_right = 0.9980 * low_right + 0.0020 * rng.uniform(-1.0, 1.0)
        breath = 0.55 + 0.45 * math.sin(TAU * t / duration)
        channels[0][n] = low_left * 0.18 * breath
        channels[1][n] = low_right * 0.18 * (1.0 - 0.22 * breath)
    chirps = [(1.4, 3120.0, -0.65), (3.9, 2840.0, 0.55),
              (6.2, 3310.0, -0.28), (9.4, 2980.0, 0.70)]
    for start, frequency, pan in chirps:
        for pulse in range(3):
            add_voice(channels, start + pulse * 0.085, 0.055, frequency,
                      0.045, pan, "reed", True)
    make_loop_seamless(channels)
    return channels


def wing_loop() -> list[array]:
    duration = 1.25
    channels = buffer(duration)
    for n in range(len(channels[0])):
        t = n / RATE
        modulation = 1.0 + 0.06 * math.sin(TAU * 8.0 * t)
        channels[0][n] = 0.20 * modulation * (
            math.sin(TAU * 196.0 * t)
            + 0.42 * math.sin(TAU * 392.0 * t)
            + 0.16 * math.sin(TAU * 588.0 * t)
        )
    make_loop_seamless(channels)
    return channels


def strike_swing() -> list[array]:
    duration = 0.42
    channels = buffer(duration)
    rng = random.Random(122)
    low = 0.0
    for n in range(len(channels[0])):
        t = n / RATE
        x = t / duration
        white = rng.uniform(-1.0, 1.0)
        low = 0.92 * low + 0.08 * white
        band = white - low
        env = math.sin(math.pi * x) ** 2
        channels[0][n] = env * (0.24 * band + 0.06 * math.sin(TAU * (180 + 280 * x) * t))
    return channels


def strike_impact() -> list[array]:
    duration = 0.30
    channels = buffer(duration)
    rng = random.Random(410)
    for n in range(len(channels[0])):
        t = n / RATE
        env = math.exp(-18.0 * t)
        body = math.sin(TAU * 118.0 * t) + 0.55 * math.sin(TAU * 231.0 * t)
        channels[0][n] = env * (0.25 * body + 0.10 * rng.uniform(-1.0, 1.0))
    return channels


def bite_start() -> list[array]:
    duration = 0.36
    channels = buffer(duration)
    for n in range(len(channels[0])):
        t = n / RATE
        x = t / duration
        env = math.sin(math.pi * min(1.0, x)) * math.exp(-1.4 * x)
        glide = 420.0 + 150.0 * x
        channels[0][n] = env * 0.22 * (
            math.sin(TAU * glide * t) + 0.33 * math.sin(TAU * glide * 2.03 * t)
        )
    return channels


def ui_ready() -> list[array]:
    channels = buffer(0.42)
    add_voice(channels, 0.0, 0.28, NOTE["D4"], 0.20, kind="pluck")
    add_voice(channels, 0.105, 0.30, NOTE["A4"], 0.17, kind="pluck")
    return channels


def normalize(channels: list[array], peak: float = 0.68) -> None:
    largest = max(max(abs(value) for value in channel) for channel in channels)
    if largest <= 0.0:
        return
    scale = peak / largest
    for channel in channels:
        for index in range(len(channel)):
            channel[index] *= scale


def write_pcm24(path: Path, channels: list[array]) -> None:
    normalize(channels)
    path.parent.mkdir(parents=True, exist_ok=True)
    frame_count = len(channels[0])
    with wave.open(str(path), "wb") as target:
        target.setnchannels(len(channels))
        target.setsampwidth(3)
        target.setframerate(RATE)
        packet = bytearray()
        for frame in range(frame_count):
            for channel in channels:
                value = max(-1.0, min(1.0, channel[frame]))
                encoded = int(round(value * 8_388_607))
                packet.extend(struct.pack("<i", encoded)[:3])
            if len(packet) >= 65_536:
                target.writeframesraw(packet)
                packet.clear()
        if packet:
            target.writeframesraw(packet)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def build(source_directory: Path, runtime_directory: Path) -> None:
    assets = {
        "MUS_NightMischief_Menu.wav": (menu_music, "music", True),
        "AMB_NightHouse.wav": (night_ambience, "ambience", True),
        "SFX_MosquitoWingLoop.wav": (wing_loop, "character", True),
        "SFX_StrikeSwing.wav": (strike_swing, "character", False),
        "SFX_StrikeImpact.wav": (strike_impact, "critical", False),
        "SFX_BiteStart.wav": (bite_start, "critical", False),
        "UI_Ready.wav": (ui_ready, "ui", False),
    }
    source_directory.mkdir(parents=True, exist_ok=True)
    runtime_directory.mkdir(parents=True, exist_ok=True)
    manifest = {
        "schema": "lms.original-audio/1",
        "sample_rate_hz": RATE,
        "sample_width_bits": 24,
        "generator": Path(__file__).name,
        "score": SCORE,
        "assets": [],
    }
    for name, (factory, group, loop) in assets.items():
        source_path = source_directory / name
        write_pcm24(source_path, factory())
        runtime_path = runtime_directory / name
        shutil.copyfile(source_path, runtime_path)
        with wave.open(str(source_path), "rb") as built:
            frames = built.getnframes()
            channels = built.getnchannels()
        manifest["assets"].append({
            "name": name,
            "group": group,
            "loop": loop,
            "channels": channels,
            "duration_seconds": round(frames / RATE, 5),
            "sha256": sha256(source_path),
        })
    (ROOT / "audio_manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )


def verify(source_directory: Path, runtime_directory: Path) -> None:
    manifest = json.loads((ROOT / "audio_manifest.json").read_text(encoding="utf-8"))
    for asset in manifest["assets"]:
        source = source_directory / asset["name"]
        runtime = runtime_directory / asset["name"]
        if sha256(source) != asset["sha256"] or sha256(runtime) != asset["sha256"]:
            raise SystemExit(f"Hash mismatch: {asset['name']}")
        with wave.open(str(runtime), "rb") as clip:
            if clip.getframerate() != RATE or clip.getsampwidth() != 3:
                raise SystemExit(f"Unexpected WAV format: {asset['name']}")
    print(f"AUDIO_OK assets={len(manifest['assets'])}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--runtime-directory", type=Path,
                        default=ROOT.parents[2] / "unity" / "Assets" / "LetMeSleep" / "Audio" / "Clips")
    args = parser.parse_args()
    source_directory = ROOT / "generated"
    if args.verify:
        verify(source_directory, args.runtime_directory)
    else:
        build(source_directory, args.runtime_directory)
        verify(source_directory, args.runtime_directory)


if __name__ == "__main__":
    main()
