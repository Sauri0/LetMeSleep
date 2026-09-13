"""Capture Blender's own framebuffer, never the Windows desktop."""
import json
import site
from pathlib import Path

site.addsitedir(r'C:\Users\brank\AppData\Roaming\Blender Foundation\Blender\5.2\extensions\.local\lib\python3.13\site-packages')
from blmcp.tools_helpers.connection import send_code

path = Path('N:/LetMeSleep/Temp/Higgsfield/blender-internal.png')
path.parent.mkdir(parents=True, exist_ok=True)
code = '''import bpy
from pathlib import Path
expected=Path('N:/LetMeSleep/Worktrees/characters/Higgsfield/HUM-NOCHE-001/source/HUM-NOCHE-001_workspace.blend')
assert Path(bpy.data.filepath).resolve()==expected.resolve(), 'Wrong workspace'
window=bpy.context.window or next(iter(bpy.context.window_manager.windows),None)
assert window is not None, 'No Blender window'
area=max((a for a in window.screen.areas if a.type=='VIEW_3D'),key=lambda a:a.width*a.height)
with bpy.context.temp_override(window=window, area=area):
    bpy.ops.screen.screenshot_area(filepath='N:/LetMeSleep/Temp/Higgsfield/blender-internal.png')
result={'path':'N:/LetMeSleep/Temp/Higgsfield/blender-internal.png','source':'Blender VIEW_3D framebuffer','file':bpy.data.filepath}
'''
print(json.dumps(send_code(code,strict_json=True),ensure_ascii=False))
