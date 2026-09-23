#!/usr/bin/env python3
"""Voice chat evidence report (v0.3.0 voice front).

Reads the per-segment metrics that tools/audio_evidence/lms_audio_evidence.py wrote for a run
(<run>/analysis/captures.json) and checks the V01 voice scenario (and S11 if present) against the
voice targets: start latency, dropouts, clicks, distance law and cut, direction, occlusion,
interior/exterior, loss/jitter, push-to-talk transitions, leaving the room, mosquito timbre.

Usage:  python tools/voice_evidence/voice_report.py <run dir> [--baseline <run dir>]
Writes: <run>/analysis/voice_summary.md and <run>/analysis/voice_checks.json
"""
import argparse
import json
import math
from pathlib import Path

TARGETS = {
    "start_latency_max_ms": 200.0,
    "steady_gap_ms_max": 0.0,
    "clicks_max": 0,
    "distance_drop_1_to_10m_min_db": 14.0,
    "silence_beyond_cut_max_dbfs": -100.0,
    "lr_at_90_db": [9.0, 26.0],
    "behind_hf_cut_min_db": 2.0,
    "wall_min_db": 6.0,
    "wall_extra_hf_cut_min_db": 6.0,
    "interior_late_to_early_increase_min_db": 10.0,
    "interior_edt_increase_min_ms": 150.0,
    "mosquito_f0_ratio": [1.2, 1.4],
    "residual_max_dbfs": -70.0,
    "true_peak_max_dbtp": -1.0,
    "pan_tracking_min_r": 0.8,
}


def load(run):
    path = Path(run) / "analysis" / "captures.json"
    return {c["scenario"]: c for c in json.loads(path.read_text(encoding="utf-8"))}


def seg(capture, label):
    for s in capture["segments"]:
        if s["label"] == label:
            return s
    return None


def f(v, d=1, unit=""):
    if v is None:
        return "n/a"
    if isinstance(v, float) and (math.isnan(v) or math.isinf(v)):
        return "n/a"
    return f"{v:.{d}f}{unit}" if isinstance(v, (int, float)) else str(v)


class Report:
    def __init__(self):
        self.rows, self.tables = [], []

    def check(self, name, ok, measured, target):
        status = "INFO" if ok is None else ("PASS" if ok else "FAIL")
        self.rows.append({"check": name, "status": status, "measured": measured, "target": target})

    def table(self, title, header, rows):
        self.tables.append((title, header, rows))


def voice_segments(capture):
    return [s for s in capture["segments"] if s["kind"] == "voice"]


