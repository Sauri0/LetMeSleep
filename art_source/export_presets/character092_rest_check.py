"""Compare the new hand rest to the sealed facial GLB and rig contract."""
import argparse
import json
import math
from pathlib import Path
import numpy as np
from character091_glb_check import GLB, same_polygon_patches

ROOT = Path(__file__).resolve().parents[2]


def run(baseline):
    checks, failures = 0, []
    def check(ok, label):
        nonlocal checks
        checks += 1
        if not ok:
            failures.append(label)
    old = GLB(baseline / 'human_lms06.glb')
    new = GLB(ROOT / 'game/assets/art/characters/human/human_lms06.glb')
    old_rig = json.loads((baseline / 'rig_contract.json').read_text())
    new_rig = json.loads((ROOT / 'game/assets/art/characters/human/rig_contract.json').read_text())
    check(new_rig['rig_version'] == 'LMS092.palm1', 'explicit new palmar bind version')
    check(set(old_rig['bones']) == set(new_rig['bones']), 'same bone names')
    check(len(new_rig['bones']) == 36, '36 bones retained')
    maximum_length_error = 0.0
    frames = {}
    for side in ['l', 'r']:
        wrist = np.array(old_rig['bones']['hand_' + side]['from'])
        axis = np.array(old_rig['bones']['hand_' + side]['to']) - wrist
        axis /= np.linalg.norm(axis)
        width = np.array([1., 0., 0.])
        width -= axis * width.dot(axis)
        width /= np.linalg.norm(width)
        frames[side] = wrist, axis, width, -np.cross(width, axis)
    for name, a in old_rig['bones'].items():
        b = new_rig['bones'][name]
        check(a['parent'] == b['parent'], name + ' parent retained')
        if name.startswith(('finger', 'thumb')):
            before = np.array(a['to']) - a['from']
            after = np.array(b['to']) - b['from']
            error = abs(np.linalg.norm(after) - np.linalg.norm(before))
            maximum_length_error = max(maximum_length_error, float(error))
            check(error < 1e-7, name + ' old-to-new authored length retained')
            check(after.dot(frames[name[-1]][3]) >= -1e-8, name + ' palmar rest vector')
            if '_a_' in name:
                check(np.linalg.norm(np.array(a['from']) - b['from']) < 1e-7, name + ' proximal root retained')
        else:
            check(a == b, name + ' rest definition exactly unchanged')
    check(set(old.meshes) == set(new.meshes), 'mesh IDs retained')
    for name in old.meshes:
        if name != 'human_core':
            check(old.geometry(name) == new.geometry(name), name + ' geometry/morphs/weights exactly preserved')
    changed, maximum_shift, outside_edits = 0, 0.0, 0
    a, b = old.meshes['human_core'], new.meshes['human_core']
    check(len(a['primitives']) == len(b['primitives']), 'core material surfaces retained')
    for before, after in zip(a['primitives'], b['primitives']):
        check(same_polygon_patches(old.values(before['indices']), new.values(after['indices'])), 'source polygon boundaries retained')
        for attribute in ['TEXCOORD_0']:
            check(old.values(before['attributes'][attribute]) == new.values(after['attributes'][attribute]), 'core ' + attribute + ' retained')
        src = np.array(old.values(before['attributes']['POSITION']))
        dst = np.array(new.values(after['attributes']['POSITION']))
        check(src.shape == dst.shape, 'core vertex count retained')
        if src.shape != dst.shape:
            continue
        check(bool(np.isfinite(dst).all()), 'core finite vertices')
        old_names=[old.doc['nodes'][j]['name'] for j in old.doc['skins'][0]['joints']]
        new_names=[new.doc['nodes'][j]['name'] for j in new.doc['skins'][0]['joints']]
        old_ids=old.values(before['attributes']['JOINTS_0']);new_ids=new.values(after['attributes']['JOINTS_0'])
        old_weights=old.values(before['attributes']['WEIGHTS_0']);new_weights=new.values(after['attributes']['WEIGHTS_0'])
        invalid_weights=0
        for p,ai,bi,aw,bw in zip(src,old_ids,new_ids,old_weights,new_weights):
            aweights={old_names[j]:w for j,w in zip(ai,aw) if w>1e-6}
            bweights={new_names[j]:w for j,w in zip(bi,bw) if w>1e-6}
            wrist,axis,width,palm=frames['l' if p[0]<0 else 'r']
            if aweights!=bweights and not (-.09001<(p-wrist).dot(axis)<.12501 and abs((p-wrist).dot(width))<.10501):invalid_weights+=1
            if abs(sum(bw)-1)>1e-5 or min(bw)<0:invalid_weights+=1
            if any(not n.startswith(('forearm','hand','finger','thumb')) for n in bweights):invalid_weights+=1
        check(invalid_weights==0,'weights normalized and edits restricted to distal forearm/hand bones')
        for p, q in zip(src, dst):
            shift = q - p
            if np.linalg.norm(shift) < 1e-8:
                continue
            changed += 1
            maximum_shift = max(maximum_shift, float(np.linalg.norm(shift)))
            wrist, axis, width, palm = frames['l' if p[0] < 0 else 'r']
            longitudinal = (p - wrist).dot(axis)
            lateral = (p - wrist).dot(width)
            allowed = .01199 < longitudinal < .12501 and abs(lateral) < .10501
            if not allowed or abs(shift.dot(axis)) > 1e-7 or abs(shift.dot(width)) > 1e-7:
                outside_edits += 1
        normals = new.values(after['attributes']['NORMAL'])
        check(all(all(math.isfinite(c) for c in n) and abs(math.sqrt(sum(c*c for c in n))-1) < 1e-5 for n in normals), 'finite unit source normals')
    check(changed > 0, 'distal source geometry actually changed')
    check(outside_edits == 0, 'all source edits remain in hand normal direction; wrist/forearm/width/longitudinal unchanged')
    return {'checks': checks, 'failures': failures, 'changed_export_vertices': changed,
            'maximum_source_shift_m': maximum_shift, 'outside_scope_edits': outside_edits,
            'maximum_old_to_new_bone_length_error_m': maximum_length_error,
            'before_glb_sha256': old.sha256, 'after_glb_sha256': new.sha256,
            'scope': 'Source rest/length/scope invariants. Native deformed triangles and tool contacts require separate checks.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = run(args.baseline)
    args.output.write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))
    raise SystemExit(1 if report['failures'] else 0)
