"""Select reproducible current contact witnesses; ranking is not visual approval."""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import numpy as np


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run",required=True)
    parser.add_argument("--output",required=True)
    args=parser.parse_args()
    folder=Path(args.run)
    runner=json.loads((folder/"runner-index.json").read_text(encoding="utf-8"))
    if not runner.get("complete"):
        parser.error("consumer run must finish first")
    if Path(args.output).exists():
        parser.error("preserve prior evidence; output already exists")
    ranked=defaultdict(list)
    pair_counts=Counter()
    contact_kinds=Counter()
    for entry in runner["reports"]:
        report=json.loads(Path(entry["report"]).read_text(encoding="utf-8"))
        for case in report["cases"]:
            for pair in case["pairs"]:
                count=pair["counts"].get("unexpected_contact_requires_review",0)
                if not count:
                    continue
                group=(case["role"],pair["b"].split("_")[1])
                pair_counts[(pair["a"],pair["b"])]+=count
                values=report["contact_sets"][pair["contact_set"]]["contacts"]
                selected=[v for v,k in zip(values,pair["classification_per_contact"]) if k=="unexpected_contact_requires_review"]
                kinds=Counter(v["kind"] for v in selected)
                contact_kinds.update(kinds)
                ranked[group].append({"report":entry["report"],"case_id":case["id"],"pair_a":pair["a"],"pair_b":pair["b"],
                                      "contacts":count,"crossings":kinds["crossing"]})
    # One exact witness for each contacted semantic target category. Root may
    # prioritize three, while the other categories remain explicitly listed.
    chosen=[]
    for group,rows in ranked.items():
        winner=max(rows,key=lambda r:(r["crossings"],r["contacts"],-int(r["case_id"].split("-")[1])))
        report=json.loads(Path(winner["report"]).read_text(encoding="utf-8"))
        case=next(c for c in report["cases"] if c["id"]==winner["case_id"])
        pair=next(p for p in case["pairs"] if (p["a"],p["b"])==(winner["pair_a"],winner["pair_b"]))
        contact_set=report["contact_sets"][pair["contact_set"]]
        raw=json.loads(Path(report["input"]).read_text(encoding="utf-8"))
        native_case=next(c for c in raw["cases"] if c["id"]==winner["case_id"])
        aa=raw["geometries"][contact_set["geometry_a"]]
        bb=raw["geometries"][contact_set["geometry_b"]]
        va,vb=np.asarray(aa["vertices"]),np.asarray(bb["vertices"])
        ia=np.asarray(raw["topologies"][winner["pair_a"]]["indices"]).reshape(-1,3)
        ib=np.asarray(raw["topologies"][winner["pair_b"]]["indices"]).reshape(-1,3)
        values=[v for v,k in zip(contact_set["contacts"],pair["classification_per_contact"]) if k=="unexpected_contact_requires_review"]
        # Plane extent is an auditable tri-local ranking aid, not a signed mesh
        # distance or a proof of visibility. Keep that distinction in the JSON.
        max_extent=-1
        deepest=None
        for value in values:
            a,b=va[ia[value["triangle_a"]]],vb[ib[value["triangle_b"]]]
            na,nb=np.cross(a[1]-a[0],a[2]-a[0]),np.cross(b[1]-b[0],b[2]-b[0])
            na/=np.linalg.norm(na);nb/=np.linalg.norm(nb)
            da,db=(a-b[0])@nb,(b-a[0])@na
            extent=min(max(0,min(max(da),-min(da))),max(0,min(max(db),-min(db))))
            if extent>max_extent:
                max_extent=float(extent)
                deepest=dict(value,triangle_a_world=a.tolist(),triangle_b_world=b.tolist())
        appearance=native_case["appearance"].copy()
        appearance.update(accessory=0,mustache=0,beard=0)
        for name in [winner["pair_a"],winner["pair_b"]]:
            parts=name.split("_")
            if len(parts)>2 and parts[2].isdigit():
                appearance[parts[1]]=int(parts[2])
        allpoints=np.array([point for v in values for point in v["points"]])
        winner.update(
            native_part=report["input"],native_part_sha256=report["input_sha256"],
            source_sha256=report["source_sha256"],report_sha256=sha(winner["report"]),
            actor=case["actor"],facial_values=case["facial_values"],expression=case["expression"],
            appearance_to_reproduce=appearance,original_appearance=native_case["appearance"],
            coexistence_verified=pair["a"].split("_")[1]!=pair["b"].split("_")[1],
            original_visibility=pair["original_visibility"],
            effective_morphs={winner["pair_a"]:aa["morph_values"],winner["pair_b"]:bb["morph_values"]},
            local_plane_cross_extent_m=max_extent,
            contact_world_bounds={"min":allpoints.min(0).tolist(),"max":allpoints.max(0).tolist()},
            exact_triangle_witness=deepest,
            reproduction="Skin.setup(role); set_appearance(appearance_to_reproduce); first_person=false; use native exporter preconditioning (12x.10s human apply_human or mosquito specified state); apply_facial_values(facial_values); wait process_frame+post_draw. Do not use a generic neutral Preview pose.",
            visible_defect_confirmed=False)
        chosen.append(winner)
    chosen.sort(key=lambda r:(r["pair_b"].split("_")[1] not in ["beard","mouth","head"],-r["local_plane_cross_extent_m"]))
    output={"format":"LMS_FACIAL09_MOTION_WITNESSES","run":str(folder.resolve()),
            "run_manifest_sha256":sha(folder/"runner-index.json"),"witnesses":chosen,
            "contacts_by_pair":[{"a":a,"b":b,"count":count} for (a,b),count in pair_counts.most_common()],
            "unexpected_contact_kinds":dict(contact_kinds),
            "limits":["Ranking uses transversal triangle counts and local plane cross extent, not signed penetration depth.",
                      "No contact is declared visibly defective until inspected in the exact deformed runtime pose.",
                      "Hidden export alternatives are selected explicitly as a compatible runtime appearance for each witness."]}
    Path(args.output).write_text(json.dumps(output,indent=2,ensure_ascii=False),encoding="utf-8")
    print("FACIAL09_MOTION_WITNESSES",len(chosen),"output="+args.output)
    for value in chosen:
        print(value["case_id"],value["pair_a"],value["pair_b"],"crossings="+str(value["crossings"]),"local_extent_m="+str(value["local_plane_cross_extent_m"]))


if __name__=="__main__":
    main()
