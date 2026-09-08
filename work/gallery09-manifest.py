"""Offline planning and strict capture validation. Never launches Godot.

Captured and visually reviewed are independent states. Previous six-angle,
blank-frame or different-hash evidence cannot satisfy this eight-angle plan.
"""
import argparse
import hashlib
import json
import math
import pathlib
import re
import struct
import sys
import zlib
from datetime import datetime, timezone

ROOT = pathlib.Path(__file__).resolve().parent.parent
VIEWS = ["front", "quarter-left", "profile-left", "profile-right", "quarter-right", "back", "above-quarter", "below-quarter"]
COUNTS = {"human": 2916, "mosquito": 243}
YAWS = [0, -math.pi / 4, -math.pi / 2, math.pi / 2, math.pi / 4, math.pi, -math.pi / 4, math.pi / 4]
PITCHES = [-.035] * 6 + [-.60, .50]
CHANNELS = {"BlinkL", "BlinkR", "BrowUp", "BrowDown", "CheekLift", "GazeX", "GazeY", "MouthOpen", "MouthPress", "MouthSmile"}
WITNESSES = [
    ("human-brow-hair", "human", 18, "neutral,brow-up,blink-half,blink"),
    ("human-eyes-1", "human", 1, "neutral,blink-half,blink"),
    ("human-eyes-2", "human", 2, "neutral,blink-half,blink"),
    ("human-mustache-mouth", "human", 324, "neutral,smile"),
    ("human-beard-jaw", "human", 1944, "neutral,mouth-open"),
    ("mosquito-goggles-mouth", "mosquito", 162, "neutral,mouth-open-smile"),
    ("mosquito-eyes-1", "mosquito", 1, "neutral,blink-half,blink"),
    ("mosquito-eyes-2", "mosquito", 2, "neutral,blink-half,blink"),
    ("mosquito-brows-0", "mosquito", 0, "neutral,brow-up,brow-down,brow-up-blink,brow-down-blink"),
    ("mosquito-brows-1", "mosquito", 9, "neutral,brow-up,brow-down,brow-up-blink,brow-down-blink"),
    ("mosquito-brows-2", "mosquito", 18, "neutral,brow-up,brow-down,brow-up-blink,brow-down-blink"),
]


