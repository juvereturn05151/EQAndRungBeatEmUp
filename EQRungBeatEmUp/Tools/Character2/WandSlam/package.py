"""Pack imagegen's three wand poses into existing sprite canvases without redrawing."""
from pathlib import Path
from PIL import Image
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = ROOT / 'Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Character2/AirPunch'
source = Image.open(HERE / 'Generated.png').convert('RGBA')
corrected = Image.open(HERE / 'LeftHandCorrection.png').convert('RGBA')
assert corrected.size == source.size
cell_width = source.width // 3
# Match the body/head size of the idle and existing punch/air-punch sprites.
scale = .225
review = Image.new('RGBA', (256 * 4, 160), (24, 28, 36, 255))
review.alpha_composite(Image.open(ART / 'AirPunch3_01.png'), (0, 0))
for i in range(3):
    # Only the wind-up needed handedness repair; retain the accepted strike and recovery.
    pose_source = corrected if i == 0 else source
    cell = pose_source.crop((i * cell_width, 0, (i + 1) * cell_width, source.height))
    pixels = np.array(cell)
    pixels[:, :, 3] = np.where(pixels[:, :, 3] > 150, 255, 0)
    cell = Image.fromarray(pixels)
    box = cell.getbbox()
    # The bottom of the blue sandals is the existing eight-pixel ground anchor.
    yy, xx = np.where((pixels[:, :, 3] > 0) & (pixels[:, :, 2] > pixels[:, :, 0].astype(float) * 1.25)
                      & (pixels[:, :, 2] > 40) & (np.indices(pixels.shape[:2])[0] > 480))
    bottom = int(yy.max()) + 1
    foot_x = float(np.median(xx[yy > bottom - 14]))
    crop = cell.crop(box).resize((round((box[2] - box[0]) * scale), round((box[3] - box[1]) * scale)), Image.Resampling.NEAREST)
    frame = Image.new('RGBA', (256, 160))
    x = round(128 - (foot_x - box[0]) * scale)
    y = round(152 - (bottom - box[1]) * scale)
    assert 0 <= x and 0 <= y and x + crop.width <= 256 and y + crop.height <= 160
    frame.alpha_composite(crop, (x, y))
    path = ART / f'AirPunch3_{i + 2:02}.png'
    frame.save(path)
    review.alpha_composite(frame, ((i + 1) * 256, 0))
    print(path.name, frame.size, frame.getbbox(), 'foot anchor', (128, 152))
review.resize((2048, 320), Image.Resampling.NEAREST).save(HERE / 'Preview.png')

# Compare at the same pixels-per-unit and foot anchor, including the smaller idle canvas.
from PIL import ImageDraw
comparison = Image.new('RGBA', (256 * 3, 180 * 2), (24, 28, 36, 255))
references = [('Idle', ART.parent / 'Idle_01.png'),
              ('Ground punch', ART.parent / 'Punch1_03.png'),
              ('Earlier air punch', ART / 'AirPunch2_02.png'),
              ('Wand wind-up', ART / 'AirPunch3_02.png'),
              ('Wand smash', ART / 'AirPunch3_03.png'),
              ('Follow-through', ART / 'AirPunch3_04.png')]
for i, (label, path) in enumerate(references):
    sprite = Image.open(path).convert('RGBA')
    x, y = (i % 3) * 256, (i // 3) * 180
    comparison.alpha_composite(sprite, (x + (256 - sprite.width) // 2, y + 160 - sprite.height))
    ImageDraw.Draw(comparison).text((x + 12, y + 163), label, fill='white')
comparison.resize((1536, 720), Image.Resampling.NEAREST).save(HERE / 'SizeComparison.png')
