"""Summarize already captured framing evidence; never launches Godot."""
import hashlib
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / "outputs/0.9-facial-gallery-fit4-framed"
rows = []
parts = []
proboscis = set()
for path in sorted(OUT.glob("*/*-page-???.json")):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    run = json.loads(path.with_name("run.json").read_text(encoding="utf-8-sig"))
    projections = [part for view in data["captured_views"] for part in view["projected_head_parts"]]
    parts.extend(projections)
    for view in data["captured_views"]:
        proboscis.add((data["role"], sum(part["proboscis_vertices"] for part in view["projected_head_parts"])))
    rows.append({
        "report": str(path.relative_to(ROOT)),
        "report_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "checks": data["checks"], "failures": data["failures"],
        "heads": len(data["records"]), "views": len(data["captured_views"]),
        "elapsed_seconds": run["elapsed_seconds"],
        "distance_range": [min(record["head_framing"]["distance"] for record in data["records"]), max(record["head_framing"]["distance"] for record in data["records"])],
        "minimum_margin_px": min(part["min_margin_px"] for part in projections),
    })
assert len(rows) == 3, "Only the three requested calibration pages must exist"
result = {
    "scope": "Three source-rendered calibration pages; not the full gallery or artistic approval",
    "rows": rows,
    "checks": sum(row["checks"] for row in rows),
    "failures": sum(row["failures"] for row in rows),
    "heads": sum(row["heads"] for row in rows),
    "views": sum(row["views"] for row in rows),
    "elapsed_seconds": sum(row["elapsed_seconds"] for row in rows),
    "minimum_margin_px": min(part["min_margin_px"] for part in parts),
    "maximum_native_bake_error_m": max(part["native_base_max_error_m"] for part in parts),
    "proboscis_vertex_counts_by_role": sorted(proboscis),
    "visual_review_status": "pending",
}
(OUT / "calibration-metrics.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
