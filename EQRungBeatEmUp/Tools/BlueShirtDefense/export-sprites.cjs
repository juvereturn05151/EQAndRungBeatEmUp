// Asset export only: reference-palette mapping, nearest sampling, binary alpha,
// and shared grounding. Drawings are authored by image_gen, never by this script.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..');
const art=path.join(root,'Assets/ArtAssets/Characters/BlueShirtGuy');
const guid=()=>crypto.randomBytes(16).toString('hex');
function bbox(data,w,h){let l=w,t=h,r=-1,b=-1;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]>=128){l=Math.min(l,x);t=Math.min(t,y);r=Math.max(r,x);b=Math.max(b,y);}return{left:l,top:t,width:r-l+1,height:b-t+1};}
function meta(file){if(fs.existsSync(file+'.meta'))return;let template=fs.readFileSync(path.join(root,'Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_Attack1_01.png.meta'),'utf8');template=template.replace(/^guid: .+$/m,'guid: '+guid()).replace(/spriteID: [a-f0-9]+/,'spriteID: '+guid());fs.writeFileSync(file+'.meta',template);}
(async()=>{
const colors=new Map();for(const name of fs.readdirSync(path.join(root,'Assets/EQ_Rung_BeatEmUp/Sprites')).filter(n=>n.startsWith('BlueShirtGuy')&&n.endsWith('.png'))){const p=await sharp(path.join(root,'Assets/EQ_Rung_BeatEmUp/Sprites',name)).ensureAlpha().raw().toBuffer();for(let i=0;i<p.length;i+=4)if(p[i+3]===255){const c=[p[i],p[i+1],p[i+2]];colors.set(c.join(','),c);}}
const palette=[...colors.values()],cache=new Map(),previews=[];
function nearest(r,g,b){const k=(r<<16)|(g<<8)|b;if(cache.has(k))return cache.get(k);let best=palette[0],d=Infinity;for(const c of palette){const v=(r-c[0])**2+(g-c[1])**2+(b-c[2])**2;if(v<d){d=v;best=c;}}cache.set(k,best);return best;}
for(const [name,cols,rows,height] of [['Parry',2,1,106],['KnockDown',2,2,100]]){
const file=path.join(art,'Source/Defense',name+'.png'),info=await sharp(file).metadata();const cw=Math.floor(info.width/cols),ch=Math.floor(info.height/rows),poses=[];
const crops=name==='KnockDown'?[{left:100,top:60,width:530,height:580},{left:730,top:160,width:630,height:520},{left:50,top:750,width:690,height:340},{left:740,top:750,width:640,height:340}]:Array.from({length:cols*rows},(_,i)=>({left:(i%cols)*cw,top:Math.floor(i/cols)*ch,width:cw,height:ch}));
for(const crop of crops){const rgba=await sharp(file).extract(crop).ensureAlpha().raw().toBuffer();const b=bbox(rgba,crop.width,crop.height);poses.push({rgba,b,crop});}
const scale=height/poses[0].b.height,out=path.join(art,'Animations',name);fs.mkdirSync(out,{recursive:true});
for(let i=0;i<poses.length;i++){const {rgba,b,crop}=poses[i];let w=Math.round(b.width*scale),h=Math.round(b.height*scale);if(w>154||h>116)throw Error('Pose exceeds native canvas; inspect source');const pixels=await sharp(rgba,{raw:{width:crop.width,height:crop.height,channels:4}}).extract(b).resize(w,h,{kernel:'nearest'}).raw().toBuffer();const canvas=Buffer.alloc(160*128*4),x0=Math.round(80-w/2),y0=120-h;for(let y=0;y<h;y++)for(let x=0;x<w;x++){const k=(y*w+x)*4;if(pixels[k+3]<128)continue;const c=nearest(pixels[k],pixels[k+1],pixels[k+2]),p=((y+y0)*160+x+x0)*4;canvas[p]=c[0];canvas[p+1]=c[1];canvas[p+2]=c[2];canvas[p+3]=255;}
const target=path.join(out,`BlueShirtGuy_${name}_${String(i+1).padStart(2,'0')}.png`);await sharp(canvas,{raw:{width:160,height:128,channels:4}}).png().toFile(target);meta(target);previews.push(canvas);console.log(name,i+1,w,h,'ground row119');}
}
const sheet=Buffer.alloc(480*256*4);previews.forEach((f,i)=>{for(let y=0;y<128;y++)f.copy(sheet,((Math.floor(i/3)*128+y)*480+(i%3)*160)*4,y*640,(y+1)*640);});const review=path.join(art,'Source/Defense/NativeReview.png');await sharp(sheet,{raw:{width:480,height:256,channels:4}}).resize(1440,768,{kernel:'nearest'}).png().toFile(review);console.log('All six exported poses use only existing palette colors and binary alpha; review:',review);
})().catch(e=>{console.error(e);process.exitCode=1;});
