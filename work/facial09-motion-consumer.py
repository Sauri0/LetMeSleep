"""Read-only consumer of native deformed facial triangles. Requires NumPy.

No Godot/Blender or runtime edits. Bounds only discard candidates. Measured
boundary contacts, data completeness, and visual approval remain distinct.
"""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import time
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
EPS = 1e-7  # Native exporter coordinate rounding, metres.
TOTALS = {"human": 243, "mosquito": 42}
SOURCE_PATHS = {
    "res://assets/art/characters/shared/character_skin.gd",
    "res://assets/art/characters/shared/facial_expression.gd",
    "res://scripts/human_pose.gd", "res://scripts/mosquito_pose.gd",
    "res://assets/art/characters/human/human_lms06.glb",
    "res://assets/art/characters/mosquito/mosquito_lms06.glb",
    "res://assets/art/characters/human/model.json",
    "res://assets/art/characters/mosquito/model.json",
}
POLICY = {
    "version": 1, "world_contact_epsilon_m": EPS,
    "neck_head_local": {"y": [-.245, -.115], "ellipse_x_radius": .097,
                        "ellipse_z_radius": .089, "ellipse_z_center": .012},
    "antenna_root_bone_local_radius": .015,
    "attachment_vertex_bone_weight_minimum": .99,
    "other_contacts": "review_required_even_when_only_tangent",
    "scope": "boundary triangle contacts at exported finite poses",
}
LIMITS = [
    "No visual approval, full customization-gallery approval, or continuous-motion proof.",
    "All exported effective morphs including correctives are retained. Finite seeded states do not span the full morph domain.",
    "Only head/garment and antenna/head-piece relations are covered. Static head/head pairs require the separate facial08 audit.",
    "Touching and coplanar seams are contacts. A flagged relation requires review; it is not automatically a visible defect.",
    "Complete containment without boundary contact, minimum clearance, floating pieces and self-intersections are not certified.",
    "No colors/outfits/footwear combinations are counted as independently verified classes by this report.",
    "Coordinates and numeric contact epsilon are 0.1 micrometre; this is not a collision-radius allowance.",
]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8")


def category(name):
    return name.split("_")[1]


def unique_points(points):
    result = []
    for point in points:
        if not any(np.linalg.norm(point - prior) <= EPS for prior in result):
            result.append(np.asarray(point, dtype=float))
    return result


def plane_section(triangle, distances):
    points = [triangle[i] for i in range(3) if abs(distances[i]) <= EPS]
    for i in range(3):
        j = (i + 1) % 3
        if (distances[i] < -EPS and distances[j] > EPS) or (distances[j] < -EPS and distances[i] > EPS):
            points.append(triangle[i] + (triangle[j] - triangle[i]) * distances[i] / (distances[i] - distances[j]))
    return unique_points(points)


def coplanar_polygon(a, b, normal):
    # Preserve ALL overlap vertices, not a first witness that can hide a seam
    # leaving the permitted neck/root region.
    drop = int(np.argmax(np.abs(normal)))
    axes = [i for i in range(3) if i != drop]
    def cross2(u, v):
        return float(u[0] * v[1] - u[1] * v[0])
    bb = b[:, axes]
    orientation = 1 if cross2(bb[1] - bb[0], bb[2] - bb[0]) >= 0 else -1
    polygon = list(a)
    for i in range(3):
        start, end = bb[i], bb[(i + 1) % 3]
        edge = end - start
        length = float(np.linalg.norm(edge))
        if length <= EPS:
            continue
        def signed(point):
            return orientation * cross2(edge, point[axes] - start) / length
        clipped = []
        for j, point in enumerate(polygon):
            prior = polygon[j - 1]
            current_d, prior_d = signed(point), signed(prior)
            current_in, prior_in = current_d >= -EPS, prior_d >= -EPS
            if current_in != prior_in and abs(prior_d - current_d) > 1e-15:
                clipped.append(prior + (point - prior) * prior_d / (prior_d - current_d))
            if current_in:
                clipped.append(point)
        polygon = unique_points(clipped)
        if not polygon:
            break
    return polygon


