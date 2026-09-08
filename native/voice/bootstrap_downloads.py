from pathlib import Path
import concurrent.futures, hashlib, json, shutil, subprocess, tarfile, urllib.request, zipfile
ROOT=Path(__file__).resolve().parent
DOWNLOADS=ROOT/'_downloads'
TOOLS=ROOT/'_tools'
DEPS=ROOT/'deps'
for p in (DOWNLOADS,TOOLS,DEPS): p.mkdir(parents=True,exist_ok=True)
items=[
 {'name':'llvm-mingw-20260826-ucrt-x86_64.zip','url':'https://github.com/mstorsjo/llvm-mingw/releases/download/20260826/llvm-mingw-20260826-ucrt-x86_64.zip','sha256':'ae601f4e0f72bbdf441ad2df8bb16f037e2e9251559ea6b37b4057aef39c06c3','kind':'zip','target':'_tools'},
 {'name':'cmake-4.4.3-windows-x86_64.zip','url':'https://github.com/Kitware/CMake/releases/download/v4.4.3/cmake-4.4.3-windows-x86_64.zip','sha256':'4d52ebab7193a698651639ed80d8d04fd903358843572cf44c7fd234cb7c26ab','kind':'zip','target':'_tools'},
 {'name':'ninja-win.zip','url':'https://github.com/ninja-build/ninja/releases/download/v1.13.2/ninja-win.zip','sha256':'07fc8261b42b20e71d1720b39068c2e14ffcee6396b76fb7a795fb460b78dc65','kind':'zip','target':'_tools/ninja-1.13.2'},
 {'name':'opus-1.6.1.tar.gz','url':'https://downloads.xiph.org/releases/opus/opus-1.6.1.tar.gz','sha256':'6ffcb593207be92584df15b32466ed64bbec99109f007c82205f0194572411a1','kind':'tar','target':'deps/opus'}]

def digest(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for block in iter(lambda:f.read(1024*1024),b''): h.update(block)
 return h.hexdigest()

def prepare(item):
 archive=DOWNLOADS/item['name']
 if not archive.exists() or digest(archive)!=item['sha256']:
  part=archive.with_suffix(archive.suffix+'.part')
  req=urllib.request.Request(item['url'],headers={'User-Agent':'LetMeSleep-build-bootstrap'})
  try:
   with urllib.request.urlopen(req,timeout=90) as src, part.open('wb') as dst: shutil.copyfileobj(src,dst,1024*1024)
  except OSError:
   # Windows curl uses the OS certificate store; never disable certificate verification.
   subprocess.run(['curl.exe','--silent','--show-error','--fail','--location','--retry','2','--output',str(part),item['url']],check=True)
  actual=digest(part)
  if actual!=item['sha256']: raise RuntimeError('Digest mismatch: '+item['name'])
  part.replace(archive)
 target=(ROOT/item['target']).resolve()
 if not target.is_relative_to(ROOT.resolve()): raise RuntimeError('Target outside bootstrap')
 target.mkdir(parents=True,exist_ok=True)
 marker=target/('.extracted-'+item['sha256'])
 if not marker.exists():
  if item['kind']=='zip':
   with zipfile.ZipFile(archive) as bundle:
    for member in bundle.infolist():
     destination=(target/member.filename).resolve()
     if not destination.is_relative_to(target): raise RuntimeError('Unsafe archive member')
    bundle.extractall(target)
  else:
   with tarfile.open(archive,'r:gz') as bundle:
    for member in bundle.getmembers():
     components=Path(member.name).parts
     if len(components)<=1: continue
     destination=(target/Path(*components[1:])).resolve()
     if not destination.is_relative_to(target): raise RuntimeError('Unsafe archive member')
     if member.isdir(): destination.mkdir(parents=True,exist_ok=True)
     elif member.isfile():
      destination.parent.mkdir(parents=True,exist_ok=True)
      with bundle.extractfile(member) as src, destination.open('wb') as dst: shutil.copyfileobj(src,dst)
     else: raise RuntimeError('Unsupported link in dependency archive')
  marker.write_text(item['sha256']+'\n',encoding='utf-8')
 result=dict(item,archive_bytes=archive.stat().st_size,verified_sha256=digest(archive),extracted_to=str(target))
 print(json.dumps({'prepared':item['name'],'verified':True}),flush=True)
 return result
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
 results=list(pool.map(prepare,items))
(ROOT/'dependency-downloads.json').write_text(json.dumps({'artifacts':results,'source':'Official upstream releases; tool digests from GitHub API, Opus digest from opus-codec.org/downloads/'},indent=2)+'\n',encoding='utf-8')
print('BOOTSTRAP_DOWNLOADS_DONE',flush=True)