const fs=require('fs'),path=require('path'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const paths=['Assets/EQ_Rung_BeatEmUp/Characters/BlueShirtGuy.asset','Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.asset','Assets/EQ_Rung_BeatEmUp/Characters/BlueShirtGuy_Run.asset','Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_Run.asset','Assets/EQ_Rung_BeatEmUp/Animations/BlueShirtGuy_Run.anim','Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_Run.anim','Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab','Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.prefab','Assets/EQ_Rung_BeatEmUp/Animations/BlueShirtGuy.controller','Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_Locomotion.controller','Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset'];
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);}
(async()=>{
 const guids=new Set(['Assets','Packages','Library/PackageCache'].filter(p=>fs.existsSync(p)).flatMap(files).filter(p=>p.endsWith('.meta')).map(p=>fs.readFileSync(p,'utf8').match(/^guid: (\w+)/m)?.[1]));let refs=0;
 for(const p of paths){for(const m of fs.readFileSync(p,'utf8').matchAll(/guid: ([a-f0-9]{32})/g)){if(m[1].startsWith('0000000000000000'))continue;refs++;if(!guids.has(m[1]))throw new Error('Unresolved '+m[1]+' in '+p);}
  if(fs.readFileSync(p).compare(fs.readFileSync('Temp/DestructibleValidationProject/'+p)))throw new Error('Validated asset changed after transfer: '+p);
 }
 const roots=['Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/BlueShirtGuy/Animations/Run','Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Character2/Run'];
 for(const root of roots)for(const p of files(root).filter(p=>p.endsWith('.png'))){
  const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==160||info.height!==144)throw new Error('Canvas mismatch '+p);
  for(let i=3;i<data.length;i+=4)if(data[i]!==0&&data[i]!==255)throw new Error('Soft alpha '+p);
  if(fs.readFileSync(p).compare(fs.readFileSync('Temp/DestructibleValidationProject/'+p)))throw new Error('Sprite changed after validation '+p);
 }
 const result=`PASS: ${refs} serialized GUID references resolve in transferred run assets.\nPASS: All 11 wired assets match the Unity-validated project copy.\nPASS: Both eight-frame sprite sets have 160 x 144 canvases and crisp binary alpha; all match validated sprites.\n`;
 fs.writeFileSync('Documentation/PlayerRunAssetVerification.txt',result);console.log(result);
})();
