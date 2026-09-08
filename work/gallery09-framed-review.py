"""Record the completed agent framing inspection; never approves model artwork."""
import hashlib
import json
import pathlib
import shutil

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / "outputs/0.9-facial-gallery-fit4-framed"
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
files = sorted(OUT.glob("*/*.png"))
assert len(files) == 12
rows = []
for path in files:
    role = "human" if path.name.startswith("human") else "mosquito"
    rows.append({
        "file": path.relative_to(OUT).as_posix(), "sha256": digest(path),
        "reviewer": "agent /root/ui", "review_scope": "full-sheet framing inspection",
        "displayed_pixels": [1682, 1472], "original_pixels": [3840, 3360],
        "framing_result": "complete_head_observed",
        "finding": "Head remains fully framed across the displayed pair; hair and facial pieces are legible." if role == "human" else "Head, antenna tips and proboscis remain fully framed across the displayed pair; wings/body cropping is outside the agreed scope.",
        "artistic_approval": False, "motion_quality_reviewed": False,
    })
ledger = {
    "schema": 1, "scope": "Only the 12 inspected calibration sheets, not the full neutral catalog",
    "framing_review_status": "completed", "full_gallery_review_status": "pending",
    "artistic_approval": False, "sheets": rows,
}
(OUT / "review-framing.json").write_text(json.dumps(ledger, indent=2) + "\n", encoding="utf-8")
for name in ["gallery09-fit4-framed-plan.json", "gallery09-fit4-framed-calibration-result.json", "gallery09-validator-tests.json"]:
    shutil.copy2(ROOT / "work" / name, OUT / name)
manifest = {"schema": 1, "scope": "Calibration artifacts and their exact bytes", "files": [
    {"file": path.relative_to(OUT).as_posix(), "sha256": digest(path), "bytes": path.stat().st_size}
    for path in sorted(OUT.rglob("*")) if path.is_file() and path.name != "manifest.json"
]}
(OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"sheets_reviewed_for_framing": len(rows), "manifest_files": len(manifest["files"]), "full_gallery_review_status": "pending"}))
