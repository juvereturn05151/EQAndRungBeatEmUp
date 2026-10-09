const fs=require('fs'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir='Assets/EQ_Rung_BeatEmUp/ArtAssets/UI/ExitMarkers';fs.mkdirSync(dir,{recursive:true});
const ink=[34,27,24,255],amber=[195,156,66,255],bone=[255,229,139,255];
async function save(name,w,h,draw){const b=Buffer.alloc(w*h*4);const p=(x,y,c)=>{if(x>=0&&x<w&&y>=0&&y<h)b.set(c,(y*w+x)*4);};draw(p);await sharp(b,{raw:{width:w,height:h,channels:4}}).png().toFile(dir+'/'+name+'.png');}
(async()=>{
 for(let f=0;f<3;f++)await save('ExitChevron_'+String(f+1).padStart(2,'0'),32,32,p=>{
  for(const cy of [8,18])for(let x=5;x<=26;x++)for(let t=0;t<5;t++){const y=cy+Math.min(x-5,26-x)+t;p(x,y,t===0||t===4||x===5||x===26?ink:t===1?bone:amber);}
  if(f!==1)for(const [x,y] of [[2,15],[29,11],[15,2]]){p(x,y,ink);p(x,y-1,bone);p(x-1,y,amber);p(x+1,y,amber);}
 });
 await save('ExitGround',48,16,p=>{for(let x=5;x<43;x++){if(x%8<6){p(x,4,ink);p(x,5,amber);p(x,11,ink);p(x,10,bone);}}for(let y=6;y<=9;y++){p(4,y,ink);p(5,y,bone);p(43,y,ink);p(42,y,amber);}for(let x=20;x<=27;x++)p(x,8,amber);});
 const font={N:['10001','11001','10101','10011','10001','10001','10001'],E:['11111','10000','10000','11110','10000','10000','11111'],X:['10001','10001','01010','00100','01010','10001','10001'],T:['11111','00100','00100','00100','00100','00100','00100']};
 await save('ExitNEXT',32,12,p=>{for(let n=0;n<4;n++)for(let y=0;y<7;y++)for(let x=0;x<5;x++)if(font['NEXT'[n]][y][x]==='1'){const px=4+n*6+x,py=2+y;for(let dx=-1;dx<=1;dx++)for(let dy=-1;dy<=1;dy++)p(px+dx,py+dy,ink);}for(let n=0;n<4;n++)for(let y=0;y<7;y++)for(let x=0;x<5;x++)if(font['NEXT'[n]][y][x]==='1')p(4+n*6+x,2+y,y<3?bone:amber);});
})();
