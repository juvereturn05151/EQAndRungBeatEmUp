const fs=require('fs'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
(async()=>{
 const roots=['Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/BlueShirtGuy/Animations/Run','Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Character2/Run'];
 const names=['BlueShirtGuy','Character2'];fs.mkdirSync('Documentation/PlayerRunPreview',{recursive:true});
 const cells=[],animated=[];
 for(let row=0;row<2;row++)for(let i=0;i<8;i++){
  const path=roots[row]+'/'+names[row]+'_Run_'+String(i+1).padStart(2,'0')+'.png';
  const cell=await sharp({create:{width:160,height:144,channels:4,background:'#383947'}}).composite([{input:path}]).png().toBuffer();
  cells.push({input:cell,left:i*160,top:row*144});
 }
 await sharp({create:{width:1280,height:288,channels:4,background:'#383947'}}).composite(cells).resize(2560,576,{kernel:'nearest'}).png().toFile('Documentation/PlayerRunPreview/RunContactSheet.png');
 for(let i=0;i<8;i++){
  const scene=await sharp({create:{width:360,height:156,channels:4,background:'#383947'}}).composite([{input:cells[i].input,left:10,top:0},{input:cells[8+i].input,left:190,top:0}]).resize(720,312,{kernel:'nearest'}).raw().toBuffer();animated.push(scene);
 }
 await sharp(Buffer.concat(animated),{raw:{width:720,height:312*8,channels:4,pageHeight:312}}).gif({delay:Array(8).fill(50),loop:0}).toFile('Documentation/PlayerRunPreview/RunLoops.gif');
})();
