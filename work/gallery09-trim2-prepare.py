"""Prepare the new trim2 gallery identity without launching Godot."""
import importlib.util
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("gallery09_manifest", ROOT / "work/gallery09-manifest.py")
gallery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gallery)
plan_path = ROOT / "work/gallery09-trim2-plan.json"
output = ROOT / "outputs/0.9-facial-gallery-trim2"
assert not plan_path.exists(), "Preserve the existing plan; do not overwrite it"
plan = gallery.build_plan(output, ROOT / "work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe")
expected_human = "90cd6a695b32c3b98395df8f8da496ee56ffebd6026db757c03f9006eb777a06"
expected_camera = "3e159a6e3a30ff93c7240ee6f602686fdd354631d1df1925c9e6230f8c0fe242"
assert plan["source_sha256"]["game/assets/art/characters/human/human_lms06.glb"] == expected_human, "Unexpected human trim2 model"
assert plan["source_sha256"]["game/tests/facial_catalog08_gallery.gd"] == expected_camera, "Calibrated framing fixture changed"
presentation = "game/scripts/human_presentation.gd"
assert presentation in plan["source_sha256"], "Literal dependency crawl omitted HumanPresentation"
assert plan["source_sha256"][presentation] == gallery.digest(ROOT / presentation)
for relative in ["work/gallery09-trim2-prepare.py", "work/gallery09-trim2-batch.ps1"]:
    plan["source_sha256"][relative] = gallery.digest(ROOT / relative)
plan["preparation_identity"] = {
    "runtime_checkpoint": "55b4e5c",
    "repository_head_at_preparation": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
    "character_revision": "trim2",
    "head_framing_report_schema": 4,
    "calibrated_camera_checkpoint": "9afd45a",
    "human_presentation_sha256": plan["source_sha256"][presentation],
}
plan["batch_policy"] = {"jobs_per_batch": 12, "batches": 12, "pause_after_each_batch": True, "automatic_next_batch": False, "first_job_review_before_continuing": True}
assert len(plan["jobs"]) == 144 and plan["neutral_pages"] == 133 and plan["witness_jobs"] == 11
assert sum(job["expected_tiles"] for job in plan["jobs"]) == 25568
assert sum(job["expected_sheets"] for job in plan["jobs"]) == 680
gallery.write(plan_path, plan)
gallery.write(output / "gallery09-trim2-plan.json", plan)
print(f"TRIM2_PREPARED: {len(plan['source_sha256'])} hashed inputs; 133 neutral pages + 11 witness jobs; 12 explicit batches. No Godot launched.")
