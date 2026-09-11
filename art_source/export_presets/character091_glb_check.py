"""Inspect exported GLBs without Blender/Godot; compare real rest silhouettes.

This does not certify continuous intersections or visual quality. The native
views and the existing triangle/blink audits provide those separate witnesses.
"""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[2]


class GLB:
    def __init__(self, path):
        self.path = path
        raw = path.read_bytes()
        assert raw[:4] == b"glTF"
        length = struct.unpack_from("<I", raw, 12)[0]
        self.doc = json.loads(raw[20:20 + length])
        self.binary = raw[28 + length:]
        self.sha256 = hashlib.sha256(raw).hexdigest()
        self.meshes = {node["name"]: self.doc["meshes"][node["mesh"]]
                       for node in self.doc["nodes"] if "mesh" in node}

    def values(self, index):
        accessor = self.doc["accessors"][index]
        count = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[accessor["type"]]
        code = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}[accessor["componentType"]]
        fmt = "<" + code * count
        result = [(0,) * count for _ in range(accessor["count"])]
        if "bufferView" in accessor:
            view = self.doc["bufferViews"][accessor["bufferView"]]
            stride = view.get("byteStride", struct.calcsize(fmt))
            offset = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
            result = [struct.unpack_from(fmt, self.binary, offset + i * stride) for i in range(accessor["count"])]
        if "sparse" in accessor:
            sparse = accessor["sparse"]
            indices, values = sparse["indices"], sparse["values"]
            index_fmt = "<" + {5121: "B", 5123: "H", 5125: "I"}[indices["componentType"]]
            index_offset = self.doc["bufferViews"][indices["bufferView"]].get("byteOffset", 0) + indices.get("byteOffset", 0)
            value_offset = self.doc["bufferViews"][values["bufferView"]].get("byteOffset", 0) + values.get("byteOffset", 0)
            for i in range(sparse["count"]):
                index = struct.unpack_from(index_fmt, self.binary, index_offset + i * struct.calcsize(index_fmt))[0]
                result[index] = struct.unpack_from(fmt, self.binary, value_offset + i * struct.calcsize(fmt))
        return result

    def primitive(self, name, material):
        return next(p for p in self.meshes[name]["primitives"]
                    if self.doc["materials"][p["material"]]["name"] == material)

    def points(self, name, material):
        return self.values(self.primitive(name, material)["attributes"]["POSITION"])

    def geometry(self, name):
        result = []
        for primitive in self.meshes[name]["primitives"]:
            result.append({"material": self.doc["materials"][primitive["material"]]["name"],
                           "attributes": {k: self.values(v) for k, v in primitive["attributes"].items()},
                           "indices": self.values(primitive["indices"]),
                           "targets": [{k: self.values(v) for k, v in t.items()} for t in primitive.get("targets", [])]})
        return result

    def rig(self):
        return [{"joints": [{k: self.doc["nodes"][i].get(k) for k in ["name", "translation", "rotation", "scale", "matrix"]}
                            for i in skin["joints"]], "binds": self.values(skin["inverseBindMatrices"])}
                for skin in self.doc["skins"]]


def width(points):
    return max(p[0] for p in points) - min(p[0] for p in points)


def curvature(points):
    half = max(abs(p[0]) for p in points)
    centre = [p[1] for p in points if abs(p[0]) < half * .25]
    ends = [p[1] for p in points if abs(p[0]) > half * .75]
    return sum(ends) / len(ends) - sum(centre) / len(centre)


def same_polygon_patches(before, after):
    """Blender may choose a different diagonal after reshaping a source quad.

Require identical triangle count and oriented boundaries of the changed patches;
the exported vertex count/weights are independently checked below.
"""
    if len(before) != len(after):
        return False
    triangles = lambda values: Counter(tuple(v[0] for v in values[i:i+3]) for i in range(0, len(values), 3))
    old, new = triangles(before), triangles(after)
    def boundary(faces):
        edges = Counter()
        for (a, b, c), count in faces.items():
            for start, end in [(a, b), (b, c), (c, a)]:
                edges[tuple(sorted([start, end]))] += count * (1 if start < end else -1)
        return {edge: count for edge, count in edges.items() if count}
    return boundary(old-new) == boundary(new-old)


