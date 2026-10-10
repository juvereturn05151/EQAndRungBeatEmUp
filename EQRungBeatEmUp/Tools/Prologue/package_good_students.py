"""Package the generated student sheet as two grounded Unity sprites."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/EQ_Rung_BeatEmUp/ArtAssets/Story'
source = Image.open(ART / 'GoodStudentsSource.png').convert('RGBA')
for index, name in enumerate(('GoodStudentBoy', 'GoodStudentGirl')):
    crop = source.crop((index * source.width // 2, 0,
                        (index + 1) * source.width // 2, source.height))
    crop = crop.crop(crop.getbbox())
    height = 100 if index == 0 else 94
    crop = crop.resize((round(crop.width * height / crop.height), height),
                       Image.Resampling.NEAREST)
    frame = Image.new('RGBA', (160, 160))
    frame.alpha_composite(crop, ((160 - crop.width) // 2, 152 - height))
    frame.save(ART / f'{name}.png')
