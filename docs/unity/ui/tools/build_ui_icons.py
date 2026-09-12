"""Rebuild owned UI pictograms. Pillow only; no editor, network or GPU."""
from pathlib import Path
import uuid
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'unity/Assets/LetMeSleep/UI/Resources/AlfaUiIcons'
S = 4

def meta(path, folder=False):
    target = Path(str(path) + '.meta')
    if target.exists(): return
    text = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    if folder: text += 'folderAsset: yes\nDefaultImporter:\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else: text += 'TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n  isReadable: 0\n  textureSettings:\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n  maxTextureSize: 128\n  textureCompression: 0\n  alphaIsTransparency: 1\n  textureType: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target.write_text('\n'.join(line.rstrip() for line in text.splitlines()) + '\n', encoding='utf-8')

for folder in [OUT.parent, OUT]:
    folder.mkdir(parents=True, exist_ok=True)
    meta(folder, True)

for kind in ['Online','Human','Customize','Mosquito','Audio','Video','Controls','Training','Back','Exit','Settings','Play','Ready']:
    im = Image.new('RGBA', (128*S,128*S))
    d = ImageDraw.Draw(im)
    def poly(points): d.polygon([(int(x*S),int(y*S)) for x,y in points],fill='white')
    def ellipse(box): d.ellipse(tuple(int(v*S) for v in box),fill='white')
    def line(points, width=8): d.line([(int(x*S),int(y*S)) for x,y in points], fill='white', width=width*S, joint='curve')
    def rect(box, radius=0): d.rounded_rectangle(tuple(int(v*S) for v in box),radius=radius*S,fill='white')
    def head(cx, cy, r): ellipse((cx-r,cy-r,cx+r,cy+r))
    def bust(cx, y, scale=1):
        head(cx,y,15*scale)
        d.rounded_rectangle(tuple(int(v*S) for v in (cx-27*scale,y+21*scale,cx+27*scale,y+57*scale)),radius=int(12*scale*S),fill='white')
    if kind=='Human': bust(64,34)
    elif kind=='Online':
        bust(36,40,.75); bust(94,40,.75); bust(65,26,.9)
    elif kind=='Customize':
        poly([(42,20),(51,28),(64,31),(77,28),(86,20),(115,44),(99,64),(86,53),(86,108),(42,108),(42,53),(29,64),(13,44)])
    elif kind=='Mosquito':
        # Two broad wings, eyes, abdomen, proboscis and six separate legs.
        poly([(55,57),(13,22),(5,22),(8,38),(45,68)])
        poly([(73,57),(115,22),(123,22),(120,38),(83,68)])
        ellipse((54,48,74,75)); poly([(54,71),(74,71),(64,107)])
        head(56,43,10); head(72,43,10); line([(64,34),(64,11)],5)
        for y, end in [(61,55),(71,84),(81,114)]:
            line([(58,y),(35,y+6),(22,end)],5)
            line([(70,y),(93,y+6),(106,end)],5)
    elif kind=='Audio':
        rect((13,47,37,81),4); poly([(34,46),(64,23),(64,105),(34,82)])
        d.arc((56*S,35*S,97*S,93*S),-65,65,fill='white',width=7*S)
        d.arc((54*S,18*S,117*S,110*S),-65,65,fill='white',width=7*S)
    elif kind=='Video':
        line([(15,24),(113,24),(113,88),(15,88),(15,24)],9)
        rect((57,89,71,104)); rect((37,102,91,111),3)
    elif kind=='Controls':
        d.rounded_rectangle((14*S,31*S,114*S,96*S),radius=18*S,fill='white')
        # Transparent cutouts read as a directional pad and face buttons.
        d.rectangle((29*S,58*S,57*S,66*S),fill=(0,0,0,0)); d.rectangle((39*S,48*S,47*S,76*S),fill=(0,0,0,0))
        for x,y in [(88,53),(100,68)]: d.ellipse(((x-5)*S,(y-5)*S,(x+5)*S,(y+5)*S),fill=(0,0,0,0))
    elif kind=='Training':
        d.ellipse((17*S,17*S,111*S,111*S),outline='white',width=9*S)
        d.ellipse((37*S,37*S,91*S,91*S),outline='white',width=8*S)
        head(64,64,10)
    elif kind=='Settings':
        for y,x in [(30,83),(64,43),(98,76)]:
            line([(17,y),(111,y)],7); rect((x-7,y-12,x+7,y+12),4)
    elif kind=='Back': poly([(13,64),(51,25),(51,48),(115,48),(115,80),(51,80),(51,103)])
    elif kind=='Exit':
        line([(61,17),(19,17),(19,111),(61,111)],8)
        poly([(72,39),(115,64),(72,89),(72,73),(43,73),(43,55),(72,55)])
    elif kind=='Play': poly([(34,15),(112,64),(34,113)])
    elif kind=='Ready': line([(17,66),(49,98),(112,25)],14)
    dest = OUT / (kind+'.png')
    im.resize((128,128),Image.Resampling.LANCZOS).save(dest)
    meta(dest)
print('Generated 13 RGBA UI icons and preserved existing GUIDs.')