def evaluate_v01(c, r):
    r.check("master true peak", c["truePeakDbtp"] <= TARGETS["true_peak_max_dbtp"], f"{f(c['truePeakDbtp'])} dBTP", "<= -1 dBTP")
    r.check("clipped samples", c["clippedSamples"] == 0, str(c["clippedSamples"]), "0")

    # Continuity and latency of every audible talk with a startup window.
    rows = []
    for s in voice_segments(c):
        p = s.get("params", {})
        if p.get("startupWindow", 0) and p.get("audible") and (s.get("rmsDbfs") or -200) > -90:
            ok_lat = (s.get("continuousFromMs") or 9999) <= TARGETS["start_latency_max_ms"]
            ok_gap = (s.get("steadyGapMs") or 0) <= TARGETS["steady_gap_ms_max"] and (s.get("steadyClicks") or 0) <= TARGETS["clicks_max"]
            r.check(f"continuity: {s['label']}", ok_lat and ok_gap,
                    f"continuous from {f(s.get('continuousFromMs'), 0)} ms, {s.get('steadyGaps')} gaps ({f(s.get('steadyGapMs'))} ms), {s.get('steadyClicks')} clicks",
                    "<= 200 ms, 0 gaps, 0 clicks")
        rows.append([s["label"], f(p.get("distance"), 2), f(p.get("azimuth"), 0), f(20 * math.log10(max(1e-9, p.get("policyGain") or 0)), 1),
                     f(p.get("occlusion"), 2), f(p.get("interior"), 2), f(p.get("reverbSend"), 3), f(p.get("policyLowPass"), 0),
                     f(s.get("rmsDbfs")), f(s.get("lrDb")), f(s.get("hfAbove3500Db")), f(s.get("f0Hz")),
                     f(s.get("continuousFromMs"), 0), str(s.get("steadyGaps")), f(s.get("steadyGapMs")), str(s.get("clicks")),
                     str(p.get("starvations")), f"{p.get('framesLost')}/{p.get('framesSent')}"])
    r.table("Voice segments (V01, production path)",
            ["segment", "dist m", "az°", "gain dB", "occl", "interior", "send", "LPF Hz", "RMS dBFS", "L−R dB", "HF>3.5k", "f0 Hz",
             "continuous ms", "gaps", "gap ms", "clicks", "underruns", "lost/sent"], rows)

    # Distance law.
    dist = {}
    for d in ["1", "2", "3", "5", "8", "10", "12", "15", "20"]:
        s = seg(c, f"human_front_{d}m")
        if s:
            dist[float(d)] = s["rmsDbfs"]
    if dist:
        ds = sorted(dist)
        audible = [d for d in ds if d < 12]
        mono = all(dist[audible[i]] >= dist[audible[i + 1]] - .5 for i in range(len(audible) - 1))
        r.check("distance: monotonic 1→10 m", mono, " → ".join(f"{d:g} m {dist[d]:.1f}" for d in audible), "non-increasing (±0.5 dB)")
        if 1.0 in dist and 10.0 in dist:
            drop = dist[1.0] - dist[10.0]
            r.check("distance: 1→10 m drop", drop >= TARGETS["distance_drop_1_to_10m_min_db"], f"{drop:.1f} dB", ">= 14 dB")
        beyond = [d for d in ds if d >= 12]
        r.check("distance: zero voice at/after the 12 m cut", all(dist[d] <= TARGETS["silence_beyond_cut_max_dbfs"] for d in beyond),
                ", ".join(f"{d:g} m {dist[d]:.0f} dBFS" for d in beyond), "<= -100 dBFS (digital silence)")
        ref = dist.get(2.0)
        r.table("Distance law (human → human, straight ahead)", ["distance m", "RMS dBFS", "re 2 m dB"],
                [[f"{d:g}", f(dist[d]), f(dist[d] - ref) if ref is not None else "n/a"] for d in ds])

    # Mosquito.
    ear = seg(c, "mosquito_at_right_ear_0.3m")
    m1 = seg(c, "mosquito_front_1m")
    m9 = seg(c, "mosquito_front_9m")
    href = seg(c, "human_front_3m_reference") or seg(c, "human_front_3m")
    m3 = seg(c, "mosquito_front_3m")
    if ear and m1:
        r.check("mosquito at the ear sounds close", ear["rmsDbfs"] > m1["rmsDbfs"] + 1.5 and abs(ear["lrDb"]) >= 9,
                f"ear {f(ear['rmsDbfs'])} dBFS (L−R {f(ear['lrDb'])}) vs 1 m {f(m1['rmsDbfs'])}", "louder than 1 m and clearly to one side")
    if m9:
        r.check("mosquito → human: zero voice at 9 m (cut 8 m)", m9["rmsDbfs"] <= TARGETS["silence_beyond_cut_max_dbfs"], f"{f(m9['rmsDbfs'])} dBFS", "<= -100 dBFS")
    if m3 and href and m3.get("f0Hz") and href.get("f0Hz"):
        ratio = m3["f0Hz"] / href["f0Hz"]
        lo, hi = TARGETS["mosquito_f0_ratio"]
        r.check("mosquito timbre raises pitch", lo <= ratio <= hi, f"f0 {href['f0Hz']:.1f} → {m3['f0Hz']:.1f} Hz (x{ratio:.2f})", "x1.2..x1.4")
        r.check("mosquito timbre keeps level (3 m, same distance law)", abs(m3["rmsDbfs"] - href["rmsDbfs"]) <= 2.0,
                f"{m3['rmsDbfs'] - href['rmsDbfs']:+.1f} dB vs human", "±2 dB (codec + timbre)")
    above = seg(c, "mosquito_above_2m")
    if above:
        r.check("mosquito above the head", None, f"RMS {f(above['rmsDbfs'])}, HF {f(above['hfAbove3500Db'])} dB, elevation {f(above['params'].get('elevation'), 2)}", "info (brighter)")

    # Direction.
    left, right, behind, front = (seg(c, "human_left_3m"), seg(c, "human_right_3m"), seg(c, "human_behind_3m"),
                                  seg(c, "human_front_3m_reference"))
    if left and right:
        lo, hi = TARGETS["lr_at_90_db"]
        ok = lo <= left["lrDb"] <= hi and -hi <= right["lrDb"] <= -lo and not left["oneChannelSilent"] and not right["oneChannelSilent"]
        r.check("L/R at ±90° 3 m", ok, f"left {f(left['lrDb'])} dB / right {f(right['lrDb'])} dB", "9..26 dB, far ear never silent")
    if behind and front:
        hf = (front["hfAbove3500Db"] or 0) - (behind["hfAbove3500Db"] or 0)
        lvl = front["rmsDbfs"] - behind["rmsDbfs"]
        r.check("front vs behind", hf >= TARGETS["behind_hf_cut_min_db"] or lvl >= 1.5, f"behind: {-lvl:+.1f} dB level, {-hf:+.1f} dB HF>3.5k", ">= 2 dB HF cut or >= 1.5 dB level")

    # Occlusion.
    open_, wall, door_open, door_closed, two = (seg(c, "human_4m_open"), seg(c, "human_4m_wall"), seg(c, "human_4m_door_open"),
                                                seg(c, "human_4m_door_closed"), seg(c, "human_6m_two_walls"))
    if open_ and wall:
        d = open_["rmsDbfs"] - wall["rmsDbfs"]
        dh = (open_["hfAbove3500Db"] or 0) - (wall["hfAbove3500Db"] or 0)
        r.check("wall between (4 m)", d >= TARGETS["wall_min_db"] and dh >= TARGETS["wall_extra_hf_cut_min_db"], f"-{d:.1f} dB, HF>3.5k -{dh:.1f} dB extra", ">= 6 dB and >= 6 dB extra HF cut")
    if open_ and wall and door_open:
        ok = wall["rmsDbfs"] + .5 < door_open["rmsDbfs"] < open_["rmsDbfs"] + .1
        r.check("open doorway is intermediate", ok, f"open {f(open_['rmsDbfs'])} > door open {f(door_open['rmsDbfs'])} > wall {f(wall['rmsDbfs'])}", "between open and wall")
    if wall and door_closed:
        r.check("closed door ≈ wall", abs(door_closed["rmsDbfs"] - wall["rmsDbfs"]) <= 2.0, f"{door_closed['rmsDbfs'] - wall['rmsDbfs']:+.1f} dB vs wall", "±2 dB")
    if wall and two and open_:
        r.check("two walls muffle more", None, f"two walls at 6 m {f(two['rmsDbfs'])} dBFS, HF {f(two['hfAbove3500Db'])} dB", "info")
    rows = []
    for label in ["human_4m_open", "human_4m_door_open", "human_4m_door_closed", "human_4m_wall", "human_6m_two_walls"]:
        s = seg(c, label)
        if s:
            rows.append([label, f(s["params"].get("occlusion"), 2), f(s["params"].get("walls"), 0), f(s["params"].get("policyLowPass"), 0),
                         f(s["rmsDbfs"]), f(s["hfAbove3500Db"])])
    r.table("Occlusion", ["segment", "blocked", "walls", "LPF Hz", "RMS dBFS", "HF>3.5k dB"], rows)

    # Rooms.
    out_b, small_b, hall_b = seg(c, "burst_3m_exterior"), seg(c, "burst_3m_interior_small_room"), seg(c, "burst_3m_interior_hall")
    rows = []
    for s in [out_b, small_b, hall_b]:
        if s:
            rows.append([s["label"], f(s["params"].get("interior"), 2), f(s["params"].get("decay"), 2), f(s["params"].get("reverbSend"), 3),
                         f(s.get("edtMs"), 0), f(s.get("t20Ms"), 0), f(s.get("lateToEarlyDb"))])
    r.table("Interior / exterior (60 ms burst through the voice path)", ["segment", "interior", "decay s", "send", "EDT ms", "T20 ms", "late/early dB"], rows)
    if out_b and small_b:
        dl = (small_b.get("lateToEarlyDb") or -99) - (out_b.get("lateToEarlyDb") or -99)
        de = (small_b.get("edtMs") or 0) - (out_b.get("edtMs") or 0)
        r.check("interior vs exterior (small room)", dl >= TARGETS["interior_late_to_early_increase_min_db"] or de >= TARGETS["interior_edt_increase_min_ms"],
                f"late/early {dl:+.1f} dB, EDT {de:+.0f} ms", ">= +10 dB tail or +150 ms EDT")
    if small_b and hall_b:
        r.check("hall rings longer than a small room", (hall_b.get("edtMs") or 0) > (small_b.get("edtMs") or 0),
                f"EDT {f(small_b.get('edtMs'), 0)} → {f(hall_b.get('edtMs'), 0)} ms", "hall > small room")

    # Push-to-talk and leaving the room.
    for label in ["ptt_presses_x5", "leave_room_while_talking"]:
        s = seg(c, label)
        if s:
            r.check(f"transition clicks/hard stops: {label}", s.get("clicks", 1) <= 0 and s.get("hardStops", 1) <= 0,
                    f"{s.get('clicks')} clicks, {s.get('hardStops')} hard stops", "0 / 0")
    res = seg(c, "residual_after_leave")
    if res:
        r.check("no residual voice after leaving", res["rmsDbfs"] <= TARGETS["residual_max_dbfs"], f"{f(res['rmsDbfs'])} dBFS", "<= -70 dBFS")

    # Movement.
    walk = seg(c, "human_walking_away_2_to_14m")
    if walk and walk.get("levelWindowsDb"):
        w = walk["levelWindowsDb"]
        rises = [w[i + 1] - w[i] for i in range(len(w) - 1) if w[i] > -100]
        r.check("walking away 2→14 m fades smoothly to zero", max(rises or [0]) <= 4.5 and w[-1] <= -100,
                f"{w[0]:.1f} → {w[-1]:.1f} dB over {len(w)} windows, max rise {max(rises or [0]):+.1f} dB",
                "no rise > 4.5 dB between 250 ms windows (the test voice has 4.5 Hz syllables), silent after the cut")
    circle = seg(c, "mosquito_circling_head_1m")
    if circle and circle.get("panTrackingR") is not None:
        r.check("circling mosquito: pan follows position", circle["panTrackingR"] >= TARGETS["pan_tracking_min_r"],
                f"r={f(circle['panTrackingR'], 2)}, f0 spread {f(circle.get('f0SpreadCents'))} cents", ">= 0.8")


