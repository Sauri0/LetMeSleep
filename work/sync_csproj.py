"""Refresh <Compile Include> lists of Unity-generated .csproj files without opening Unity.

Unity writes explicit file lists into the (untracked) .csproj files, so .cs files added on
other branches are missing until Unity regenerates them. This script maps each csproj to the
folder of its .asmdef (or Assets/ for Assembly-CSharp*) and rewrites the Compile items with every
.cs file under that folder that is not owned by a nested .asmdef. Usage:

    python work/sync_csproj.py <unity-project-dir>
"""
import re
import sys
from pathlib import Path

project = Path(sys.argv[1] if len(sys.argv) > 1 else 'unity').resolve()
assets = project / 'Assets'

asmdefs = {}
for asmdef in assets.rglob('*.asmdef'):
    name = re.search(r'"name"\s*:\s*"([^"]+)"', asmdef.read_text(encoding='utf-8-sig'))
    if name:
        asmdefs[name.group(1)] = asmdef.parent
owned_roots = set(asmdefs.values())


def owner(path):
    for parent in path.parents:
        if parent in owned_roots:
            return parent
        if parent == assets:
            return None
    return None


def files_for(root, editor_only):
    out = []
    for cs in sorted(root.rglob('*.cs')):
        own = owner(cs)
        if root in owned_roots:
            if own != root:
                continue
        else:
            if own is not None:
                continue
            in_editor = 'Editor' in cs.relative_to(assets).parts
            if in_editor != editor_only:
                continue
        out.append(cs)
    return out


changed = 0
for csproj in sorted(project.glob('*.csproj')):
    name = csproj.stem
    if name in asmdefs:
        wanted = files_for(asmdefs[name], False)
    elif name == 'Assembly-CSharp':
        wanted = files_for(assets, False)
    elif name == 'Assembly-CSharp-Editor':
        wanted = files_for(assets, True)
    else:
        continue
    text = csproj.read_text(encoding='utf-8-sig')
    items = ''.join('    <Compile Include="%s" />\r\n' % str(p.relative_to(project)).replace('/', '\\') for p in wanted)
    pattern = re.compile(r'(  <ItemGroup>\r?\n)((?:    <Compile Include="[^"]+" />\r?\n)+)(  </ItemGroup>)')
    match = pattern.search(text)
    if not match:
        continue
    if match.group(2).replace('\r\n', '\n') != items.replace('\r\n', '\n'):
        text = text[:match.start(2)] + items + text[match.end(2):]
        csproj.write_text(text, encoding='utf-8')
        changed += 1
        print('updated', csproj.name, len(wanted), 'files')
print('sync_csproj done, changed', changed)
