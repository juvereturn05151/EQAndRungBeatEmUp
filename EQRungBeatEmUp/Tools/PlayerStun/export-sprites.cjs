// Generated art export: crop, nearest-neighbor resize, palette mapping and foot alignment.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'Assets/ArtAssets/Characters/BlueShirtGuy/Stun');
const source=path.join(out,'Source');fs.mkdirSync(source,{recursive:true});
const generated='C:/Users/drago/.codex/generated_images/01a10a61-c340-7fc3-8e6a-6acc21ead110';
const inputs=[['Stun','exec-9d9a4ae9-40b4-4c80-92d7-f4e5e4dce345.png',5,1],['StunVfx','exec-710a691f-56f7-429a-a524-d1a6996cb855.png',3,2]];
function bbox(p,w,h){let left=w,top=h,right=-1,bottom=-1;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(p[(y*w+x)*4+3]>=128){left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);}return{left,top,width:right-left+1,height:bottom-top+1};}
(async()=>{
 const reference=await sharp(path.join(root,'Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_Idle2_01.png')).ensureAlpha().raw().toBuffer();
 const colors=new Map();for(let i=0;i<reference.length;i+=4)if(reference[i+3]===255)colors.set(`${reference[i]},${reference[i+1]},${reference[i+2]}`,[reference[i],reference[i+1],reference[i+2]]);
 const palette=[...colors.values()],cache=new Map();
 function nearest(r,g,b){let key=(r<<16)|(g<<8)|b;if(cache.has(key))return cache.get(key);let best,d=Infinity;for(const c of palette){let distance=(r-c[0])**2+(g-c[1])**2+(b-c[2])**2;if(distance<d){d=distance;best=c;}}cache.set(key,best);return best;}
 const sheets=[];
 for(const [name,file,cols,rows] of inputs){
  const saved=path.join(source,name+'Generated.png');fs.copyFileSync(path.join(generated,file),saved);const m=await sharp(saved).metadata(),cw=Math.floor(m.width/cols),ch=Math.floor(m.height/rows),poses=[];
  for(let i=0;i<cols*rows;i++){const p=await sharp(saved).extract({left:i%cols*cw,top:Math.floor(i/cols)*ch,width:cw,height:ch}).ensureAlpha().raw().toBuffer();poses.push({p,b:bbox(p,cw,ch)});}
  const scale=name==='Stun'?106/poses[0].b.height:56/Math.max(...poses.map(p=>p.b.width));const W=name==='Stun'?128:64,H=name==='Stun'?128:32,frames=[];
  for(let i=0;i<poses.length;i++){
   const {p,b}=poses[i],w=Math.round(b.width*scale),h=Math.round(b.height*scale);
   const pixels=await sharp(p,{raw:{width:cw,height:ch,channels:4}}).extract(b).resize(w,h,{kernel:'nearest'}).raw().toBuffer();let x0,y0;
   if(name==='Stun'){let left=w,right=0;for(let y=Math.floor(h*.88);y<h;y++)for(let x=0;x<w;x++)if(pixels[(y*w+x)*4+3]>=128){left=Math.min(left,x);right=Math.max(right,x);}x0=Math.round(64-(left+right)/2);y0=120-h;}
   else{x0=Math.round((W-w)/2);y0=Math.round((H-h)/2);}
   if(x0<0||y0<0||x0+w>W||y0+h>H)throw Error('Overflow '+name+i);
   const canvas=Buffer.alloc(W*H*4);
   for(let y=0;y<h;y++)for(let x=0;x<w;x++){let k=(y*w+x)*4;if(pixels[k+3]<128)continue;let color=name==='Stun'?nearest(...pixels.subarray(k,k+3)):[pixels[k],pixels[k+1],pixels[k+2]];let dest=((y+y0)*W+x+x0)*4;canvas[dest]=color[0];canvas[dest+1]=color[1];canvas[dest+2]=color[2];canvas[dest+3]=255;}
   await sharp(canvas,{raw:{width:W,height:H,channels:4}}).png().toFile(path.join(out,`${name}_${String(i+1).padStart(2,'0')}.png`));frames.push(canvas);
   console.log(name,i+1,'native bounds',w,h,'baseline',y0+h);
  }
  const sheet=Buffer.alloc(W*frames.length*H*4);frames.forEach((p,i)=>{for(let y=0;y<H;y++)p.copy(sheet,(y*W*frames.length+i*W)*4,y*W*4,(y+1)*W*4);});
  const review=path.join(source,name+'NativeReview.png');await sharp(sheet,{raw:{width:W*frames.length,height:H,channels:4}}).resize(W*frames.length*3,H*3,{kernel:'nearest'}).png().toFile(review);sheets.push(review);
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
