import fs from 'node:fs';
import zlib from 'node:zlib';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='Assets/EQ_Rung_BeatEmUp',art=root+'/ArtAssets/Characters/BlueShirtGuy/Animations/Attack1';
const docs='Documentation/Art/BlueShirtGuyNeckRepair',backup=docs+'/Original';
async function write(path,data){
 if(fs.existsSync(path)&&fs.readFileSync(path).equals(data))return;
 for(let attempt=0;attempt<20;attempt++){try{fs.writeFileSync(path,data);return;}catch(e){if(attempt===19)throw e;await new Promise(resolve=>setTimeout(resolve,200));}}
}
fs.mkdirSync(backup,{recursive:true});
const name=i=>'BlueShirtGuy_Attack1_'+String(i).padStart(2,'0')+'.png';
function preserve(path){const dst=backup+'/'+path.replace(root+'/','');fs.mkdirSync(dst.slice(0,dst.lastIndexOf('/')),{recursive:true});if(!fs.existsSync(dst))fs.copyFileSync(path,dst);return dst;}
const asePath=art+'/BlueShirtGuy_Attack1.aseprite';
const originalAse=fs.readFileSync(preserve(asePath));
for(const file of ['BlueShirtGuy_Attack1_Sheet.png','BlueShirtGuy_Attack1_Preview.gif'])preserve(art+'/'+file);
for(let n=1;n<=3;n++){preserve(root+'/Attacks/Punch'+n+'.asset');preserve(root+'/Animations/Punch '+n+'.anim');}
const original=[];
for(let n=1;n<=12;n++){preserve(art+'/'+name(n));original.push(await sharp(preserve(root+'/Sprites/'+name(n))).ensureAlpha().raw().toBuffer());}
const paletteMap=new Map();for(const pixels of original)for(let k=0;k<pixels.length;k+=4)if(pixels[k+3])paletteMap.set(pixels.subarray(k,k+3).join(','),[...pixels.subarray(k,k+3)]);
const palette=[...paletteMap.values()];
function components(pixels){
 const labels=new Int32Array(128*128),parts=[];
 for(let p=0;p<labels.length;p++)if(!labels[p]&&pixels[p*4+3]){
  const id=parts.length+1,queue=[p],b={x0:128,y0:128,x1:-1,y1:-1};labels[p]=id;
  for(let k=0;k<queue.length;k++){const pos=queue[k],x=pos%128,y=pos>>7;b.x0=Math.min(b.x0,x);b.x1=Math.max(b.x1,x);b.y0=Math.min(b.y0,y);b.y1=Math.max(b.y1,y);
   for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const xx=x+dx,yy=y+dy,q=yy*128+xx;if(xx>=0&&xx<128&&yy>=0&&yy<128&&!labels[q]&&pixels[q*4+3]){labels[q]=id;queue.push(q);}}
  }parts.push({id,count:queue.length,...b});
 }return {labels,parts};
}
const frames=[],report=[];
for(let n=1;n<=12;n++){
 const source=original[n-1],fixed=Buffer.from(source),{labels,parts}=components(source);
 const body=parts.reduce((a,b)=>a.count>b.count?a:b),head=parts.filter(p=>p.count>100).sort((a,b)=>a.y0-b.y0)[0];
 const edits=[];
 if(n>=2&&n<=11&&head.id!==body.id){
  // Bridge only short vertical transparent runs at the head/collar join. Never overwrite drawn pixels.
  for(let x=head.x0;x<=head.x1;x++)for(let y=Math.max(head.y0,head.y1-6);y<=head.y1;y++){
   if(labels[y*128+x]!==head.id)continue;
   let end=y+1;while(end<128&&!source[(end*128+x)*4+3]&&end-y<=6)end++;
   if(end-y<2||end-y>6||labels[end*128+x]!==body.id)continue;
   const a=(y*128+x)*4,b=(end*128+x)*4;
   for(let yy=y+1;yy<end;yy++){
    const t=(yy-y)/(end-y),wanted=[0,1,2].map(c=>source[a+c]*(1-t)+source[b+c]*t);
    let best=palette[0],distance=Infinity;for(const color of palette){const d=color.reduce((v,c,i)=>v+(c-wanted[i])**2,0);if(d<distance){distance=d;best=color;}}
    const k=(yy*128+x)*4;fixed[k]=best[0];fixed[k+1]=best[1];fixed[k+2]=best[2];fixed[k+3]=255;edits.push([x,yy]);
   }
  }
  const connected=components(fixed).parts;
  if(connected.filter(p=>p.count>100).length!==1)throw Error('Head still disconnected in frame '+n);
 }
 for(let k=0;k<source.length;k+=4)if(source[k+3]&&!source.subarray(k,k+4).equals(fixed.subarray(k,k+4)))throw Error('Existing artwork changed');
 report.push({frame:n,filledPixels:edits.length,coordinates:edits,headAttached:components(fixed).parts.filter(p=>p.count>100).length===1});frames.push(fixed);
 for(const dir of [art,root+'/Sprites'])await write(dir+'/'+name(n),await sharp(fixed,{raw:{width:128,height:128,channels:4}}).png().toBuffer());
}
// Update compressed source cels while retaining every layer/tag/profile and original frame duration.
let cursor=128;const nativeFrames=[];
for(let f=0;f<12;f++){
 const end=cursor+originalAse.readUInt32LE(cursor),chunks=[];let q=cursor+16;
 while(q<end){const length=originalAse.readUInt32LE(q),type=originalAse.readUInt16LE(q+4);let chunk=Buffer.from(originalAse.subarray(q,q+length));
  if(type===0x2005){if(chunk.readUInt16LE(13)!==2||chunk.readUInt16LE(22)!==128||chunk.readUInt16LE(24)!==128)throw Error('Unexpected cel format');
   const raw=zlib.inflateSync(chunk.subarray(26));if(!raw.equals(original[f]))throw Error('Source PNG/cel mismatch');
   chunk=Buffer.concat([chunk.subarray(0,26),zlib.deflateSync(frames[f])]);chunk.writeUInt32LE(chunk.length);
  }chunks.push(chunk);q+=length;
 }
 const header=Buffer.from(originalAse.subarray(cursor,cursor+16));header.writeUInt32LE(16+chunks.reduce((sum,c)=>sum+c.length,0));nativeFrames.push(Buffer.concat([header,...chunks]));cursor=end;
}
const header=Buffer.from(originalAse.subarray(0,128));header.writeUInt32LE(128+nativeFrames.reduce((sum,f)=>sum+f.length,0));await write(asePath,Buffer.concat([header,...nativeFrames]));
const sheet=Buffer.alloc(512*384*4);for(let n=0;n<12;n++)for(let y=0;y<128;y++)frames[n].copy(sheet,((Math.floor(n/4)*128+y)*512+n%4*128)*4,y*128*4,(y+1)*128*4);
await sharp(sheet,{raw:{width:512,height:384,channels:4}}).png().toFile(art+'/BlueShirtGuy_Attack1_Sheet.png');
const gifPath=art+'/BlueShirtGuy_Attack1_Preview.gif',gifInfo=await sharp(preserve(gifPath),{animated:true}).metadata();
await sharp(Buffer.concat(frames),{raw:{width:128,height:1536,channels:4,pageHeight:128}}).gif({delay:gifInfo.delay,loop:gifInfo.loop??0}).toFile(gifPath);
const before=await sharp(original[4],{raw:{width:128,height:128,channels:4}}).extract({left:40,top:25,width:38,height:23}).resize(380,230,{kernel:'nearest'}).png().toBuffer();
const after=await sharp(frames[4],{raw:{width:128,height:128,channels:4}}).extract({left:40,top:25,width:38,height:23}).resize(380,230,{kernel:'nearest'}).png().toBuffer();
await sharp({create:{width:780,height:230,channels:4,background:'#242424'}}).composite([{input:before,left:0,top:0},{input:after,left:400,top:0}]).png().toFile(docs+'/NeckBeforeAfter.png');
await sharp(frames[4],{raw:{width:128,height:128,channels:4}}).resize(512,512,{kernel:'nearest'}).png().toFile(docs+'/RepairedFrame05.png');
for(let n=1;n<=3;n++)for(const path of [root+'/Attacks/Punch'+n+'.asset',root+'/Animations/Punch '+n+'.anim'])if(!fs.readFileSync(path).equals(fs.readFileSync(preserve(path))))throw Error('Gameplay asset changed');
fs.writeFileSync(docs+'/PixelRepairResults.json',JSON.stringify({checks:['All original opaque pixels unchanged','Frame 1 and 12 pixel-identical','All heads connected to bodies','Original 12 poses and durations retained','All three gameplay assets/clips unchanged'],report},null,2));
console.log(report.map(r=>'Frame '+r.frame+': '+r.filledPixels+' neck pixels filled; attached='+r.headAttached).join('\n'));
