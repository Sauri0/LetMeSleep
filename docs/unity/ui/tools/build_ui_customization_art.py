"""Colour art for the customization screen (UI-06 screens 5-6, PER-08). Pillow only; no editor, network or GPU.

Writes
  unity/Assets/LetMeSleep/UI/Resources/AlfaUiOptionArt/<Name>.png   256 px option thumbnails, full colour on
      transparency (wing styles, eye styles, proboscis styles, body builds). The UI shows them untinted on the option
      cards when the catalogue supplies no thumbnail of its own (AlfaOptionStyle picks one from the option id/label).
  unity/Assets/LetMeSleep/UI/Resources/AlfaUiBackdrops/PreviewBedroom.png   1600 x 1000 painted warm bedroom behind
      the 3D viewer (cream wall, wardrobe, #FFB347 lamp with halo, #8B5A2B wooden floor, blue rug), flat low-poly style.

    python build_ui_customization_art.py
Existing .meta files (and their GUIDs) are preserved.
"""
from pathlib import Path
import math
import uuid
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[4]
RES = ROOT / 'unity/Assets/LetMeSleep/UI/Resources'
ART = RES / 'AlfaUiOptionArt'
BACK = RES / 'AlfaUiBackdrops'
S = 4  # supersampling


def meta(path, folder=False, max_size=256, mips=0, alpha=1):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    text = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    if folder:
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        text += ('TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: %d\n  isReadable: 0\n'
                 '  textureSettings:\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n'
                 '  maxTextureSize: %d\n  textureCompression: 0\n  alphaIsTransparency: %d\n  textureType: 0\n'
                 '  userData: \n  assetBundleName: \n  assetBundleVariant: \n') % (mips, max_size, alpha)
    target.write_text(text, encoding='utf-8')


def hexc(value, alpha=255):
    value = value.lstrip('#')
    return (int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha)


class Canvas:
    def __init__(self, w, h, background=(0, 0, 0, 0)):
        self.w, self.h = w, h
        self.im = Image.new('RGBA', (w * S, h * S), background)
        self.d = ImageDraw.Draw(self.im)

    def p(self, pts):
        return [(x * S, y * S) for x, y in pts]

    def poly(self, pts, fill, outline=None, width=0):
        self.d.polygon(self.p(pts), fill=fill)
        if outline:
            self.line(list(pts) + [pts[0]], outline, width)

    def line(self, pts, fill, width):
        self.d.line(self.p(pts), fill=fill, width=int(width * S), joint='curve')

    def ellipse(self, box, fill, outline=None, width=0):
        self.d.ellipse([v * S for v in box], fill=fill, outline=outline, width=int(width * S))

    def rect(self, box, fill, radius=0, outline=None, width=0):
        self.d.rounded_rectangle([v * S for v in box], radius=radius * S, fill=fill, outline=outline, width=int(width * S))

    def layer(self):
        return Canvas(self.w, self.h)

    def paste(self, other, opacity=1.0):
        src = other.im
        if opacity < 1.0:
            a = src.split()[3].point(lambda v: int(v * opacity))
            src = src.copy(); src.putalpha(a)
        self.im = Image.alpha_composite(self.im, src)
        self.d = ImageDraw.Draw(self.im)

    def save(self, path):
        self.im.resize((self.w, self.h), Image.Resampling.LANCZOS).save(path)


INK = hexc('2A1A1A')
WING = hexc('DCDDF5', 215)
WING_EDGE = hexc('7E81B4')
VEIN = hexc('9A9DD0')
RED = hexc('B8262B')
RED_DARK = hexc('6E1418')
WHITE = hexc('FFFFFF')
PUPIL = hexc('14101A')