def triangle_contact(a, b):
    """Closed triangle-boundary intersection polygon/segment; no AABB-only hit."""
    na, nb = np.cross(a[1] - a[0], a[2] - a[0]), np.cross(b[1] - b[0], b[2] - b[0])
    la, lb = float(np.linalg.norm(na)), float(np.linalg.norm(nb))
    if la <= 1e-16 or lb <= 1e-16:
        return None  # Degenerate mesh faces separately invalidate completeness.
    na, nb = na / la, nb / lb
    da, db = (a - b[0]) @ nb, (b - a[0]) @ na
    if min(da) > EPS or max(da) < -EPS or min(db) > EPS or max(db) < -EPS:
        return None
    line = np.cross(na, nb)
    length = float(np.linalg.norm(line))
    if length < 1e-10:
        if max(abs(db)) > EPS:
            return None
        points = coplanar_polygon(a, b, na)
        return {"kind": "coplanar", "points": [p.tolist() for p in points]} if points else None
    aa, bb = plane_section(a, da), plane_section(b, db)
    if not aa or not bb:
        return None
    line /= length
    pa, pb = np.array(aa) @ line, np.array(bb) @ line
    low, high = max(min(pa), min(pb)), min(max(pa), max(pb))
    if low > high + EPS:
        return None
    origin = np.linalg.solve(np.stack([na, nb, line]), np.array([na @ a[0], nb @ b[0], 0.0]))
    points = unique_points([origin + line * low, origin + line * high])
    crossing = high - low > EPS and min(da) < -EPS and max(da) > EPS and min(db) < -EPS and max(db) > EPS
    return {"kind": "crossing" if crossing else "touching", "points": [p.tolist() for p in points]}


class BVH:
    def __init__(self, low, high, ids=None):
        ids = np.arange(len(low)) if ids is None else ids
        self.low, self.high = low[ids].min(axis=0), high[ids].max(axis=0)
        self.count, self.ids, self.children = len(ids), ids, None
        if len(ids) > 12:
            centers = (low[ids] + high[ids]) * .5
            axis = int(np.argmax(np.ptp(centers, axis=0)))
            order = ids[np.argsort(centers[:, axis], kind="stable")]
            middle = len(ids) // 2
            self.children = (BVH(low, high, order[:middle]), BVH(low, high, order[middle:]))
            self.ids = None


class Mesh:
    def __init__(self, topology, geometry):
        self.topology = topology
        self.vertices = np.asarray(geometry["vertices"], dtype=np.float64)
        self.indices = np.asarray(topology["indices"], dtype=np.int64).reshape(-1, 3)
        self.triangles = self.vertices[self.indices]
        self.low, self.high = self.triangles.min(axis=1), self.triangles.max(axis=1)
        self.tree = BVH(self.low, self.high)
        normals = np.cross(self.triangles[:, 1] - self.triangles[:, 0], self.triangles[:, 2] - self.triangles[:, 0])
        self.degenerate = np.flatnonzero(np.linalg.norm(normals, axis=1) <= 1e-16).tolist()
        self.materials = [""] * len(self.indices)
        for surface in topology["surfaces"]:
            for i in range(surface["triangle_start"], surface["triangle_start"] + surface["triangle_count"]):
                self.materials[i] = surface["material"]

    def triangle_bones(self, index):
        common = None
        for vertex in self.indices[index]:
            weights = {}
            for bind, weight in zip(self.topology["vertex_bind_indices"][vertex], self.topology["vertex_weights"][vertex]):
                name = self.topology["binds"][bind]["name"]
                weights[name] = weights.get(name, 0) + weight
            selected = {name for name, weight in weights.items() if weight >= .99}
            common = selected if common is None else common.intersection(selected)
        return common or set()


