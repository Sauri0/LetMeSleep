"""Control the one visible human workspace through the installed Blender MCP."""
import argparse
import json
import site
from pathlib import Path

site.addsitedir(r'C:\Users\brank\AppData\Roaming\Blender Foundation\Blender\5.2\extensions\.local\lib\python3.13\site-packages')
from blmcp.tools_helpers.connection import send_code

parser = argparse.ArgumentParser()
parser.add_argument('--script')
args = parser.parse_args()
expected = 'N:/LetMeSleep/Worktrees/characters/Higgsfield/HUM-NOCHE-001/source/HUM-NOCHE-001_workspace.blend'
guard = "import bpy\nfrom pathlib import Path\nassert Path(bpy.data.filepath).resolve() == Path(" + repr(expected) + ").resolve(), 'Wrong Blender workspace; no mutation allowed'\n"
if args.script:
    script = Path(args.script).resolve()
    allowed = Path('N:/LetMeSleep/Worktrees/characters/Higgsfield').resolve()
    if not script.is_relative_to(allowed):
        raise SystemExit('Script must belong to the human department directory')
    code = guard + script.read_text(encoding='utf-8-sig')
else:
    code = guard + "import importlib\ns=importlib.import_module('bl_ext.user_default.higgsfield_blender.fnf.session')\nresult={'file':bpy.data.filepath,'objects':[o.name for o in bpy.data.objects],'authenticated':s.is_authenticated(),'mesh_count':sum(o.type=='MESH' for o in bpy.data.objects)}"
reply = send_code(code, strict_json=True)
print(json.dumps(reply, ensure_ascii=False))
