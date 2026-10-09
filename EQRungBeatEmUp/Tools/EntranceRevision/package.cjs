const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..');
const base=path.join(root,'Assets/EQ_Rung_BeatEmUp/ArtAssets/Environments/HauntedHouse/Stage01_EntranceGate');
const art=path.join(root,'Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/Destructibles');
async function run(){
 const [background,floor,fence,motorcycle]=process.argv.slice(2);
 if(!motorcycle)throw new Error('Pass background, floor, fence sheet and motorcycle sheet.');
 fs.mkdirSync(path.join(__dirname,'Originals'),{recursive:true});
 for(const [key,file] of [['Background','Background/Stage01_EntranceGate_Background.png'],['Floor','Floor/Stage01_EntranceGate_Floor.png']]){
  const source=path.join(base,file),backup=path.join(__dirname,'Originals',key+'.png');
  if(!fs.existsSync(backup))fs.copyFileSync(source,backup);
 }
 // The generated plate includes foreground below the entrance. Keep its architectural
 // ground contact aligned with the separately authored floor by excluding those rows.
 fs.writeFileSync(path.join(base,'Background/Stage01_EntranceGate_Background.png'),await sharp(background).resize(720,360,{kernel:'nearest',fit:'fill'}).extract({left:0,top:0,width:720,height:320}).png().toBuffer());
 const extension=await sharp(floor).resize(720,480,{kernel:'nearest',fit:'fill'}).extract({left:0,top:240,width:720,height:240}).png().toBuffer();
 const original=path.join(__dirname,'Originals/Floor.png');
 const before=await sharp(original).ensureAlpha().raw().toBuffer();
 const lower=await sharp(extension).ensureAlpha().raw().toBuffer();
 const final=await sharp(Buffer.concat([before,lower]),{raw:{width:720,height:480,channels:4}}).png().toBuffer();
 const after=await sharp(final).extract({left:0,top:0,width:720,height:240}).ensureAlpha().raw().toBuffer();
 if(!before.equals(after))throw new Error('Original floor pixels changed.');
 fs.writeFileSync(path.join(base,'Floor/Stage01_EntranceGate_Floor.png'),final);
 const template=fs.readFileSync(path.join(art,'CardboardBox_Intact.png.meta'),'utf8');
 const names=['Intact','Damaged','Destroyed','Debris1','Debris2','Debris3'];
 for(const [name,sheet,width] of [['EntranceFence',fence,160],['EntranceMotorcycle',motorcycle,192]]){
  fs.copyFileSync(sheet,path.join(__dirname,name+'-generated.png'));
  const image=await sharp(sheet).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const cellW=image.info.width/3,cellH=image.info.height/2;
  const pieces=[];
  for(let i=0;i<6;i++){
   const cell=await sharp(sheet).extract({left:(i%3)*cellW,top:Math.floor(i/3)*cellH,width:cellW,height:cellH}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
   let left=cellW,top=cellH,right=0,bottom=0;
   for(let y=0;y<cellH;y++)for(let x=0;x<cellW;x++)if(cell.data[(y*cellW+x)*4+3]>96){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
   if(right<=left)throw new Error('Missing sprite in '+name+' '+i);
   pieces.push({cell,left,top,width:right-left+1,height:bottom-top+1});
  }
  const scale=Math.min((width-8)/Math.max(...pieces.slice(0,3).map(p=>p.width)),112/Math.max(...pieces.slice(0,3).map(p=>p.height)));
  for(let i=0;i<6;i++){
   const p=pieces[i],isDebris=i>=3,canvasW=isDebris?48:width,canvasH=isDebris?48:128;
   const ratio=isDebris?Math.min(40/p.width,40/p.height):scale;
   const w=Math.max(1,Math.round(p.width*ratio)),h=Math.max(1,Math.round(p.height*ratio));
   const input=await sharp(p.cell.data,{raw:p.cell.info}).extract({left:p.left,top:p.top,width:p.width,height:p.height}).resize(w,h,{kernel:'nearest'}).png().toBuffer();
   const output=path.join(art,name+'_'+names[i]+'.png');
   fs.writeFileSync(output,await sharp({create:{width:canvasW,height:canvasH,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input,left:Math.floor((canvasW-w)/2),top:isDebris?Math.floor((canvasH-h)/2):canvasH-8-h}]).png().toBuffer());
   if(!fs.existsSync(output+'.meta'))fs.writeFileSync(output+'.meta',template.replace(/^guid: \w+/m,'guid: '+crypto.randomBytes(16).toString('hex')).replace(/spriteID: \w+/g,'spriteID: '+crypto.randomBytes(16).toString('hex')).replace(/  spritePivot: .*/,'  spritePivot: {x: 0.5, y: '+(isDebris?.5:8/128)+'}'));
  }
 }
 // Short deterministic metallic clanks, used by both steel objects.
 for(const [name,duration] of [['MetalHit',.12],['MetalBreak',.45]]){
  const rate=44100,n=Math.floor(rate*duration),buffer=Buffer.alloc(44+n*2);
  buffer.write('RIFF',0);buffer.writeUInt32LE(36+n*2,4);buffer.write('WAVEfmt ',8);buffer.writeUInt32LE(16,16);buffer.writeUInt16LE(1,20);buffer.writeUInt16LE(1,22);buffer.writeUInt32LE(rate,24);buffer.writeUInt32LE(rate*2,28);buffer.writeUInt16LE(2,32);buffer.writeUInt16LE(16,34);buffer.write('data',36);buffer.writeUInt32LE(n*2,40);
  let seed=12345;
  for(let i=0;i<n;i++){const t=i/rate;seed=(Math.imul(seed,1664525)+1013904223)>>>0;const noise=seed/4294967296*2-1;const ring=Math.sin(t*2*Math.PI*437)+.6*Math.sin(t*2*Math.PI*1103)+.3*Math.sin(t*2*Math.PI*1877);const envelope=Math.exp(-t/(duration*.22));buffer.writeInt16LE(Math.round(Math.max(-1,Math.min(1,(ring*.24+noise*.18)*envelope))*26000),44+i*2);}
  fs.writeFileSync(path.join(art,name+'.wav'),buffer);
 }
 fs.writeFileSync(path.join(__dirname,'generation.json'),JSON.stringify({builtInImagegen:true,background,floor,fence,motorcycle,backgroundSize:[720,320],floorSize:[720,480],originalFloorPixelsPreserved:true},null,2));
 console.log('Entrance art and twelve transparent prop sprites packaged; original floor verified unchanged.');
}
run().catch(e=>{console.error(e);process.exitCode=1;});
