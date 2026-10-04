const sharp = require('sharp');
const fs = require('fs');
async function scan(file) {
  const {data,info} = await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const {width:w,height:h}=info, seen=new Uint8Array(w*h), queue=new Int32Array(w*h), blobs=[];
  for(let start=0;start<w*h;start++) {
    if(seen[start] || data[start*4+3]<128) continue;
    let head=0,tail=1,x0=w,y0=h,x1=0,y1=0; queue[0]=start;seen[start]=1;
    while(head<tail) {
      const p=queue[head++], x=p%w,y=Math.floor(p/w);
      x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);
      for(let dy=-1;dy<=1;dy++) for(let dx=-1;dx<=1;dx++) {
        const xx=x+dx,yy=y+dy;
        if(xx<0||xx>=w||yy<0||yy>=h) continue;
        const q=yy*w+xx;
        if(seen[q]||data[q*4+3]<128) continue;
        seen[q]=1;queue[tail++]=q;
      }
    }
    if(tail>=200 && x1-x0>=15 && y1-y0>=15) blobs.push({left:x0,top:y0,width:x1-x0+1,height:y1-y0+1,area:tail});
  }
  return {data,info,blobs};
}
module.exports=scan;
if(require.main===module) {
  (async()=>{for(const j of JSON.parse(fs.readFileSync(__dirname+'/generation.json'))) {
    const r=await scan(j.source),rows=Array.from({length:7},()=>[]);
    for(const b of r.blobs) if(b.area>3000) rows[Math.max(0,Math.min(6,Math.round((b.top+b.height)/r.info.height*7)-1))].push(b);
    console.log(j.key,r.info.width,r.info.height,rows.map(a=>a.length).join(','));
    fs.writeFileSync(__dirname+'/'+j.key+'-bounds.json',JSON.stringify({width:r.info.width,height:r.info.height,rows:rows.map(a=>a.sort((a,b)=>a.left-b.left))},null,2));
  }})();
}
