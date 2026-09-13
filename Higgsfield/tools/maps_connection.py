"""Use the installed Blender connector, including its bundled pure-Python wheel."""
import site
import sys
import zipfile
from pathlib import Path

extensions = Path('C:/Users/brank/AppData/Roaming/Blender Foundation/Blender/5.2/extensions')
site.addsitedir(str(extensions / '.local/lib/python3.13/site-packages'))
try:
    from blmcp.tools_helpers.connection import send_code
except ModuleNotFoundError as error:
    # Read directly from the installed add-on's wheel; never reinstall or mutate
    # shared extension dependencies while the visible Blender session is active.
    wheels = list((extensions / 'user_default/higgsfield_blender/wheels').glob('blender_mcp-*-py3-none-any.whl'))
    if len(wheels) != 1:
        raise RuntimeError('One installed Blender MCP wheel required') from error
    # The socket helper itself only uses the standard library. Loading the full
    # package initializer would unnecessarily require the MCP server's YAML stack.
    member = 'blmcp/tools_helpers/connection.py'
    with zipfile.ZipFile(wheels[0]) as archive:
        source = archive.read(member)
    namespace = {'__name__': 'higgsfield_bundled_socket'}
    exec(compile(source, str(wheels[0]) + '/' + member, 'exec'), namespace)
    send_code = namespace['send_code']
