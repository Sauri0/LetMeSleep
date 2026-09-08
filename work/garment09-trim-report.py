"""Assemble the current bounded trim closure and continuous presentation comparison."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def read(path):return json.loads((ROOT/path).read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
before='outputs/0.9-garment-continuous-final1';after='outputs/0.9-garment-trim/continuous-after2'
a,b=read(before+'/report.json'),read(after+'/report.json')
assert a['frame_count']==b['frame_count']==630 and not a['failures'] and not b['failures']
assert all(x['actors'][i]['root']==y['actors'][i]['root'] and x['actors'][i]['crouch']==y['actors'][i]['crouch'] for x,y in zip(a['rows'],b['rows']) for i in range(3))
depth=read('work/garment09-trim2-depth-interiors.json')['sources'][1]
assert all(x['front_samples_buried']==0 for x in depth['outfits'])
motion=read('work/garment09-trim-motion-final/index.json')
assert motion['finite_pose_coverage_preserved'] and motion['unexpected_contacts']==0
invariance=read('work/garment09-trim-invariance.json')
assert not invariance['failures'] and len(invariance['unchanged_complete_meshes'])==30
sources=['art_source/characters/shared/garment_trim.py','art_source/export_presets/characters_trim_repair.py',
         'art_source/export_presets/characters_selected_pipeline.py','art_source/characters/human/human_lms06.blend',
         'art_source/characters/human/model.json','art_source/characters/human/manifest.json',
         'game/assets/art/characters/human/human_lms06.glb','game/assets/art/characters/human/model.json',
         'game/assets/art/characters/human/rig_contract.json','game/assets/art/characters/shared/character_skin.gd',
         'game/scripts/human_pose.gd','game/scripts/actor_view.gd','game/scripts/human_presentation.gd',
         'game/tests/garment09_continuous_views.gd']
comparison={}
for name,lo,hi in [('crouch',60,81),('stand',228,253),('wave',270,342),('celebrate',360,432)]:
    comparison[name]={'before_max_vertex_step_m':max(x['actors'][0]['max_vertex_step_m'] for x in a['rows'][lo:hi]),
                      'after_max_vertex_step_m':max(x['actors'][0]['max_vertex_step_m'] for x in b['rows'][lo:hi])}
report={'format':'LMS_GARMENT09_TRIM_FINAL','source_files':{p:sha(p) for p in sources},
        'current_glb_sha256':invariance['after_sha256'],'continuous_before':before,'continuous_after':after,
        'continuous_video_sha256':sha(after+'/human-fit4-continuous.mp4'),'continuous_frames':630,'continuous_seconds':21,
        'continuous_checks':b['checks'],'continuous_failures':0,'authority_inputs_results_identical':True,
        'presentation_comparison':comparison,'comparison_scope':'Remote noncritical crouch; no FPS/focus/strike/throw. Includes actual rig displacement, not isolated cloth deformation.',
        'invariance':{'path':'work/garment09-trim-invariance.json','sha256':sha('work/garment09-trim-invariance.json'),'checks':invariance['checks']},
        'triangle_support':{'path':'work/garment09-trim2-depth-interiors.json','sha256':sha('work/garment09-trim2-depth-interiors.json'),'samples_per_outfit':7560,'buried_front_samples':0},
        'factored_motion':{'path':'work/garment09-trim-motion-final/index.json','sha256':sha('work/garment09-trim-motion-final/index.json'),'recalculated_human_poses':243,'mosquito_poses_by_identity':42,'changed_pairs':5832,'contacts':0},
        'preserved_before':'outputs/0.9-garment-trim/source-fit4',
        'authoring_source_vertices_per_piping':1104,'export_vertices_per_piping':1154,'extra_triangles_per_visible_outfit':1952,
        'default_source_triangles':54644,'visual_approved':False,
        'limits':['The earlier native972 contact gate belongs to trim1 before local subdivision; do not relabel its GLB hash as trim2.',
                  'Finite-pose closure reuses native matrices and unchanged geometry with a current control; it is not285 newly exported/rendered poses.',
                  'The clip covers three representative outfit/tool combinations from the front; hands/broom can reach the frame boundary.',
                  'Angular shoulders and the unsmoothed20Hz emote presentation remain recorded limitations. No further art change is made.']}
assert report['current_glb_sha256']==sha('game/assets/art/characters/human/human_lms06.glb')
(ROOT/'work/garment09-trim-final.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('GARMENT_TRIM09_FINAL source_current=true changed_pairs5832 contacts0 frames630')
