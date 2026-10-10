from pathlib import Path
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/EQ_Rung_BeatEmUp/ArtAssets/Story'
source=Image.open(ART/'NPCSource.png').convert('RGBA')
# Explicit source bounds separate the full figures from the larger portrait row.
for name,box,height in [('Prapot',(125,100,425,625),106),('Cream',(645,160,880,625),92),('Chanai',(1030,20,1490,625),124)]:
    crop=source.crop(box);data=np.array(crop);data[:,:,3]=np.where(data[:,:,3]>180,255,0);crop=Image.fromarray(data);crop=crop.crop(crop.getbbox())
    size=(round(crop.width*height/crop.height),height);crop=crop.resize(size,Image.Resampling.NEAREST)
    frame=Image.new('RGBA',(160,160));frame.alpha_composite(crop,((160-crop.width)//2,152-crop.height));frame.save(ART/f'{name}.png')
for i,name in enumerate(['Prapot','Cream','Chanai']):
    crop=source.crop((i*512,630,(i+1)*512,1024));crop=crop.resize((256,197),Image.Resampling.NEAREST);crop.save(ART/f'{name}Portrait.png')
