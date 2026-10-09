const fs=require('fs'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
async function bounds(input){const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});let x0=info.width,y0=info.height,x1=0,y1=0;for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>200){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}return {data,info,x0,y0,x1,y1};}
(async()=>{
 const input=process.argv[2],name=process.argv[3],ref=process.argv[4],out=process.argv[5];
 const m=await sharp(input).metadata(),w=Math.floor(m.width/4),h=Math.floor(m.height/2),tiles=[];
 const reference=await bounds(ref);const targetHeight=Math.round((reference.y1-reference.y0+1)*.9);
 for(let i=0;i<8;i++){const cell=await sharp(input).extract({left:(i%4)*w,top:Math.floor(i/4)*h,width:w,height:h}).png().toBuffer();tiles.push(await bounds(cell));}
 const maxHeight=Math.max(...tiles.map(t=>t.y1-t.y0+1));const scale=targetHeight/maxHeight;
 fs.mkdirSync(out,{recursive:true});fs.copyFileSync(input,'Tools/PlayerRun/'+name+'-generated.png');
 for(let i=0;i<8;i++){
  const t=tiles[i],tile=await sharp(t.data,{raw:{width:w,height:h,channels:4}}).extract({left:t.x0,top:t.y0,width:t.x1-t.x0+1,height:t.y1-t.y0+1}).resize({width:Math.round((t.x1-t.x0+1)*scale),height:Math.round((t.y1-t.y0+1)*scale),kernel:'nearest'}).raw().toBuffer({resolveWithObject:true});
  for(let j=0;j<tile.data.length;j+=4)tile.data[j+3]=tile.data[j+3]>200?255:0;
  const rw=tile.info.width,rh=tile.info.height,b=Buffer.alloc(160*144*4),left=Math.floor((160-rw)/2),top=136-rh-([3,7].includes(i)?3:0);
  for(let y=0;y<rh;y++)tile.data.copy(b,((top+y)*160+left)*4,y*rw*4,(y+1)*rw*4);
  await sharp(b,{raw:{width:160,height:144,channels:4}}).png().toFile(out+'/'+name+'_Run_'+String(i+1).padStart(2,'0')+'.png');
 }
 console.log(name+' fixed-scale reference target height '+targetHeight+'px, scale '+scale.toFixed(4));
})();
