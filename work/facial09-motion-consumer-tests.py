"""CPU-only regressions for the consumer. Synthetic geometry is not game evidence."""
import copy
import importlib.util
import itertools
import json
from pathlib import Path
import time
import numpy as np

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("consumer", HERE / "facial09-motion-consumer.py")
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)
checks, failures = 0, []


def check(value, label):
    global checks
    checks += 1
    if not value:
        failures.append(label)


def tri(points):
    return np.asarray(points, dtype=float)


def edge_oracle(a, b):
    # Independent Moller-Trumbore segment test, used only for generic random
    # nonparallel samples (not the consumer's plane-section implementation).
    for source, target in ((a, b), (b, a)):
        e1, e2 = target[1]-target[0], target[2]-target[0]
        for i in range(3):
            origin, direction = source[i], source[(i+1)%3]-source[i]
            cross = np.cross(direction, e2)
            det = e1 @ cross
            if abs(det) < 1e-12:
                continue
            delta = origin-target[0]
            u = delta @ cross / det
            q = np.cross(delta, e1)
            v, t = direction @ q / det, e2 @ q / det
            if 0 <= u <= 1 and 0 <= v and u+v <= 1 and 0 <= t <= 1:
                return True
    return False


def sat2(a, b):
    # Separating axes for two convex coplanar triangles, independent of clipping.
    for source in (a, b):
        for i in range(3):
            edge = source[(i+1)%3, :2]-source[i, :2]
            normal = np.array([-edge[1], edge[0]])
            pa, pb = a[:, :2] @ normal, b[:, :2] @ normal
            if max(pa) < min(pb)-1e-9 or max(pb) < min(pa)-1e-9:
                return False
    return True


a = tri([[0,0,0], [1,0,0], [0,1,0]])
samples = [
    ("transverse", tri([[.2,.2,-1],[.2,.2,1],[.8,.2,0]]), True, "crossing"),
    ("parallel disjoint", a+[0,0,.000001], False, None),
    ("coplanar containment", a*.2+[.1,.1,0], True, "coplanar"),
    ("coplanar crossing", tri([[.5,-.2,0],[1.2,.5,0],[-.2,.5,0]]), True, "coplanar"),
    ("coplanar AABB false positive", tri([[.6,.6,0],[1,.6,0],[.6,1,0]]), False, None),
    ("shared edge", tri([[0,0,0],[1,0,0],[.5,0,1]]), True, "touching"),
    ("shared vertex", tri([[0,0,0],[0,-1,1],[-1,0,1]]), True, "touching"),
]
for label, b, exists, kind in samples:
    for order in itertools.permutations(range(3)):
        for first, second in ((a, b[list(order)]), (b[list(order)], a)):
            result = c.triangle_contact(first, second)
            check((result is not None) == exists, label+" permutation")
            if result:
                check(result["kind"] == kind, label+" type")
                check(all(np.isfinite(result["points"]).ravel()), label+" finite")

rng = np.random.default_rng(907)
for i in range(300):
    x, y = rng.uniform(-1, 1, (3,3)), rng.uniform(-1, 1, (3,3))
    expected = edge_oracle(x,y)
    check((c.triangle_contact(x,y) is not None) == expected, f"independent segment oracle {i}")
    # Same geometry at mosquito scale and nonzero world translation.
    angle = .73
    rotation = np.array([[np.cos(angle),0,np.sin(angle)],[0,1,0],[-np.sin(angle),0,np.cos(angle)]])
    xx, yy = (x@rotation.T)*.35+[2,3,-4], (y@rotation.T)*.35+[2,3,-4]
    check((c.triangle_contact(xx,yy) is not None) == expected, f"world transform {i}")
for i in range(150):
    x, y = rng.uniform(-1, 1, (3,3)), rng.uniform(-1, 1, (3,3))
    x[:,2], y[:,2] = 0, 0
    check((c.triangle_contact(x,y) is not None) == sat2(x,y), f"coplanar SAT {i}")


