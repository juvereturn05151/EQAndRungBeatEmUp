// ImageGen paints the upper scenery; packaging retains the original gameplay pixels.
const fs = require('fs'), path = require('path'), sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const art = path.join(root, 'Assets/EQ_Rung_BeatEmUp/ArtAssets/Environments/PlayerSanctuary');
async function run() {
  const inputs = process.argv.slice(2);
  if (inputs.length !== 5) throw new Error('Supply five ImageGen output paths in panel order.');
  const records = [];
  fs.mkdirSync(path.join(__dirname, 'Originals'), { recursive: true });
  for (let i = 0; i < 5; i++) {
    const target = path.join(art, `HubPanel${i}.png`);
    const original = path.join(__dirname, 'Originals', `HubPanel${i}.png`);
    if (!fs.existsSync(original)) fs.copyFileSync(target, original);
    const info = await sharp(original).metadata();
    if (info.width !== 1672 || info.height !== 941) throw new Error('Unexpected source dimensions.');
    const top = await sharp(inputs[i]).resize(1672, 1672, { kernel: 'nearest', fit: 'fill' })
      .extract({ left: 0, top: 0, width: 1672, height: 731 }).png().toBuffer();
    const final = await sharp({ create: { width: 1672, height: 1672, channels: 3, background: '#382859' } })
      .composite([{ input: top, left: 0, top: 0 }, { input: original, left: 0, top: 731 }]).png().toBuffer();
    const before = await sharp(original).removeAlpha().raw().toBuffer();
    const after = await sharp(final).extract({ left: 0, top: 731, width: 1672, height: 941 }).removeAlpha().raw().toBuffer();
    if (!before.equals(after)) throw new Error(`Panel ${i} lower pixels changed.`);
    fs.writeFileSync(target, final);
    let meta = fs.readFileSync(target + '.meta', 'utf8');
    meta = meta.replace(/  alignment: \d+/, '  alignment: 9')
      .replace(/  spritePivot: \{x: [^,]+, y: [^}]+\}/, `  spritePivot: {x: 0.5, y: ${941 / (2 * 1672)}}`);
    fs.writeFileSync(target + '.meta', meta);
    records.push({ panel: i, generated: inputs[i], dimensions: [1672, 1672], addedTopPixels: 731,
      unchangedLowerPixels: true, original: path.relative(root, original), pivotY: 941 / (2 * 1672),
      worldBottom: -.8, worldTop: 7.3 });
  }
  fs.writeFileSync(path.join(__dirname, 'generation.json'), JSON.stringify({ builtInImagegen: true, panels: records }, null, 2));
  console.log(JSON.stringify(records));
}
run().catch(e => { console.error(e); process.exitCode = 1; });
