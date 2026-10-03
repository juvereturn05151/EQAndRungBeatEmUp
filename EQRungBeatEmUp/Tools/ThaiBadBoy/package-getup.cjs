// Deterministic export only: crop generated poses, nearest-size sampling,
// reference palette, binary alpha, shared canvas/pivot, previews and Unity clip.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const zlib = require('zlib');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const art = path.join(root, 'Assets/ArtAssets/Characters/NPCs/ThaiBadBoy');
const out = path.join(art, 'Animations/GetUp');
const source = path.join(art, 'Source/GetUp.png');
const durations = [120, 180, 180, 160, 160];
const guid = () => crypto.randomBytes(16).toString('hex');
const u16 = n => { const b = Buffer.alloc(2); b.writeUInt16LE(n); return b; };
const u32 = n => { const b = Buffer.alloc(4); b.writeUInt32LE(n); return b; };
const str = s => Buffer.concat([u16(Buffer.byteLength(s)), Buffer.from(s)]);
const chunk = (type, payload) => Buffer.concat([u32(payload.length + 6), u16(type), payload]);
function box(data, w, h) {
  let x0=w,y0=h,x1=-1,y1=-1;
  for(let y=0;y<h;y++) for(let x=0;x<w;x++) if(data[(y*w+x)*4+3]>=128){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}
  if(x1<0) throw Error('Empty pose');
  return {left:x0,top:y0,width:x1-x0+1,height:y1-y0+1,bottom:y1};
}
function textMeta(file) {
  if(!fs.existsSync(file+'.meta')) fs.writeFileSync(file+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}
function folderMeta(dir) {
  if(!fs.existsSync(dir+'.meta')) fs.writeFileSync(dir+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}
function spriteMeta(file) {
  if(!fs.existsSync(file+'.meta')) {
    let meta=fs.readFileSync(path.join(art,'Animations/Idle/ThaiBadBoy_Idle_01.png.meta'),'utf8');
    meta=meta.replace(/^guid: .+$/m,`guid: ${guid()}`).replace(/spriteID: [a-f0-9]+/,`spriteID: ${guid()}`);
    fs.writeFileSync(file+'.meta',meta);
  }
  return fs.readFileSync(file+'.meta','utf8').match(/^guid: (.+)$/m)[1];
}
async function main() {
  fs.mkdirSync(out,{recursive:true});folderMeta(out);
  const idle=await sharp(path.join(art,'Animations/Idle/ThaiBadBoy_Idle_01.png')).ensureAlpha().raw().toBuffer();
  const prone=await sharp(path.join(art,'Animations/Knockdown/ThaiBadBoy_Knockdown_06.png')).ensureAlpha().raw().toBuffer();
  const ground=box(idle,160,128).bottom;
  const paletteMap=new Map();
  for(const dir of ['Idle','Knockdown']) for(const name of fs.readdirSync(path.join(art,'Animations',dir)).filter(x=>/_\d\d\.png$/.test(x))) {
    const pixels=await sharp(path.join(art,'Animations',dir,name)).ensureAlpha().raw().toBuffer();
    for(let p=0;p<pixels.length;p+=4) if(pixels[p+3]===255) {const c=[pixels[p],pixels[p+1],pixels[p+2]];paletteMap.set(c.join(','),c);}
  }
  const palette=[...paletteMap.values()];
  const matchCache=new Map();
  const match=(r,g,b)=>{
    const key=(r<<16)|(g<<8)|b;if(matchCache.has(key)) return matchCache.get(key);
    let best=palette[0],distance=Infinity;
    for(const c of palette){const d=(r-c[0])**2+(g-c[1])**2+(b-c[2])**2;if(d<distance){distance=d;best=c;}}
    matchCache.set(key,best);return best;
  };
  // Crops exclude adjacent drawings; all three poses use the same scale factor.
  const crops=[{left:580,top:155,width:510,height:290},{left:1115,top:105,width:390,height:345},{left:105,top:535,width:335,height:425}];
  const frames=[prone];
  for(const crop of crops){
    const full=await sharp(source).extract(crop).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const bounds=box(full.data,full.info.width,full.info.height);
    const w=Math.round(bounds.width*.225),h=Math.round(bounds.height*.225);
    const pose=await sharp(full.data,{raw:{width:full.info.width,height:full.info.height,channels:4}})
      .extract({left:bounds.left,top:bounds.top,width:bounds.width,height:bounds.height})
      .resize(w,h,{kernel:'nearest'}).raw().toBuffer();
    const canvas=Buffer.alloc(160*128*4);
    const left=Math.round(80-w/2),top=ground-h+1;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){
      const p=(y*w+x)*4;if(pose[p+3]<128)continue;
      const c=match(pose[p],pose[p+1],pose[p+2]);const dst=((y+top)*160+x+left)*4;
      canvas[dst]=c[0];canvas[dst+1]=c[1];canvas[dst+2]=c[2];canvas[dst+3]=255;
    }
    frames.push(canvas);
  }
  frames.push(idle);
  const names=frames.map((_,i)=>`ThaiBadBoy_GetUp_${String(i+1).padStart(2,'0')}.png`);
  const guids=[];
  for(let i=0;i<frames.length;i++) {
    const f=path.join(out,names[i]);await sharp(frames[i],{raw:{width:160,height:128,channels:4}}).png().toFile(f);guids.push(spriteMeta(f));
  }
  const sheet=Buffer.alloc(640*256*4);
  frames.forEach((f,i)=>{const x=(i%4)*160,y=Math.floor(i/4)*128;for(let row=0;row<128;row++)f.copy(sheet,((y+row)*640+x)*4,row*160*4,(row+1)*160*4);});
  const sheetFile=path.join(out,'ThaiBadBoy_GetUp_Sheet.png');
  await sharp(sheet,{raw:{width:640,height:256,channels:4}}).png().toFile(sheetFile);
  // Contact sheet is a review/export image; the individual PNGs drive the clip.
  textMeta(sheetFile);
  const json={frames:names.map((filename,i)=>({filename,frame:{x:(i%4)*160,y:Math.floor(i/4)*128,w:160,h:128},rotated:false,trimmed:false,spriteSourceSize:{x:0,y:0,w:160,h:128},sourceSize:{w:160,h:128},duration:durations[i]})),meta:{app:'Aseprite-compatible export',image:'ThaiBadBoy_GetUp_Sheet.png',format:'RGBA8888',size:{w:640,h:256},scale:'1',frameTags:[{name:'GetUp',from:0,to:4,direction:'forward'}],layers:[{name:'Character',opacity:255,blendMode:'normal'}]}};
  const jsonFile=path.join(out,'ThaiBadBoy_GetUp_Sheet.json');fs.writeFileSync(jsonFile,JSON.stringify(json,null,2)+'\n');textMeta(jsonFile);
  // Preview repeats with a pause; the gameplay clip does not loop.
  const previewFrames=await Promise.all(frames.map(f=>sharp(f,{raw:{width:160,height:128,channels:4}}).resize(640,512,{kernel:'nearest'}).raw().toBuffer()));
  await sharp(Buffer.concat(previewFrames),{raw:{width:640,height:2560,channels:4,pageHeight:512}}).gif({loop:0,delay:[120,180,180,160,600],effort:7}).toFile(path.join(out,'ThaiBadBoy_GetUp_Preview.gif'));
  textMeta(path.join(out,'ThaiBadBoy_GetUp_Preview.gif'));
  await sharp(sheet,{raw:{width:640,height:256,channels:4}}).resize(1280,512,{kernel:'nearest'}).png().toFile(path.join(out,'ThaiBadBoy_GetUp_Review.png'));textMeta(path.join(out,'ThaiBadBoy_GetUp_Review.png'));
  // Native editable Aseprite file: one RGBA pixel layer and five timed cels.
  const aseFrames=[];
  for(let i=0;i<frames.length;i++){
    const chunks=[];
    if(i===0){chunks.push(chunk(0x2004,Buffer.concat([u16(1),u16(0),u16(0),u16(0),u16(0),u16(0),Buffer.from([255]),Buffer.alloc(3),str('Character')])));
      chunks.push(chunk(0x2018,Buffer.concat([u16(1),Buffer.alloc(8),u16(0),u16(4),Buffer.from([0]),u16(0),Buffer.alloc(6),Buffer.alloc(3),Buffer.from([0]),str('GetUp')])));}
    chunks.push(chunk(0x2005,Buffer.concat([u16(0),u16(0),u16(0),Buffer.from([255]),u16(2),Buffer.alloc(7),u16(160),u16(128),zlib.deflateSync(frames[i])])));
    const head=Buffer.alloc(16);head.writeUInt32LE(16+chunks.reduce((s,c)=>s+c.length,0));head.writeUInt16LE(0xf1fa,4);head.writeUInt16LE(chunks.length,6);head.writeUInt16LE(durations[i],8);aseFrames.push(Buffer.concat([head,...chunks]));
  }
  const header=Buffer.alloc(128);header.writeUInt32LE(128+aseFrames.reduce((s,f)=>s+f.length,0));header.writeUInt16LE(0xa5e0,4);header.writeUInt16LE(5,6);header.writeUInt16LE(160,8);header.writeUInt16LE(128,10);header.writeUInt16LE(32,12);header.writeUInt32LE(1,14);header.writeUInt16LE(100,18);header[32]=0;header[34]=1;header[35]=1;
  const aseFile=path.join(out,'ThaiBadBoy_GetUp.aseprite');fs.writeFileSync(aseFile,Buffer.concat([header,...aseFrames]));textMeta(aseFile);
  // Same SpriteRenderer binding as the other ThaiBadBoy animation clips.
  let clip=fs.readFileSync(path.join(art,'Animations/Hurt/ThaiBadBoy_Hurt.anim'),'utf8');
  let t=0;const keys=guids.map((g,i)=>{const v=`    - time: ${t.toFixed(3)}\n      value: {fileID: 21300000, guid: ${g}, type: 3}`;t+=durations[i]/1000;return v;});
  // Unity includes the last sample's 1/100 second in AnimationClip.length.
  keys.push(`    - time: ${(t-.01).toFixed(3)}\n      value: {fileID: 21300000, guid: ${guids[4]}, type: 3}`);
  clip=clip.replace('m_Name: ThaiBadBoy_Hurt','m_Name: ThaiBadBoy_GetUp').replace(/    curve:\r?\n[\s\S]*?    attribute:/,`    curve:\n${keys.join('\n')}\n    attribute:`).replace(/    pptrCurveMapping:\r?\n[\s\S]*?  m_AnimationClipSettings:/,`    pptrCurveMapping:\n${[...guids,guids[4]].map(g=>`    - {fileID: 21300000, guid: ${g}, type: 3}`).join('\n')}\n  m_AnimationClipSettings:`).replace(/m_StopTime: [\d.]+/,`m_StopTime: ${t.toFixed(3)}`);
  const clipFile=path.join(out,'ThaiBadBoy_GetUp.anim');fs.writeFileSync(clipFile,clip);
  if(!fs.existsSync(clipFile+'.meta'))fs.writeFileSync(clipFile+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 7400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
  const checks=frames.map((f,i)=>{const b=box(f,160,128);const colors=new Set();for(let p=0;p<f.length;p+=4){if(f[p+3]!==0&&f[p+3]!==255)throw Error('Nonbinary alpha');if(f[p+3])colors.add([...f.subarray(p,p+3)].join(','));}return {frame:names[i],bounds:b,colors:colors.size,sha256:crypto.createHash('sha256').update(f).digest('hex')};});
  if(new Set(checks.map(c=>c.sha256)).size!==5)throw Error('Duplicate frames');
  if(checks.slice(1).some(c=>c.bounds.bottom!==ground))throw Error('Ground mismatch');
  const result={canvas:[160,128],pivot:[.5,.0625],pixelsPerUnit:100,durationsMs:durations,clipDurationSeconds:t,loop:false,referencePaletteColors:palette.length,firstFrameExactKnockdown:true,lastFrameExactIdle:true,frames:checks};
  const report=path.join(out,'GetUp_Validation.json');fs.writeFileSync(report,JSON.stringify(result,null,2)+'\n');textMeta(report);console.log(JSON.stringify(result,null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
