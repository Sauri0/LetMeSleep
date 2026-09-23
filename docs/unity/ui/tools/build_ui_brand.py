"""Render the v0.3 menu wordmark and its cartoon mosquito. Pillow only; no editor, network or GPU.

UI-06 / art direction:
- "LET ME" in #FFC93C (gradient #FFD455 -> #FFBE26) with "ME" at 75 %, "SLEEP" in #49B2FF (#5EBDFF -> #36A6F6).
- Upright, heavy, rounded letters: Barlow Bold (OFL, docs/unity/ui/tools/fonts) fattened with a round-joined
  stroke of its own colour, so every corner is rounded like Lilita One.
- 9 px ink outline (#0B1426) and a solid 6 px downward extrusion, all measured at 1080p.
- A cartoon mosquito (white eyes, red faceted body, lavender wings) drawn next to "SLEEP".
Everything is rendered at 2x the 1080p size so it stays crisp at 1440p; the runtime shows it as a plain Image.

Usage: python build_ui_brand.py            (writes Resources/AlfaUiBrand/LogoWordmark.png and LogoMosquito.png)
       python build_ui_brand.py --preview  (also writes a preview on a night background to the scratch folder)
"""
from pathlib import Path
import math
import sys
import uuid
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[4]
FONT = ROOT / 'docs/unity/ui/tools/fonts/Barlow-Bold.ttf'
OUT = ROOT / 'unity/Assets/LetMeSleep/UI/Resources/AlfaUiBrand'
X = 2  # render scale against the 1080p layout

INK = (11, 20, 38, 255)
INK_EXTRUDE = (6, 11, 22, 255)
# v0.3.0 scenes r2 (director #10, GUIA-ESTILO accent.yellow / accent.blue): "LET ME" reads #FFC93C and "SLEEP" #49B2FF
# (narrow gradients centred on the tokens; the soft top gloss stays), ink outline #0B1426.
CREAM = ((0xFF, 0xD4, 0x55), (0xFF, 0xBE, 0x26))
SKY = ((0x5E, 0xBD, 0xFF), (0x36, 0xA6, 0xF6))


