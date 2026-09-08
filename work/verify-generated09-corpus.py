"""Verify complete batches against current source and independent repeat runs."""
from pathlib import Path
import hashlib
import json

root = Path(__file__).resolve().parent.parent
folder = root / "outputs/0.9-generated-house/corpus"
cases = []
errors = []
expected_seeds = [i + 1 if i < 500 else ((i - 499) * 15485863) % 2147483646 + 1 for i in range(1000)]
for batch in range(10):
    versions = []
    for suffix in ("", "-repeat"):
        path = folder / f"batch-{batch:02d}{suffix}.json"
        if not path.exists():
            errors.append(f"Missing {path.name}")
            continue
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if data["count"] != 100 or data["failures"]:
            errors.append(f"Incomplete or failed {path.name}")
        if not data.get("source_hashes"):
            errors.append(f"No source provenance in {path.name}")
        for name, expected in data.get("source_hashes", {}).items():
            current = hashlib.sha256((root / "game/scripts" / (name + ".gd")).read_bytes()).hexdigest()
            if current.lower() != expected.lower():
                errors.append(f"Stale {name} in {path.name}")
        seeds = [entry["seed"] for entry in data["cases"]]
        if seeds != expected_seeds[batch * 100 : (batch + 1) * 100]:
            errors.append(f"Wrong seed coverage in {path.name}")
        versions.append(data)
    if len(versions) == 2:
        comparable = lambda rows: [{key: row[key] for key in ("seed", "fingerprint", "rooms", "floors", "errors")} for row in rows]
        if comparable(versions[0]["cases"]) != comparable(versions[1]["cases"]):
            errors.append(f"Independent-process mismatch in batch {batch}")
        cases.extend(versions[1]["cases"])
if len(cases) != 1000 or len({entry["fingerprint"] for entry in cases}) != 1000:
    errors.append("Need 1000 distinct verified house fingerprints")
if len({entry.get("layout_signature") for entry in cases}) != 1000 or any(not entry.get("layout_signature") for entry in cases):
    errors.append("Need 1000 distinct physical layouts excluding seed and cosmetics")
report = {"cases": len(cases), "independent_process_repeats": len(cases),
          "failures": errors, "floors": {str(n): sum(c["floors"] == n for c in cases) for n in (2, 3)},
          "minimum_rooms": min((c["rooms"] for c in cases), default=0),
          "scope": "Generated geometry and deterministic fingerprints; not visual/performance/WAN approval."}
(folder / "summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))
raise SystemExit(bool(errors))
