// Generated artwork export: crop, nearest scaling, palette mapping and alignment only.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'Assets/ArtAssets/Characters/BlueShirtGuy/ShadowDragon');
const source=path.join(out,'Source'); fs.mkdirSync(source,{recursive:true});
const inputs=[['Cast','exec-f1d3cda3-1da3-4eef-a0da-7135118d35ee.png',4,1],['Dragon','exec-db43f148-2d0c-489c-a60d-2524ca49c7fd.png',3,2]];
const generated='C:/Users/drago/.codex/generated_images/01a10a61-c340-7fc3-8e6a-6acc21ead110';
function bbox(p,w,h){let l=w,t=h,r=-1,b=-1;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(p[(y*w+x)*4+3]>=128){l=Math.min(l,x);t=Math.min(t,y);r=Math.max(r,x);b=Math.max(b,y);}return {left:l,top:t,width:r-l+1,height:b-t+1};}
(async()=>{
 const colors=new Map(); const sprites=path.join(root,'Assets/EQ_Rung_BeatEmUp/Sprites');
 for(const f of fs.readdirSync(sprites).filter(f=>/^BlueShirtGuy.*png$/.test(f))){const p=await sharp(path.join(sprites,f)).ensureAlpha().raw().toBuffer();for(let i=0;i<p.length;i+=4)if(p[i+3]===255)colors.set(`${p[i]},${p[i+1]},${p[i+2]}`,[p[i],p[i+1],p[i+2]]);}
 const palette=[...colors.values()],cache=new Map();
 function nearest(r,g,b){const key=(r<<16)|(g<<8)|b;if(cache.has(key))return cache.get(key);let best,d=Infinity;for(const c of palette){const v=(r-c[0])**2+(g-c[1])**2+(b-c[2])**2;if(v<d){best=c;d=v;}}cache.set(key,best);return best;}
 for(const [name,file,cols,rows] of inputs){const saved=path.join(source,name+'Generated.png');fs.copyFileSync(path.join(generated,file),saved);const m=await sharp(saved).metadata();const cw=Math.floor(m.width/cols),ch=Math.floor(m.height/rows),poses=[];
 for(let i=0;i<cols*rows;i++){const p=await sharp(saved).extract({left:i%cols*cw,top:Math.floor(i/cols)*ch,width:cw,height:ch}).ensureAlpha().raw().toBuffer();poses.push({p,b:bbox(p,cw,ch)});}
 const scale=name==='Cast'?106/poses[0].b.height:220/Math.max(...poses.map(p=>p.b.width));const frames=[];const W=name==='Cast'?160:256,H=name==='Cast'?128:112;
 for(let i=0;i<poses.length;i++){const {p,b}=poses[i],w=Math.round(b.width*scale),h=Math.round(b.height*scale);const pixels=await sharp(p,{raw:{width:cw,height:ch,channels:4}}).extract(b).resize(w,h,{kernel:'nearest'}).raw().toBuffer();let x0,y0;
 if(name==='Cast'){let l=w,r=0;for(let y=Math.floor(h*.86);y<h;y++)for(let x=0;x<w;x++)if(pixels[(y*w+x)*4+3]>=128){l=Math.min(l,x);r=Math.max(r,x);}x0=Math.round(80-(l+r)/2);y0=120-h;}else{x0=238-w;y0=Math.round(56-h/2);}
 if(x0<0||y0<0||x0+w>W||y0+h>H)throw Error('Inspect overflowing pose '+name+i);
 const canvas=Buffer.alloc(W*H*4);for(let y=0;y<h;y++)for(let x=0;x<w;x++){let k=(y*w+x)*4;if(pixels[k+3]<128)continue;let c=name==='Cast'?nearest(...pixels.subarray(k,k+3)):[pixels[k],pixels[k+1],pixels[k+2]];let dest=((y+y0)*W+x+x0)*4;canvas[dest]=c[0];canvas[dest+1]=c[1];canvas[dest+2]=c[2];canvas[dest+3]=255;}
 const target=path.join(out,`${name}_${String(i+1).padStart(2,'0')}.png`);await sharp(canvas,{raw:{width:W,height:H,channels:4}}).png().toFile(target);frames.push(canvas);console.log(target,w,h);}
 const sheet=Buffer.alloc(W*cols*H*rows*4);frames.forEach((p,i)=>{for(let y=0;y<H;y++)p.copy(sheet,((Math.floor(i/cols)*H+y)*W*cols+i%cols*W)*4,y*W*4,(y+1)*W*4);});await sharp(sheet,{raw:{width:W*cols,height:H*rows,channels:4}}).resize(W*cols*3,H*rows*3,{kernel:'nearest'}).png().toFile(path.join(source,name+'NativeReview.png'));
 }
})().catch(e=>{console.error(e);process.exitCode=1;});