def meta(path, folder=False, max_size=2048):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    text = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    if folder:
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        text += ('TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n  isReadable: 0\n'
                 '  textureSettings:\n    filterMode: 2\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n'
                 '  maxTextureSize: %d\n  textureCompression: 0\n  alphaIsTransparency: 1\n  textureType: 8\n'
                 '  spriteMode: 1\n  spritePixelsToUnits: 100\n  spriteMeshType: 0\n'
                 '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % max_size)
    target.write_text(text, encoding='utf-8')


def text_mask(size, runs, fat, tracking):
    """Mask of runs [(text, px, x, baseline)] fattened by a round-joined stroke of `fat` px.

    Characters are placed one by one with extra `tracking` so the fattened letters never merge.
    """
    mask = Image.new('L', size, 0)
    draw = ImageDraw.Draw(mask)
    for text, px, x, y in runs:
        font = ImageFont.truetype(str(FONT), px)
        for char in text:
            draw.text((x, y), char, font=font, fill=255, stroke_width=fat, stroke_fill=255, anchor='ls')
            x += font.getlength(char) + tracking
    return mask


def run_width(text, px, tracking):
    font = ImageFont.truetype(str(FONT), px)
    return sum(font.getlength(char) for char in text) + tracking * (len(text) - 1)


def gradient_fill(size, box, top, bottom):
    w, h = size
    grad = Image.new('RGBA', size, top + (255,))
    draw = ImageDraw.Draw(grad)
    y0, y1 = box
    for y in range(h):
        t = min(1.0, max(0.0, (y - y0) / max(1, y1 - y0)))
        c = tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        draw.line([(0, y), (w, y)], fill=c)
    return grad


def paste_masked(base, layer, mask):
    layer = layer.copy()
    layer.putalpha(ImageChops.multiply(layer.getchannel('A'), mask))
    base.alpha_composite(layer)


def wordmark():
    # Layout at 1x (1080p units), multiplied by X. "LET ME" sits above and slightly right of "SLEEP".
    w, h = 760 * X, 340 * X
    fat = 5 * X            # rounding / weight gain
    outline = 9 * X        # ink ring beyond the fattened face
    extrude = 6 * X        # solid ink extrusion downwards
    tracking = 2 * fat + 3 * X
    let_px, me_px, sleep_px = 150 * X, round(150 * 0.75) * X, 200 * X
    base1, base2 = 132 * X, 300 * X
    x1 = 34 * X
    line1 = [('LET', let_px, x1, base1),
             ('ME', me_px, x1 + run_width('LET', let_px, tracking) + 26 * X, base1)]
    line2 = [('SLEEP', sleep_px, 16 * X, base2)]
    size = (w, h)

    face1 = text_mask(size, line1, fat, tracking)
    face2 = text_mask(size, line2, fat, tracking)
    # Exact round offset: the same glyphs stroked wider (round joins) instead of a square max filter.
    ring = ImageChops.lighter(text_mask(size, line1, fat + outline, tracking), text_mask(size, line2, fat + outline, tracking))
    shadow = Image.new('L', size, 0)
    for dy in range(0, extrude + 1):
        shadow = ImageChops.lighter(shadow, ImageChops.offset(ring, 0, dy))

    img = Image.new('RGBA', size, (0, 0, 0, 0))
    paste_masked(img, Image.new('RGBA', size, INK_EXTRUDE), shadow)
    paste_masked(img, Image.new('RGBA', size, INK), ring)
    # Line gradients span each line's glyph height.
    b1 = face1.getbbox(); b2 = face2.getbbox()
    paste_masked(img, gradient_fill(size, (b1[1], b1[3]), *CREAM), face1)
    paste_masked(img, gradient_fill(size, (b2[1], b2[3]), *SKY), face2)
    # Soft top gloss inside the letters.
    for face, box in ((face1, b1), (face2, b2)):
        gloss = Image.new('L', size, 0)
        gd = ImageDraw.Draw(gloss)
        gd.rectangle((0, box[1], w, box[1] + (box[3] - box[1]) * 0.2), fill=64)
        gloss = gloss.filter(ImageFilter.GaussianBlur(3 * X))
        paste_masked(img, Image.new('RGBA', size, (255, 255, 255, 255)), ImageChops.multiply(gloss, face))
    return img.crop(img.getbbox())


def mosquito():
    """Cartoon mosquito in the style of UI-06 / PER-02: red faceted body, huge white eyes, lavender wings."""
    s = 4  # supersample
    W = H = 256 * s
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    red, red_dark, red_light = (184, 38, 43, 255), (110, 20, 24, 255), (222, 78, 76, 255)
    legs = (42, 26, 26, 255)
    ow = 4  # ink outline, px at 256

    def P(x, y):
        return (round(x * s), round(y * s))

    def ellipse(cx, cy, rx, ry, fill, outline=ow):
        if outline:
            draw.ellipse([P(cx - rx - outline, cy - ry - outline), P(cx + rx + outline, cy + ry + outline)], fill=INK)
        draw.ellipse([P(cx - rx, cy - ry), P(cx + rx, cy + ry)], fill=fill)

    def polygon(points, fill, outline=ow):
        pts = [P(*p) for p in points]
        if outline:
            draw.line(pts + [pts[0]], fill=INK, width=2 * outline * s, joint='curve')
            for x, y in pts:
                draw.ellipse([x - outline * s, y - outline * s, x + outline * s, y + outline * s], fill=INK)
        draw.polygon(pts, fill=fill)

    # Wings (translucent lavender) behind everything.
    wings = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    wd = ImageDraw.Draw(wings)
    for pts in ([(122, 100), (84, 20), (58, 20), (60, 58), (104, 112)], [(140, 96), (200, 30), (228, 44), (206, 84), (150, 110)]):
        scaled = [P(*p) for p in pts]
        wd.polygon(scaled, fill=(220, 221, 245, 140))
        wd.line(scaled + [scaled[0]], fill=(11, 20, 38, 230), width=3 * s, joint='curve')
        wd.line([scaled[0], scaled[2]], fill=(150, 152, 210, 190), width=2 * s)
        wd.line([scaled[0], scaled[3]], fill=(150, 152, 210, 150), width=2 * s)
    img.alpha_composite(wings)
    # Six long thin legs with a knee each.
    for x0, y0, x1, y1, x2, y2 in [(118, 152, 92, 182, 80, 236), (128, 158, 118, 196, 116, 246), (140, 158, 150, 198, 158, 246),
                                   (152, 152, 180, 180, 202, 232), (112, 146, 80, 156, 50, 196), (160, 144, 200, 152, 232, 190)]:
        draw.line([P(x0, y0), P(x1, y1), P(x2, y2)], fill=INK, width=7 * s, joint='curve')
        draw.line([P(x0, y0), P(x1, y1), P(x2, y2)], fill=legs, width=3 * s, joint='curve')
    # Abdomen: long, pointed and segmented, behind the thorax.
    polygon([(132, 132), (156, 124), (240, 184), (232, 198), (146, 158)], red)
    for t in (0.3, 0.5, 0.7):
        ax, ay = 144 + (236 - 144) * t, 140 + (191 - 140) * t
        draw.line([P(ax - 10, ay + 12), P(ax + 8, ay - 12)], fill=red_dark, width=5 * s)
    # Thorax with a faceted highlight.
    ellipse(130, 134, 30, 26, red)
    polygon([(112, 124), (130, 112), (148, 120), (134, 132)], red_light, outline=0)
    # Proboscis, long and thin.
    draw.line([P(88, 128), P(20, 170)], fill=INK, width=9 * s)
    draw.line([P(88, 128), P(22, 169)], fill=red_dark, width=4 * s)
    # Head.
    ellipse(96, 118, 20, 19, red)
    # Huge eyes, bigger than the head, pupils looking forward and down.
    for cx, cy in ((78, 92), (112, 86)):
        ellipse(cx, cy, 25, 25, (255, 255, 255, 255))
        draw.ellipse([P(cx - 15, cy - 6), P(cx + 1, cy + 10)], fill=(12, 12, 16, 255))
        draw.ellipse([P(cx - 11, cy - 4), P(cx - 6, cy + 1)], fill=(255, 255, 255, 255))
    img = img.resize((256, 256), Image.Resampling.LANCZOS)
    return img.crop(img.getbbox())


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT, folder=True)
    mark = wordmark()
    mark_path = OUT / 'LogoWordmark.png'
    mark.save(mark_path)
    meta(mark_path)
    bug = mosquito()
    bug_path = OUT / 'LogoMosquito.png'
    bug.save(bug_path)
    meta(bug_path, max_size=512)
    print('Wordmark %s, mosquito %s' % (mark.size, bug.size))
    if '--preview' in sys.argv:
        preview_dir = Path(sys.argv[sys.argv.index('--preview') + 1]) if len(sys.argv) > sys.argv.index('--preview') + 1 else OUT
        bg = Image.new('RGBA', (mark.width + 400, mark.height + 200), (14, 26, 48, 255))
        bg.alpha_composite(mark, (60, 100))
        small = bug.resize((bug.width * 120 // max(bug.size) * 2, bug.height * 120 // max(bug.size) * 2), Image.Resampling.LANCZOS)
        bg.alpha_composite(small, (mark.width + 60, 90))
        bg.save(preview_dir / 'brand-preview.png')


if __name__ == '__main__':
    main()
