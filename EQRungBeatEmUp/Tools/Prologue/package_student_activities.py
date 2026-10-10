"""Slice the generated 4-column, 2-row student activity sheet for Unity."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/EQ_Rung_BeatEmUp/ArtAssets/Story'
source = Image.open(ART / 'StudentActivitiesSource.png').convert('RGBA')
destination = ART / 'StudentActivities'
destination.mkdir(exist_ok=True)
width, height = source.width // 4, source.height // 2
split = round(source.height * 535 / 1024)
for col, name in enumerate(('IceCream', 'Drink', 'Phone', 'Wave')):
    crops = []
    for row in range(2):
        crop = source.crop((col * width, 0 if row == 0 else split,
                            (col + 1) * width, split if row == 0 else source.height))
        crops.append(crop.crop(crop.getbbox()))
    scale = (100 if col % 2 == 0 else 94) / max(crop.height for crop in crops)
    for row, crop in enumerate(crops):
        crop = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.NEAREST)
        frame = Image.new('RGBA', (160, 160))
        frame.alpha_composite(crop, ((160 - crop.width) // 2, 152 - crop.height))
        frame.save(destination / f'{name}_{row}.png')