def wing_pair(c, shape):
    """Two wings in a V over a tiny thorax, as the UI-06 'ESTILO DE ALAS' thumbnails."""
    cx, cy = 128, 170
    stem = None
    def mirror(pts):
        return [(2 * cx - x, y) for x, y in pts]
    if shape == 'Faceted':
        left = [(cx - 8, cy - 6), (58, 40), (30, 58), (22, 104), (56, 150), (cx - 14, cy + 10)]
        veins = [[(cx - 8, cy - 6), (30, 58)], [(cx - 10, cy), (22, 104)], [(58, 40), (56, 150)]]
    elif shape == 'Round':
        # Broad rounded lobe: an ellipse rotated outwards, joined to the thorax.
        ex, ey, rx, ry, rot = cx - 62, cy - 70, 50, 66, math.radians(-38)
        lobe = [(ex + rx * math.cos(t) * math.cos(rot) - ry * math.sin(t) * math.sin(rot),
                 ey + rx * math.cos(t) * math.sin(rot) + ry * math.sin(t) * math.cos(rot))
                for t in [i * 2 * math.pi / 48 for i in range(48)]]
        left = lobe
        stem = [(cx - 8, cy - 4), (ex + 20, ey + 30), (ex + 34, ey + 44), (cx - 14, cy + 8)]
        veins = [[(cx - 10, cy - 8), (ex - 10, ey - 30)], [(cx - 12, cy - 4), (ex - 40, ey + 20)]]
    elif shape == 'Long':
        left = [(cx - 8, cy - 4), (74, 22), (58, 14), (52, 26), (cx - 18, cy + 6)]
        veins = [[(cx - 10, cy - 2), (60, 22)]]
    else:  # Short
        left = [(cx - 8, cy - 4), (86, 86), (60, 96), (56, 124), (84, 150), (cx - 14, cy + 8)]
        veins = [[(cx - 10, cy), (62, 110)]]
    for pts in (left, mirror(left)):
        c.poly(pts, WING, WING_EDGE, 3.2)
    if stem:
        for pts in (stem, mirror(stem)):
            c.poly(pts, WING, WING_EDGE, 3.2)
    for v in veins:
        c.line(v, VEIN, 2.6)
        c.line([(2 * cx - x, y) for x, y in v], VEIN, 2.6)
    # thorax + head stub so the pair reads as mosquito wings
    c.ellipse((cx - 16, cy - 8, cx + 16, cy + 34), RED, hexc('5A1014'), 2.5)
    c.ellipse((cx - 10, cy + 26, cx + 10, cy + 46), RED_DARK)


def eye_pair(c, style):
    """Head front with two eyes, as UI-06 'OJOS'."""
    c.ellipse((34, 70, 222, 206), RED, hexc('5A1014'), 3)
    r = {'Big': 50, 'Small': 32, 'Angry': 46, 'Sleepy': 46}[style]
    for ex in (92, 164):
        ey = 132
        c.ellipse((ex - r, ey - r, ex + r, ey + r), WHITE, hexc('3A2A2A'), 3)
        pr = r * (0.36 if style != 'Small' else 0.42)
        px = ex + (6 if ex < 128 else -6)
        c.ellipse((px - pr, ey - pr + 4, px + pr, ey + pr + 4), PUPIL)
        c.ellipse((px - pr * .35 - 4, ey - pr * .6, px - pr * .35 + 6, ey - pr * .6 + 10), WHITE)
        if style == 'Angry':
            # slanted lids down towards the centre
            inner = ex + (r if ex < 128 else -r)
            outer = ex - (r if ex < 128 else -r)
            c.poly([(outer - 6, ey - r - 8), (inner + 4, ey - r * .15), (inner + 4, ey - r - 14), (outer - 6, ey - r - 14)], RED_DARK)
            c.line([(outer - 4, ey - r - 2), (inner + 2, ey - r * .2)], INK, 5)
        elif style == 'Sleepy':
            c.poly([(ex - r - 4, ey - r - 6), (ex + r + 4, ey - r - 6), (ex + r + 4, ey + 2), (ex - r - 4, ey + 2)], RED_DARK)
            c.line([(ex - r, ey + 2), (ex + r, ey + 2)], INK, 5)


def proboscis(c, style):
    """Head in profile with the proboscis (UI-06 'PROBÓSCIDE')."""
    c.ellipse((40, 60, 150, 160), RED, hexc('5A1014'), 3)
    c.ellipse((84, 66, 146, 128), WHITE, hexc('3A2A2A'), 3)
    c.ellipse((112, 86, 136, 110), PUPIL)
    base = (144, 132)
    if style == 'Standard':
        c.line([base, (232, 188)], INK, 7)
    elif style == 'Long':
        c.line([base, (248, 214)], INK, 6)
    elif style == 'Short':
        c.line([base, (196, 164)], INK, 9)
    else:  # Curved
        pts = [(base[0] + 80 * t, base[1] + 30 * t + 70 * t * t) for t in [i / 12 for i in range(13)]]
        c.line(pts, INK, 7)


