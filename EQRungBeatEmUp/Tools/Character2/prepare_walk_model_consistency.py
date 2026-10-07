"""Package imagegen identity polish without changing clip data or import GUIDs."""
from pathlib import Path
import sys
from PIL import Image,ImageDraw
import prepare_walk_repair as repair

def preview():
    repair.preview()
    frames=[];before=repair.ROOT/'Tools/Character2/WalkModelConsistencyBefore'
    for i in range(1,13):
        image=Image.new('RGBA',(384,148),(20,24,30,255));draw=ImageDraw.Draw(image)
        items=[('Idle',repair.ART/'Idle_01.png'),('Before polish',before/f'Walk_{i:02}.png'),('Identity polish',repair.ART/'Walk'/f'Walk_{i:02}.png')]
        for col,(title,path) in enumerate(items):
            image.alpha_composite(Image.open(path).convert('RGBA'),(col*128,0));draw.text((col*128+4,130),title,fill='white')
        frames.append(image.resize((768,296),Image.Resampling.NEAREST).convert('RGB'))
    frames[0].save(repair.OUT/'ModelConsistencyBeforeAfter.gif',save_all=True,append_images=frames[1:],duration=50,loop=0)
    image=Image.new('RGBA',(512,150),(20,24,30,255));draw=ImageDraw.Draw(image)
    for col,(label,path) in enumerate([('Idle',repair.ART/'Idle_01.png')]+[(f'Walk_{i:02}',repair.ART/'Walk'/f'Walk_{i:02}.png') for i in [1,6,12]]):
        image.alpha_composite(Image.open(path).convert('RGBA'),(col*128,0));draw.text((col*128+4,132),label,fill='white')
    image.resize((1024,300),Image.Resampling.NEAREST).save(repair.OUT/'IdentityContacts.png')

if __name__=='__main__':
    if sys.argv[1]=='preview': preview()
    elif sys.argv[1]=='lower-row':
        repair.correct_frames(Path(sys.argv[2]),[8,9,10,11],'WalkModelConsistencyProportions.png');preview()
    else:
        repair.package(Path(sys.argv[1]),'WalkModelConsistencySource.png');preview()
