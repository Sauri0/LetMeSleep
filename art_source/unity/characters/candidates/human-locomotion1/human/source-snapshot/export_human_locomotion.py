"""Export only Human locomotion into a new candidate; requires Director native slot.

Does not invoke the whole character builder, menu export or mosquito generation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys
import bpy

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from human_locomotion_contract import BASELINE_BLEND_SHA256, PROFILES, REPLACED, manifest
from repair_human_lid_winding import invariant, digest
from verify_human_menu import curves
from author_human_locomotion import author


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf8', newline='\n')


def scene_fingerprint():
    result = invariant()
    result.pop('clips')
    # The winding repair invariant deliberately ignored polygon order; here it must stay exact.
    result['winding'] = digest([(o.name, [list(p.vertices) for p in o.data.polygons])
                                for o in sorted(bpy.context.scene.objects, key=lambda o: o.name)
                                if o.type == 'MESH'])
    return result


def action_fingerprints():
    return {a.name: digest([(f.data_path, f.array_index, f.extrapolation,
                            [(list(k.co), list(k.handle_left), list(k.handle_right), k.interpolation,
                              k.handle_left_type, k.handle_right_type) for k in f.keyframe_points])
                           for f in curves(a)]) for a in bpy.data.actions}


def export_candidate(baseline, output, audit_after=False):
    source = baseline / 'LMS_Human_alpha.blend'
    assert file_hash(source) == BASELINE_BLEND_SHA256, 'Wrong baseline; obtain a reviewed repin'
    baseline_audit = json.loads((baseline / 'audit.json').read_text(encoding='utf8'))
    for name, expected in baseline_audit['files_sha256'].items():
        assert file_hash(baseline / name) == expected, ('Baseline manifest mismatch', name)
    assert not output.exists() and not baseline.is_relative_to(output), 'Output must be new and isolated'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    before = scene_fingerprint()
    actions_before = action_fingerprints()
    assert len(actions_before) == 15 and set(actions_before) == {c['name'] for c in baseline_audit['clips']}
    assert REPLACED <= actions_before.keys()
    rigs = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
    assert len(rigs) == 1 and len([o for o in bpy.context.scene.objects if o.type == 'MESH']) == 5
    result = author(rigs[0])
    after = scene_fingerprint()
    actions_after = action_fingerprints()
    assert before == after, 'Mesh/morph/weights/rig/winding changed'
    preserved = {name: value for name, value in actions_before.items() if name not in REPLACED}
    assert len(preserved) == 13
    assert all(actions_after[name] == value for name, value in preserved.items()), 'Unrelated action changed'
    assert set(actions_after) == set(preserved) | {p['clip'] for p in PROFILES}
    assert all(actions_before[name] != actions_after[name] for name in REPLACED)
    output.mkdir(parents=True)
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 61
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / source.name))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in scene.objects:
        if obj.type in {'MESH', 'ARMATURE'}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rigs[0]
    bpy.ops.export_scene.fbx(filepath=str(output / 'LMS_Human_alpha.fbx'), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=True, add_leaf_bones=False, use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_step=1, bake_anim_simplify_factor=0, path_mode='AUTO', mesh_smooth_type='FACE')
    files = {name: file_hash(output / name) for name in ('LMS_Human_alpha.blend', 'LMS_Human_alpha.fbx')}
    replacement = {c['name']: c for c in result['clips']}
    clip_entries = [replacement.get(c['name'], c) for c in baseline_audit['clips']]
    clip_entries += [c for c in result['clips'] if c['name'] not in actions_before]
    receipt = dict(baseline_blend_sha256=BASELINE_BLEND_SHA256, baseline_root=str(baseline),
                   scene_before=before, scene_after=after, actions_before=actions_before,
                   actions_after=actions_after, preserved_action_names=sorted(preserved),
                   files_sha256=files, author=result,
                   scope='Exact source geometry/skin/morph/rig/winding and 13 unrelated action curves. New motion has separate audit; perception pending.')
    audit = dict(baseline_audit, clips=clip_entries, files_sha256=files, locomotion_reauthoring=receipt)
    write_json(output / 'audit.json', audit)
    write_json(output / 'preservation.json', receipt)
    snapshot = output / 'source-snapshot'
    snapshot.mkdir()
    dependencies = ['human_locomotion_contract.py', 'author_human_locomotion.py', 'export_human_locomotion.py',
                    'audit_human_locomotion.py', 'author_motion.py', 'build_characters.py',
                    'verify_human_menu.py', 'human_menu_contract.py', 'repair_human_lid_winding.py',
                    'author_human_facial.py', 'audit_human_joints.py', 'compare_fbx_winding_repair.py',
                    'inspect_fbx_morph_regions.py']
    for name in dependencies:
        shutil.copy2(ROOT / name, snapshot / name)
    contract = manifest()
    contract.update(status='exported-native-audit-pending', files_sha256=files,
                    source_snapshot_sha256={name: file_hash(snapshot / name) for name in dependencies})
    write_json(output / 'locomotion_profiles.json', contract)
    assert file_hash(source) == BASELINE_BLEND_SHA256, 'Input changed'
    if audit_after:
        from audit_human_locomotion import audit_candidate
        audit_candidate(baseline, output)
        contract['status'] = 'native-numeric-gates-passed-visual-audio-pending'
        contract['gates_pending'].remove('native source/FBX audit')
        write_json(output / 'locomotion_profiles.json', contract)
    print('HUMAN_LOCOMOTION_CANDIDATE ' + json.dumps(dict(output=str(output), files=files, status=contract['status'])), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-human', type=Path, required=True)
    parser.add_argument('--output-human', type=Path, required=True)
    parser.add_argument('--audit', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    export_candidate(args.baseline_human.resolve(), args.output_human.resolve(), args.audit)


if __name__ == '__main__':
    main()