def body(c, build):
    """Side silhouette of the mosquito body (thorax + abdomen + legs)."""
    fat = {'Standard': 1.0, 'Robust': 1.35, 'Slim': 0.72}[build]
    for i, x in enumerate((96, 120, 146)):
        c.line([(x, 150), (x - 18 + i * 10, 196), (x - 30 + i * 16, 236)], INK, 4)
    c.ellipse((70, 96, 138, 162), RED, hexc('5A1014'), 3)
    ax, ay = 150, 138
    pts = [(ax + 88 * math.cos(t), ay + 30 * fat * math.sin(t)) for t in [i * 2 * math.pi / 40 for i in range(40)]]
    pts = [(x, y) for x, y in pts]
    c.poly(pts, RED, hexc('5A1014'), 3)
    for k in range(1, 4):
        x = ax - 50 + k * 30
        c.line([(x, ay - 26 * fat), (x - 6, ay + 26 * fat)], RED_DARK, 5)
    c.ellipse((40, 92, 84, 136), RED, hexc('5A1014'), 3)
    c.ellipse((44, 94, 72, 122), WHITE)
    c.ellipse((50, 102, 62, 114), PUPIL)
    c.line([(46, 124), (8, 158)], INK, 4)


OPTION_ART = {}
for shape in ('Faceted', 'Round', 'Long', 'Short'):
    OPTION_ART['Wings' + shape] = lambda c, s=shape: wing_pair(c, s)
for style in ('Big', 'Small', 'Angry', 'Sleepy'):
    OPTION_ART['Eyes' + style] = lambda c, s=style: eye_pair(c, s)
for style in ('Standard', 'Curved', 'Short', 'Long'):
    OPTION_ART['Proboscis' + style] = lambda c, s=style: proboscis(c, s)
for build in ('Standard', 'Robust', 'Slim'):
    OPTION_ART['Body' + build] = lambda c, b=build: body(c, b)


