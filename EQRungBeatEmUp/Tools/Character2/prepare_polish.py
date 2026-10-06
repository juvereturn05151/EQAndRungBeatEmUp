"""Package generated sprite sheets into existing Unity assets; no procedural drawing.
Keep a uniform scale across poses, retain detached effects, and preserve asset GUIDs.
"""
from pathlib import Path
import sys, json, shutil
import numpy as np
from PIL import Image
from prepare_art import components

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/ArtAssets/Characters/Character2'
ARCHIVE=ROOT/'Tools/Character2/Archive'
ROWS=[['Idle']*4+['Walk']*4,['Punch1']*4+['Punch2']*4,['Punch3']*4+['Launcher']*4,
      ['Jump']*2+['AirPunch1']*3+['AirPunch2']*3,['AirPunch3']*4+['AirDive']*4,
      ['Dodge']*3+['Guard']*2+['Parry']*2+['Grabbed'],['Hit']*2+['Stun']*2+['Knockdown']*2+['GetUp']*2,
      ['Cast']*6+['Die']*2]

def backup(path):
    ARCHIVE.mkdir(parents=True,exist_ok=True)
    if path.exists() and not (ARCHIVE/path.name).exists(): shutil.copy2(path,ARCHIVE/path.name)

def save(image,name):
    path=ART/name; backup(path); image.save(path)

def package(source,mode):
    im=Image.open(source).convert('RGBA'); labels,regions=components(im)
    main=[r for r in regions if r['count']>1500]
    if mode=='base':
        # Generated base has one omitted second-parry pose. Reuse the polished
        # first-parry drawing there; retain its separate existing timing asset.
        boundaries=[170,335,520,690,860,1006,1134,1254]
        rows=[]; remaining=list(main)
        for bottom in boundaries:
            row=sorted([r for r in remaining if r['box'][3]<=bottom],key=lambda r:r['box'][0])
            remaining=[r for r in remaining if r not in row]; rows.append(row)
        assert [len(r) for r in rows]==[8,8,8,8,8,7,8,8], [len(r) for r in rows]
        rows[5].insert(6,rows[5][5])
        names=[]
        for row in ROWS:
            counts={}
            for name in row:
                counts[name]=counts.get(name,0)+1; names.append(f'{name}_{counts[name]:02}.png')
        figures=[r for row in rows for r in row]
        scale=106/143
    else:
        assert len(main)==12,len(main)
        main.sort(key=lambda r:(r['box'][1]+r['box'][3])/2)
        figures=[r for i in range(0,12,4) for r in sorted(main[i:i+4],key=lambda r:r['box'][0])]
        figures=[figures[i-1] for i in [1,2,3,4,6,5,7,8,9,10,12,11]]
        names=[f'Walk_{i:02}.png' for i in range(1,13)]
        # One uniform scale preserves bob and proportions; don't fit each pose.
        scale=106/np.median([r['box'][3]-r['box'][1] for r in figures])
    # Assign detached pixels (glasses, snake, wand, stars) to nearest body bounds.
    assigned={r['id']:[r] for r in main}
    for r in regions:
        if r in main: continue
        x0,y0,x1,y1=r['box']; cx=(x0+x1)/2; cy=(y0+y1)/2
        def distance(f):
            l,t,rr,b=f['box']; return max(l-cx,0,cx-rr)**2+max(t-cy,0,cy-b)**2
        nearest=min(main,key=distance)
        if distance(nearest)<100**2: assigned[nearest['id']].append(r)
    data=np.array(im); manifest=[]; review=Image.new('RGBA',(256*8,160*((len(names)+7)//8)))
    for i,(region,name) in enumerate(zip(figures,names)):
        pieces=assigned[region['id']]; boxes=[r['box'] for r in pieces]
        x0=min(b[0] for b in boxes); y0=min(b[1] for b in boxes); x1=max(b[2] for b in boxes); y1=max(b[3] for b in boxes)
        mask=np.isin(labels[y0:y1,x0:x1],[r['id'] for r in pieces])
        pixels=data[y0:y1,x0:x1].copy(); pixels[:,:,3]=np.where(mask,255,0)
        crop=Image.fromarray(pixels).resize((round((x1-x0)*scale),round((y1-y0)*scale)),Image.Resampling.NEAREST)
        # Feet are the anchor, not the bounding box center of an extended snake.
        l,t,rr,b=region['box']; feet=np.where(labels[max(t,b-8):b,l:rr]==region['id'])[1]
        anchor=l+(float(feet.min()+feet.max())/2 if len(feet) else (rr-l)/2)
        wide=not name.startswith(('Idle','Walk'))
        frame=Image.new('RGBA',(256,160) if wide else (128,128))
        px=round((128 if wide else 64)-(anchor-x0)*scale); py=frame.height-8-round((b-y0)*scale)
        assert px>=0 and py>=0 and px+crop.width<=frame.width and py+crop.height<=frame.height,(name,px,py,crop.size)
        frame.alpha_composite(crop,(px,py)); save(frame,name)
        review.alpha_composite(frame,((i%8)*256,(i//8)*160))
        manifest.append(dict(file=name,source_box=[x0,y0,x1,y1],scale=scale,canvas=list(frame.size),anchor=anchor))
    save(review,'Character2_PolishedSheet.png' if mode=='base' else 'Character2_WalkSheet.png')
    shutil.copy2(source,ART/('Character2_PolishedSource.png' if mode=='base' else 'Character2_WalkSource.png'))
    (ART/f'polish_{mode}_manifest.json').write_text(json.dumps(manifest,indent=2))

def barrier(source):
    im=Image.open(source).convert('RGBA')
    for row in range(3):
        for col in range(4):
            cell=im.crop((round(col*im.width/4),round(row*im.height/3),round((col+1)*im.width/4),round((row+1)*im.height/3)))
            name=f'Barrier_{row*4+col+1:02}.png' if row<2 else f'Pulse_{col+1:02}.png'
            save(cell.resize((256,256),Image.Resampling.NEAREST),name)
    shutil.copy2(source,ART/'Barrier_Source.png')

def preview():
    output=ROOT/'Documentation/Character2PolishPreview'; output.mkdir(parents=True,exist_ok=True)
    frames=[]; sheet=Image.new('RGBA',(512,384),(20,24,30,255))
    for i in range(12):
        sprite=Image.open(ART/f'Walk_{i+1:02}.png').convert('RGBA')
        sheet.alpha_composite(sprite,((i%4)*128,(i//4)*128))
        frame=Image.new('RGBA',(128,128),(20,24,30,255)); frame.alpha_composite(sprite)
        frames.append(frame.resize((256,256),Image.Resampling.NEAREST).convert('RGB'))
    frames[0].save(output/'WalkLoop.gif',save_all=True,append_images=frames[1:],duration=50,loop=0)
    sheet.save(output/'WalkContactSheet.png')

if __name__=='__main__':
    if sys.argv[1]=='preview': preview()
    elif sys.argv[1]=='barrier': barrier(sys.argv[2])
    else: package(sys.argv[2],sys.argv[1])