def evaluate_s11(c, r):
    gaps = [s for s in voice_segments(c) if s.get("params", {}).get("audible") and s.get("params", {}).get("startupWindow")]
    worst_lat = max((s.get("continuousFromMs") or 9999) for s in gaps) if gaps else None
    total_gaps = sum(s.get("steadyGaps") or 0 for s in gaps)
    r.check("S11 (legacy harness scenario): worst start latency", worst_lat is not None and worst_lat <= 200,
            f"{f(worst_lat, 0)} ms over {len(gaps)} talks", "<= 200 ms")
    r.check("S11: steady dropouts", total_gaps == 0, f"{total_gaps} gaps", "0")


def write(run, r, baseline=None):
    out = Path(run) / "analysis"
    lines = ["# Voice evidence report", "", f"Run: `{run}`" + (f" · baseline: `{baseline}`" if baseline else ""), ""]
    counts = {k: sum(1 for x in r.rows if x["status"] == k) for k in ("PASS", "FAIL", "INFO")}
    lines += [f"PASS {counts['PASS']} · FAIL {counts['FAIL']} · INFO {counts['INFO']}", "", "| check | status | measured | target |", "|---|---|---|---|"]
    lines += [f"| {x['check']} | {x['status']} | {x['measured']} | {x['target']} |" for x in r.rows]
    for title, header, rows in r.tables:
        lines += ["", f"## {title}", "", "| " + " | ".join(header) + " |", "|" + "---|" * len(header)]
        lines += ["| " + " | ".join(row) + " |" for row in rows]
    (out / "voice_summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (out / "voice_checks.json").write_text(json.dumps(r.rows, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"voice report: {counts} -> {out / 'voice_summary.md'}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run")
    ap.add_argument("--baseline")
    a = ap.parse_args()
    caps = load(a.run)
    r = Report()
    if "v01_voice_environment" in caps:
        evaluate_v01(caps["v01_voice_environment"], r)
    if "s11_voice" in caps:
        evaluate_s11(caps["s11_voice"], r)
    write(a.run, r, a.baseline)


if __name__ == "__main__":
    main()
