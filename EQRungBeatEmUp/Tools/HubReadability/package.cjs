const fs=require('fs'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const input=process.argv[2],out='Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/HubInteractables';
(async()=>{
 const m=await sharp(input).metadata(),splitX=Math.floor(m.width/2),splitY=572;
 fs.copyFileSync(input,'Tools/HubReadability/Stations-generated.png');
 const names=['CharacterWardrobe','StatBlessingGong','SkillFlameShrine','WorldEntranceGate'];
 const heights=[132,110,132,176],widths=[112,128,112,136];
 for(let i=0;i<4;i++){
  const w=i%2?m.width-splitX:splitX,h=i<2?splitY:m.height-splitY;
  const {data}=await sharp(input).extract({left:i%2?splitX:0,top:i<2?0:splitY,width:w,height:h}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let x0=w,y0=h,x1=0,y1=0;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]>200){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
  const tile=await sharp(data,{raw:{width:w,height:h,channels:4}}).extract({left:x0,top:y0,width:x1-x0+1,height:y1-y0+1}).resize({width:widths[i],height:heights[i],fit:'inside',kernel:'nearest'}).raw().toBuffer({resolveWithObject:true});
  for(let j=0;j<tile.data.length;j+=4)tile.data[j+3]=tile.data[j+3]>200?255:0;
  const rw=tile.info.width,rh=tile.info.height,b=Buffer.alloc(160*208*4),left=Math.floor((160-rw)/2),top=200-rh;
  for(let y=0;y<rh;y++)tile.data.copy(b,((top+y)*160+left)*4,y*rw*4,(y+1)*rw*4);
  await sharp(b,{raw:{width:160,height:208,channels:4}}).png().toFile(out+'/'+names[i]+'.png');
 }
})();
