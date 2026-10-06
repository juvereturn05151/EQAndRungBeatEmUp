"""Slice approved generated walk artwork at one uniform scale. Never redraw pixels.
Anchor the shirt/hip axis instead of centering changing stride bounding boxes.
"""
from pathlib import Path
import sys,json,shutil,hashlib
import numpy as np
from PIL import Image,ImageDraw
from prepare_art import components
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/ArtAssets/Characters/Character2'
OUT=ROOT/'Documentation/Character2WalkPreview'

def package(source):
    im=Image.open(source).convert('RGBA'); labels,regions=components(im)
    figures=[r for r in regions if r['count']>1500]
    assert len(figures)==12,len(figures)
    figures.sort(key=lambda r:(r['box'][1]+r['box'][3])/2)
    figures=[r for row in range(3) for r in sorted(figures[row*4:row*4+4],key=lambda r:r['box'][0])]
    idle=Image.open(ART/'Idle_01.png').convert('RGBA'); ib=idle.getbbox()
    scale=(ib[3]-ib[1])/float(np.median([r['box'][3]-r['box'][1] for r in figures]))
    data=np.array(im); OUT.mkdir(parents=True,exist_ok=True); manifest=[]; frames=[]
    for i,r in enumerate(figures):
        x0,y0,x1,y1=r['box']; pixels=data[y0:y1,x0:x1].copy()
        # Keep attached body artwork. Detached isolated sheet noise is excluded.
        pixels[:,:,3]=np.where(labels[y0:y1,x0:x1]==r['id'],pixels[:,:,3],0)
        # Shirt gray in lower torso is the stable anatomical axis. Feet bounds
        # cannot define this: the leading foot alternates across the body.
        rgb=pixels[:,:,:3].astype(int); yy=np.arange(y1-y0)[:,None]
        shirt=(rgb[:,:,2]>rgb[:,:,0]) & (rgb[:,:,0]>65) & (rgb[:,:,1]-rgb[:,:,0]<35) & (rgb[:,:,2]-rgb[:,:,0]<65)
        shirt &= (yy>(y1-y0)*.38) & (yy<(y1-y0)*.55) & (pixels[:,:,3]>128)
        sx=np.where(shirt)[1]; assert len(sx)>30
        axis=(float(np.percentile(sx,5))+float(np.percentile(sx,95)))/2
        crop=Image.fromarray(pixels).resize((round((x1-x0)*scale),round((y1-y0)*scale)),Image.Resampling.NEAREST)
        px=round(64-axis*scale); py=120-crop.height
        assert px>=0 and py>=0 and px+crop.width<=128
        frame=Image.new('RGBA',(128,128)); frame.alpha_composite(crop,(px,py))
        path=ART/'Walk'/f'Walk_{i+1:02}.png'; frame.save(path); frames.append(frame)
        manifest.append(dict(frame=i+1,source_box=r['box'],uniform_scale=scale,hip_axis=axis+x0,bounds=frame.getbbox(),canvas=[128,128],ppu=100,pivot=[.5,.0625],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    shutil.copy2(source,ROOT/'Tools/Character2/WalkCorrectedSource.png')
    (OUT/'Manifest.json').write_text(json.dumps(dict(idle_bounds=ib,frames=manifest),indent=2))
    preview()
    print(json.dumps(dict(idle_bounds=ib,walk_bounds=[f.getbbox() for f in frames],uniform_scale=scale)))

def preview():
    idle=Image.open(ART/'Idle_01.png').convert('RGBA')
    frames=[Image.open(ART/'Walk'/f'Walk_{i:02}.png').convert('RGBA') for i in range(1,13)]
    manifest_path=OUT/'Manifest.json'; manifest=json.loads(manifest_path.read_text())
    for i,frame in enumerate(frames):
        manifest['frames'][i]['bounds']=frame.getbbox()
        manifest['frames'][i]['sha256']=hashlib.sha256((ART/'Walk'/f'Walk_{i+1:02}.png').read_bytes()).hexdigest()
    manifest_path.write_text(json.dumps(manifest,indent=2))
    sheet=Image.new('RGBA',(128*4,150*4),(20,24,30,255)); draw=ImageDraw.Draw(sheet)
    for i,frame in enumerate([idle]+frames):
        x=(i%4)*128;y=(i//4)*150;sheet.alpha_composite(frame,(x,y));draw.text((x+5,y+130),'Idle' if i==0 else f'Walk_{i:02}',fill='white')
    sheet.resize((1024,1200),Image.Resampling.NEAREST).save(OUT/'IdleAndWalk.png')
    animated=[]
    for frame in frames:
        bg=Image.new('RGBA',(128,128),(20,24,30,255));bg.alpha_composite(frame);animated.append(bg.resize((384,384),Image.Resampling.NEAREST).convert('RGB'))
    animated[0].save(OUT/'WalkLoop.gif',save_all=True,append_images=animated[1:],duration=50,loop=0)
    for group,name in [([3,4,5],'Onion_04_05_06'),([9,10,11,0],'Onion_10_11_12_01')]:
        onion=Image.new('RGBA',(128,128),(20,24,30,255))
        for index in group:
            layer=frames[index].copy();layer.putalpha(layer.getchannel('A').point(lambda a:a//len(group)));onion.alpha_composite(layer)
        onion.resize((512,512),Image.Resampling.NEAREST).save(OUT/(name+'.png'))

def correct_frames(source,indices,source_name):
    # The refinement sheet shares the original source resolution. Keep the
    # original uniform scale; copy only its approved tenth drawing.
    im=Image.open(source).convert('RGBA'); labels,regions=components(im)
    figures=[r for r in regions if r['count']>1500]; assert len(figures)==12
    figures.sort(key=lambda r:(r['box'][1]+r['box'][3])/2)
    figures=[r for row in range(3) for r in sorted(figures[row*4:row*4+4],key=lambda r:r['box'][0])]
    path=OUT/'Manifest.json';manifest=json.loads(path.read_text());scale=manifest['frames'][0]['uniform_scale']
    for index in indices:
        r=figures[index];x0,y0,x1,y1=r['box'];pixels=np.array(im)[y0:y1,x0:x1].copy()
        pixels[:,:,3]=np.where(labels[y0:y1,x0:x1]==r['id'],pixels[:,:,3],0)
        rgb=pixels[:,:,:3].astype(int);yy=np.arange(y1-y0)[:,None]
        shirt=(rgb[:,:,2]>rgb[:,:,0]) & (rgb[:,:,0]>65) & (rgb[:,:,1]-rgb[:,:,0]<35) & (rgb[:,:,2]-rgb[:,:,0]<65)
        shirt &= (yy>(y1-y0)*.38) & (yy<(y1-y0)*.55) & (pixels[:,:,3]>128)
        sx=np.where(shirt)[1];axis=(float(np.percentile(sx,5))+float(np.percentile(sx,95)))/2
        crop=Image.fromarray(pixels).resize((round((x1-x0)*scale),round((y1-y0)*scale)),Image.Resampling.NEAREST)
        frame=Image.new('RGBA',(128,128));frame.alpha_composite(crop,(round(64-axis*scale),120-crop.height))
        frame.save(ART/'Walk'/f'Walk_{index+1:02}.png')
        manifest['frames'][index]['source_box']=r['box'];manifest['frames'][index]['hip_axis']=axis+x0
        manifest['frames'][index]['correction_source']='Tools/Character2/'+source_name
    shutil.copy2(source,ROOT/'Tools/Character2'/source_name)
    path.write_text(json.dumps(manifest,indent=2));preview()

if __name__=='__main__':
    if sys.argv[1]=='preview': preview()
    elif sys.argv[1]=='frame10': correct_frames(Path(sys.argv[2]),[9],'Walk10CorrectedSource.png')
    elif sys.argv[1]=='row3': correct_frames(Path(sys.argv[2]),[8,9,10,11],'WalkLowerLegCorrectedSource.png')
    else: package(Path(sys.argv[1]))
