const fs=require('fs'), path=require('path');
const root=path.resolve(__dirname,'../..');
function edit(file, replacements) {
 let text=fs.readFileSync(path.join(root,file),'utf8').replace(/\r\n/g,'\n');
 for(const [from,to] of replacements) { if(!text.includes(from)) throw new Error('Missing: '+from); text=text.split(from).join(to); }
 fs.writeFileSync(path.join(root,file),text);
}
edit('Assets/Editor/Combat/PunchPlaytestValidation.cs',[
 ['{ 23, 24, 33 }, first = { 6, 6, 9 }, last = { 10, 10, 14 }','{ 26, 27, 38 }, first = { 7, 7, 11 }, last = { 12, 12, 18 }'],
 ['i >= 11 && i <= (p == 0 ? 17 : 19)','i >= 13 && i <= (p == 0 ? 20 : 22)'],
 ['Step(18); player.RequestAttack();','Step(21); player.RequestAttack();'],
 ['Step(11)','Step(13)'],['Step(20); player.Request','Step(23); player.Request'],
 ['Step(23); Check(player.CurrentAttack == punches[1]','Step(26); Check(player.CurrentAttack == punches[1]'],
 ['Step(7); player.RequestAttack(); Step(3);','Step(9); player.RequestAttack(); Step(3);'],
 ['Step(32)','Step(37)'],['Step(5); player.RequestAttack(); Step(6);','Step(7); player.RequestAttack(); Step(6);'],
 ['new[] { 11, 19 }','new[] { 13, 22 }'],['i < 55','i < 64'],
 ['Step(17); player.RequestAttack();','Step(20); player.RequestAttack();'],['Step(19); player.RequestAttack();','Step(22); player.RequestAttack();'],
 ['Step(6); player.RequestLauncher(); Step(5);','Step(8); player.RequestLauncher(); Step(5);'],
 ['player.RequestAttack(); Step(6); player.RequestAttack(); Step(3);','player.RequestAttack(); Step(7); player.RequestAttack(); Step(3);'],
 ['player.attackPlayer.CurrentFrame == 6','player.attackPlayer.CurrentFrame == 7'],
 ['Step(5);\n        Check(player.CurrentAttack == punches[1], "Hitstop-buffered','Step(6);\n        Check(player.CurrentAttack == punches[1], "Hitstop-buffered'],
 ['Step(6); Step(4); Step(5); player.RequestAttack(); Step(9); Step(6); Step(33);','Step(7); Step(4); Step(6); player.RequestAttack(); Step(11); Step(6); Step(38);'],
 ['frame 11','frame 13'],['frame 17','frame 20'],['frame 19','frame 22'],
]);
edit('Assets/Editor/Combat/AirPunchPlaytestValidation.cs',[
 ['{ 20, 21, 30 }, first = { 5, 5, 8 }, last = { 8, 8, 13 }','{ 23, 24, 36 }, first = { 6, 6, 10 }, last = { 10, 10, 17 }'],
 ['i >= 8 && i <= (p == 0 ? 16 : 17)','i >= 10 && i <= (p == 0 ? 19 : 20)'],
 ['Step(5); player.RequestAttack(); Step(2);','Step(7); player.RequestAttack(); Step(2);'],
 ['Step(8); player.RequestAttack();','Step(10); player.RequestAttack();'],
 ['Step(30); player.RequestAttack();','Step(36); player.RequestAttack();'],
 ['Step(p == 0 ? 16 : 17)','Step(p == 0 ? 19 : 20)'],['frame 8','frame 10'],
]);
