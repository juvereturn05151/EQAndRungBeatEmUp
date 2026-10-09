const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
async function pack(input,name){
 const meta=await sharp(input).metadata();
 const out='Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/Destructibles';
 fs.mkdirSync(out,{recursive:true});
 fs.copyFileSync(input,'Tools/Destructibles/'+name+'-generated.png');
 for(let i=0;i<6;i++){
  const w=Math.floor(meta.width/3),split=name==='CeramicDragonJar'?576:Math.floor(meta.height/2),h=i<3?split:meta.height-split;
  const {data,info}=await sharp(input).extract({left:(i%3)*w,top:i<3?0:split,width:w,height:h}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let x0=w,y0=h,x1=0,y1=0;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]>200){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
  const region=await sharp(data,{raw:{width:w,height:h,channels:4}}).extract({left:x0,top:y0,width:x1-x0+1,height:y1-y0+1}).resize({width:i<3?72:24,height:i<3?64:24,fit:'inside',kernel:'nearest'}).raw().toBuffer({resolveWithObject:true});
  for(let j=0;j<region.data.length;j+=4)region.data[j+3]=region.data[j+3]>200?255:0;
  const rw=region.info.width,rh=region.info.height;
  const target=Buffer.alloc(96*96*4);const left=Math.floor((96-rw)/2),top=i<3?88-rh:Math.floor((96-rh)/2);
  for(let y=0;y<rh;y++)region.data.copy(target,((top+y)*96+left)*4,y*rw*4,(y+1)*rw*4);
  const suffix=['Intact','Damaged','Destroyed','Debris1','Debris2','Debris3'][i];
  await sharp(target,{raw:{width:96,height:96,channels:4}}).png().toFile(path.join(out,name+'_'+suffix+'.png'));
 }
}
pack(process.argv[2],process.argv[3]).catch(e=>{console.error(e);process.exit(1)});