def digest(path):
    with open(path, "rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def md5(path):
    # Godot's import freshness format uses MD5. Evidence identity uses SHA-256.
    with open(path, "rb") as source:
        return hashlib.file_digest(source, "md5").hexdigest()


def model_imports():
    links = []
    for role in COUNTS:
        source = ROOT / f"game/assets/art/characters/{role}/{role}_lms06.glb"
        sidecar = pathlib.Path(str(source) + ".import")
        match = re.search(r'^path="(res://[^"\n]+)"', sidecar.read_text(encoding="utf-8-sig"), re.MULTILINE)
        if match is None:
            raise ValueError("Missing model import remap: " + role)
        destination = ROOT / "game" / match[1].removeprefix("res://")
        links.append({"source": source.relative_to(ROOT).as_posix(), "destination": destination.relative_to(ROOT).as_posix(), "import_md5": destination.with_suffix(".md5").relative_to(ROOT).as_posix()})
    return links


def verify_import_link(link):
    metadata = (ROOT / link["import_md5"]).read_text(encoding="utf-8-sig")
    source = re.search(r'source_md5="([0-9a-f]{32})"', metadata)
    destination = re.search(r'dest_md5="([0-9a-f]{32})"', metadata)
    assert source and source[1] == md5(ROOT / link["source"]), "Model GLB has not been imported at its current bytes"
    assert destination and destination[1] == md5(ROOT / link["destination"]), "Imported model cache differs from Godot import metadata"


def png_dimensions(path):
    """Check complete PNG chunk framing/CRCs, without treating pixels as approval."""
    with pathlib.Path(path).open("rb") as source:
        assert source.read(8) == b"\x89PNG\r\n\x1a\n", "Not a PNG"
        size = None
        data_seen = False
        while True:
            header = source.read(8)
            assert len(header) == 8, "Truncated PNG: missing chunk or IEND"
            length, kind = struct.unpack(">I4s", header)
            assert length <= 64 * 1024 * 1024, "PNG chunk exceeds capture bound"
            payload = source.read(length)
            checksum = source.read(4)
            assert len(payload) == length and len(checksum) == 4, "Truncated PNG chunk"
            assert zlib.crc32(kind + payload) == struct.unpack(">I", checksum)[0], "PNG chunk CRC mismatch"
            if size is None:
                assert kind == b"IHDR" and length == 13, "PNG must start with IHDR"
                size = struct.unpack(">II", payload[:8])
                assert payload[8:] == bytes([8, 6, 0, 0, 0]), "Expected Godot RGBA8 non-interlaced capture"
            elif kind == b"IHDR":
                raise AssertionError("Duplicate PNG header")
            if kind == b"IDAT":
                data_seen = data_seen or length > 0
            if kind == b"IEND":
                assert length == 0 and data_seen, "PNG has no image data"
                assert source.read(1) == b"", "Data after PNG IEND"
                return size


def head_keys(role):
    return ["eyes", "mouth", "brows", "hair", "accessory"] + (["mustache", "beard"] if role == "human" else [])


def appearance_for(role, ordinal):
    data = {"outfit": 0, "footwear": 0, "color": 1, "accent": 4}
    if role == "human":
        data["hair_color"] = 0
    for key in head_keys(role):
        count = 4 if key == "accessory" and role == "human" else 3
        data[key] = ordinal % count
        ordinal //= count
    return data


def report_sources(role):
    return [f"res://assets/art/characters/{role}/{role}_lms06.glb", "res://assets/art/characters/shared/character_skin.gd", "res://scripts/cosmetics.gd", "res://scripts/avatar_preview.gd"]


def read(path):
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8-sig"))


def write(path, value):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def dependencies():
    # Resolve literal script/shader/resource dependencies. Dynamic character
    # paths are covered by the whole authored character directory below.
    found = {ROOT / "game/project.godot", ROOT / "work/gallery09-manifest.py", ROOT / "work/gallery09-run.ps1"}
    queue = [ROOT / "game/tests/facial_catalog08_gallery.gd"]
    queue += list((ROOT / "game/assets/art/characters").rglob("*"))
    while queue:
        path = queue.pop()
        if not path.is_file() or path in found or path.suffix == ".uid":
            continue
        found.add(path)
        if path.suffix in {".gd", ".gdshader", ".tscn", ".tres", ".import"}:
            content = path.read_text(encoding="utf-8-sig")
            for match in re.findall(r'["\'](res://[^"\'\n]+)["\']', content):
                target = ROOT / "game" / match.removeprefix("res://")
                if "%" not in match and target.is_file():
                    queue.append(target)
        sidecar = pathlib.Path(str(path) + ".import")
        if sidecar.is_file():
            queue.append(sidecar)
        if ".godot/imported/" in path.as_posix() and path.with_suffix(".md5").is_file():
            queue.append(path.with_suffix(".md5"))
    return {path.relative_to(ROOT).as_posix(): digest(path) for path in sorted(found)}


def build_plan(output, exe):
    imports = model_imports()
    for link in imports:
        verify_import_link(link)
    jobs = []
    for role, count in COUNTS.items():
        for page in range(math.ceil(count / 24)):
            jobs.append({"id": f"neutral-{role}-{page:03}", "kind": "catalog", "role": role, "page": page, "head": None, "states": ["neutral"], "first": page * 24, "last": min(page * 24 + 23, count - 1)})
    for name, role, head, states in WITNESSES:
        jobs.append({"id": "witness-" + name, "kind": "witness", "role": role, "page": head // 24, "head": head, "states": states.split(","), "first": head, "last": head})
    for index, job in enumerate(jobs):
        job["index"] = index
        job["output"] = str(pathlib.Path(output).resolve() / job["id"])
        job["expected_report"] = f"{job['role']}-page-{job['page']:03}.json"
        job["expected_sheets"] = 4 * len(job["states"])
        job["expected_tiles"] = (job["last"] - job["first"] + 1) * len(job["states"]) * 8
    return {"schema": 1, "created_utc": datetime.now(timezone.utc).isoformat(), "status": "prepared_not_executed", "execution_mode": "source_with_Godot_not_exported_game", "root": str(ROOT), "exe": str(exe), "exe_sha256": digest(exe), "source_sha256": dependencies(), "model_imports": imports, "page_timeout_seconds": 55, "views": VIEWS, "tile_pixels": [480, 560], "head_counts": COUNTS, "neutral_pages": 133, "neutral_tiles": 3159 * 8, "neutral_sheets": 133 * 4, "witness_jobs": len(WITNESSES), "jobs": jobs, "capture_status": "pending", "visual_review_status": "pending", "review_rule": "Review must name the exact sheet hash and findings. A valid capture never marks visual approval. Witness states supplement, not replace, the exhaustive neutral catalog."}


def verify_inputs(plan):
    if digest(plan["exe"]) != plan["exe_sha256"]:
        raise ValueError("Executable changed; create a new plan and output directory.")
    for relative, expected in plan["source_sha256"].items():
        if digest(ROOT / relative) != expected:
            raise ValueError("Source/import changed: " + relative)
    for link in plan.get("model_imports", []):
        verify_import_link(link)


def verify_page(plan, job):
    folder = pathlib.Path(job["output"])
    report_path = folder / job["expected_report"]
    report = read(report_path)
    assert report["schema"] == 4 and report["views"] == VIEWS, "Exact eight-angle schema with projected head framing required"
    assert report["tile_pixels"] == [480, 560], "Native tile dimensions changed"
    assert len(report["camera_yaws"]) == 8 and all(math.isclose(a, b, abs_tol=1e-9) for a, b in zip(report["camera_yaws"], YAWS)), "Wrong camera yaw contract"
    assert len(report["camera_pitches"]) == 8 and all(math.isclose(a, b, abs_tol=1e-9) for a, b in zip(report["camera_pitches"], PITCHES)), "Wrong camera pitch contract"
    assert report["role"] == job["role"] and report["page"] == job["page"], "Wrong page identity"
    assert report["states"] == job["states"], "Wrong expression states"
    assert report["head_count"] == COUNTS[job["role"]] and report["page_size"] == 24, "Changed domain"
    assert report["head_keys"] == head_keys(job["role"]), "Changed mixed-radix category order"
    count = job["last"] - job["first"] + 1
    assert report["captured_head_count"] == count, "Incomplete page"
    assert report["failures"] == 0 and report["capture_passed"] and report["actor_content_checked"], "Capture gate failed"
    assert report["head_framing_checked"] and report["framing_margin_px"] == 20 and report["useful_viewport_pixels"] == [480, 496], "Missing useful-viewport framing contract"
    assert report["visual_review_status"] == "pending", "Capture must not invent visual review"
    expected_records = {(ordinal, state) for ordinal in range(job["first"], job["last"] + 1) for state in job["states"]}
    records = report["records"]
    assert len(records) == len(expected_records) and {(r["ordinal"], r["state"]) for r in records} == expected_records, "Missing, duplicate or foreign record"
    for record in records:
        expected_id = ("H" if job["role"] == "human" else "M") + f"-H{record['ordinal']:04}"
        assert record["id"] == expected_id, "Head ID does not match ordinal"
        assert record["appearance"] == appearance_for(job["role"], record["ordinal"]), "Appearance does not match exhaustive ordinal"
        expected_meshes = {f"{job['role']}_{key}_{record['appearance'][key]}" for key in ["eyes", "mouth", "brows"]}
        assert len(record["selected_facial_meshes"]) == 3 and set(record["selected_facial_meshes"]) == expected_meshes, "Wrong independent facial pieces"
        assert record["facial_application"] == "CharacterSkin.apply_facial_values", "Missing real expression application"
        assert set(record["channels"]) == CHANNELS and all(math.isfinite(v) for v in record["channels"].values()), "Incomplete expression channels"
        if record["state"] == "neutral":
            assert all(abs(v) < 1e-6 for v in record["channels"].values()), "Neutral catalog contains a non-neutral expression"
        assert isinstance(record["corrective_weights"], dict), "Missing corrective application evidence"
        assert record["head_framing"]["method"] == "native_skeleton_plus_skinned_morphs" and record["head_framing"]["distance"] > 0, "Missing deformed-head framing identity"
    record_ids = {(r["id"], r["state"]) for r in records}
    expected_views = {(head, state, view) for head, state in record_ids for view in VIEWS}
    views = report["captured_views"]
    assert len(views) == len(expected_views) and {(v["id"], v["state"], v["view"]) for v in views} == expected_views, "Missing or duplicate tile"
    assert all(v["sample_count"] == 576 and v["occupied_actor_samples"] >= 24 for v in views), "Blank avatar tile"
    by_identity = {(record["id"], record["state"]): record for record in records}
    for view in views:
        appearance = by_identity[(view["id"], view["state"])]["appearance"]
        required = {"eyes", "mouth", "brows", "hair", "head" if job["role"] == "human" else "core"}
        for category in ["accessory", "mustache", "beard"]:
            if appearance.get(category, 0) > 0:
                required.add(category)
        projected = view["projected_head_parts"]
        assert required <= {part["category"] for part in projected}, "Projected bounds omit a selected head/antenna part"
        assert len({part["mesh"] for part in projected}) == len(projected), "Duplicate projected head part"
        if job["role"] == "mosquito":
            assert sum(part["proboscis_vertices"] for part in projected) > 0, "Projected bounds omit the separately articulated proboscis"
        for part in projected:
            assert part["vertices"] > 0 and not part["behind_camera"], "Invalid or behind-camera head geometry"
            assert isinstance(part["proboscis_vertices"], int) and 0 <= part["proboscis_vertices"] <= part["vertices"], "Invalid proboscis vertex count"
            assert math.isfinite(part["min_margin_px"]) and part["min_margin_px"] >= 19.9, "Head part exceeds useful capture rectangle"
            assert math.isfinite(part["native_base_max_error_m"]) and part["native_base_max_error_m"] <= .00015, "Deformation palette differs from native geometry"
            x, y, width, height = part["rect_pixels"]
            assert all(math.isfinite(value) for value in [x, y, width, height]) and width >= 0 and height >= 0 and x >= 19.9 and y >= 19.9 and x + width <= 460.1 and y + height <= 476.1, "Projected head rectangle exceeds explicit margins"
    assert set(report["source_sha256"]) == set(report_sources(job["role"])), "Incomplete source identity in report"
    for source, expected in report["source_sha256"].items():
        relative = "game/" + source.removeprefix("res://")
        assert expected.lower() == plan["source_sha256"][relative].lower(), "Report does not match frozen source"
    assert len(report["sheets"]) == job["expected_sheets"], "Missing sheets"
    sheet_rows = []
    expected_pairs = {(state, tuple(VIEWS[pair:pair + 2])) for state in job["states"] for pair in range(0, 8, 2)}
    assert {(s["state"], tuple(s["views"])) for s in report["sheets"]} == expected_pairs, "Wrong view-pair sheets"
    assert len({s["file"] for s in report["sheets"]}) == len(report["sheets"]), "Repeated sheet file"
    for sheet in report["sheets"]:
        name = sheet["file"]
        assert pathlib.Path(name).name == name and ".." not in name, "Sheet filename escapes job directory"
        assert sheet["first_head"] == job["first"] and sheet["last_head"] == job["last"], "Sheet range differs from records"
        assert sheet["rows"] == (1 if job["kind"] == "witness" else 6) and sheet["head_pairs_per_row"] == (1 if job["kind"] == "witness" else 4), "Sheet layout differs from capture contract"
        image = folder / name
        expected_size = (960, 560) if job["kind"] == "witness" else (3840, 3360)
        assert png_dimensions(image) == expected_size, "Unexpected sheet dimensions"
        sheet_rows.append({"file": name, "sha256": digest(image), "bytes": image.stat().st_size, "state": sheet["state"], "views": sheet["views"], "visual_review_status": "pending"})
    result = {"schema": 1, "job": job["id"], "capture_status": "validated", "visual_review_status": "pending", "report_sha256": digest(report_path), "head_count": count, "tile_count": len(views), "sheets": sheet_rows, "source_sha256": plan["source_sha256"], "exe_sha256": plan["exe_sha256"]}
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["plan", "verify-inputs", "validate-page", "inventory"])
    parser.add_argument("--plan", default=str(ROOT / "work/gallery09-plan.json"))
    parser.add_argument("--output", default=str(ROOT / "outputs/0.9-facial-gallery"))
    parser.add_argument("--exe", default=str(ROOT / "work/tools/godot-4.5.2/Godot_v4.5.2-stable_win64_console.exe"))
    parser.add_argument("--job", type=int, default=0)
    parser.add_argument("--inventory", default=str(ROOT / "work/gallery09-coverage.json"))
    arguments = parser.parse_args()
    if arguments.command == "plan":
        if pathlib.Path(arguments.plan).exists():
            raise ValueError("Plan already exists. Preserve evidence and choose a new plan filename.")
        plan = build_plan(arguments.output, pathlib.Path(arguments.exe).resolve())
        write(arguments.plan, plan)
        print(f"PREPARED {plan['neutral_pages']} neutral pages + {plan['witness_jobs']} witness jobs; no Godot launched")
    else:
        plan = read(arguments.plan)
        verify_inputs(plan)
        if arguments.command == "validate-page":
            job = plan["jobs"][arguments.job]
            result = verify_page(plan, job)
            write(pathlib.Path(job["output"]) / "validated.json", result)
            print("CAPTURE_VALIDATED_VISUAL_REVIEW_PENDING " + job["id"])
        elif arguments.command == "inventory":
            entries = []
            for job in plan["jobs"]:
                folder = pathlib.Path(job["output"])
                row = {"job": job["id"], "kind": job["kind"], "capture_status": "pending", "visual_review_status": "pending"}
                if folder.exists():
                    try:
                        previous = read(folder / "validated.json")
                        run = read(folder / "run.json")
                        assert run["passed"] and run["plan_sha256"] == digest(arguments.plan), "Run does not match frozen plan"
                        current = verify_page(plan, job)
                        assert previous == current, "Capture evidence changed after validation"
                        row.update(current)
                    except (AssertionError, ValueError, KeyError, OSError) as error:
                        row.update(capture_status="invalid", error=str(error))
                entries.append(row)
            counts = {state: sum(row["capture_status"] == state for row in entries) for state in ["pending", "validated", "invalid"]}
            write(arguments.inventory, {"schema": 1, "plan_sha256": digest(arguments.plan), "counts": counts, "all_neutral_pages_captured": all(row["capture_status"] == "validated" for row in entries if row["kind"] == "catalog"), "all_neutral_pages_visually_reviewed": False, "review_rule": "Separate human review ledger must cite exact sheet hashes. This inventory cannot certify visual approval.", "jobs": entries})
            print("CAPTURE_INVENTORY " + json.dumps(counts) + "; visual review remains separate")
        else:
            print("INPUT_HASHES_UNCHANGED")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, AssertionError, OSError, KeyError) as error:
        print("GALLERY09_ERROR: " + str(error), file=sys.stderr)
        sys.exit(1)
