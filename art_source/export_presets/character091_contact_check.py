"""Focused exact-triangle witnesses for the modified cosmetic features.

Uses the established intersection implementation and reports every unexpected
exposed contact, without treating embedded attachment roots as free surfaces.
Finite samples are explicit; this is not a proof for all continuous poses.
"""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import sys
import time
import bpy
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import characters_facial_audit as audit


def weights(category):
    if category == "mouth":
        return [dict(zip(["MouthOpen", "MouthSmile", "MouthPress"], values))
                for values in itertools.product([0.0, 1.0], repeat=3)] + [{"MouthOpen": .5, "MouthSmile": .5}]
    if category == "brows":
        return [{}, {"BrowUp": 1.0}, {"BrowDown": 1.0}, {"BrowUp": .5, "BrowDown": .5}, {"BrowUp": 1.0, "BrowDown": 1.0}]
    if category == "eyes":
        return [{}, {"BlinkL": .5, "BlinkR": .5}, {"BlinkL": 1.0, "BlinkR": 1.0}]
    return [{}]


def run(role, source_dir=None):
    started = time.monotonic()
    path = source_dir / f"{role}_lms06.blend" if source_dir else ROOT / f"art_source/characters/{role}/{role}_lms06.blend"
    bpy.ops.wm.open_mainfile(filepath=str(path))
    objects = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    head = objects["human_head" if role == "human" else "mosquito_core"]
    if role == "human":
        head_bvh = audit.state(head, {})["bvh"]
    else:
        group = head.vertex_groups["head"].index
        indices = {v.index for v in head.data.vertices if any(w.group == group and w.weight > .99 for w in v.groups)}
        faces = [list(p.vertices) for p in head.data.polygons if all(i in indices for i in p.vertices)]
        head_bvh = BVHTree.FromPolygons([v.co for v in head.data.vertices], faces)
    categories = {name: name.split("_")[1] for name in objects}
    changed = {"brows", "mouth"} | ({"eyes"} if role == "mosquito" else set())
    partners = {"brows": {"eyes", "hair", "accessory"},
                "mouth": {"mustache", "beard", "accessory", "eyes"},
                "eyes": {"brows", "accessory"}}
    pairs = set()
    for name, category in categories.items():
        if category not in changed:
            continue
        for other, other_category in categories.items():
            if other_category in partners[category] and audit.compatible(name, other):
                pairs.add(tuple(sorted([name, other])))
    cache = {}

    def geometry(name, controls):
        # A shared controller drives both pieces. Only available channels enter
        # the bake; the established audit supplies coupled blink correctives.
        obj = objects[name]
        keys = obj.data.shape_keys.key_blocks if obj.data.shape_keys else {}
        local = {key: value for key, value in controls.items() if key in keys}
        signature = (name, tuple(sorted(local.items())))
        if signature not in cache:
            cache[signature] = audit.state(obj, local)
        return cache[signature]

    records, failures = [], []
    for a, b in sorted(pairs):
        samples, exposed_samples, contacts, witness = 0, 0, 0, None
        cases = {tuple(sorted(dict(wa, **wb).items())) for wa in weights(categories[a]) for wb in weights(categories[b])}
        for case in sorted(cases):
            controls = dict(case)
            count, point, exposed, exposed_point = audit.contacts(geometry(a, controls), geometry(b, controls), head_bvh)
            samples += 1
            contacts += int(count > 0)
            exposed_samples += int(exposed > 0)
            if witness is None and (exposed or (count and "human_accessory_2" in [a, b])):
                witness = {"controls": controls, "position": exposed_point if exposed else point,
                           "triangle_contacts": count, "exposed_triangle_contacts": exposed}
        classification = audit.contact_classification(a, b, contacts, exposed_samples)
        if classification in ["unexpected_exposed_contact", "unexpected_accessory_contact"]:
            failures.append({"a": a, "b": b, "classification": classification, "witness": witness})
        records.append({"a": a, "b": b, "samples": samples, "samples_with_contact": contacts,
                        "exposed_samples": exposed_samples, "classification": classification, "witness": witness})
        if len(records) % 12 == 0:
            print("CHARACTER091_CONTACT_PROGRESS", role, len(records), "pairs", round(time.monotonic() - started, 1), "seconds", flush=True)
    result = {"role": role, "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
              "pairs": records, "sample_count": sum(r["samples"] for r in records), "failures": failures,
              "elapsed_seconds": time.monotonic() - started,
              "scope": "Changed brows/mouth and mosquito lids against compatible neighboring cosmetics, with shared neutral/extreme/midpoint controls; exact triangle intersections. Rigid head attachments only; garment/antenna motion requires separate native views."}
    print("CHARACTER091_CONTACT", role, len(records), "pairs", result["sample_count"], "samples", len(failures), "failures", flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--role", choices=["human", "mosquito", "both"], default="both")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-dir", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    results = [run(role, args.source_dir) for role in (["human", "mosquito"] if args.role == "both" else [args.role])]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(results, indent=2), encoding="utf8")
    if any(result["failures"] for result in results):
        raise RuntimeError("Unresolved cosmetic contacts in the listed finite samples")
