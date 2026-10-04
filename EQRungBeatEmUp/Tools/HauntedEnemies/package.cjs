// Export generated artwork; no procedural character drawing. Requires bundled sharp.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),zlib=require('zlib'),sharp=require('sharp');
const scan=require('./scan.cjs');
const root=path.resolve(__dirname,'../..'), art=path.join(root,'Assets/ArtAssets/Characters/Enemies');
const jobs=JSON.parse(fs.readFileSync(path.join(__dirname,'generation.json')));
const reactionStates=['Hurt_Light','Hurt_Heavy','Air_Hit','Knockdown','Downed','GetUp','Defeated'];
const roster=[
 {id:'Rusher',prefix:'rusher',height:100,states:['Idle','Walk','Sprint','Attack_Slash1','Attack_Slash2','Attack_Lunge','Recovery']},
 {id:'GrapplerBruiser',prefix:'grappler',height:116,states:['Idle','Walk','Grab_Start','Grab_Hold','BodySlam','HeavyAttack','Recovery']},
 {id:'Thrower',prefix:'thrower',height:100,states:['Idle','Walk','Throw_Notebook','Throw_Pens','Throw_Papers','Recovery','Alert']},
 {id:'Screamer',prefix:'screamer',height:100,states:['Idle','Walk','Scream_Wave','Wail_Burst','Telegraph','Recovery','Stunned']},
 {id:'Ambusher',prefix:'ambusher',height:70,states:['Idle_Crouched','Crawl','Hide','PopOut','LegTrip','Walk','Recovery']},
 {id:'Prefect',prefix:'prefect',height:106,states:['Idle','Walk','Whistle_Call','Talisman_Cast','BatonAttack','Command','Recovery']},
 {id:'WhiteGhostBoss',prefix:'boss',height:150,canvas:[192,192],ground:184,states:['Idle_Float','Glide','Invulnerable','Vulnerable','Stunned','Recovery_Rise'],attacks:['Warp_Start','Warp_Disappear','Warp_Appear','Attack_Swipe','Attack_CurseWave','Attack_Summon']},
 {id:'Totems',prefix:'totems',height:88,states:['Idle','Damaged','Destroyed']},
 {id:'Effects',prefix:'effects',height:64,canvas:[160,128],ground:64,states:['Notebook','Pen','ExamPaper','ScreamWave','StunBurst','CurseWave','TalismanSlip','SummonSeal']}
];
const spriteTemplate=fs.readFileSync(path.join(root,'Assets/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Idle/ThaiBadBoy_Idle_01.png.meta'),'utf8');
const clipTemplate=fs.readFileSync(path.join(root,'Assets/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Hurt/ThaiBadBoy_Hurt.anim'),'utf8');
const guid=()=>crypto.randomBytes(16).toString('hex');
function meta(file,kind='text',pivot=.0625) {
 const f=file+'.meta';if(fs.existsSync(f)) {let txt=fs.readFileSync(f,'utf8');if(kind==='sprite'){txt=txt.replace(/spritePivot: \{.+\}/,`spritePivot: {x: 0.5, y: ${pivot}}`);fs.writeFileSync(f,txt);}return txt.match(/^guid: (.+)$/m)[1];}
 const g=guid();let txt=`fileFormatVersion: 2\nguid: ${g}\n`;
 if(kind==='sprite') txt=spriteTemplate.replace(/^guid: .+$/m,`guid: ${g}`).replace(/spriteID: [a-f0-9]+/,`spriteID: ${guid()}`).replace(/spritePivot: \{.+\}/,`spritePivot: {x: 0.5, y: ${pivot}}`);
 else if(kind==='anim')txt+='NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 7400000\n';
 else txt+=(kind==='folder'?'folderAsset: yes\n':'')+'DefaultImporter:\n  externalObjects: {}\n';
 fs.writeFileSync(f,txt);return g;
}
function mkdir(dir) {fs.mkdirSync(dir,{recursive:true});let p=dir;while(p.startsWith(art)){meta(p,'folder');p=path.dirname(p);}}
function bounds(data,w,h) {let x0=w,y0=h,x1=-1,y1=-1;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]>=128){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}return x1<0?null:{left:x0,top:y0,width:x1-x0+1,height:y1-y0+1};}
function splitWide(b,r,cols) {
 const cw=r.info.width/cols;
 if(b.width<cw*1.45) return [b];
 let cuts=[];
 for(let c=1;c<cols;c++) {
  const nominal=c*cw;if(nominal<b.left+cw*.4||nominal>b.left+b.width-cw*.4)continue;
  let bx=-1,count=Infinity;
  for(let x=Math.round(nominal-cw*.12);x<=Math.round(nominal+cw*.12);x++){
   let n=0;for(let y=b.top;y<b.top+b.height;y++)if(r.data[(y*r.info.width+x)*4+3]>=128)n++;
   if(n<count){count=n;bx=x;}
  }
  cuts.push(bx);
 }
 if(!cuts.length)return[b];
 let x=b.left,parts=[];for(const end of [...cuts,b.left+b.width]){
  if(end>x)parts.push({...b,left:x,width:end-x});x=end;
 }return parts;
}
async function source(key,rows,cols=6,grid=false) {
 const job=jobs.find(j=>j.key===key);if(!job)throw Error('Missing source '+key);
 const owner=roster.find(e=>key===e.prefix||key.startsWith(e.prefix+'_'));const preserved=path.join(art,owner.id,'Source',key+'.png');
 const r=await scan(fs.existsSync(job.source)?job.source:preserved),groups=Array.from({length:rows},()=>[]);
 if(grid){for(let row=0;row<rows;row++)for(let col=0;col<cols;col++){
  const left=Math.round(col*r.info.width/cols),top=Math.round(row*r.info.height/rows);
  groups[row].push({left,top,width:Math.round((col+1)*r.info.width/cols)-left,height:Math.round((row+1)*r.info.height/rows)-top});
 }}else{
  for(const blob of r.blobs){if(blob.area<(key==='boss_attacks'?200:3000))continue;
   const row=Math.max(0,Math.min(rows-1,Math.round((blob.top+blob.height)/r.info.height*rows)-1));
   const pieces=key==='rusher_core'&&row===5?[blob]:splitWide(blob,r,cols);
   groups[row].push(...pieces);
  }groups.forEach(g=>g.sort((a,b)=>a.left-b.left));
  if(key==='boss_attacks')for(let row=0;row<rows;row++){
   const slots=Array.from({length:cols},()=>[]);
   for(const b of groups[row])slots[Math.min(cols-1,Math.floor((b.left+b.width/2)/r.info.width*cols))].push(b);
   groups[row]=slots.filter(s=>s.length).map(s=>{const left=Math.min(...s.map(b=>b.left)),top=Math.min(...s.map(b=>b.top));return {left,top,width:Math.max(...s.map(b=>b.left+b.width))-left,height:Math.max(...s.map(b=>b.top+b.height))-top};});
  }
 }
 return {key,job,...r,groups};
}
async function normalize(src,b,scale,w,h,ground,center=false) {
 const input=await sharp(src.data,{raw:{width:src.info.width,height:src.info.height,channels:4}}).extract({left:b.left,top:b.top,width:b.width,height:b.height}).raw().toBuffer();
 // Isolate the primary connected pose so neighboring tails/weapons cannot leak into an export.
 const seen=new Uint8Array(b.width*b.height),queue=new Int32Array(seen.length);let largest=[];
 for(let s=0;s<seen.length;s++){if(seen[s]||input[s*4+3]<128)continue;let head=0,tail=1;queue[0]=s;seen[s]=1;
  while(head<tail){const p=queue[head++],x=p%b.width,y=Math.floor(p/b.width);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const xx=x+dx,yy=y+dy;if(xx<0||xx>=b.width||yy<0||yy>=b.height)continue;const q=yy*b.width+xx;if(seen[q]||input[q*4+3]<128)continue;seen[q]=1;queue[tail++]=q;}}
  if(tail>largest.length)largest=Array.from(queue.subarray(0,tail));
 }
 const retained=new Uint8Array(seen.length);for(const p of largest)retained[p]=1;for(let p=0;p<seen.length;p++)if(!retained[p])input[p*4+3]=0;
 const bb=bounds(input,b.width,b.height);if(!bb)return Buffer.alloc(w*h*4);
 let rw=Math.max(1,Math.round(bb.width*scale)),rh=Math.max(1,Math.round(bb.height*scale));
 const fit=Math.min(1,(w-8)/rw,(center?h-8:ground-3)/rh);rw=Math.max(1,Math.round(rw*fit));rh=Math.max(1,Math.round(rh*fit));
 const pixels=await sharp(input,{raw:{width:b.width,height:b.height,channels:4}}).extract(bb).resize(rw,rh,{kernel:'nearest'}).raw().toBuffer();
 const out=Buffer.alloc(w*h*4),xx=Math.floor((w-rw)/2),yy=center?Math.floor((h-rh)/2):ground-rh;
 for(let y=0;y<rh;y++)for(let x=0;x<rw;x++){
  const p=(y*rw+x)*4,q=((y+yy)*w+x+xx)*4;if(pixels[p+3]<128)continue;
  out[q]=pixels[p];out[q+1]=pixels[p+1];out[q+2]=pixels[p+2];out[q+3]=255;
 }return out;
}
function assemble(frames,w,h,cols=6){const sw=w*cols,sh=h*Math.ceil(frames.length/cols),out=Buffer.alloc(sw*sh*4);frames.forEach((f,i)=>{for(let y=0;y<h;y++)f.copy(out,((Math.floor(i/cols)*h+y)*sw+(i%cols)*w)*4,y*w*4,(y+1)*w*4);});return {data:out,width:sw,height:sh};}
function cropRaw(data,sw,x,y,w,h){const out=Buffer.alloc(w*h*4);for(let yy=0;yy<h;yy++)data.copy(out,yy*w*4,((y+yy)*sw+x)*4,((y+yy)*sw+x+w)*4);return out;}
const u16=n=>{const b=Buffer.alloc(2);b.writeUInt16LE(n);return b;},u32=n=>{const b=Buffer.alloc(4);b.writeUInt32LE(n);return b;};
const str=s=>Buffer.concat([u16(Buffer.byteLength(s)),Buffer.from(s)]),chunk=(type,p)=>Buffer.concat([u32(p.length+6),u16(type),p]);
function aseprite(file,frames,w,h,state,durations){const packets=[];frames.forEach((f,i)=>{const chunks=[];
 if(i===0){chunks.push(chunk(0x2004,Buffer.concat([u16(1),Buffer.alloc(10),Buffer.from([255]),Buffer.alloc(3),str('Character')])));
 chunks.push(chunk(0x2018,Buffer.concat([u16(1),Buffer.alloc(8),u16(0),u16(frames.length-1),Buffer.from([0]),u16(0),Buffer.alloc(6),Buffer.alloc(3),Buffer.from([0]),str(state)])));}
 chunks.push(chunk(0x2005,Buffer.concat([u16(0),u16(0),u16(0),Buffer.from([255]),u16(2),Buffer.alloc(7),u16(w),u16(h),zlib.deflateSync(f)])));
 const head=Buffer.alloc(16);head.writeUInt32LE(16+chunks.reduce((s,c)=>s+c.length,0));head.writeUInt16LE(0xf1fa,4);head.writeUInt16LE(chunks.length,6);head.writeUInt16LE(durations[i],8);packets.push(Buffer.concat([head,...chunks]));});
 const header=Buffer.alloc(128);header.writeUInt32LE(128+packets.reduce((s,f)=>s+f.length,0));header.writeUInt16LE(0xa5e0,4);header.writeUInt16LE(frames.length,6);header.writeUInt16LE(w,8);header.writeUInt16LE(h,10);header.writeUInt16LE(32,12);header.writeUInt32LE(1,14);header.writeUInt16LE(100,18);header[34]=header[35]=1;fs.writeFileSync(file,Buffer.concat([header,...packets]));meta(file);
}
function writeClip(file,name,guids,durations,loop){let t=0;const keys=guids.map((g,i)=>{const k=`    - time: ${t.toFixed(3)}\n      value: {fileID: 21300000, guid: ${g}, type: 3}`;t+=durations[i]/1000;return k;});keys.push(`    - time: ${(t-.01).toFixed(3)}\n      value: {fileID: 21300000, guid: ${guids.at(-1)}, type: 3}`);
 let clip=clipTemplate.replace('m_Name: ThaiBadBoy_Hurt',`m_Name: ${name}`).replace(/    curve:\r?\n[\s\S]*?    attribute:/,`    curve:\n${keys.join('\n')}\n    attribute:`).replace(/    pptrCurveMapping:\r?\n[\s\S]*?  m_AnimationClipSettings:/,`    pptrCurveMapping:\n${[...guids,guids.at(-1)].map(g=>`    - {fileID: 21300000, guid: ${g}, type: 3}`).join('\n')}\n  m_AnimationClipSettings:`).replace(/m_StopTime: [\d.]+/,`m_StopTime: ${t.toFixed(3)}`).replace('m_LoopTime: 0',`m_LoopTime: ${loop?1:0}`);
 fs.writeFileSync(file,clip);meta(file,'anim');return t;
}
function looping(s,id){return ['Idle','Idle_Crouched','Walk','Sprint','Crawl','Downed','Idle_Float','Glide','Invulnerable','Vulnerable','Hide','Grab_Hold'].includes(s)||(id==='Effects'&&['Notebook','Pen','ExamPaper','TalismanSlip'].includes(s));}
function durations(state,n){const ms=/Idle|Downed|Hide|Invulnerable|Vulnerable/.test(state)?160:/Walk|Crawl|Glide/.test(state)?110:/Sprint/.test(state)?75:/Hurt|Air_Hit/.test(state)?90:/Knockdown|GetUp|Defeated|Destroyed/.test(state)?130:100;const out=Array(n).fill(ms);if(!looping(state,''))out[n-1]=Math.max(160,ms);return out;}
async function saveAnimation(enemy,state,frames,w,h,ground,sourceKey){const dir=path.join(art,enemy.id,'Animations',state);mkdir(dir);const stem=enemy.id+'_'+state,ms=durations(state,frames.length),loop=looping(state,enemy.id),guids=[],checks=[];
 for(let i=0;i<frames.length;i++){const filename=`${stem}_${String(i+1).padStart(2,'0')}.png`,file=path.join(dir,filename);await sharp(frames[i],{raw:{width:w,height:h,channels:4}}).png().toFile(file);guids.push(meta(file,'sprite',enemy.id==='Effects'?.5:(h-ground)/h));const b=bounds(frames[i],w,h);checks.push({file:filename,bounds:b,sha256:crypto.createHash('sha256').update(frames[i]).digest('hex')});}
 const montage=assemble(frames,w,h),sheetFile=path.join(dir,stem+'_Sheet.png');await sharp(montage.data,{raw:{width:montage.width,height:montage.height,channels:4}}).png().toFile(sheetFile);meta(sheetFile);
 const json={frames:frames.map((f,i)=>({filename:`${stem}_${String(i+1).padStart(2,'0')}.png`,frame:{x:(i%6)*w,y:Math.floor(i/6)*h,w,h},rotated:false,trimmed:false,spriteSourceSize:{x:0,y:0,w,h},sourceSize:{w,h},duration:ms[i]})),meta:{app:'Haunted Enemies export',image:stem+'_Sheet.png',format:'RGBA8888',size:{w:montage.width,h:montage.height},scale:'1',frameTags:[{name:state,from:0,to:frames.length-1,direction:'forward'}]}};
 const jf=path.join(dir,stem+'_Sheet.json');fs.writeFileSync(jf,JSON.stringify(json,null,2));meta(jf);
 await sharp(Buffer.concat(frames),{raw:{width:w,height:h*frames.length,channels:4,pageHeight:h}}).gif({loop:0,delay:ms,effort:3}).toFile(path.join(dir,stem+'_Preview.gif'));meta(path.join(dir,stem+'_Preview.gif'));
 aseprite(path.join(dir,stem+'.aseprite'),frames,w,h,state,ms);
 const duration=writeClip(path.join(dir,stem+'.anim'),stem,guids,ms,loop);
 return {name:state,frameCount:frames.length,uniqueFrames:new Set(checks.map(c=>c.sha256)).size,durationSeconds:duration,loop,source:sourceKey,folder:path.relative(root,dir).replace(/\\/g,'/'),frames:checks};
}
async function main(){mkdir(art);const report={builtInImageGen:true,pixelsPerUnit:100,filter:'Point',compression:'None',alpha:'binary',enemies:[]};
 for(const enemy of roster){const w=enemy.canvas?.[0]||160,h=enemy.canvas?.[1]||128,ground=enemy.ground??120,dir=path.join(art,enemy.id),sourceDir=path.join(dir,'Source');mkdir(sourceDir);
  const keys=jobs.filter(j=>j.key.startsWith(enemy.prefix+'_')||j.key===enemy.prefix);for(const j of keys){const f=path.join(sourceDir,j.key+'.png');if(fs.existsSync(j.source))fs.copyFileSync(j.source,f);else if(!fs.existsSync(f))throw Error('Missing preserved source '+f);meta(f);}
  let primary=await source(enemy.prefix==='boss'?'boss_presence':enemy.prefix==='totems'?'totems':enemy.prefix==='effects'?'effects':enemy.prefix+'_core',enemy.states.length,6,enemy.prefix==='effects'||enemy.prefix==='totems');
  const idleBox=primary.groups[0][0];if(!idleBox)throw Error('No idle '+enemy.id);
  const idlePixels=await sharp(primary.data,{raw:{width:primary.info.width,height:primary.info.height,channels:4}}).extract({left:idleBox.left,top:idleBox.top,width:idleBox.width,height:idleBox.height}).raw().toBuffer();const idleBounds=bounds(idlePixels,idleBox.width,idleBox.height);
  const scale=enemy.height/idleBounds.height,animations=[];
  for(let row=0;row<enemy.states.length;row++)animations.push({name:enemy.states[row],src:primary,boxes:primary.groups[row],scale});
  if(enemy.prefix==='boss'){const attacks=await source('boss_attacks',6,6,false);enemy.attacks.forEach((name,row)=>animations.push({name,src:attacks,boxes:attacks.groups[row],scale:scale*primary.info.width/attacks.info.width}));}
  if(!['totems','effects'].includes(enemy.prefix)){const react=await source(enemy.prefix+'_reactions',7,6,false);reactionStates.forEach((name,row)=>animations.push({name,src:react,boxes:react.groups[row],scale:scale*primary.info.width/react.info.width}));}
  if(enemy.prefix==='ambusher'){const leg=await source('ambusher_legtrip',2,4,true),kd=await source('ambusher_knockdown',2,4,true);const cell=leg.groups[0][0],px=await sharp(leg.data,{raw:{width:leg.info.width,height:leg.info.height,channels:4}}).extract(cell).raw().toBuffer(),b=bounds(px,cell.width,cell.height),newScale=enemy.height/b.height;
   for(const [name,src]of[['LegTrip',leg],['Knockdown',kd]]){const entry=animations.find(a=>a.name===name);entry.src=src;entry.boxes=src.groups.flat();entry.scale=newScale*leg.info.width/src.info.width;}}
  const all=[];
  for(const a of animations){if(a.boxes.length<4)throw Error(enemy.id+' '+a.name+' too few poses: '+a.boxes.length);a.frames=[];for(const b of a.boxes)a.frames.push(await normalize(a.src,b,a.scale,w,h,ground,enemy.id==='Effects'));all.push(...a.frames);}
  // One shared <=48-colour palette per character, binary alpha and no dithering.
  const allSheet=assemble(all,w,h);const indexed=await sharp(allSheet.data,{raw:{width:allSheet.width,height:allSheet.height,channels:4}}).png({palette:true,colours:32,dither:0}).toBuffer();const quantized=await sharp(indexed).ensureAlpha().raw().toBuffer();let k=0;
  // Pin decoded RGB values to a shared palette as PNG decoding may expand colours.
  const histogram=new Map();for(let p=0;p<quantized.length;p+=4)if(quantized[p+3]>=128){const c=(quantized[p]<<16)|(quantized[p+1]<<8)|quantized[p+2];histogram.set(c,(histogram.get(c)||0)+1);}
  const buckets=[[...histogram].map(([c,n])=>({rgb:[(c>>16)&255,(c>>8)&255,c&255],n}))];
  const extent=b=>[0,1,2].map(ch=>Math.max(...b.map(c=>c.rgb[ch]))-Math.min(...b.map(c=>c.rgb[ch])));
  while(buckets.length<47){let index=-1,score=-1;for(let i=0;i<buckets.length;i++){if(buckets[i].length<2)continue;const s=Math.max(...extent(buckets[i]))*Math.sqrt(buckets[i].reduce((n,c)=>n+c.n,0));if(s>score){score=s;index=i;}}if(index<0)break;const b=buckets.splice(index,1)[0],ranges=extent(b),channel=ranges.indexOf(Math.max(...ranges));b.sort((a,c)=>a.rgb[channel]-c.rgb[channel]);const half=b.reduce((n,c)=>n+c.n,0)/2;let count=0,cut=1;for(let i=0;i<b.length-1;i++){count+=b[i].n;cut=i+1;if(count>=half)break;}buckets.push(b.slice(0,cut),b.slice(cut));}
  const palette=buckets.map(b=>{const total=b.reduce((n,c)=>n+c.n,0);return [0,1,2].map(ch=>Math.round(b.reduce((n,c)=>n+c.rgb[ch]*c.n,0)/total));}),mapped=new Map();
  for(let p=0;p<quantized.length;p+=4){if(quantized[p+3]<128){quantized.fill(0,p,p+4);continue;}const c=(quantized[p]<<16)|(quantized[p+1]<<8)|quantized[p+2];let rgb=mapped.get(c);if(!rgb){let distance=Infinity;for(const candidate of palette){const d=(candidate[0]-quantized[p])**2+(candidate[1]-quantized[p+1])**2+(candidate[2]-quantized[p+2])**2;if(d<distance){distance=d;rgb=candidate;}}mapped.set(c,rgb);}quantized[p]=rgb[0];quantized[p+1]=rgb[1];quantized[p+2]=rgb[2];quantized[p+3]=255;}
  for(const a of animations){a.frames=a.frames.map(()=>{const f=cropRaw(quantized,allSheet.width,(k%6)*w,Math.floor(k/6)*h,w,h);k++;return f;});}
  // Connect downed/get-up endpoints exactly to the authored knockdown and idle.
  if(enemy.id!=='Totems'&&enemy.id!=='Effects'){const kd=animations.find(a=>a.name==='Knockdown'),down=animations.find(a=>a.name==='Downed'),up=animations.find(a=>a.name==='GetUp');if(kd&&down&&up){down.frames=down.frames.filter(f=>{const b=bounds(f,w,h);return b&&b.height<enemy.height*.8;});if(!down.frames.length)down.frames=[kd.frames.at(-1)];down.frames[0]=kd.frames.at(-1);up.frames[0]=kd.frames.at(-1);up.frames[up.frames.length-1]=animations[0].frames[0];}}
  const result={id:enemy.id,canvas:[w,h],ground,pivot:[.5,enemy.id==='Effects'?.5:(h-ground)/h],standingOrCrouchedIdleHeight:enemy.height,animations:[]};
  for(const a of animations){const entry=await saveAnimation(enemy,a.name,a.frames,w,h,ground,a.src.key);result.animations.push(entry);}
  const manifest=path.join(dir,'manifest.json');fs.writeFileSync(manifest,JSON.stringify(result,null,2));meta(manifest);report.enemies.push(result);console.log(enemy.id,result.animations.length+' animations',result.animations.reduce((n,a)=>n+a.frameCount,0)+' frames');
  // Labeled review sheets are not gameplay textures.
  const review=assemble(animations.flatMap(a=>a.frames),w,h,6);await sharp(review.data,{raw:{width:review.width,height:review.height,channels:4}}).png().toFile(path.join(dir,enemy.id+'_AllFrames.png'));meta(path.join(dir,enemy.id+'_AllFrames.png'));
 }
 const rf=path.join(art,'ArtValidation.json');fs.writeFileSync(rf,JSON.stringify(report,null,2));meta(rf);console.log('TOTAL',report.enemies.reduce((n,e)=>n+e.animations.length,0),'animations',report.enemies.reduce((n,e)=>n+e.animations.reduce((m,a)=>m+a.frameCount,0),0),'frames');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
