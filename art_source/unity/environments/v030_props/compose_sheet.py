"""Let me sleep v0.3.0 - contact sheet, annotated lineups and sketch comparisons.

System Python 3 + Pillow (not Blender). Run after render_props.py:
  python compose_sheet.py --renders N:/LetMeSleep/Validation/V030/Props [--manifest <manifest.json>]
Writes into --renders: contact_sheet.png, lineup_<group>_labeled.png, compare_<sketch>.png.
Fonts come from the repo (Bangers / Atkinson Hyperlegible, OFL).
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FONTS = HERE.parents[3] / 'unity' / 'Assets' / 'LetMeSleep' / 'UI' / 'Fonts'
SKETCHES = Path('C:/Users/brank/Desktop/bocetos')
COMPARE = {
    'PRP-01': (SKETCHES / 'objetos' / 'ChatGPT Image 12 sept 2026, 04_23_20 a.m. (1).png',
               ['Crate', 'Toolbox', 'HandLantern', 'Medkit', 'Campfire', 'Barrel', 'Signpost', 'Mailbox', 'Firewood']),
    'PRP-02': (SKETCHES / 'objetos' / 'ChatGPT Image 12 sept 2026, 05_21_31 a.m. (3).png',
               ['Bookshelf', 'RugRedStriped', 'PottedPlant', 'PaintingLandscape', 'HangingPans', 'Mug', 'RockCluster',
                'TreeStump', 'Bush', 'Mushrooms', 'FlowersWhite', 'Reeds', 'LilyPads', 'FenceSegment', 'DockLampPost',
                'Barrel']),
    'ENV-01': (SKETCHES / 'mapa y terreno' / 'ChatGPT Image 12 sept 2026, 03_48_04 a.m..png',
               ['Bed', 'Nightstand', 'TableLamp', 'Window', 'FenceSegment', 'Signpost', 'Mailbox', 'RockCluster',
                'Crate', 'Barrel', 'Firewood', 'Campfire', 'HandLantern', 'Chest', 'RugRedStriped', 'PottedPlant']),
    'ENV-03': (SKETCHES / 'mapa y terreno' / 'ChatGPT Image 12 sept 2026, 05_21_31 a.m. (4).png',
               ['Bed', 'Nightstand', 'Window', 'HandLantern', 'Chest', 'RugRedStriped', 'RugBlue', 'PaintingLandscape',
                'HangingPans', 'HangingPlant', 'Barrel', 'Crate', 'Toolbox', 'Firewood', 'Bookshelf', 'Mug']),
    'ENV-04': (SKETCHES / 'mapa y terreno' / 'ChatGPT Image 12 sept 2026, 05_21_31 a.m. (5).png',
               ['Signpost', 'FenceSegment', 'FlowersWhite', 'Campfire', 'LogBench', 'Toolbox', 'Crate', 'DockLampPost',
                'Rowboat', 'Reeds', 'Mailbox', 'RockCluster']),
    'ENV-05': (SKETCHES / 'mapa y terreno' / 'ChatGPT Image 12 sept 2026, 05_39_07 a.m. (6).png',
               ['Campfire', 'TreeStump', 'Mug', 'Toolbox', 'Mailbox', 'FlowersYellow', 'FlowersWhite', 'FoldingChair',
                'SoccerBall', 'RolledMap', 'Medkit', 'HandLantern']),
}
# UI-06 main menu: only the 3-D bedroom part of panel 1 (crop in 1536x1024 sketch pixels) + the vignette
UI06 = (SKETCHES / 'ui' / 'ChatGPT Image 12 sept 2026, 06_01_41 p.m..png', (150, 20, 400, 405),
        ['Bed', 'Nightstand', 'TableLamp', 'AlarmClock', 'Window'])
BG = (51, 64, 90)
TEXT = (242, 246, 255)
SUB = (168, 184, 216)
YELLOW = (255, 201, 60)
BLUE = (73, 178, 255)
INK = (11, 20, 38)


def font(name, size):
    return ImageFont.truetype(str(FONTS / name), size)


def m_es(v):
    return ('%.2f' % v).replace('.', ',') + ' m'


def outlined(draw, xy, text, fnt, fill, stroke=4):
    draw.text(xy, text, font=fnt, fill=fill, stroke_width=stroke, stroke_fill=INK)


def header(img, title_sub, x=40, y=26):
    d = ImageDraw.Draw(img)
    big = font('Bangers-Regular.ttf', 86)
    outlined(d, (x, y), 'LET ME', big, YELLOW)
    w = d.textlength('LET ME ', font=big)
    outlined(d, (x + w, y), 'SLEEP', big, BLUE)
    d.text((x + 4, y + 100), title_sub, font=font('AtkinsonHyperlegible-Regular.ttf', 26), fill=SUB)


def contact_sheet(renders, manifest):
    props = manifest['props']
    cols, cell, lab, gap, margin, top = 7, 300, 62, 14, 40, 170
    rows = (len(props) + cols - 1) // cols
    W = margin * 2 + cols * cell + (cols - 1) * gap
    H = top + rows * (cell + lab + gap) + 50
    img = Image.new('RGB', (W, H), BG)
    header(img, 'PROPS v0.3.0  ·  LOW-POLY FACETADO  ·  %d OBJETOS  ·  ESCALA REAL  ·  SIN ARMAS' % len(props))
    d = ImageDraw.Draw(img)
    tag = font('AtkinsonHyperlegible-Regular.ttf', 22)
    for i, t in enumerate(('SIMPLE', 'COLORIDO', 'CÁLIDO')):
        d.text((W - margin - d.textlength(t, font=tag), 40 + i * 30), t, font=tag, fill=SUB)
    f1 = font('AtkinsonHyperlegible-Regular.ttf', 21)
    f2 = font('AtkinsonHyperlegible-Regular.ttf', 16)
    for i, e in enumerate(props):
        r, c = divmod(i, cols)
        x = margin + c * (cell + gap)
        y = top + r * (cell + lab + gap)
        im = Image.open(renders / 'individual' / ('Prop_%s.png' % e['name'])).convert('RGB').resize((cell, cell), Image.LANCZOS)
        img.paste(im, (x, y))
        d.rectangle((x, y, x + cell - 1, y + cell - 1), outline=(59, 94, 156), width=2)
        name = e['display_name_es'].upper()
        size = 21
        fn = f1
        while d.textlength(name, font=fn) > cell - 6 and size > 12:
            size -= 1
            fn = font('AtkinsonHyperlegible-Regular.ttf', size)
        d.text((x + (cell - d.textlength(name, font=fn)) / 2, y + cell + 8 + (21 - size) / 2), name, font=fn, fill=TEXT)
        dims = e['dimensions_m']
        info = '%d tris  ·  %s alto' % (e['triangles'], m_es(dims['z']))
        d.text((x + (cell - d.textlength(info, font=f2)) / 2, y + cell + 36), info, font=f2, fill=SUB)
    foot = 'Fuente: art_source/unity/environments/v030_props (build_props.py). Render EEVEE de los FBX exportados.'
    d.text((margin, H - 36), foot, font=f2, fill=SUB)
    img.save(renders / 'contact_sheet.png')
    return img


def lineups(renders):
    f1 = font('AtkinsonHyperlegible-Regular.ttf', 24)
    f2 = font('AtkinsonHyperlegible-Regular.ttf', 20)
    for js in sorted(renders.glob('lineup_*.json')):
        data = json.loads(js.read_text(encoding='utf-8'))
        src = renders / (js.stem + '.png')
        im = Image.open(src).convert('RGB')
        W, H = im.size
        band = 90
        out = Image.new('RGB', (W, H + band + 70), BG)
        out.paste(im, (0, 70))
        d = ImageDraw.Draw(out)
        d.text((24, 18), 'ESCALA REAL  ·  %s  ·  humano de referencia 1,72 m' % data['group'].replace('_', ' ').upper(),
               font=f1, fill=TEXT)
        right_edge = [-1e9, -1e9]
        for lb in data['labels']:
            x = lb['u'] * W
            half = max(d.textlength(lb['es'], font=f2), d.textlength(m_es(lb['height_m']), font=f2)) / 2 + 8
            row = 0 if x - half > right_edge[0] else 1
            right_edge[row] = x + half
            y = 70 + H + 8 + row * 42
            t1 = lb['es']
            t2 = m_es(lb['height_m'])
            d.text((x - d.textlength(t1, font=f2) / 2, y), t1, font=f2, fill=TEXT)
            d.text((x - d.textlength(t2, font=f2) / 2, y + 20), t2, font=f2, fill=SUB)
        out.save(renders / (js.stem + '_labeled.png'))


def comparisons(renders, manifest):
    by = {e['name']: e for e in manifest['props']}
    f1 = font('AtkinsonHyperlegible-Regular.ttf', 18)
    fh = font('AtkinsonHyperlegible-Regular.ttf', 28)
    for key, (path, names) in COMPARE.items():
        if not path.exists():
            print('missing sketch', path)
            continue
        sk = Image.open(path).convert('RGB')
        Hs = 1000
        sk = sk.resize((int(sk.width * Hs / sk.height), Hs), Image.LANCZOS)
        cols = 4
        cell = 230
        rows = (len(names) + cols - 1) // cols
        gw = cols * cell + (cols - 1) * 10
        out = Image.new('RGB', (sk.width + gw + 60, max(Hs, rows * (cell + 34)) + 60), BG)
        d = ImageDraw.Draw(out)
        d.text((20, 12), 'BOCETO %s' % key, font=fh, fill=YELLOW)
        d.text((sk.width + 40, 12), 'PROPS v0.3.0 (render de los FBX)', font=fh, fill=BLUE)
        out.paste(sk, (20, 50))
        for i, n in enumerate(names):
            r, c = divmod(i, cols)
            x = sk.width + 40 + c * (cell + 10)
            y = 50 + r * (cell + 34)
            im = Image.open(renders / 'individual' / ('Prop_%s.png' % n)).convert('RGB').resize((cell, cell), Image.LANCZOS)
            out.paste(im, (x, y))
            t = by[n]['display_name_es']
            d.text((x + (cell - d.textlength(t, font=f1)) / 2, y + cell + 4), t, font=f1, fill=TEXT)
        out.save(renders / ('compare_%s.png' % key))


def compare_menu(renders, manifest):
    path, box, names = UI06
    vig = renders / 'vignette_menu.png'
    if not path.exists() or not vig.exists():
        print('missing UI-06 sketch or vignette')
        return
    by = {e['name']: e for e in manifest['props']}
    sk = Image.open(path).convert('RGB')
    sx, sy = sk.width / 1536, sk.height / 1024
    sk = sk.crop((int(box[0] * sx), int(box[1] * sy), int(box[2] * sx), int(box[3] * sy)))
    Hs = 900
    sk = sk.resize((int(sk.width * Hs / sk.height), Hs), Image.LANCZOS)
    v = Image.open(vig).convert('RGB')
    v = v.resize((int(v.width * Hs / v.height), Hs), Image.LANCZOS)
    cell = 250
    W = 20 + sk.width + 20 + v.width + 20
    out = Image.new('RGB', (W, 50 + Hs + 50 + cell + 40), BG)
    d = ImageDraw.Draw(out)
    fh = font('AtkinsonHyperlegible-Regular.ttf', 28)
    f1 = font('AtkinsonHyperlegible-Regular.ttf', 18)
    d.text((20, 12), 'BOCETO UI-06 (menú)', font=fh, fill=YELLOW)
    d.text((40 + sk.width, 12), 'VIÑETA CON LOS PROPS v0.3.0 (FBX, luz de luna + velador)', font=fh, fill=BLUE)
    out.paste(sk, (20, 50))
    out.paste(v, (40 + sk.width, 50))
    y = 50 + Hs + 30
    for i, n in enumerate(names):
        x = 20 + i * (cell + 12)
        im = Image.open(renders / 'individual' / ('Prop_%s.png' % n)).convert('RGB').resize((cell, cell), Image.LANCZOS)
        out.paste(im, (x, y))
        t = by[n]['display_name_es']
        d.text((x + (cell - d.textlength(t, font=f1)) / 2, y + cell + 4), t, font=f1, fill=TEXT)
    out.save(renders / 'compare_UI-06.png')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--renders', required=True)
    ap.add_argument('--manifest', default=str(HERE / 'manifest.json'))
    a = ap.parse_args()
    renders = Path(a.renders)
    manifest = json.loads(Path(a.manifest).read_text(encoding='utf-8'))
    contact_sheet(renders, manifest)
    lineups(renders)
    comparisons(renders, manifest)
    compare_menu(renders, manifest)
    print('LMS_COMPOSE_DONE', renders)


if __name__ == '__main__':
    main()
