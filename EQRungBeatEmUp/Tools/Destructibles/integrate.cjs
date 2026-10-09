const fs=require('fs');
const file='Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset';
const from=fs.readFileSync('Temp/DestructibleValidationProject/'+file,'utf8');const current=fs.readFileSync(file,'utf8');
function stage(text){const start=text.indexOf('    stageId: Stage01_EntranceGate');const end=text.indexOf('\n  - maxActiveEnemies:',start);return {start,end:end<0?text.length:end};}
const a=stage(from),b=stage(current);const source=from.slice(a.start,a.end),target=current.slice(b.start,b.end);
const rx=/    destructibles:[\s\S]*?(?=    decorativeProps:)/;
const match=source.match(rx);if(!match||!target.match(rx))throw new Error('Stage destructible block not found');
fs.writeFileSync(file,current.slice(0,b.start)+target.replace(rx,match[0])+current.slice(b.end));
