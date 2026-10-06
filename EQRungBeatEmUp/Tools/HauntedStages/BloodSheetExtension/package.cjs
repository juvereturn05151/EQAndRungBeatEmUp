// ImageGen paints the extension. This packages it while retaining all original lower pixels.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const base=path.join(root,'Assets/ArtAssets/Environments/HauntedHouse/Stage02_BloodSheetCorridor');
const original=path.join(base,'Background/Stage02_BloodSheetCorridor_Background.png');
const generated=process.argv[2];
if(!generated) throw new Error('Pass generated extension image path');
const target=path.join(base,'Background/Stage02_BloodSheetCorridor_Background_v2.png');
const id=()=>crypto.randomBytes(16).toString('hex');
async function run() {
 const top=await sharp(generated).resize(720,240,{kernel:'nearest',fit:'fill'}).extract({left:0,top:0,width:720,height:80}).png().toBuffer();
 await sharp({create:{width:720,height:240,channels:4,background:'#100c0c'}}).composite([{input:top,left:0,top:0},{input:original,left:0,top:80}]).png().toFile(target);
 if(!fs.existsSync(target+'.meta')) fs.writeFileSync(target+'.meta',fs.readFileSync(original+'.meta','utf8').replace(/^guid: \w+/m,'guid: '+id()).replace(/spriteID: \w+/,'spriteID: '+id()));
 const oldGuid=fs.readFileSync(original+'.meta','utf8').match(/^guid: (\w+)/m)[1];
 const newGuid=fs.readFileSync(target+'.meta','utf8').match(/^guid: (\w+)/m)[1];
 const levelFile=path.join(root,'Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset');
 let level=fs.readFileSync(levelFile,'utf8');
 const block=level.match(/  - stageId: Stage02_BloodSheet[^\n]*\n[\s\S]*?(?=  - stageId:|$)/)[0];
 const oldHeight=Number(block.match(/    backgroundHeight: ([^\n]+)/)[1]);
 const oldCenter=Number(block.match(/    backgroundCenterY: ([^\n]+)/)[1]);
 if(!block.includes(oldGuid)) throw new Error('Background reference already revised or unexpected');
 const height=oldHeight*1.5,center=oldCenter+(height-oldHeight)*.5;
 const next=block.replace(oldGuid,newGuid).replace(/    backgroundHeight: [^\n]+/,'    backgroundHeight: '+Number(height.toFixed(6))).replace(/    backgroundCenterY: [^\n]+/,'    backgroundCenterY: '+Number(center.toFixed(6)));
 fs.writeFileSync(levelFile,level.replace(block,next));
 const composite=path.join(__dirname,'Stage02_BloodSheetCorridor_Composition_v2.png');
 await sharp({create:{width:720,height:480,channels:4,background:'#100c0c'}}).composite([{input:target,left:0,top:0},{input:path.join(base,'Floor/Stage02_BloodSheetCorridor_Floor.png'),left:0,top:240}]).png().toFile(composite);
 fs.writeFileSync(path.join(__dirname,'generation.json'),JSON.stringify({generated,background:target,canvas:[720,240],addedTopPixels:80,preservedLowerPixels:160,oldHeight,oldCenter,newHeight:height,newCenter:center,lowerSeam:oldCenter-oldHeight*.5,builtInImagegen:true},null,2));
 console.log(JSON.stringify({height,center,seam:oldCenter-oldHeight*.5,newGuid}));
}
run().catch(e=>{console.error(e);process.exitCode=1;});
