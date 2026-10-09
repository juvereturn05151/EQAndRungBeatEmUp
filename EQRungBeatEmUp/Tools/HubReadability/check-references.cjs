const fs=require('fs'),path=require('path');
const guids=new Set();function walk(p){for(const e of fs.readdirSync(p,{withFileTypes:true})){const f=path.join(p,e.name);if(e.isDirectory())walk(f);else if(f.endsWith('.meta')){const m=fs.readFileSync(f,'utf8').match(/^guid: ([a-f0-9]+)/m);if(m)guids.add(m[1]);}}}walk('Assets');
let refs=0;const files=['Assets/EQ_Rung_BeatEmUp/Hub/SanctuaryEnvironment.prefab',...fs.readdirSync('Assets/EQ_Rung_BeatEmUp/Prefabs/HubInteractables').filter(p=>p.endsWith('.prefab')).map(p=>'Assets/EQ_Rung_BeatEmUp/Prefabs/HubInteractables/'+p)];
for(const f of files)for(const m of fs.readFileSync(f,'utf8').matchAll(/guid: ([a-f0-9]{32})/g)){if(!m[1].startsWith('0000000000000000')&&!guids.has(m[1]))throw new Error('Missing asset reference '+m[1]+' in '+f);refs++;}
console.log('PASS: '+refs+' final workspace prefab references resolve.');