def bedroom():
    W, H = 1600, 1000
    c = Canvas(W, H, hexc('2A1D16'))
    floor_y = 610
    # Wall: cream, darker towards the ceiling, with a warm pool of light from the lamp.
    for y in range(0, floor_y):
        t = y / floor_y
        col = tuple(int(a + (b - a) * t) for a, b in zip((196, 170, 132), (234, 217, 184))) + (255,)
        c.d.line([(0, y * S), (W * S, y * S)], fill=col, width=S)
    # Wall panelling (wainscot) and baseboard.
    c.rect((0, floor_y - 150, W, floor_y), hexc('D7BE93'))
    for x in range(40, W, 180):
        c.rect((x, floor_y - 134, x + 150, floor_y - 18), None, 6, hexc('C4A67A'), 4)
    c.rect((0, floor_y - 22, W, floor_y + 6), hexc('8B5A2B'))
    # Window with the night outside (left of centre) and a picture frame (right of centre).
    c.rect((555, 170, 715, 390), hexc('7A4A24'), 10)
    c.rect((569, 184, 701, 376), hexc('1C3160'), 6)
    c.ellipse((648, 200, 682, 234), hexc('F4E7B8'))
    for sx, sy in [(590, 220), (612, 300), (676, 340), (588, 350)]:
        c.ellipse((sx - 3, sy - 3, sx + 3, sy + 3), hexc('F2F6FF'))
    c.rect((630, 184, 640, 376), hexc('7A4A24'))
    c.rect((569, 276, 701, 286), hexc('7A4A24'))
    c.poly([(545, 160), (725, 160), (715, 176), (555, 176)], hexc('8B5A2B'))
    c.rect((905, 170, 1035, 266), hexc('8B5A2B'), 6)
    c.rect((917, 182, 1023, 254), hexc('4F7A5A'), 4)
    c.poly([(921, 250), (955, 212), (981, 234), (1019, 198), (1019, 250)], hexc('3C5E45'))
    # Wardrobe on the left.
    c.rect((250, 120, 520, floor_y + 20), hexc('7A4A24'), 8)
    c.rect((266, 150, 382, floor_y - 10), hexc('96602F'), 6)
    c.rect((388, 150, 504, floor_y - 10), hexc('8E5A2C'), 6)
    for x0 in (280, 402):
        c.rect((x0, 170, x0 + 88, 330), None, 4, hexc('7A4A24'), 5)
        c.rect((x0, 350, x0 + 88, floor_y - 40), None, 4, hexc('7A4A24'), 5)
    c.ellipse((368, 360, 380, 372), hexc('F0C060'))
    c.ellipse((392, 360, 404, 372), hexc('F0C060'))
    c.rect((236, 104, 534, 128), hexc('8B5A2B'), 6)
    c.poly([(250, 128), (520, 128), (520, 150), (250, 150)], hexc('5E3818'))
    # Nightstand and lamp on the right.
    ns = (1080, floor_y - 170, 1270, floor_y + 20)
    c.rect(ns, hexc('8B5A2B'), 8)
    c.rect((ns[0] + 14, ns[1] + 20, ns[2] - 14, ns[1] + 80), hexc('A86F3A'), 6)
    c.rect((ns[0] + 14, ns[1] + 96, ns[2] - 14, ns[3] - 16), hexc('A86F3A'), 6)
    c.ellipse((1165, ns[1] + 44, 1185, ns[1] + 58), hexc('F0C060'))
    c.rect((1161, ns[1] - 70, 1189, ns[1]), hexc('6E4A2A'), 6)
    c.ellipse((1135, ns[1] - 14, 1215, ns[1] + 6), hexc('5E3818'))
    # Floor: wooden planks in perspective, lighter in the middle.
    vx, vy = W / 2, floor_y - 900
    c.rect((0, floor_y + 6, W, H), hexc('8B5A2B'))
    for i in range(-16, 17):
        x_bottom = W / 2 + i * 150
        t = (floor_y + 6 - vy) / (H - vy)
        x_top = vx + (x_bottom - vx) * t
        c.line([(x_top, floor_y + 6), (x_bottom, H)], hexc('6E4420'), 3)
        if i % 2 == 0:
            # alternate plank tone
            x2b = W / 2 + (i + 1) * 150
            x2t = vx + (x2b - vx) * t
            c.poly([(x_top, floor_y + 6), (x2t, floor_y + 6), (x2b, H), (x_bottom, H)], hexc('A86F3A', 120))
    for y in (660, 730, 830, 960):
        c.line([(0, y), (W, y)], hexc('7A4A24', 150), 2)
    # Rug under the pedestal.
    c.ellipse((460, 760, 1140, 1040), hexc('2D4F9A'))
    c.ellipse((500, 782, 1100, 1018), None, hexc('F2F6FF', 160), 6)
    c.ellipse((560, 810, 1040, 990), hexc('3A63B8'))
    # Warm floor light under the lamp.
    glow = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((950 * S, 600 * S, 1410 * S, 800 * S), fill=(255, 179, 71, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(50 * S))
    c.im = Image.alpha_composite(c.im, glow); c.d = ImageDraw.Draw(c.im)
    # Vignette and a slight overall dim so the character and the UI read first.
    vig = Image.new('L', (W, H), 0)
    vd = ImageDraw.Draw(vig)
    for i in range(40):
        t = i / 40
        vd.ellipse((W * (-0.2 + 0.25 * t), H * (-0.25 + 0.3 * t), W * (1.2 - 0.25 * t), H * (1.25 - 0.3 * t)), fill=int(255 * t))
    vig = vig.filter(ImageFilter.GaussianBlur(60)).resize((W * S, H * S))
    dark = Image.new('RGBA', (W * S, H * S), (14, 12, 20, 255))
    inv = vig.point(lambda v: int((255 - v) * 0.62))
    dark.putalpha(inv)
    c.im = Image.alpha_composite(c.im, dark)
    shade_all = Image.new('RGBA', (W * S, H * S), (26, 16, 30, 26))
    c.im = Image.alpha_composite(c.im, shade_all)
    c.d = ImageDraw.Draw(c.im)
    # The lamp and its halo are light sources: drawn after the dimming so they glow.
    shade = [(1107, ns[1] - 70), (1243, ns[1] - 70), (1217, ns[1] - 176), (1133, ns[1] - 176)]
    # Halo: soft warm radial light behind the shade.
    halo = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    hd = ImageDraw.Draw(halo)
    hx, hy = 1175 * S, (ns[1] - 120) * S
    for i in range(60, 0, -1):
        r = i * 7 * S
        a = int(3 + 2.6 * (60 - i))
        hd.ellipse((hx - r, hy - r, hx + r, hy + r), fill=(255, 179, 71, min(a, 150)))
    halo = halo.filter(ImageFilter.GaussianBlur(40 * S))
    c.im = Image.alpha_composite(c.im, halo); c.d = ImageDraw.Draw(c.im)
    c.poly(shade, hexc('FFB347'))
    c.poly([(1133, ns[1] - 176), (1217, ns[1] - 176), (1211, ns[1] - 160), (1139, ns[1] - 160)], hexc('FFD9A0'))
    out = c.im.resize((W, H), Image.Resampling.LANCZOS).convert('RGB')
    return out


def main():
    for folder in (ART, BACK):
        folder.mkdir(parents=True, exist_ok=True)
        meta(folder, folder=True)
    for name, draw in OPTION_ART.items():
        c = Canvas(256, 256)
        draw(c)
        dest = ART / (name + '.png')
        c.save(dest)
        meta(dest, max_size=256)
    dest = BACK / 'PreviewBedroom.png'
    bedroom().save(dest)
    meta(dest, max_size=2048, mips=1, alpha=0)
    print('Generated %d option thumbnails and the preview bedroom backdrop.' % len(OPTION_ART))


if __name__ == '__main__':
    main()