def run(baseline=None):
    failures, metrics, hashes = [], {}, {}
    checks = 0

    def check(ok, label):
        nonlocal checks
        checks += 1
        if not ok:
            failures.append(label)

    for role in ["human", "mosquito"]:
        glb = GLB(ROOT / f"game/assets/art/characters/{role}/{role}_lms06.glb")
        hashes[role] = glb.sha256
        mouth = [glb.points(f"{role}_mouth_{i}", "ink") for i in range(3)]
        curls = [curvature(points) for points in mouth]
        widths = [width(points) for points in mouth]
        check(curls[0] - curls[1] > .001, role + " smile opening curves upward distinctly from rest")
        check(curls[1] - curls[2] > .001, role + " serious opening curves downward distinctly from rest")
        check(widths[1] < widths[0] * .85 and widths[1] < widths[2] * .88, role + " rest opening is recognizably shorter")
        brows = [glb.points(f"{role}_brows_{i}", "hair" if role == "human" else "insect_dark") for i in range(3)]
        brow_widths = [width(points) for points in brows]
        check(brow_widths[1] < brow_widths[0] * .97, role + " arched brows have a distinct narrower silhouette")
        check(brow_widths[2] > brow_widths[1] * 1.07, role + " firm/low brows differ from arched/high brows")
        metrics[role] = {"mouth_curvature_source_m": curls, "mouth_width_source_m": widths, "brow_width_source_m": brow_widths}
        for name, mesh in glb.meshes.items():
            if not any(f"_{category}_" in name for category in ["eyes", "mouth", "brows"]):
                continue
            check(all(math.isfinite(c) for p in mesh["primitives"] for v in glb.values(p["attributes"]["POSITION"]) for c in v), name + " finite rest vertices")
            check(set(["BlinkL", "BlinkR", "GazeX", "GazeY", "BrowUp", "BrowDown", "MouthOpen", "MouthSmile", "MouthPress", "CheekLift"]).issubset(mesh["extras"]["targetNames"]), name + " preserves ten public channels")
        if role == "mosquito":
            p0, p1 = [glb.points(f"mosquito_eyes_{i}", "insect_primary") for i in range(2)]
            rms = math.sqrt(sum(sum((a - b) ** 2 for a, b in zip(p, q)) for p, q in zip(p0, p1)) / len(p0))
            check(rms > .002, "mosquito Alertas has a different actual lid surface from Redondos")
            metrics[role]["alert_lid_rms_difference_source_m"] = rms
        if baseline:
            old = GLB(baseline / f"{role}_lms06.glb")
            check(glb.rig() == old.rig(), role + " exact joint transforms and inverse bind matrices preserved")
            check(set(glb.meshes) == set(old.meshes), role + " mesh selection IDs preserved")
            changed = []
            for name in glb.meshes:
                category = name.split("_")[1]
                mutable = category in ["brows", "mouth"] or (role == "mosquito" and (category == "eyes" or name == "mosquito_accessory_2"))
                if not mutable:
                    check(glb.geometry(name) == old.geometry(name), name + " all original geometry/skin/morph arrays preserved")
                else:
                    changed.append(name)
                    a, b = glb.meshes[name], old.meshes[name]
                    check(a.get("extras", {}).get("targetNames", []) == b.get("extras", {}).get("targetNames", []), name + " morph names/order preserved")
                    check(len(a["primitives"]) == len(b["primitives"]), name + " surface count preserved")
                    for pa, pb in zip(a["primitives"], b["primitives"]):
                        old_indices, new_indices = old.values(pb["indices"]), glb.values(pa["indices"])
                        check(same_polygon_patches(old_indices, new_indices), name + " source polygon boundaries and triangle count preserved")
                        check(len(glb.values(pa["attributes"]["POSITION"])) == len(old.values(pb["attributes"]["POSITION"])), name + " exported vertex count preserved")
                        for attr in ["JOINTS_0", "WEIGHTS_0"]:
                            check(glb.values(pa["attributes"][attr]) == old.values(pb["attributes"][attr]), name + " " + attr + " unchanged")
                    if category == "eyes":
                        for material in ["eye_white", "pupil"]:
                            check(glb.points(name, material) == old.points(name, material), name + " original ocular volume " + material)
            metrics[role]["allowed_changed_meshes"] = changed
    return {"checks": checks, "failures": failures, "metrics": metrics, "glb_sha256": hashes,
            "scope": "Exported neutral silhouette distinction and exact baseline geometry/rig invariance outside edited features. No continuous collision or visual approval implied."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    report = run(args.baseline)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding="utf8")
    print("CHARACTER091_GLB", report["checks"], "checks", len(report["failures"]), "failures")
    for failure in report["failures"][:12]:
        print(failure)
    if args.verify and report["failures"]:
        raise SystemExit(1)
