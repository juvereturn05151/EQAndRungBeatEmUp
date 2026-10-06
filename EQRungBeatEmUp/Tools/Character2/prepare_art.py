"""Slice generated sprite assets; preserve the source and normalize to BlueShirtGuy's 128px canvas."""
from pathlib import Path
from PIL import Image
import numpy as np
import json, sys

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/ArtAssets/Characters/Character2'
ART.mkdir(parents=True, exist_ok=True)

def components(image):
    mask = np.array(image.getchannel('A')) > 150
    labels = np.zeros(mask.shape, dtype=np.int32)
    regions = []
    ys, xs = np.where(mask)
    for y, x in zip(ys, xs):
        if labels[y,x]: continue
        index = len(regions) + 1
        labels[y,x] = index
        stack = [(int(x),int(y))]; count=0; left=right=int(x); top=bottom=int(y)
        while stack:
            cx,cy=stack.pop(); count+=1
            left=min(left,cx); right=max(right,cx); top=min(top,cy); bottom=max(bottom,cy)
            for nx,ny in ((cx-1,cy),(cx+1,cy),(cx,cy-1),(cx,cy+1)):
                if 0 <= nx < mask.shape[1] and 0 <= ny < mask.shape[0] and mask[ny,nx] and not labels[ny,nx]:
                    labels[ny,nx]=index; stack.append((nx,ny))
        regions.append(dict(id=index,count=count,box=(left,top,right+1,bottom+1)))
    return labels, regions

def character(source):
    im=Image.open(source).convert('RGBA'); im.save(ART/'Character2_Source.png')
    labels, regions=components(im)
    figures=[r for r in regions if r['count']>1500]
    print('Figure regions:',len(figures), [r['count'] for r in figures])
    if len(figures)!=64: raise ValueError('Expected 64 distinct body silhouettes; inspect connected components before slicing.')
    figures.sort(key=lambda r:(r['box'][1]+r['box'][3])/2)
    rows=[sorted(figures[i:i+8],key=lambda r:r['box'][0]) for i in range(0,64,8)]
    names=[
        [('Idle',i) for i in range(4)]+[('Walk',i) for i in range(4)],
        [('Punch1',i) for i in range(4)]+[('Punch2',i) for i in range(4)],
        [('Punch3',i) for i in range(4)]+[('Launcher',i) for i in range(4)],
        [('Jump',i) for i in range(2)]+[('AirPunch1',i) for i in range(3)]+[('AirPunch2',i) for i in range(3)],
        [('AirPunch3',i) for i in range(4)]+[('AirDive',i) for i in range(4)],
        [('Dodge',i) for i in range(3)]+[('Guard',i) for i in range(2)]+[('Parry',i) for i in range(2)]+[('Grabbed',0)],
        [('Hit',i) for i in range(2)]+[('Stun',i) for i in range(2)]+[('Knockdown',i) for i in range(2)]+[('GetUp',i) for i in range(2)],
        [('Cast',i) for i in range(6)]+[('Die',i) for i in range(2)]
    ]
    scale=106/max(r['box'][3]-r['box'][1] for r in rows[0][:4])
    review=Image.new('RGBA',(128*8,128*8)); manifest=[]
    data=np.array(im)
    for row, (regions, poses) in enumerate(zip(rows,names)):
        for col, (region,(name,index)) in enumerate(zip(regions,poses)):
            x0,y0,x1,y1=region['box']; pixels=data[y0:y1,x0:x1].copy()
            pixels[:,:,3][labels[y0:y1,x0:x1]!=region['id']]=0
            crop=Image.fromarray(pixels); local_scale=min(scale,122/crop.width,116/crop.height)
            crop=crop.resize((max(1,round(crop.width*local_scale)),max(1,round(crop.height*local_scale))),Image.Resampling.NEAREST)
            alpha=np.array(crop.getchannel('A')); crop.putalpha(Image.fromarray(np.where(alpha>150,255,0).astype('uint8')))
            frame=Image.new('RGBA',(128,128)); frame.alpha_composite(crop,((128-crop.width)//2,120-crop.height))
            filename=f'{name}_{index+1:02}.png'; frame.save(ART/filename)
            review.alpha_composite(frame,(col*128,row*128))
            manifest.append(dict(file=filename,source_box=region['box'],scale=local_scale))
    review.save(ART/'Character2_NativeSheet.png')
    (ART/'manifest.json').write_text(json.dumps(manifest,indent=2))

def guardian(source):
    im=Image.open(source).convert('RGBA'); im.save(ART/'Guardian_Source.png')
    for row,name in enumerate(('Guardian','Wave')):
        for col in range(4):
            cell=im.crop((round(col*im.width/4),round(row*im.height/2),round((col+1)*im.width/4),round((row+1)*im.height/2)))
            cell.resize((256,256),Image.Resampling.NEAREST).save(ART/f'{name}_{col+1:02}.png')
    ring=Image.open(ART/'Wave_03.png').convert('RGBA')
    bounds=ring.getchannel('A').point(lambda value: 255 if value>150 else 0).getbbox()
    if bounds:
        ring.crop(bounds).save(ART/'WarningRing.png')

if __name__=='__main__':
    if sys.argv[1]=='character': character(sys.argv[2])
    else: guardian(sys.argv[2])
