const fs = require('fs'), path = require('path'), crypto = require('crypto'), sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const dir = path.join(root, 'Assets/ArtAssets/Characters/BlueShirtGuy/AirCombo3');
const source = process.argv[2];
if (!source) throw new Error('Pass the generated 3x2 transparent sheet path.');
const names = ['AirReady', 'HandsTogether', 'OverheadWindup', 'SmashStart', 'Impact', 'Recovery'];
const starts = [0, 3, 5, 8, 11, 14], lengths = [3, 2, 3, 3, 3, 16];
const template = fs.readFileSync(path.join(root, 'Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_AirAttack1_10.png.meta'), 'utf8');
const guid = () => crypto.randomBytes(16).toString('hex');
function meta(file, sprite = true, folder = false) {
  if (fs.existsSync(file + '.meta')) return fs.readFileSync(file + '.meta', 'utf8').match(/^guid: (.+)$/m)[1];
  const id = guid();
  const text = folder ? `fileFormatVersion: 2\nguid: ${id}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n`
    : template.replace(/^guid: .+$/m, 'guid: ' + id).replace(/spriteID: [a-f0-9]+/, 'spriteID: ' + guid()).replace('spriteGenerateFallbackPhysicsShape: 1', 'spriteGenerateFallbackPhysicsShape: 0')
      .replace('spriteMode: 1', 'spriteMode: ' + (sprite ? 1 : 0)).replace('textureType: 8', 'textureType: ' + (sprite ? 8 : 0));
  fs.writeFileSync(file + '.meta', text); return id;
}
(async () => {
  fs.mkdirSync(dir, {recursive: true}); meta(dir, false, true);
  const sourceFile = path.join(dir, 'Source.png'); fs.copyFileSync(source, sourceFile); meta(sourceFile, false);
  const info = await sharp(sourceFile).metadata();
  if (info.width % 3 || info.height % 2 || info.width / 3 !== info.height / 2) throw new Error('Expected equal square cells in a 3x2 sheet.');
  const size = info.width / 3, frames = [], ids = [];
  for (let i = 0; i < 6; i++) {
    const pixels = await sharp(sourceFile).extract({left: (i % 3) * size, top: Math.floor(i / 3) * size, width: size, height: size})
      .resize(128, 128, {kernel: 'nearest'}).ensureAlpha().raw().toBuffer();
    // Native sprite import convention: crisp binary alpha, no soft halo surrounding the character.
    let occupied = 0;
    for (let p = 0; p < pixels.length; p += 4) {
      if (pixels[p+3] < 128) pixels.fill(0, p, p+4);
      else { pixels[p+3] = 255; occupied++; }
    }
    if (occupied < 800) throw new Error('Empty/incomplete generated pose: ' + names[i]);
    const file = path.join(dir, 'BlueShirtGuy_AirCombo3_' + names[i] + '.png');
    await sharp(pixels, {raw: {width: 128, height: 128, channels: 4}}).png().toFile(file);
    ids.push(meta(file)); frames.push(pixels);
  }
  const sheet = Buffer.alloc(384 * 256 * 4);
  for (let i = 0; i < 6; i++) for (let y = 0; y < 128; y++)
    frames[i].copy(sheet, ((Math.floor(i/3)*128+y)*384 + (i%3)*128)*4, y*512, (y+1)*512);
  const sheetFile = path.join(dir, 'NativeSheet.png');
  await sharp(sheet, {raw: {width: 384, height: 256, channels: 4}}).png().toFile(sheetFile); meta(sheetFile, false);
  const review = path.join(dir, 'Review.png');
  await sharp(sheet, {raw: {width: 384, height: 256, channels: 4}}).resize(1152,768,{kernel:'nearest'}).png().toFile(review); meta(review, false);
  const timeline = Buffer.alloc(128*128*30*4);
  for (let i = 0; i < 6; i++) for (let f = starts[i]; f < starts[i]+lengths[i]; f++) frames[i].copy(timeline, f*128*128*4);
  const animation = path.join(dir, 'AirCombo3_Preview.gif');
  await sharp(timeline, {raw: {width:128,height:128*30,channels:4,pageHeight:128}}).gif({delay:Array(30).fill(17),loop:0}).toFile(animation);
  meta(animation, false);
  const assetFile = path.join(root, 'Assets/EQ_Rung_BeatEmUp/Attacks/AirPunch3.asset');
  let asset = fs.readFileSync(assetFile,'utf8'), index = 0;
  if ((asset.match(/^  - sprite:/gm)||[]).length !== 30) throw new Error('Expected current 30-frame AirPunch3.');
  asset = asset.replace(/^  - sprite: .+$/gm, () => {
    const frame = index++; let pose = 0; while (pose < 5 && frame >= starts[pose+1]) pose++;
    return `  - sprite: {fileID: 21300000, guid: ${ids[pose]}, type: 3}`;
  });
  asset = asset.replace(/^  artworkNotes:.*$/m, '  artworkNotes: Redrawn six-pose two-handed overhead air smash. 30-frame readability timing, active frames 8-13, hitbox and bounce settings preserved.');
  fs.writeFileSync(assetFile,asset);
  fs.writeFileSync(path.join(__dirname,'generation.json'),JSON.stringify({mode:'built-in image_gen edit',source,canvas:[128,128],pixelsPerUnit:100,pivot:[.5,.0625],poses:names.map((name,i)=>({name,start:starts[i],length:lengths[i],guid:ids[i]}))},null,2));
  console.log('Packaged six dedicated native sprites, sheet, animated preview and updated only AirPunch3 sprite references.');
})().catch(error => {console.error(error); process.exitCode = 1;});