def contacts(a, b, deadline):
    queue, result, candidates = [(a.tree, b.tree)], [], 0
    while queue:
        if time.monotonic() > deadline:
            raise TimeoutError("time budget reached; no partial pair is certified")
        x, y = queue.pop()
        if np.any(x.high < y.low - EPS) or np.any(y.high < x.low - EPS):
            continue
        if x.children is None and y.children is None:
            mask = np.all(a.high[x.ids, None] >= b.low[y.ids] - EPS, axis=2) & np.all(b.high[y.ids] >= a.low[x.ids, None] - EPS, axis=2)
            for ii, jj in np.argwhere(mask):
                i, j = int(x.ids[ii]), int(y.ids[jj])
                candidates += 1
                contact = triangle_contact(a.triangles[i], b.triangles[j])
                if contact:
                    result.append(dict(contact, triangle_a=i, triangle_b=j,
                                       material_a=a.materials[i], material_b=b.materials[j]))
        elif y.children is None or (x.children is not None and x.count >= y.count):
            queue.extend((child, y) for child in x.children)
        else:
            queue.extend((x, child) for child in y.children)
    return result, candidates


def bone_local(points, transform):
    # Basis exports columns and includes mosquito's .35 model scale.
    matrix = np.asarray(transform["basis"], dtype=float).T
    return np.linalg.solve(matrix, (np.asarray(points) - np.asarray(transform["origin"])).T).T


def classify(contact, a_name, b_name, a, b, case):
    points = contact["points"]
    cats = {category(a_name), category(b_name)}
    if case["role"] == "human" and cats == {"head", "outfit"}:
        q = bone_local(points, case["bones"]["head"])
        inside = ((q[:, 1] >= -.245) & (q[:, 1] <= -.115) &
                  ((q[:, 0] / .097) ** 2 + ((q[:, 2] - .012) / .089) ** 2 <= 1.0))
        if bool(np.all(inside)):
            return "expected_neck_attachment"
    if case["role"] == "mosquito" and cats == {"hair", "core"}:
        antenna, core = (a, b) if category(a_name) == "hair" else (b, a)
        ai, ci = (contact["triangle_a"], contact["triangle_b"]) if antenna is a else (contact["triangle_b"], contact["triangle_a"])
        roots = antenna.triangle_bones(ai).intersection({"antenna_l", "antenna_r"})
        if "head" in core.triangle_bones(ci):
            for root in roots:
                q = bone_local(points, case["bones"][root])
                if bool(np.all(np.linalg.norm(q, axis=1) <= .015)):
                    return "expected_antenna_root_attachment"
    return "unexpected_contact_requires_review"


def required_names(role):
    if role == "human":
        return ({"human_head"} | {f"human_mouth_{i}" for i in range(3)} |
                {f"human_{cat}_{i}" for cat in ("mustache", "beard") for i in (1, 2)} |
                {f"human_outfit_{i}{suffix}" for i in range(3) for suffix in ("", "_trim")})
    return ({"mosquito_core"} | {f"mosquito_{cat}_{i}" for cat in ("eyes", "brows", "mouth", "hair") for i in range(3)} |
            {f"mosquito_accessory_{i}" for i in (1, 2)})


def expected_pairs(role, names):
    source = "outfit" if role == "human" else "hair"
    targets = {"head", "mouth", "mustache", "beard"} if role == "human" else {"core", "eyes", "brows", "mouth", "accessory"}
    return {(a, b) for a in names for b in names if category(a) == source and category(b) in targets}