def mesh(triangles, bone="head"):
    points = np.asarray(triangles).reshape(-1,3)
    n = len(points)
    topology = {"indices":list(range(n)), "surfaces":[{"triangle_start":0,"triangle_count":n//3,"material":"synthetic"}],
                "binds":[{"name":bone}], "vertex_bind_indices":[[0]]*n, "vertex_weights":[[1.]]*n}
    return c.Mesh(topology, {"vertices":points.tolist()})


# BVH traversal compared with brute force on nontrivial leaves.
aa, bb = rng.uniform(-1,1,(21,3,3)), rng.uniform(-1,1,(19,3,3))
ma, mb = mesh(aa), mesh(bb)
found, candidates = c.contacts(ma,mb,time.monotonic()+5)
brute = {(i,j) for i,x in enumerate(aa) for j,y in enumerate(bb) if edge_oracle(x,y)}
check({(r["triangle_a"],r["triangle_b"]) for r in found} == brute, "BVH retains every exact pair")
check(candidates < 21*19, "BVH prunes some candidates")
try:
    c.contacts(ma,mb,time.monotonic()-1)
    check(False, "timeout enforced")
except TimeoutError:
    check(True, "timeout enforced")

identity = {"basis":np.eye(3).tolist(),"origin":[0,0,0]}
human = {"role":"human","bones":{"head":identity}}
record = {"points":[[0,-.18,0],[.01,-.18,0]],"triangle_a":0,"triangle_b":0}
check(c.classify(record,"human_outfit_0","human_head",ma,mb,human)=="expected_neck_attachment","neck allows exact local attachment")
crossing = dict(record,points=[[0,-.18,0],[.2,-.18,0]])
check(c.classify(crossing,"human_outfit_0","human_head",ma,mb,human)=="unexpected_contact_requires_review","all seam endpoints must stay in neck")
check(c.classify(record,"human_outfit_0","human_beard_1",ma,mb,human)=="unexpected_contact_requires_review","beard has no neck exception")
check(c.classify(record,"human_outfit_0_trim","human_mouth_0",ma,mb,human)=="unexpected_contact_requires_review","mouth and trim must be checked")
scale = {"basis":(np.eye(3)*.35).tolist(),"origin":[1,2,3]}
mosquito = {"role":"mosquito","bones":{"head":scale,"antenna_l":scale,"antenna_r":identity}}
antenna, core = mesh([a],"antenna_l"), mesh([a],"head")
root = dict(record,points=[[1.003,2,3],[1.004,2,3]])
check(c.classify(root,"mosquito_hair_0","mosquito_core",antenna,core,mosquito)=="expected_antenna_root_attachment","root uses world scale and own bone")
outside = dict(root,points=[[1.003,2,3],[1.006,2,3]])
check(c.classify(outside,"mosquito_hair_0","mosquito_core",antenna,core,mosquito)=="unexpected_contact_requires_review","external antenna beyond5.25mm fails")
check(c.classify(root,"mosquito_hair_0","mosquito_eyes_0",antenna,core,mosquito)=="unexpected_contact_requires_review","eyes never receive root allowance")
wrong_core = mesh([a],"thorax")
check(c.classify(root,"mosquito_hair_0","mosquito_core",antenna,wrong_core,mosquito)=="unexpected_contact_requires_review","core triangle must bind to head")
check(len(c.expected_pairs("human",c.required_names("human")))==48,"48 required human relations")
check(len(c.expected_pairs("mosquito",c.required_names("mosquito")))==36,"36 required mosquito relations")

# Real historical schema, not a current geometry verdict.
data = json.loads((HERE/"facial-motion08-smoke.json").read_text(encoding="utf-8"))
check(not c.validate_part(data),"historical native schema parses")
broken = copy.deepcopy(data)
broken["compatible_pairs"]["human"].pop()
check(any("pairs" in e for e in c.validate_part(broken)),"missing hidden alternative relation rejected")
broken = copy.deepcopy(data)
broken["cases"].append(copy.deepcopy(broken["cases"][0]))
check(any("duplicate" in e for e in c.validate_part(broken)),"duplicate case rejected")
broken = copy.deepcopy(data)
key = next(iter(broken["topologies"]))
broken["topologies"][key]["surfaces"][0]["triangle_count"]-=1
check(any("surfaces" in e for e in c.validate_part(broken)),"omitted surface triangles rejected")
broken = copy.deepcopy(data)
broken["summary"]["max_base_error_m"]=.01
check(any("bake" in e for e in c.validate_part(broken)),"unverified native deformation rejected")

report={"format":"LMS_FACIAL09_CONSUMER_SYNTHETIC_TESTS","checks":checks,"failures":failures,
        "analyzer_sha256":c.sha(HERE/"facial09-motion-consumer.py"),
        "scope":"Synthetic numeric regressions plus historical schema validation; no current game geometry certification."}
c.write_json(HERE/"facial09-motion-consumer-tests.json",report)
print(f"FACIAL09_CONSUMER_TESTS checks={checks} failures={len(failures)}")
for failure in failures[:20]:
    print(failure)
raise SystemExit(1 if failures else 0)
