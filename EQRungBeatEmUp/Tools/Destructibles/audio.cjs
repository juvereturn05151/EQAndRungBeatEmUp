const fs=require('fs');
const out='Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/Destructibles';
let seed=814;function noise(){seed=(seed*1664525+1013904223)>>>0;return seed/4294967296*2-1;}
for(const [name,ceramic,broken,duration] of [['CardboardHit',false,false,.12],['CardboardBreak',false,true,.45],['CeramicHit',true,false,.18],['CeramicBreak',true,true,.65]]){
 const rate=22050,n=Math.floor(rate*duration),b=Buffer.alloc(44+n*2);
 b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(rate,24);b.writeUInt32LE(rate*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(n*2,40);
 let low=0;
 for(let i=0;i<n;i++){
  const t=i/rate,env=Math.exp(-t/(broken?.13:.035));const z=noise();low=low*.65+z*.35;
  let v=ceramic?(.24*Math.sin(2*Math.PI*1380*t)*Math.exp(-t*24)+.18*Math.sin(2*Math.PI*2271*t)*Math.exp(-t*34)+z*.25*env):(.65*low+.25*Math.sin(2*Math.PI*95*t))*env;
  if(broken){const interval=ceramic?.062:.047,local=t%interval;v+=noise()*.16*Math.exp(-local*110)*Math.exp(-t*4);}
  b.writeInt16LE(Math.round(Math.max(-1,Math.min(1,v))*.8*32767),44+i*2);
 }
 fs.writeFileSync(out+'/'+name+'.wav',b);
}