def validate_part(data):
    errors = []
    def require(value, message):
        if not value:
            errors.append(message)
    require(data.get("format") == "LMS_FACIAL_MOTION_08" and data.get("version") == 1, "format/version unsupported")
    require(data.get("units") == "metres", "units must be metres")
    require(SOURCE_PATHS.issubset(data.get("source_sha256", {})), "required dependency hashes missing")
    require(all(isinstance(v, str) and len(v) == 64 and all(ch in "0123456789abcdef" for ch in v.lower())
                for v in data.get("source_sha256", {}).values()), "dependency hashes must be actual SHA256, not missing-source placeholders")
    require(not data["summary"]["failures"], "native exporter contains failures")
    require(data["summary"]["max_base_error_m"] <= .00015, "native bake error exceeds contract")
    require(data["summary"]["native_compared_vertices"] > 0, "native bake not measured")
    ids = [case["id"] for case in data["cases"]]
    require(len(ids) == len(set(ids)), "duplicate case IDs within part")
    require(len(ids) == data["summary"]["cases"], "case count mismatch")
    for name, topology in data["topologies"].items():
        n, indices = topology["vertex_count"], np.asarray(topology["indices"])
        require(n > 0 and len(indices) > 0 and len(indices) % 3 == 0, name + ": invalid triangles")
        require(bool(np.all((indices >= 0) & (indices < n))), name + ": index out of bounds")
        require(len(topology["vertex_weights"]) == n and len(topology["vertex_bind_indices"]) == n, name + ": weight count mismatch")
        for binds, weights in zip(topology["vertex_bind_indices"], topology["vertex_weights"]):
            require(len(binds) == len(weights) and all(0 <= b < len(topology["binds"]) for b in binds) and
                    all(np.isfinite(w) and w >= 0 for w in weights) and abs(sum(weights) - 1) < .001,
                    name + ": malformed skin weights")
        covered = []
        for surface in topology["surfaces"]:
            covered.extend(range(surface["triangle_start"], surface["triangle_start"] + surface["triangle_count"]))
        require(covered == list(range(len(indices) // 3)), name + ": surfaces omit/duplicate triangles")
    for gid, geometry in data["geometries"].items():
        require(geometry["mesh"] in data["topologies"], gid + ": unknown mesh")
        vertices = np.asarray(geometry["vertices"])
        require(vertices.shape == (data["topologies"][geometry["mesh"]]["vertex_count"], 3) and bool(np.isfinite(vertices).all()), gid + ": invalid vertices")
        require(geometry["native_base_max_error_m"] <= .00015, gid + ": unverified skin palette")
        require(all(np.isfinite(v) for v in geometry["morph_values"].values()), gid + ": nonfinite morph")
    for case in data["cases"]:
        role = case["role"]
        require(role in TOTALS and case["id"] in {f"{role}-{i}" for i in range(TOTALS.get(role, 0))}, "unknown case ID")
        names = set(case["meshes"])
        require(required_names(role).issubset(names), case["id"] + ": missing alternatives")
        pairs = [(p["a"], p["b"]) for p in data["compatible_pairs"][role]]
        require(len(pairs) == len(set(pairs)) and set(pairs) == expected_pairs(role, names), case["id"] + ": omitted/duplicated/unexpected pairs")
        for name, state in case["meshes"].items():
            require(state["geometry"] in data["geometries"] and data["geometries"][state["geometry"]]["mesh"] == name, case["id"] + ": geometry reference mismatch")
        for name in (["head"] if role == "human" else ["head", "antenna_l", "antenna_r"]):
            matrix = np.asarray(case["bones"].get(name, {}).get("basis", []))
            require(matrix.shape == (3, 3) and bool(np.isfinite(matrix).all()) and abs(np.linalg.det(matrix)) > 1e-9, case["id"] + ": invalid bone " + name)
    return sorted(set(errors))


def source_differences(hashes, root):
    differences = []
    for source, expected in hashes.items():
        path = root / "game" / source.removeprefix("res://")
        actual = sha(path) if path.is_file() else "missing"
        if actual.lower() != str(expected).lower():
            differences.append({"source": source, "export_sha256": expected, "current_sha256": actual})
    return differences


def part_provenance(path, data):
    """Sidecar proves requested span/hash; process-level proof is checked by merge."""
    sidecar_path = Path(str(path) + ".index.json")
    errors = []
    if not sidecar_path.is_file():
        return {"sidecar": str(sidecar_path), "verified": False, "errors": ["native sidecar absent"]}
    sidecar = json.loads(sidecar_path.read_text(encoding="utf-8-sig"))
    if sidecar.get("format") != "LMS_FACIAL_MOTION_08_PART":
        errors.append("native sidecar format mismatch")
    if sidecar.get("sha256", "").lower() != sha(path) or sidecar.get("bytes") != path.stat().st_size:
        errors.append("native sidecar file hash/size mismatch")
    if sidecar.get("source_sha256") != data.get("source_sha256"):
        errors.append("native sidecar dependency hashes mismatch")
    coverage = sidecar.get("coverage", {})
    actual = [case["id"] for case in data["cases"]]
    if coverage.get("case_ids") != actual or not coverage.get("span_complete"):
        errors.append("native sidecar case union mismatch/incomplete")
    if sidecar.get("summary", {}).get("failures") != []:
        errors.append("native sidecar exporter failures")
    return {"sidecar": str(sidecar_path), "sha256": sha(sidecar_path), "verified": not errors, "errors": errors}


def analyze(args):
    started = time.monotonic()
    input_path = Path(args.input).resolve()
    data = json.loads(input_path.read_text(encoding="utf-8-sig"))
    try:
        validation = validate_part(data)
    except (KeyError, TypeError, ValueError, IndexError) as error:
        validation = ["malformed export schema: " + str(error)]
    report = {"format": "LMS_FACIAL_MOTION_09_CONSUMER", "version": 1,
              "analyzer_sha256": sha(__file__), "policy": POLICY, "policy_sha256": digest(POLICY),
              "input": str(input_path), "input_sha256": sha(input_path),
              "source_sha256": data.get("source_sha256", {}), "limits": LIMITS,
              "validation_errors": validation, "source_differences": source_differences(data.get("source_sha256", {}), Path(args.root)),
              "cases": [], "contact_sets": {}, "mesh_diagnostics": {}, "complete_285": False}
    selected = data.get("cases", [])[args.case_start:args.case_start + args.case_count if args.case_count else None]
    report["requested_case_ids"] = [c["id"] for c in selected]
    report["evidence_mode"] = "historical_diagnostic" if args.historical else "current_source"
    errors = report["validation_errors"]
    report["native_provenance"] = part_provenance(input_path, data) if not errors else {"verified": False, "errors": ["invalid schema"]}
    if not report["native_provenance"]["verified"] and not args.historical:
        errors.append("native sidecar is not verified")
    if report["source_differences"] and not args.historical:
        errors.append("source hashes differ; --historical permits diagnostics only")
    if not selected:
        errors.append("no cases selected")
    meshes, contact_cache = {}, {}
    report["status"] = "invalid_input" if errors else "running"
    total_pairs, candidates = 0, 0
    deadline = started + args.seconds
    try:
        if not errors:
            for case in selected:
                record = {"id": case["id"], "role": case["role"], "label": case["label"], "expression": case["expression"],
                          "actor": case["actor"], "facial_values": case["facial_values"], "bones": case["bones"],
                          "pairs": [], "complete": False}
                report["cases"].append(record)
                for pair in data["compatible_pairs"][case["role"]]:
                    an, bn = pair["a"], pair["b"]
                    ga, gb = case["meshes"][an]["geometry"], case["meshes"][bn]["geometry"]
                    for gid in (ga, gb):
                        if gid not in meshes:
                            geometry = data["geometries"][gid]
                            meshes[gid] = Mesh(data["topologies"][geometry["mesh"]], geometry)
                            report["mesh_diagnostics"][gid] = {"mesh": geometry["mesh"], "degenerate_triangles": meshes[gid].degenerate,
                                                               "morph_values": geometry["morph_values"]}
                    a, b = meshes[ga], meshes[gb]
                    key = (ga, gb)
                    if key not in contact_cache:
                        values, count = contacts(a, b, deadline)
                        cid = "contact-set-" + str(len(contact_cache))
                        contact_cache[key] = cid
                        report["contact_sets"][cid] = {"mesh_a": an, "mesh_b": bn, "geometry_a": ga, "geometry_b": gb,
                                                       "candidate_triangle_pairs": count, "contacts": values}
                        candidates += count
                    cid = contact_cache[key]
                    values = report["contact_sets"][cid]["contacts"]
                    classes = [classify(value, an, bn, a, b, case) for value in values]
                    record["pairs"].append({"a": an, "b": bn, "relation": pair["relation"], "contact_set": cid,
                                            "counts": dict(Counter(classes)), "classification_per_contact": classes,
                                            "original_visibility": [case["meshes"][an]["visible"], case["meshes"][bn]["visible"]]})
                    total_pairs += 1
                record["complete"] = True
                print(f"FACIAL09_CONSUMER_PROGRESS case={case['id']} pairs={total_pairs} seconds={time.monotonic()-started:.2f}", flush=True)
            report["status"] = "analyzed"
    except TimeoutError as error:
        report["status"] = "partial_time_budget"
        report["interruption"] = str(error)
    report["source_differences_after"] = source_differences(data.get("source_sha256", {}), Path(args.root))
    report["input_unchanged"] = sha(input_path) == report["input_sha256"]
    counts = Counter()
    for case in report["cases"]:
        for pair in case["pairs"]:
            counts.update(pair["counts"])
    report["summary"] = {"requested_cases": len(selected), "completed_cases": sum(c["complete"] for c in report["cases"]),
                         "evaluated_case_pairs": total_pairs, "unique_geometry_pairs": len(contact_cache), "candidate_triangle_pairs": candidates,
                         "classifications": dict(counts), "degenerate_triangles_in_unique_geometry": sum(len(m.degenerate) for m in meshes.values()),
                         "elapsed_seconds": time.monotonic() - started}
    report["summary"]["contact_kinds"] = dict(Counter(v["kind"] for s in report["contact_sets"].values() for v in s["contacts"]))
    report["measured_span_complete"] = report["status"] == "analyzed" and report["input_unchanged"] and report["summary"]["degenerate_triangles_in_unique_geometry"] == 0
    report["current_span_complete"] = report["measured_span_complete"] and not args.historical and not report["source_differences_after"]
    report["visual_approved"] = False
    report["unexpected_contacts"] = counts["unexpected_contact_requires_review"]
    write_json(args.output, report)
    print(f"FACIAL09_CONSUMER_RESULT status={report['status']} cases={report['summary']['completed_cases']} unexpected={report['unexpected_contacts']} output={args.output}")
    return 0 if report["measured_span_complete"] and (args.historical or report["current_span_complete"]) and not (args.fail_on_contact and report["unexpected_contacts"]) else 2


def merge(args):
    reports = [(Path(path), json.loads(Path(path).read_text(encoding="utf-8-sig"))) for path in args.merge]
    errors, seen, references = [], set(), []
    hashes = reports[0][1]["source_sha256"]
    for path, report in reports:
        if report["source_sha256"] != hashes or report["analyzer_sha256"] != sha(__file__) or report["policy_sha256"] != digest(POLICY):
            errors.append("mixed source/analyzer/policy: " + str(path))
        if not report["current_span_complete"]:
            errors.append("non-current or incomplete report: " + str(path))
        for case in report["cases"]:
            if case["id"] in seen:
                errors.append("duplicate case: " + case["id"])
            seen.add(case["id"])
        references.append({"path": str(path.resolve()), "sha256": sha(path), "cases": len(report["cases"]), "unexpected_contacts": report["unexpected_contacts"]})
    expected = {f"{role}-{i}" for role, total in TOTALS.items() for i in range(total)}
    missing, extra = sorted(expected - seen), sorted(seen - expected)
    if missing or extra:
        errors.append("case union is not exactly 243 human + 42 mosquito")
    differences = source_differences(hashes, Path(args.root))
    if differences:
        errors.append("sources no longer match exported data")
    # Full coverage needs runner process evidence; raw JSON alone is insufficient.
    exporter_reference = None
    if not args.export_index:
        errors.append("--export-index required for complete285 native provenance")
    else:
        index_path = Path(args.export_index)
        index = json.loads(index_path.read_text(encoding="utf-8-sig"))
        exporter_reference = {"path": str(index_path.resolve()), "sha256": sha(index_path)}
        if index.get("format") != "LMS_FACIAL_MOTION_08_INDEX" or not index.get("complete") or index.get("failures"):
            errors.append("native exporter index incomplete/failed")
        if index.get("source_sha256") != hashes:
            errors.append("native exporter index source hashes mismatch")
        native_inputs, native_cases = {}, set()
        for part in index.get("parts", []):
            file = Path(part["file"])
            if not part.get("verified") or part.get("exit_code") != 0 or part.get("timed_out"):
                errors.append("unverified native process: " + str(file))
            if not file.is_file() or sha(file) != str(part.get("sha256", "")).lower():
                errors.append("native file changed: " + str(file))
                continue
            native_inputs[str(file.resolve())] = sha(file)
            if not Path(part["stderr"]).is_file() or Path(part["stderr"]).stat().st_size:
                errors.append("native stderr missing/nonempty: " + str(file))
            for case_id in part.get("coverage", {}).get("case_ids", []):
                if case_id in native_cases:
                    errors.append("duplicate native case: " + case_id)
                native_cases.add(case_id)
        if native_cases != expected:
            errors.append("native exporter case union incomplete")
        for path, report in reports:
            if native_inputs.get(str(Path(report["input"]).resolve())) != report["input_sha256"]:
                errors.append("consumer input not verified by native index: " + str(path))
    result = {"format": "LMS_FACIAL_MOTION_09_CONSUMER_INDEX", "version": 1, "source_sha256": hashes,
              "analyzer_sha256": sha(__file__), "policy": POLICY, "reports": references, "errors": errors,
              "complete_285": not errors, "case_count": len(seen), "missing": missing, "extra": extra,
              "source_differences": differences, "unexpected_contacts": sum(r["unexpected_contacts"] for _, r in reports),
              "native_export_index": exporter_reference, "visual_approved": False, "limits": LIMITS}
    write_json(args.output, result)
    print(f"FACIAL09_CONSUMER_INDEX complete={result['complete_285']} cases={len(seen)} errors={len(errors)}")
    return 0 if not errors and not (args.fail_on_contact and result["unexpected_contacts"]) else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument("--input")
    selection.add_argument("--merge", nargs="+")
    parser.add_argument("--output", required=True)
    parser.add_argument("--export-index", help="verified native runner index, required for merging complete285")
    parser.add_argument("--root", default=str(ROOT))
    parser.add_argument("--case-start", type=int, default=0)
    parser.add_argument("--case-count", type=int, default=0)
    parser.add_argument("--seconds", type=float, default=45)
    parser.add_argument("--historical", action="store_true", help="stale diagnostic, never current/285 coverage")
    parser.add_argument("--fail-on-contact", action="store_true", help="return 2 for contacts requiring review")
    args = parser.parse_args()
    if args.case_start < 0 or args.case_count < 0 or not 0 < args.seconds <= 50:
        parser.error("nonnegative ranges and time budget in (0,50] required")
    if Path(args.output).exists():
        parser.error("output exists; choose a new path to preserve evidence")
    return merge(args) if args.merge else analyze(args)


if __name__ == "__main__":
    raise SystemExit(main())
