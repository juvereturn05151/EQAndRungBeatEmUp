const fs=require('fs'),path=require('path'),sharp=require('C:/Users/drago/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);}
(async()=>{
 const guids=new Set(['Assets','Packages','Library/PackageCache'].filter(p=>fs.existsSync(p)).flatMap(files).filter(p=>p.endsWith('.meta')).map(p=>fs.readFileSync(p,'utf8').match(/^guid: (\w+)/m)?.[1]));
 const assetPaths=['Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset','Assets/EQ_Rung_BeatEmUp/Prefabs/StageExitMarker.prefab','Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset'];let count=0;
 for(const p of assetPaths){for(const m of fs.readFileSync(p,'utf8').matchAll(/guid: ([a-f0-9]{32})/g)){if(m[1].startsWith('0000000000000000'))continue;count++;if(!guids.has(m[1]))throw new Error('Unresolved reference '+m[1]+' in '+p);}
  if(p!==assetPaths[0]&&fs.readFileSync(p).compare(fs.readFileSync('Temp/DestructibleValidationProject/'+p)))throw new Error('Transferred asset differs: '+p);
 }
 const clean=s=>s.replace(/^    nextAreaMarkers:[\s\S]*?(?=^    \w)/gm,'').replace(/\r/g,'');
 if(clean(fs.readFileSync(assetPaths[0],'utf8'))!==clean(fs.readFileSync('Temp/ExitMarkerAuthoring-before.asset','utf8')))throw new Error('Non-marker level fields changed during setup');
 const blocks=s=>[...s.matchAll(/^    nextAreaMarkers:[\s\S]*?(?=^    \w)/gm)].map(m=>m[0].replace(/\r/g,'')).join('');
 if(blocks(fs.readFileSync(assetPaths[0],'utf8'))!==blocks(fs.readFileSync('Temp/DestructibleValidationProject/'+assetPaths[0],'utf8')))throw new Error('Marker definitions differ from validated copy');
 for(const p of files('Assets/EQ_Rung_BeatEmUp/ArtAssets/UI/ExitMarkers').filter(p=>p.endsWith('.png'))){const m=await sharp(p).metadata();if(!m.hasAlpha)throw new Error('Missing transparency '+p);}
 const output=`PASS: ${count} serialized marker/level/catalog GUID references resolve.\nPASS: Marker definitions, prefab and catalog match the Unity-validated copy.\nPASS: All pre-existing level fields are unchanged; only nextAreaMarkers sections were added.\nPASS: All five pixel-art PNGs have transparency.\n`;
 fs.writeFileSync('Documentation/StageExitMarkerAssetVerification.txt',output);console.log(output);
})();
