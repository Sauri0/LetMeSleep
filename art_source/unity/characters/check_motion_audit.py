"""Enforce measurable source-motion gates; never substitutes for Unity/art review."""
import json
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parent
report=json.loads((ROOT/'motion_audit.json').read_text())
errors=[]
for species in ['Human','Mosquito']:
    for kind in ['blend','fbx']:
        actions=[a for a in report['actions'] if a['species']==species and a['format']==kind]
        if len(actions)!=15:errors.append(f'{species}/{kind}: expected15clips, found{len(actions)}')
        path=ROOT/species.lower()/f'LMS_{species}_alpha.{kind}'
        sha=hashlib.sha256(path.read_bytes()).hexdigest()
        for action in actions:
            name=action['clip']+'/'+kind
            if action['source_sha256']!=sha:errors.append(name+': stale audit hash')
            if action['variable_curve_count']==0 or action['max_mesh_vertex_motion_m']<.0001:errors.append(name+': no measurable movement')
            if action['nonfinite_vertices']:errors.append(name+': nonfinite mesh')
            if action['max_root_head_motion_m']>.0001:errors.append(name+': Root translated')
            if action['loop_expected'] and action['first_last_mesh_difference_m']>.002:errors.append(name+': loop seam >2mm')
            if species=='Human' and action['minimum_mesh_z_m']<-.003:errors.append(name+': below floor '+str(action['minimum_mesh_z_m']))
            if action['clip']=='Mosquito_SurfaceWalk':
                contact=json.loads((ROOT/'mosquito/audit.json').read_text())['contact']
                plane=contact['ground_contact_rest_unity_m'][1]/.5
                if action['minimum_mesh_z_m']<plane-.003:errors.append(name+': surface support penetration')
for comparison in report['source_fbx_comparison']:
    if comparison['max_source_fbx_head_difference_m']>.002:errors.append(comparison['clip']+': source/FBX pose mismatch >2mm')
for hand in report['hands']:
    if hand['inward_distal_joint_displacement_m']<.015:errors.append(str(hand)+': finger does not curl inward')
for eye in report['facial']:
    if eye['closed_open_height_ratio'] is None or eye['closed_open_height_ratio']>.20:errors.append(str(eye)+': blink does not close vertically')
if len(report.get('expressions',[]))!=2:errors.append('Expected source/FBX jaw expression measurements')
for expression in report.get('expressions',[]):
    if expression['jaw_mesh_downward_motion_in_head_space_m']<.003:errors.append(str(expression)+': jaw does not open downward')
summary={'passed':not errors,'errors':errors,'scope':'60source/FBX clip evaluations, floor/support, loop, root, 10digits and blink; Unity/runtime/visual review separate',
         'motion_audit_sha256':hashlib.sha256((ROOT/'motion_audit.json').read_bytes()).hexdigest()}
(ROOT/'motion_gate.json').write_text(json.dumps(summary,indent=2),encoding='utf8',newline='\n')
print(json.dumps(summary,indent=2))
assert not errors, 'Motion audit failed'
