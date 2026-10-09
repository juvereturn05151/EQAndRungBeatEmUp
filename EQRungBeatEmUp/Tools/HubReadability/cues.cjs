const fs=require('fs'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out='Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/HubInteractables';
(async()=>{
 for(const [name,w,h] of [['GroundCue',96,24],['InteractDiamond',16,16]]){
  const b=Buffer.alloc(w*h*4);
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){
   const r=name==='GroundCue'?Math.sqrt(((x-(w-1)/2)/(w*.46))**2+((y-(h-1)/2)/(h*.4))**2):Math.abs(x-7.5)+Math.abs(y-7.5);
   const edge=name==='GroundCue'?r>.84&&r<1:r<6&&r>3;
   if(edge){const j=(y*w+x)*4;b[j]=190;b[j+1]=255;b[j+2]=235;b[j+3]=255;}
   if(name==='InteractDiamond'&&r<2){const j=(y*w+x)*4;b[j]=255;b[j+1]=225;b[j+2]=136;b[j+3]=255;}
  }
  await sharp(b,{raw:{width:w,height:h,channels:4}}).png().toFile(out+'/'+name+'.png');
 }
})();
