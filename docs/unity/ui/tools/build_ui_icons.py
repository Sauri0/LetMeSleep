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

ALFA_KINDS = ['Online','Human','Customize','Mosquito','Audio','Video','Controls','Training','Back','Exit','Settings','Play','Ready']
# v0.3 (UI-06) pictograms. Same conventions: white RGBA silhouettes, 128 px, tinted at runtime.
V030_KINDS = ['Gear','House','Enter','Map','Lock','Wifi','Warning','Invite','Crown','Refresh','ChevronLeft','ChevronRight',
              'ChevronDown','Key','Microphone','Info','Copy','Clock','Close']
# v0.3 stage 2: equipment slots (were procedural bars), customization categories, settings tabs and HUD.
V030_STAGE2_KINDS = ['Hands','Flyswatter','Slipper','ElectricRacket','Aerosol','Blood','Palette','Dice','Undo','Mouse','Eye',
                     'Wings','Proboscis','Hat','Face','Accessibility','Trophy','Bolt']
# v0.3 stage 2, art-direction pass: human health/lives heart (UI-06 HUD '100/100', mosquito 'VIDAS').
V030_STAGE2B_KINDS = ['Heart']
import sys, math
REQUESTED = [a for a in sys.argv[1:] if not a.startswith('-')] or (ALFA_KINDS + V030_KINDS + V030_STAGE2_KINDS + V030_STAGE2B_KINDS)
CLEAR = (0,0,0,0)
for kind in REQUESTED:
    im = Image.new('RGBA', (128*S,128*S))
    d = ImageDraw.Draw(im)
    def poly(points, fill='white'): d.polygon([(int(x*S),int(y*S)) for x,y in points],fill=fill)
    def ellipse(box, fill='white'): d.ellipse(tuple(int(v*S) for v in box),fill=fill)
    def line(points, width=8, fill='white'): d.line([(int(x*S),int(y*S)) for x,y in points], fill=fill, width=width*S, joint='curve')
    def rect(box, radius=0, fill='white'): d.rounded_rectangle(tuple(int(v*S) for v in box),radius=radius*S,fill=fill)
    def head(cx, cy, r, fill='white'): ellipse((cx-r,cy-r,cx+r,cy+r), fill)
    def oval(cx, cy, rx, ry, angle, fill='white', steps=48):
        a = math.radians(angle)
        pts = [(cx + rx*math.cos(t)*math.cos(a) - ry*math.sin(t)*math.sin(a),
                cy + rx*math.cos(t)*math.sin(a) + ry*math.sin(t)*math.cos(a))
               for t in (2*math.pi*i/steps for i in range(steps))]
        poly(pts, fill)
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
    elif kind=='Gear':
        import math
        teeth = []
        for i in range(16):
            a = math.pi*2*i/16 - math.pi/16
            r = 54 if i % 2 == 0 else 42
            teeth.append((64+r*math.cos(a), 64+r*math.sin(a)))
        # Wide teeth: expand each outer vertex into a flat top.
        pts = []
        for i in range(8):
            a0 = math.pi*2*i/8 - 0.22; a1 = math.pi*2*i/8 + 0.22
            b0 = math.pi*2*i/8 + 0.40; b1 = math.pi*2*(i+1)/8 - 0.40
            pts += [(64+56*math.cos(a0),64+56*math.sin(a0)),(64+56*math.cos(a1),64+56*math.sin(a1)),
                    (64+43*math.cos(b0),64+43*math.sin(b0)),(64+43*math.cos(b1),64+43*math.sin(b1))]
        poly(pts); head(64,64,38)
        d.ellipse((46*S,46*S,82*S,82*S),fill=(0,0,0,0))
    elif kind=='House':
        poly([(64,12),(118,58),(104,58),(104,114),(24,114),(24,58),(10,58)])
        d.rounded_rectangle((52*S,74*S,76*S,114*S),radius=4*S,fill=(0,0,0,0))
    elif kind=='Enter':
        line([(70,16),(108,16),(108,112),(70,112)],10)
        poly([(58,34),(92,64),(58,94),(58,76),(14,76),(14,52),(58,52)])
    elif kind=='Map':
        poly([(10,26),(44,12),(84,28),(118,14),(118,102),(84,116),(44,100),(10,114)])
        line([(44,16),(44,98)],6); line([(84,30),(84,112)],6)
        d.line([(44*S,16*S),(44*S,98*S)],fill=(0,0,0,0),width=5*S); d.line([(84*S,30*S),(84*S,112*S)],fill=(0,0,0,0),width=5*S)
    elif kind=='Lock':
        d.arc((34*S,10*S,94*S,76*S),180,360,fill='white',width=12*S)
        line([(40,44),(40,58)],12); line([(88,44),(88,58)],12)
        rect((22,54,106,118),12)
        d.ellipse((56*S,72*S,72*S,88*S),fill=(0,0,0,0)); d.rectangle((60*S,82*S,68*S,102*S),fill=(0,0,0,0))
    elif kind=='Wifi':
        for box in [(6,20,122,136),(28,44,100,116),(48,66,80,98)]:
            d.arc(tuple(v*S for v in box),222,318,fill='white',width=12*S)
        head(64,100,10)
    elif kind=='Warning':
        d.line([(64*S,12*S),(118*S,110*S),(10*S,110*S),(64*S,12*S)],fill='white',width=12*S,joint='curve')
        for x,y in [(64,12),(118,110),(10,110)]: head(x,y,6)
        rect((57,42,71,82),6); head(64,95,8)
    elif kind=='Invite':
        head(46,36,17); d.rounded_rectangle((16*S,58*S,76*S,106*S),radius=16*S,fill='white')
        head(80,40,13); d.rounded_rectangle((60*S,58*S,104*S,96*S),radius=12*S,fill='white')
        d.ellipse((84*S,64*S,126*S,106*S),fill=(0,0,0,0))
        rect((100,70,110,100),3); rect((90,80,120,90),3)
    elif kind=='Crown':
        poly([(12,42),(40,68),(64,22),(88,68),(116,42),(104,98),(24,98)])
        rect((24,104,104,116),4)
        for x,y in [(12,38),(64,18),(116,38)]: head(x,y,9)
    elif kind=='Refresh':
        d.arc((18*S,18*S,110*S,110*S),20,300,fill='white',width=13*S)
        poly([(88,6),(118,40),(78,46)])
    elif kind=='ChevronLeft': line([(82,18),(38,64),(82,110)],16)
    elif kind=='ChevronRight': line([(46,18),(90,64),(46,110)],16)
    elif kind=='ChevronDown': line([(18,44),(64,88),(110,44)],16)
    elif kind=='Key':
        head(40,64,28); d.ellipse((28*S,52*S,52*S,76*S),fill=(0,0,0,0))
        rect((62,56,120,72),4); rect((96,70,108,92),3); rect((110,70,120,86),3)
    elif kind=='Microphone':
        d.rounded_rectangle((44*S,8*S,84*S,78*S),radius=20*S,fill='white')
        d.arc((26*S,34*S,102*S,96*S),0,180,fill='white',width=9*S)
        rect((59,92,69,112)); rect((38,108,90,118),4)
    elif kind=='Info':
        d.ellipse((10*S,10*S,118*S,118*S),outline='white',width=10*S)
        head(64,38,8); rect((57,54,71,96),5)
    elif kind=='Copy':
        d.rounded_rectangle((14*S,12*S,82*S,90*S),radius=12*S,outline='white',width=10*S)
        d.rounded_rectangle((42*S,38*S,116*S,118*S),radius=12*S,fill='white')
    elif kind=='Close':
        line([(26,26),(102,102)],15); line([(102,26),(26,102)],15)
        for x,y in [(26,26),(102,102),(102,26),(26,102)]: head(x,y,7)
    elif kind=='Clock':
        d.ellipse((10*S,10*S,118*S,118*S),outline='white',width=11*S)
        line([(64,64),(64,30)],10); line([(64,64),(88,78)],10); head(64,64,8)
    # --- stage 2 -------------------------------------------------------------------------------------------
    elif kind=='Hands':
        # Open palm: rounded palm, four fingers and a thumb.
        rect((34,58,96,116),20)
        for x, top in [(36,26),(52,14),(68,16),(84,28)]:
            rect((x,top,x+13,78),7)
        line([(40,90),(16,64)],15); head(16,64,7)
    elif kind=='Flyswatter':
        # Perforated square head on a diagonal handle.
        line([(16,114),(58,72)],11); head(16,114,6)
        rect((56,8,120,72),12)
        for gx in range(3):
            for gy in range(3):
                head(71+gx*17, 23+gy*17, 5, CLEAR)
        line([(58,72),(66,64)],13)
    elif kind=='Slipper':
        # Top view of a slipper: footprint-shaped sole and a wide strap across the instep.
        def rot(x, y, a=-38, cx=64, cy=64):
            ra = math.radians(a); x -= cx; y -= cy
            return (cx + x*math.cos(ra) - y*math.sin(ra), cy + x*math.sin(ra) + y*math.cos(ra))
        sole = [rot(64 + 56*math.cos(t), 64 + (28 if math.cos(t) > 0 else 21)*math.sin(t)) for t in (2*math.pi*i/64 for i in range(64))]
        poly(sole)
        strap = [rot(76 + 17*math.cos(t), 64 + 36*math.sin(t)) for t in (2*math.pi*i/48 for i in range(48))]
        for dx in (-19, 19):
            d.line([tuple(int(v*S) for v in rot(76 + dx, 64 - 40)), tuple(int(v*S) for v in rot(76 + dx, 64 + 40))], fill=CLEAR, width=6*S)
        poly(strap)
        for dx in (-17, 17):
            d.line([tuple(int(v*S) for v in rot(76 + dx, 64 - 26)), tuple(int(v*S) for v in rot(76 + dx, 64 + 26))], fill=CLEAR, width=3*S)
    elif kind=='ElectricRacket':
        # Oval racket frame, handle and a lightning bolt inside.
        oval(70,48,44,38,-30)
        oval(70,48,33,27,-30,CLEAR)
        line([(40,84),(16,114)],12); head(16,114,6)
        poly([(76,20),(58,52),(70,52),(62,78),(86,42),(73,42),(82,20)])
    elif kind=='Aerosol':
        # Spray can with cap, nozzle and a mist of dots.
        rect((26,40,78,120),12); rect((34,24,70,42),6)
        rect((64,18,84,28),4)
        for x, y, r in [(98,16,6),(110,30,5),(100,40,5),(114,10,4),(118,46,4)]: head(x,y,r)
        rect((26,64,78,72),0,CLEAR)
    elif kind=='Blood':
        # Drop.
        poly([(64,8),(98,64),(30,64)]); head(64,78,36)
        head(50,84,8,CLEAR)
    elif kind=='Palette':
        # Painter's palette with a thumb hole and four paint wells.
        head(64,64,54)
        head(86,88,13,CLEAR); poly([(86,100),(118,122),(128,96),(98,82)],CLEAR)
        for x, y in [(40,40),(64,28),(88,40),(34,68)]: head(x,y,9,CLEAR)
    elif kind=='Dice':
        rect((14,14,114,114),22)
        for x, y in [(38,38),(90,38),(64,64),(38,90),(90,90)]: head(x,y,10,CLEAR)
    elif kind=='Undo':
        d.arc((26*S,26*S,112*S,112*S),200,110,fill='white',width=13*S)
        poly([(6,52),(44,30),(46,72)])
    elif kind=='Mouse':
        rect((32,8,96,120),32)
        rect((40,16,88,112),26,CLEAR)
        line([(64,12),(64,58)],6); line([(34,58),(94,58)],6)
        rect((58,26,70,46),6)
    elif kind=='Eye':
        # Almond from two circle arcs through the eye corners, then iris and highlight.
        k = 48; r = math.hypot(58, k)
        upper = [(64 + r*math.cos(t), 64 + k - r*math.sin(t)) for t in
                 [math.acos(58/r) + (math.acos(-58/r) - math.acos(58/r))*i/24 for i in range(25)]]
        lower = [(x, 128 - y) for x, y in reversed(upper)]
        poly(upper + lower)
        head(64,64,25,CLEAR); head(64,64,18); head(70,58,5,CLEAR)
    elif kind=='Wings':
        # Two faceted wings on a small body, as the sketch's mosquito wing styles.
        poly([(56,72),(20,6),(4,24),(8,62),(38,90)])
        poly([(72,72),(108,6),(124,24),(120,62),(90,90)])
        line([(54,70),(18,16)],4,CLEAR); line([(74,70),(110,16)],4,CLEAR)
        line([(40,84),(10,44)],3,CLEAR); line([(88,84),(118,44)],3,CLEAR)
        oval(64,92,11,26,0)
    elif kind=='Proboscis':
        # Front view of the mosquito head: two huge eyes over a long, thin proboscis.
        oval(64,42,50,32,0)
        head(42,40,17,CLEAR); head(86,40,17,CLEAR); head(46,44,7); head(82,44,7)
        poly([(56,66),(72,66),(66,124),(62,124)])
        line([(46,14),(30,2)],5); line([(82,14),(98,2)],5)
    elif kind=='Hat':
        # Sleeping cap (nightcap) with its pompom.
        poly([(14,96),(40,40),(70,18),(98,26),(112,50),(84,40),(96,96)])
        rect((8,92,104,114),10)
        head(114,58,12)
    elif kind=='Face':
        head(64,64,54)
        head(44,54,9,CLEAR); head(84,54,9,CLEAR)
        d.arc((38*S,56*S,90*S,98*S),20,160,fill=CLEAR,width=8*S)
    elif kind=='Accessibility':
        d.ellipse((6*S,6*S,122*S,122*S),outline='white',width=9*S)
        head(64,32,9)
        line([(32,48),(96,48)],9); line([(64,48),(64,78)],10)
        line([(64,76),(46,104)],9); line([(64,76),(82,104)],9)
    elif kind=='Trophy':
        poly([(28,14),(100,14),(94,58),(64,76),(34,58)])
        d.arc((6*S,18*S,46*S,62*S),90,270,fill='white',width=9*S)
        d.arc((82*S,18*S,122*S,62*S),270,90,fill='white',width=9*S)
        rect((56,72,72,98)); rect((32,96,96,116),6)
    elif kind=='Bolt':
        poly([(76,6),(24,72),(58,72),(46,122),(104,50),(70,50),(90,6)])
    elif kind=='Heart':
        # Two lobes and a point, with a small highlight cut-out.
        head(40,44,30); head(88,44,30)
        poly([(12,52),(116,52),(64,116)])
        head(34,36,8,CLEAR)
    dest = OUT / (kind+'.png')
    im.resize((128,128),Image.Resampling.LANCZOS).save(dest)
    meta(dest)
print('Generated %d RGBA UI icons (%s) and preserved existing GUIDs.' % (len(REQUESTED), ', '.join(REQUESTED)))
