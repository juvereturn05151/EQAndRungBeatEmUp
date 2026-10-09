"""Match the six cast poses to idle, preserving the ground pivot and pixel edges."""
from pathlib import Path
import shutil
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = ROOT / 'Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Character2'
ORIGINAL = HERE / 'Original'
ORIGINAL.mkdir(exist_ok=True)
# Idle is 106 pixels tall; the upright cast startup is 85 pixels tall.
# Use one scale for the whole animation so crouching retains its proportions.
SCALE = 1.25
preview = Image.new('RGBA', (256 * 7, 180 * 2), (24, 28, 36, 255))
draw = ImageDraw.Draw(preview)
idle = Image.open(ART / 'Idle_01.png').convert('RGBA')
for row in range(2):
    preview.alpha_composite(idle, (64, row * 180 + 32))
    draw.text((12, row * 180 + 4), 'Idle reference', fill='white')
for i in range(1, 7):
    name = f'Cast_{i:02}.png'
    original = ORIGINAL / name
    if not original.exists():
        shutil.copy2(ART / name, original)
    sprite = Image.open(original).convert('RGBA')
    box = sprite.getbbox()
    crop = sprite.crop(box)
    enlarged = crop.resize((round(crop.width * SCALE), round(crop.height * SCALE)), Image.Resampling.NEAREST)
    # Transform around the existing Unity pivot: center x=128, ground y=152.
    x = round(128 + (box[0] - 128) * SCALE)
    y = round(152 + (box[1] - 152) * SCALE)
    assert x >= 0 and y >= 0 and x + enlarged.width <= 256 and y + enlarged.height <= 160
    frame = Image.new('RGBA', sprite.size)
    frame.alpha_composite(enlarged, (x, y))
    frame.save(ART / name)
    preview.alpha_composite(sprite, (i * 256, 0))
    preview.alpha_composite(frame, (i * 256, 180))
    draw.text((i * 256 + 12, 4), f'{name} before', fill='white')
    draw.text((i * 256 + 12, 184), f'{name} matched to idle', fill='white')
    print(name, box, '->', frame.getbbox())
preview.resize((1792, 360), Image.Resampling.NEAREST).save(HERE / 'Comparison.png')
