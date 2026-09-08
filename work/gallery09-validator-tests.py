"""Offline validator tests; synthetic PNGs are NOT game captures or art approval."""
import copy
import importlib.util
import json
import pathlib
import struct
import sys
import tempfile
import zlib

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("gallery09_manifest", ROOT / "work/gallery09-manifest.py")
gallery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gallery)
results = []
png_cache = {}


def check(label, callback, rejects=False):
    try:
        callback()
        passed = not rejects
        reason = "accepted" if passed else "unexpected acceptance"
    except (AssertionError, ValueError, KeyError, OSError) as error:
        passed = rejects
        reason = str(error)
    results.append({"test": label, "passed": passed, "result": reason})


def require(condition):
    assert condition


def png(size):
    if size not in png_cache:
        def chunk(kind, value):
            return struct.pack(">I", len(value)) + kind + value + struct.pack(">I", zlib.crc32(kind + value))
        row = bytes(1 + size[0] * 4)
        compressor = zlib.compressobj()
        data = b"".join(compressor.compress(row) for _ in range(size[1])) + compressor.flush()
        png_cache[size] = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", *size, 8, 6, 0, 0, 0)) + chunk(b"IDAT", data) + chunk(b"IEND", b"")
    return png_cache[size]


with tempfile.TemporaryDirectory(prefix="gallery09-validator-", dir=ROOT / "work") as temporary:
    folder = pathlib.Path(temporary).resolve()
    assert folder.parent == (ROOT / "work").resolve()
    source = folder / "synthetic-source.txt"
    source.write_text("synthetic test input", encoding="utf-8")
    plan = {"exe": str(source), "exe_sha256": gallery.digest(source), "source_sha256": {}}
    for role in gallery.COUNTS:
        for path in gallery.report_sources(role):
            plan["source_sha256"]["game/" + path.removeprefix("res://")] = "a" * 64

    def fixture(role="human", page=0, head=None, states=None):
        states = states or ["neutral"]
        first = page * 24 if head is None else head
        last = min(first + 23, gallery.COUNTS[role] - 1) if head is None else head
        job = {"id": "synthetic-only", "kind": "catalog" if head is None else "witness", "role": role, "page": page, "states": states, "first": first, "last": last, "output": str(folder), "expected_report": "synthetic.json", "expected_sheets": 4 * len(states)}
        records, views, sheets = [], [], []
        for state in states:
            for ordinal in range(first, last + 1):
                appearance = gallery.appearance_for(role, ordinal)
                identity = ("H" if role == "human" else "M") + f"-H{ordinal:04}"
                records.append({"id": identity, "ordinal": ordinal, "state": state, "appearance": appearance, "selected_facial_meshes": [f"{role}_{key}_{appearance[key]}" for key in ["eyes", "mouth", "brows"]], "facial_application": "CharacterSkin.apply_facial_values", "channels": {key: 0 for key in gallery.CHANNELS}, "corrective_weights": {}, "head_framing":{"method":"native_skeleton_plus_skinned_morphs","distance":1.5}})
                categories = ["eyes", "mouth", "brows", "hair", "head" if role=="human" else "core"]
                categories += [key for key in ["accessory","mustache","beard"] if appearance.get(key,0)>0]
                parts = [{"mesh":role+"_"+key,"category":key,"vertices":100,"proboscis_vertices":20 if key=="core" else 0,"behind_camera":False,"min_margin_px":25,"native_base_max_error_m":.0000001,"rect_pixels":[25,25,200,300]} for key in categories]
                views.extend({"id": identity, "state": state, "view": view, "occupied_actor_samples": 24, "sample_count": 576,"projected_head_parts":copy.deepcopy(parts)} for view in gallery.VIEWS)
            for pair in range(4):
                name = f"synthetic-{state}-{pair}.png"
                (folder / name).write_bytes(png((3840, 3360) if head is None else (960, 560)))
                sheets.append({"file": name, "state": state, "views": gallery.VIEWS[pair * 2:pair * 2 + 2], "rows": 6 if head is None else 1, "head_pairs_per_row": 4 if head is None else 1, "first_head": first, "last_head": last})
        report = {"schema": 4, "views": gallery.VIEWS, "tile_pixels": [480, 560], "camera_yaws": gallery.YAWS, "camera_pitches": gallery.PITCHES, "role": role, "page": page, "states": states, "head_count": gallery.COUNTS[role], "head_keys": gallery.head_keys(role), "page_size": 24, "captured_head_count": last - first + 1, "failures": 0, "capture_passed": True, "actor_content_checked": True, "head_framing_checked":True,"framing_margin_px":20,"useful_viewport_pixels":[480,496],"visual_review_status": "pending", "records": records, "captured_views": views, "sheets": sheets, "source_sha256": {path: "a" * 64 for path in gallery.report_sources(role)}}
        gallery.write(folder / job["expected_report"], report)
        return job, report

    for role, page in [("human", 0), ("human", 121), ("mosquito", 0), ("mosquito", 10)]:
        job, report = fixture(role, page)
        check(f"synthetic valid {role} page {page}, including partial tail", lambda: gallery.verify_page(plan, job))
    job, report = fixture("human", 0, 18, ["neutral", "brow-up", "blink-half", "blink"])
    check("synthetic single-head multistate dimensions", lambda: gallery.verify_page(plan, job))

    mutations = {
        "six angles rejected": lambda d: d.update(views=gallery.VIEWS[:6]),
        "wrong native dimensions rejected": lambda d: d.update(tile_pixels=[400, 440]),
        "camera pitch altered rejected": lambda d: d["camera_pitches"].__setitem__(6, 0),
        "wrong page rejected": lambda d: d.update(page=1),
        "unrequested state rejected": lambda d: d.update(states=["smile"]),
        "incomplete page count rejected": lambda d: d.update(captured_head_count=23),
        "changed category order rejected": lambda d: d["head_keys"].reverse(),
        "failed capture rejected": lambda d: d.update(failures=1),
        "capture cannot assert visual approval": lambda d: d.update(visual_review_status="approved"),
        "missing record rejected": lambda d: d["records"].pop(),
        "duplicate record rejected": lambda d: d["records"].__setitem__(1, d["records"][0]),
        "mismatched head ID rejected": lambda d: d["records"][0].update(id="H-H9999"),
        "wrong cosmetic ordinal rejected": lambda d: d["records"][0]["appearance"].update(eyes=2),
        "legacy combined face rejected": lambda d: d["records"][0].update(selected_facial_meshes=["human_face_0"]),
        "missing expression channel rejected": lambda d: d["records"][0]["channels"].pop("BlinkL"),
        "non-neutral expression rejected": lambda d: d["records"][0]["channels"].update(MouthOpen=1),
        "missing angle tile rejected": lambda d: d["captured_views"].pop(),
        "duplicate angle tile rejected": lambda d: d["captured_views"].__setitem__(1, d["captured_views"][0]),
        "blank first avatar rejected": lambda d: d["captured_views"][0].update(occupied_actor_samples=0),
        "missing source hashes rejected": lambda d: d.update(source_sha256={}),
        "changed source hash rejected": lambda d: d["source_sha256"].update({gallery.report_sources("human")[0]: "b" * 64}),
        "missing sheet rejected": lambda d: d["sheets"].pop(),
        "reused sheet file rejected": lambda d: d["sheets"][1].update(file=d["sheets"][0]["file"]),
        "sheet path traversal rejected": lambda d: d["sheets"][0].update(file="../other.png"),
        "incorrect last head rejected": lambda d: d["sheets"][0].update(last_head=100),
        "incorrect layout rejected": lambda d: d["sheets"][0].update(rows=1),
        "old eight-angle schema without head bounds rejected": lambda d: d.update(schema=3),
        "framing gate missing rejected": lambda d: d.update(head_framing_checked=False),
        "antenna or head part omitted rejected": lambda d: d["captured_views"][0]["projected_head_parts"].pop(),
        "clipped projected head rejected": lambda d: d["captured_views"][0]["projected_head_parts"][0].update(min_margin_px=-1),
        "head rectangle beyond margin rejected": lambda d: d["captured_views"][0]["projected_head_parts"][0].update(rect_pixels=[0,25,200,300]),
        "unreliable native skin bake rejected": lambda d: d["captured_views"][0]["projected_head_parts"][0].update(native_base_max_error_m=.01),
    }
    job, baseline = fixture()
    for label, mutate in mutations.items():
        candidate = copy.deepcopy(baseline)
        mutate(candidate)
        gallery.write(folder / job["expected_report"], candidate)
        check(label, lambda: gallery.verify_page(plan, job), rejects=True)

    job, report = fixture("mosquito", 10)
    for part in report["captured_views"][0]["projected_head_parts"]:
        part["proboscis_vertices"] = 0
    gallery.write(folder / job["expected_report"], report)
    check("separately articulated proboscis omitted rejected", lambda: gallery.verify_page(plan, job), rejects=True)

    for label, transform in [
        ("truncated PNG rejected", lambda data: data[:-9]),
        ("corrupt PNG CRC rejected", lambda data: data[:40] + bytes([data[40] ^ 1]) + data[41:]),
        ("extra PNG bytes rejected", lambda data: data + b"unexpected"),
        ("wrong full sheet size rejected", lambda _: png((960, 560))),
    ]:
        job, report = fixture()
        target = folder / report["sheets"][0]["file"]
        target.write_bytes(transform(target.read_bytes()))
        check(label, lambda: gallery.verify_page(plan, job), rejects=True)

    historical = ROOT / "outputs/0.7-combinaciones-muestras-lote1/human-page-000.json"
    job, report = fixture()
    gallery.write(folder / job["expected_report"], gallery.read(historical))
    check("real historical six-view page rejected as obsolete", lambda: gallery.verify_page(plan, job), rejects=True)
    preserved = gallery.read(ROOT / "work/gallery08-blank-regression.json")
    check("historical first-frame blank evidence retained", lambda: require(preserved[0]["occupied_actor_samples"] == 0 and preserved[1]["occupied_actor_samples"] >= 24))

    small_plan = {"exe": str(source), "exe_sha256": gallery.digest(source), "source_sha256": {source.relative_to(ROOT).as_posix(): gallery.digest(source)}}
    check("unchanged frozen input accepted", lambda: gallery.verify_inputs(small_plan))
    source.write_text("changed", encoding="utf-8")
    check("changed executable hash rejected", lambda: gallery.verify_inputs(small_plan), rejects=True)
    small_plan["exe_sha256"] = gallery.digest(source)
    check("changed source hash rejected before launch", lambda: gallery.verify_inputs(small_plan), rejects=True)

    imported = folder / "synthetic.scn"
    imported.write_text("synthetic compiled model", encoding="utf-8")
    metadata = folder / "synthetic.md5"
    metadata.write_text(f'source_md5="{gallery.md5(source)}"\ndest_md5="{gallery.md5(imported)}"', encoding="utf-8")
    link = {"source": source.relative_to(ROOT).as_posix(), "destination": imported.relative_to(ROOT).as_posix(), "import_md5": metadata.relative_to(ROOT).as_posix()}
    check("matching GLB and compiled import accepted", lambda: gallery.verify_import_link(link))
    source.write_text("new GLB without import", encoding="utf-8")
    check("stale compiled model import rejected", lambda: gallery.verify_import_link(link), rejects=True)
    metadata.write_text(f'source_md5="{gallery.md5(source)}"\ndest_md5="{gallery.md5(imported)}"', encoding="utf-8")
    imported.write_text("modified compiled cache", encoding="utf-8")
    check("changed compiled cache rejected", lambda: gallery.verify_import_link(link), rejects=True)

    neutral_count = sum((count + 23) // 24 for count in gallery.COUNTS.values())
    check("complete catalog remains 133 pages and 25272 tiles", lambda: require(neutral_count == 133 and sum(gallery.COUNTS.values()) * 8 == 25272))
    check("11 original expressive witnesses retained", lambda: require(len(gallery.WITNESSES) == 11))

summary = {"schema": 1, "scope": "Offline validator tests only. Synthetic PNGs are not native game captures or visual approval. No Godot launched.", "checks": len(results), "failures": sum(not item["passed"] for item in results), "tests": results, "source_sha256": {"work/gallery09-manifest.py": gallery.digest(ROOT / "work/gallery09-manifest.py"), "work/gallery09-validator-tests.py": gallery.digest(__file__)}}
gallery.write(ROOT / "work/gallery09-validator-tests.json", summary)
print(json.dumps({key: summary[key] for key in ["checks", "failures", "scope"]}, ensure_ascii=False))
sys.exit(1 if summary["failures"] else 0)
