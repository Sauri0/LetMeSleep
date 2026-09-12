"""Compile owned character assemblies against installed Unity references without opening an editor."""
import json
import subprocess
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[2]
DATA=Path('N:/Unity/Editors/6000.3.24f1/Editor/Data')
OUT=ROOT/'.validation'
OUT.mkdir(exist_ok=True)
dotnet=DATA/'NetCoreRuntime/dotnet.exe'
csc=DATA/'DotNetSdkRoslyn/csc.dll'
references=[DATA/'NetStandard/ref/2.1.0/netstandard.dll']
references+=list((DATA/'Managed/UnityEngine').glob('UnityEngine*.dll'))
editor_references=list((DATA/'Managed').glob('UnityEditor*.dll'))
runtime=REPO/'unity/Assets/LetMeSleep/Content/Characters'
editor=REPO/'unity/Assets/LetMeSleep/Content/Editor/Characters'
results=[]
for name,folder,extra in [('LetMeSleep.Content.Characters',runtime,[]),
                          ('LetMeSleep.Content.Characters.Editor',editor,editor_references+[OUT/'LetMeSleep.Content.Characters.dll'])]:
    command=[str(dotnet),str(csc),'/nologo','/target:library','/nostdlib+','/langversion:9.0','/define:UNITY_EDITOR',
             '/out:'+str(OUT/(name+'.dll'))]
    command+=['/reference:'+str(p) for p in references+extra]
    command+=[str(p) for p in folder.rglob('*.cs')]
    result=subprocess.run(command,capture_output=True,text=True,timeout=60,creationflags=subprocess.CREATE_NO_WINDOW)
    results.append({'assembly':name,'exit_code':result.returncode,'output':result.stdout+result.stderr})
    print(name, result.returncode, result.stdout, result.stderr)
    if result.returncode: break
(ROOT/'unity_compile.json').write_text(json.dumps({'scope':'C# compiler against Unity 6000.3.24f1 installed reference assemblies; editor import and runtime not executed',
                                                'source_sha256':{p.relative_to(REPO).as_posix():hashlib.sha256(p.read_bytes()).hexdigest()
                                                                 for folder in [runtime,editor] for p in folder.rglob('*.cs')},
                                                'results':results},indent=2),encoding='utf8',newline='\n')
assert len(results)==2 and all(r['exit_code']==0 for r in results), 'Character compilation failed'
