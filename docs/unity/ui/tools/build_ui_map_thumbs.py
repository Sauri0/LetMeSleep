"""Map thumbnails for the JUGAR ONLINE carousel (UI-06 screen 2). Pillow only.

Crops the world-only gameplay captures (no UI, no players) that the capture harnesses write per map, e.g.
N:/LetMeSleep/Validation/V030/Maps/final/<mapId>-human-1920x1080-world.png, to 800 x 280 (2x the ~400 x 140
carousel picture at 1080p) in Resources/AlfaUiMapThumbs/<mapId>.png. The alpha house (house-patio-v1, the room's
default map) comes from the UI harness world view (UiV030Stage3FinalCapture, world/house-patio-v1-front-high.png).
Re-run it after a map changes:

    python build_ui_map_thumbs.py [capture folder] [house capture]
"""
from pathlib import Path
import sys
import uuid
from PIL import Image, ImageEnhance

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'unity/Assets/LetMeSleep/UI/Resources/AlfaUiMapThumbs'
SOURCE = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('N:/LetMeSleep/Validation/V030/Maps/final')
HOUSE = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(
    'N:/LetMeSleep/Validation/V030/UI/stage3r2/Run01/captures/world/house-patio-v1-front-high.png')
SIZE = (800, 280)
# Vertical centre of the crop (fraction of the capture height) that keeps each map's landmark in frame, and a
# brightness factor (the alpha house is lit for night play and reads too dark at thumbnail size).
MAPS = {
    'hf-casa-del-patio-v1': (0.40, 1.0),
    'hf-campamento-pinar-v2': (0.45, 1.0),
    'hf-isla-del-laguito-v2': (0.55, 1.0),
    'hf-puerto-del-faro-v1': (0.42, 1.0),
    'hf-yate-a-la-deriva-v3': (0.45, 1.0),
    'house-patio-v1': (0.68, 1.25),
}


def meta(path, folder=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    text = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    if folder:
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        text += ('TextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n  isReadable: 0\n'
                 '  textureSettings:\n    filterMode: 2\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n'
                 '  maxTextureSize: 1024\n  textureCompression: 0\n  alphaIsTransparency: 0\n  textureType: 8\n'
                 '  spriteMode: 1\n  spritePixelsToUnits: 100\n  spriteMeshType: 0\n'
                 '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    target.write_text(text, encoding='utf-8')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT, folder=True)
    written = []
    for map_id, (centre, brightness) in MAPS.items():
        source = HOUSE if map_id == 'house-patio-v1' else SOURCE / (map_id + '-human-1920x1080-world.png')
        if not source.exists():
            print('skip', map_id, '(no capture)')
            continue
        image = Image.open(source).convert('RGB')
        w, h = image.size
        crop_h = round(w * SIZE[1] / SIZE[0])
        top = min(max(0, round(h * centre - crop_h / 2)), h - crop_h)
        thumb = image.crop((0, top, w, top + crop_h)).resize(SIZE, Image.Resampling.LANCZOS)
        thumb = ImageEnhance.Color(thumb).enhance(1.12)
        if brightness != 1.0:
            thumb = ImageEnhance.Brightness(thumb).enhance(brightness)
        dest = OUT / (map_id + '.png')
        thumb.save(dest)
        meta(dest)
        written.append(map_id)
    print('Thumbnails:', ', '.join(written))


if __name__ == '__main__':
    main()
