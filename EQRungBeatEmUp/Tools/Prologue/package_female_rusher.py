"""Slice the generated 4x4 key-pose sheet without changing its character art."""
from pathlib import Path
from PIL import Image
from collections import deque

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Enemies/FemaleRusher'
source = Image.open(ART / 'FemaleRusherSource.png').convert('RGBA')
frames = ART / 'Frames'
frames.mkdir(exist_ok=True)
cell_w, cell_h = source.width // 4, source.height // 4
def isolate(crop):
    """Keep the connected figure, excluding stray sparks from adjacent poses."""
    alpha = crop.getchannel('A')
    active = set((x, y) for y in range(crop.height) for x in range(crop.width)
                 if alpha.getpixel((x, y)) > 24)
    largest = set()
    while active:
        start = active.pop()
        component = {start}
        queue = deque([start])
        while queue:
            x, y = queue.popleft()
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    neighbor = (x + dx, y + dy)
                    if neighbor in active:
                        active.remove(neighbor)
                        component.add(neighbor)
                        queue.append(neighbor)
        if len(component) > len(largest):
            largest = component
    mask = Image.new('L', crop.size)
    for point in largest:
        mask.putpixel(point, 255)
    result = Image.new('RGBA', crop.size)
    result.paste(crop, (0, 0), mask)
    return result.crop(result.getbbox())
# One shared scale retains the relative heights of hurt and grounded poses.
idle = isolate(source.crop((0, 0, cell_w, cell_h)))
scale = 100 / idle.height
for index in range(16):
    col, row = index % 4, index // 4
    crop = source.crop((col * cell_w, row * cell_h,
                        (col + 1) * cell_w, (row + 1) * cell_h))
    # The extended slash/lunge exceed the nominal cell widths in the source.
    # Use explicit adjacent bounds to retain their complete hands.
    if index in (8, 9, 10):
        left, right = {8: (0, 350), 9: (350, 724), 10: (724, 960)}[index]
        crop = source.crop((round(left * source.width / 1280), 2 * cell_h,
                            round(right * source.width / 1280), 3 * cell_h))
    bounds = crop.getbbox()
    if not bounds:
        raise ValueError(f'Empty female Rusher pose {index}')
    crop = isolate(crop)
    crop = crop.resize((round(crop.width * scale), round(crop.height * scale)),
                       Image.Resampling.NEAREST)
    if crop.width > 252 or crop.height > 148:
        raise ValueError(f'Pose {index} exceeds frame bounds: {crop.size}')
    frame = Image.new('RGBA', (256, 160))
    # Air-hit pose stays above the ground; other poses share the foot baseline.
    bottom = 132 if index == 12 else 150
    frame.alpha_composite(crop, ((256 - crop.width) // 2, bottom - crop.height))
    frame.save(frames / f'FemaleRusher_{index:02}.png')
