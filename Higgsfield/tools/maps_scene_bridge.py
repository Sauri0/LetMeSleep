"""Operate approved map scenes via the installed Higgsfield Scene Builder."""
import argparse,json,site
from pathlib import Path
site.addsitedir(r'C:\Users\brank\AppData\Roaming\Blender Foundation\Blender\5.2\extensions\.local\lib\python3.13\site-packages')
from blmcp.tools_helpers.connection import send_code
parser=argparse.ArgumentParser()
parser.add_argument('--prepare',action='store_true')
parser.add_argument('--submit',action='store_true')
parser.add_argument('--status',action='store_true')
parser.add_argument('--followup',help='Named UTF-8 prompt file inside the assigned map folder')
parser.add_argument('--map',default='01-isla')
parser.add_argument('--model',help='Scene Builder picker ID; only applied when preparing a new idle scene')
args=parser.parse_args()
assert not args.model or args.prepare,'Model selection is only allowed on new scene preparation'
assert args.map in ['01-isla','02-casa','03-campamento','04-yate','05-pueblo']
folder='N:/LetMeSleep/Artifacts/Higgsfield/Mapas/'+args.map
base="import bpy,importlib,json\nfrom pathlib import Path\np='bl_ext.user_default.higgsfield_blender'\ns=importlib.import_module(p+'.features.overlays.composer_surface')\ng=importlib.import_module(p+'.features.bridge_gate')\nh=importlib.import_module(p+'.root').addon()\n"
base+='folder=Path('+repr(folder)+')\nscene_name='+repr('HF_MAP_'+args.map.replace('-','_'))+'\n'
if args.prepare:
    base+='''
assert not s._scene_conversation().busy(), 'Scene Builder currently working'
assert not importlib.import_module(p+'.work').tasks, 'Addon generation active'
assert bpy.data.scenes.get(scene_name) is None, 'Scene already prepared; inspect before modifying'
folder.mkdir(parents=True,exist_ok=True)
checkpoint=folder.parent/'before-maps-checkpoint.blend'
if not checkpoint.exists():
    bpy.ops.wm.save_as_mainfile(filepath=str(checkpoint),copy=True)
scene=bpy.data.scenes.new(scene_name)
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
collection=bpy.data.collections.new(scene_name+'_CONTENT')
scene.collection.children.link(collection)
s._new_scene_thread(bind_scene=True)
s.switch_mode(h,s.composer_module.SCENE_BUILDER)
'''
    if args.model:
        base+='requested_model='+repr(args.model)+'\n'+'''choices=importlib.import_module(p+'.features.supercomputer').model_choices()
assert requested_model in [row[0] for row in choices],'Model not in current picker'
s._composer.scene_model=requested_model
'''
    base+='''
bpy.ops.wm.save_as_mainfile(filepath=str(folder/(scene_name+'.blend')))
result={'prepared':True,'scene':scene.name,'file':bpy.data.filepath,'objects':len(scene.objects),'bridge_ready':g.ready(),'model':s._scene_model()}
'''
elif args.followup:
    assert Path(args.followup).name==args.followup and args.followup.endswith('.txt')
    base+='prompt_name='+repr(args.followup)+'\n'
    base+='''
assert bpy.context.scene.name==scene_name,'Wrong active scene'
assert Path(bpy.data.filepath).resolve()==(folder/(scene_name+'.blend')).resolve(),'Wrong blend file'
c=s._scene_conversation()
assert c.chat_id and not c.busy() and not c.asking(),'Existing idle conversation required'
assert g.ready(),'Bridge disconnected'
receipt=folder/(prompt_name+'.request.json')
assert not receipt.exists(),'Already attempted followup, inspect conversation'
prompt=(folder/prompt_name).read_text(encoding='utf-8')
for o in bpy.context.selected_objects:o.select_set(False)
bpy.context.view_layer.objects.active=None
s._composer.scene_attachments.clear()
s._composer.prompts[s.composer_module.SCENE_BUILDER]=prompt
receipt.write_text(json.dumps({'state':'dispatching','chat_id':c.chat_id,'prompt':prompt_name},indent=2),encoding='utf-8')
accepted=s._submit_scene_builder(h)
result={'accepted':accepted,'busy':c.busy(),'chat_id':c.chat_id,'scene':scene_name,'prompt':prompt_name}
receipt.write_text(json.dumps(result,indent=2),encoding='utf-8')
'''
elif args.submit:
    base+='''
assert bpy.context.scene.name==scene_name,'Wrong active scene'
assert Path(bpy.data.filepath).resolve()==(folder/(scene_name+'.blend')).resolve(),'Wrong blend file'
assert not s._scene_conversation().busy(),'Already running'
assert g.ready(),'Bridge disconnected'
receipt=folder/'scene-builder-request.json'
assert not receipt.exists(),'Already attempted; inspect conversation, do not resubmit'
prompt=(folder/'PROMPT.txt').read_text(encoding='utf-8')
attachments=json.loads((folder/'attachments.json').read_text(encoding='utf-8'))
assert all(Path(a).is_file() for a in attachments)
for o in bpy.context.selected_objects:o.select_set(False)
bpy.context.view_layer.objects.active=None
s._composer.scene_attachments.clear()
s.attach_scene_builder(attachments)
s._composer.prompts[s.composer_module.SCENE_BUILDER]=prompt
receipt.write_text(json.dumps({'state':'dispatching','file':bpy.data.filepath,'model':s._scene_model(),'attachments':attachments},indent=2),encoding='utf-8')
accepted=s._submit_scene_builder(h)
chat=s._scene_conversation()
result={'accepted':accepted,'busy':chat.busy(),'chat_id':chat.chat_id,'scene':scene_name,'model':s._scene_model()}
receipt.write_text(json.dumps(result,indent=2),encoding='utf-8')
'''
else:
    base+='''
c=s._scene_conversation()
result={'file':bpy.data.filepath,'scene':bpy.context.scene.name,'objects':len(bpy.context.scene.objects),'bridge_ready':g.ready(),'chat_id':c.chat_id,'busy':c.busy(),'asking':c.asking(),'messages':[{'role':str(m.role),'text':m.text[-7000:],'tools':[{'label':t.label,'detail':t.detail,'status':t.status} for t in m.tools[-6:]]} for m in c.messages if str(m.role)!='user'][-1:]}
'''
print(json.dumps(send_code(base,strict_json=True),ensure_ascii=False))
