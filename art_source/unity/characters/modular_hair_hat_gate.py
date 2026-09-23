"""Hair x hat review gate for the modular human (review r1 #1).

Renders every hairstyle under every headwear (and without one) from 3/4 behind and 3/4 in front with
render_modular_preview.py (the host's authored hair hidden under hats, the under-hat variants shown only there,
exactly like the Unity assembler) and composes one sheet per view when Pillow is available.

Usage (system Python, Blender 5.2 on the path given):
  python modular_hair_hat_gate.py <out_dir> [--blender N:/Blender/blender.exe]

Numeric side of the gate: ProductionCustomizationCatalogPlayModeTests.EveryHairUnderEveryHatHugsTheSkull (every
under-hat variant within 5.5 mm of the skull in the idle pose, the full style hidden under any hat).
"""
import argparse
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
HAIRS = ('corto', 'despeinado', 'rulos')
HATS = ('none', 'gorro-dormir', 'gorra', 'gorro-lana')
VIEWS = ('threequarterback', 'threequarter')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('out_dir', type=Path)
    parser.add_argument('--blender', default='N:/Blender/blender.exe')
    args = parser.parse_args()
    out = args.out_dir.resolve()
    out.mkdir(parents=True, exist_ok=True)
    for hair in HAIRS:
        for hat in HATS:
            command = [args.blender, '--background', '--factory-startup', '--python-exit-code', '1', '--python',
                       str(HERE / 'render_modular_preview.py'), '--', 'Human', str(out), f'hh_{hair}_{hat}',
                       f'human.hair={hair}', f'human.headwear={hat}', '--closeup', 'head', '--views', ','.join(VIEWS)]
            result = subprocess.run(command, capture_output=True, text=True, timeout=600)
            if result.returncode != 0:
                sys.exit(f'render failed for {hair}/{hat}:\n{result.stdout[-2000:]}\n{result.stderr[-2000:]}')
    try:
        from PIL import Image
    except ImportError:
        print('HAIR_HAT_GATE_RENDERED (no Pillow: no sheet)', out)
        return
    for view in VIEWS:
        images = [Image.open(out / f'hh_{hair}_{hat}_{view}.png').convert('RGB') for hair in HAIRS for hat in HATS]
        w, h = images[0].width // 2, images[0].height // 2
        sheet = Image.new('RGB', (w * len(HATS), h * len(HAIRS)))
        for index, image in enumerate(images):
            sheet.paste(image.resize((w, h)), ((index % len(HATS)) * w, (index // len(HATS)) * h))
        sheet.save(out / f'sheet_hair_hat_{view}.png')
    print('HAIR_HAT_GATE_RENDERED', out)


if __name__ == '__main__':
    main()
