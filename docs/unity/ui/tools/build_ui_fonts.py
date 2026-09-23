"""Derive the v0.3 UI display face from Barlow Bold (SIL OFL 1.1). fontTools only; no editor or network.

UI-06 draws tabs, buttons, calls to action and titles in an upright, bold, condensed grotesk. Barlow Condensed
is not available offline here, so this narrows Barlow Bold (docs/unity/ui/tools/fonts/Barlow-Bold.ttf, v1.408,
unmodified upstream release) horizontally. Barlow's horizontals are slightly lighter than its verticals, so a
0.86 x-scale lands close to monoline instead of reversing the contrast. Kerning and advances are scaled with the
outlines. The result is renamed "LMS Barlow Narrow" so it is never mistaken for an official Barlow width;
Barlow has no Reserved Font Name, and the derivative stays under the OFL (license copied next to it).

To use the official Barlow Semi Condensed / Condensed Bold instead, drop that TTF in the Fonts folder and point
AlfaUiFontAssetBuilder at it; nothing else references the file name.

Usage: python build_ui_fonts.py [x-scale]
"""
from pathlib import Path
import sys
import uuid
from fontTools.ttLib import TTFont
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'docs/unity/ui/tools/fonts/Barlow-Bold.ttf'
LICENSE = ROOT / 'docs/unity/ui/tools/fonts/Barlow-OFL.txt'
OUT_DIR = ROOT / 'unity/Assets/LetMeSleep/UI/Fonts'
FAMILY = 'LMS Barlow Narrow'
POSTSCRIPT = 'LMSBarlowNarrow-Bold'
SCALE = float(sys.argv[1]) if len(sys.argv) > 1 else 0.86


def meta(path, importer):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    text = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n' + importer
    target.write_text(text, encoding='utf-8')


FONT_IMPORTER = ('TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n'
                 '  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n'
                 '  fontNames:\n  - ' + FAMILY + '\n  fallbackFontReferences: []\n  customCharacters: \n'
                 '  fontRenderingMode: 0\n  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n'
                 '  shouldRoundAdvanceValue: 1\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
TEXT_IMPORTER = 'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'


def scale_value(record):
    if record is None:
        return
    for attr in ('XAdvance', 'XPlacement'):
        if hasattr(record, attr) and getattr(record, attr):
            setattr(record, attr, round(getattr(record, attr) * SCALE))


def main():
    font = TTFont(SOURCE)
    glyphs = font.getGlyphSet()
    glyf = font['glyf']
    hmtx = font['hmtx']
    scaled = {}
    for name in font.getGlyphOrder():
        pen = TTGlyphPen(glyphs)
        glyphs[name].draw(TransformPen(pen, (SCALE, 0, 0, 1, 0, 0)))
        scaled[name] = pen.glyph()
    for name, glyph in scaled.items():
        glyf[name] = glyph
        width, lsb = hmtx[name]
        hmtx[name] = (round(width * SCALE), round(lsb * SCALE))
    if 'GPOS' in font:
        for lookup in font['GPOS'].table.LookupList.Lookup:
            for sub in lookup.SubTable:
                if lookup.LookupType == 9:
                    sub = sub.ExtSubTable
                if getattr(sub, 'LookupType', lookup.LookupType) != 2:
                    continue
                if sub.Format == 1:
                    for pair_set in sub.PairSet:
                        for pair in pair_set.PairValueRecord:
                            scale_value(pair.Value1)
                            scale_value(pair.Value2)
                elif sub.Format == 2:
                    for class1 in sub.Class1Record:
                        for class2 in class1.Class2Record:
                            scale_value(class2.Value1)
                            scale_value(class2.Value2)
    if 'kern' in font:
        del font['kern']
    os2 = font['OS/2']
    os2.usWidthClass = 4
    os2.xAvgCharWidth = round(os2.xAvgCharWidth * SCALE)
    names = font['name']
    for record in list(names.names):
        if record.nameID in (1, 3, 4, 6, 16, 17, 21, 22):
            names.removeNames(nameID=record.nameID)
    names.setName(FAMILY, 1, 3, 1, 0x409)
    names.setName('Bold', 2, 3, 1, 0x409)
    names.setName('1.408;LMS;' + POSTSCRIPT, 3, 3, 1, 0x409)
    names.setName(FAMILY + ' Bold', 4, 3, 1, 0x409)
    names.setName(POSTSCRIPT, 6, 3, 1, 0x409)
    names.setName('Derived from Barlow Bold 1.408 by the Let me sleep team: outlines, advances and kerning '
                  'narrowed to %.2f. Licensed under the SIL Open Font License 1.1.' % SCALE, 10, 3, 1, 0x409)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out = OUT_DIR / (POSTSCRIPT + '.ttf')
    font.save(out)
    meta(out, FONT_IMPORTER)
    license_out = OUT_DIR / 'LMSBarlowNarrow-OFL.txt'
    license_text = LICENSE.read_text(encoding='utf-8')
    header = ('LMS Barlow Narrow Bold is a Modified Version of Barlow Bold 1.408 (outlines, advances and kerning '
              'narrowed to %.2f, renamed). It is distributed under the same license.\n\n' % SCALE)
    license_out.write_text(header + license_text, encoding='utf-8')
    meta(license_out, TEXT_IMPORTER)
    print('Wrote %s (x%.2f) and its OFL text.' % (out, SCALE))


if __name__ == '__main__':
    main()
