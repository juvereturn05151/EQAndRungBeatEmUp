# GrayShirtGuy polish art provenance

Generated with the built-in imagegen tool, using transparent output. Project files are copied from the generated originals; originals remain under `C:/Users/drago/.codex/generated_images/01a10f9a-cc2d-77d3-afe8-b653e73869e9`.

## Base redraw

Output: `exec-01034f75-77b5-4935-8f20-18dcc00e1710.png`, retained as `Assets/ArtAssets/Characters/Character2/Character2_PolishedSource.png`.

Inputs: existing `Character2_NativeSheet.png` as edit target and `BlueShirtGuy_Idle2_01.png` as style reference.

Prompt:

> Edit target image 1: redraw and polish the exact 8 by 8 game sprite sheet, 64 distinct separated full-body poses in identical row/column order. Image 2 style reference: authentic crisp low-resolution arcade pixel art, dark clean outline, restrained palette, hard pixel clusters, not painterly dithering. Identity across every frame: stocky ordinary Thai man, black side-parted hair, clearly readable black rectangular glasses, gray tee, navy knee-length shorts, blue slides, relaxed casual non-fighter. Improve anatomical consistency and face. All poses face RIGHT, same scale, baseline and 128px equivalent cell. Transparent background, NO text/grid/ground shadows, generous spacing, no cropping. Rows: 1 four casual idle, four walking; 2 four Punch1, four Punch2; 3 four Punch3 plus four uppercut launcher; 4 two jump, three AirPunch1, three AirPunch2; 5 four AirPunch3, four AirDive; 6 three dodge, two guard, two parry, one grabbed; 7 two hit, two stun, two knockdown, two getting up; 8 SIX WAND CAST poses (hand reaches pocket, pulls short wooden wand, raises wand, casts wand, wand held at barrier release, relaxed recovery) then two death. Change Punch3 row3 first four to anticipation then a clearly serpentine ghostly jade SNAKE emerging from scalp and lunging horizontally RIGHT, then retracting; no normal punch at its impact. Snake attached to scalp, clear snake head and coiled neck, not dragon. No elephant/guardian. Body height uniform upright 106 logical pixels. Maintain unique proper action silhouettes. Save a usable transparent sprite sheet.

The generator omitted the second parry drawing. Packaging explicitly shares the first polished parry drawing between the two existing parry slots. The native game sheet retains the original timings; this is recorded in the packaging script rather than silently shifting later rows.

## Walk cycle

Output: `exec-53de0176-1b71-4223-b005-6a4ecb0c37c0.png`, retained as `Assets/ArtAssets/Characters/Character2/Character2_WalkSource.png`.

Input: generated base redraw, as identity/style reference.

Prompt:

> Use this sheet ONLY as character identity/style reference. Produce a NEW 12-frame continuous WALK CYCLE sprite sheet, 4 columns by 3 rows, order left-to-right then down. SAME stocky GrayShirtGuy with black hair, black rectangular glasses, gray tee, navy shorts, blue slides. Crisp authentic game pixel art dark outline limited palette. RIGHT facing three-quarter view. All 12 poses uniform body/head size, ground baseline, angle. Casual ordinary relaxed walking, arms hanging with gentle alternating swing, no fighting fists. Frame1 left foot forward contact; 2 left weight/down rearheel lifts; 3 trailing leg swings underbody; 4 passing feetclose; 5 right knee advances and lefttoe pushes; 6 approaching right contact; 7 right foot forward contact; 8 right weight/down rearheel lifts; 9 other trailingleg swings underbody; 10 opposite passingfeetclose; 11 left knee advances and righttoe pushes; 12 approaching originalleftcontact. Intermediate feet positions must clearly bridge contact extremes, NO repeated stride copies. Restrained 1-2 logical pixel body bob/weightshift, planted sandals, natural non-floaty. Transparent background NO text/grid/shadows/effects. 12 isolated full-body sprites in equal generously spaced cells. Same compact resolution and finished palette as reference.

Packaging orders generated poses `1,2,3,4,6,5,7,8,9,10,12,11` so the closer-foot drawings bridge the wide contact poses. No interpolated blends are used.

## Barrier effects

Output: `exec-7714472e-5d5f-472b-870b-3425d79cd011.png`, retained as `Assets/ArtAssets/Characters/Character2/Barrier_Source.png`.

Prompt:

> Production PIXEL ART game VFX sprite sheet, transparent background, exact 4 columns x 3 rows, 12 equal 256x256 logical cells, no text. GrayShirtGuy's wand barrier spell: magical protective spherical/cylindrical surrounding shield, teal jade/cyan with small warmgold wand-sparks, clean darkedge/pixelclusters, restrained palette, no blurred bloom, no elephant or guardian or creatures, NO character, NO wand in effect. Each centered effect same outer dimensions scale, surrounds an EMPTY CENTRAL SPACE where caster stands. Row1 four barrier emergence frames: faint ground ellipse with upward arcs, rising curved sides, half dome, complete transparent dome. Row2 four barrier continuation frames: fully formed barrier lattice with high central empty space, strong protective shell with groundellipse, luminous shell opening outward, fading shell. Row3 four AREA PULSE frames: small source ring, medium outward ring, large maximum impact ring, fading outward ring. Viewed side-on beatemup three-quarter walkingplane: ground circle is horizontal ellipse, barrier rises vertically around source with curved sides and dome. Bottom ring nearbottomcell, bodyarea empty, never opaque solid blob. Symmetric centered silhouettes, clear separation between all12 sprites. Circle can read as circular around caster. Avoid flat frontal disk, side-projectile, summoning a figure, random explosions, logos. Strong original arcane barrier effect fits crisp 128px character sprites; clean limited pixelart detail.

## Packaging

`Tools/Character2/prepare_polish.py` performs sprite extraction, nearest-neighbor scaling, uniform body scale and feet anchoring. It retains detached snake/wand/star fragments, preserves original Unity metadata/GUIDs and archives replaced art. `polish_base_manifest.json` and `polish_walk_manifest.json` record source regions and scale. Effect cells retain generated transparency.

Unity import and prefab/AttackData migration are implemented by `Character2PolishSetup.cs`. This utility updates only the character presentation assets and preserves existing tuned attack/skill mechanics apart from the authored Punch 3 hitbox geometry.
