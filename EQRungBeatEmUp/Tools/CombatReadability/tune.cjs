// Run once against the previous authored timings; refuses to retune already changed assets.
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../..');
const sound = 'Assets/Deadly Kombat Free version/';
const effects = 'Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/';
const rows = [
 ['Punch1',23,6,10,1,1,1,'punch_short_whoosh_16','body_hit_small_11','CFXR Hit D 3D (Yellow)',.12,.4],
 ['Punch2',24,6,10,1,1,1,'punch_short_whoosh_30','body_hit_large_32','CFXR Hit A (Red)',.14,.4],
 ['Punch3',33,9,14,2,2,1,'punch_long_whoosh_21','body_hit_finisher_23','CFXR Hit A (Red)',.20,.5],
 ['AirPunch1',20,5,8,1,1,1,'punch_short_whoosh_16','face_hit_small_13','CFXR Hit D 3D (Yellow)',.12,.4],
 ['AirPunch2',21,5,8,1,1,1,'punch_short_whoosh_30','body_hit_large_44','CFXR Hit A (Red)',.14,.4],
 ['AirPunch3',30,8,13,2,2,2,'punch_long_whoosh_30','body_hit_finisher_52','CFXR Impact Glowing HDR (Blue)',.18,.5],
];
const guid = file => fs.readFileSync(path.join(root,file+'.meta'),'utf8').match(/guid: (\w+)/)[1];
function prefabID(file) {
 const parts=fs.readFileSync(path.join(root,file),'utf8').split(/(?=--- !u!)/);
 return parts.find(p=>p.startsWith('--- !u!4 ') && p.includes('m_Father: {fileID: 0}')).match(/m_GameObject: \{fileID: (\d+)\}/)[1];
}
function hold(frame) {
 return frame.replace(/movement: \{[^\n]+/, 'movement: {x: 0, y: 0}')
 .replace(/setHorizontalVelocity: 1/g,'setHorizontalVelocity: 0').replace(/setVerticalVelocity: 1/g,'setVerticalVelocity: 0')
 .replace(/verticalVelocityModifier: [^\n]+/, 'verticalVelocityModifier: 0')
 .replace(/canCancelInto(Attack|Launcher|Jump): 1/g,'canCancelInto$1: 0').replace(/events: \[\]/,'events: []');
}
for (const [name,total,first,last,startHold,activeHold,recoveryHold,swing,impact,vfx,scale,lifetime] of rows) {
 const file=path.join(root,'Assets/EQ_Rung_BeatEmUp/Attacks/'+name+'.asset');
 const text=fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n');
 const split=text.indexOf('  frames:\n'), header=text.slice(0,split);
 const frames=text.slice(split+10).match(/  - sprite:[\s\S]*?(?=  - sprite:|$)/g);
 if(frames.length!==total) throw new Error(name+' already tuned or changed: '+frames.length);
 const result=[];
 for(let i=0;i<frames.length;i++) {
  if(i===first) for(let n=0;n<startHold;n++) result.push(hold(frames[i-1]));
  if(i===last) for(let n=0;n<activeHold;n++) result.push(hold(frames[i]));
  let f=frames[i];
  // Swing begins as the strike pose first becomes active, including on misses.
  if(i===first) f=f.replace('events: []','events:\n    - Swing');
  result.push(f);
  if(i===last+1) for(let n=0;n<recoveryHold;n++) {
   // Recovery holds retain cancel permissions so the chain remains responsive.
   result.push(frames[i].replace(/movement: \{[^\n]+/,'movement: {x: 0, y: 0}'));
  }
 }
 const vfxPath=effects+vfx+'.prefab';
 const feedback=`  feedback:\n    swingSound: {fileID: 8300000, guid: ${guid(sound+swing+'.wav')}, type: 3}\n    impactSound: {fileID: 8300000, guid: ${guid(sound+impact+'.wav')}, type: 3}\n    swingVolume: ${name.endsWith('3')?.42:.32}\n    impactVolume: ${name.endsWith('3')?.65:.5}\n    impactPrefab: {fileID: ${prefabID(vfxPath)}, guid: ${guid(vfxPath)}, type: 3}\n    impactScale: ${scale}\n    impactLifetime: ${lifetime}\n`;
 const notes='Readable anticipation/contact/follow-through holds at 60 FPS. Existing damage, hitstop, movement, sprites and routes preserved. Swing event on first active; impact cues only on confirmed hits.';
 fs.writeFileSync(file,header.replace(/  artworkNotes: [^\n]+/,'  artworkNotes: '+notes)+feedback+'  frames:\n'+result.join(''));
 console.log(name+': '+total+' -> '+result.length+'; active '+(first+startHold)+'-'+(last+startHold+activeHold));
}
