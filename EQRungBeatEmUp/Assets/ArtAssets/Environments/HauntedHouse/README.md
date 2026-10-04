# Haunted-house stage environment package

Eight fixed room segments, drawn with the built-in ImageGen tool from the supplied Thai haunted-house reference. No enemy characters, UI, stage captions, infographic arrows, or poster framing are part of the gameplay plates. Physical Thai signs and painted theatrical ghost flats are set dressing.

## Inventory

| Stage folder / filename stem | Background | Floor |
|---|---|---|
| Stage01_EntranceGate | Demon-mouth facade, ticket booth, bulbs, fair fencing, motorcycle, distant temple/ferris wheel | Wet fair pavement |
| Stage02_BloodSheetCorridor | Blood-painted hanging sheets, cloth partitions, rear door, bare bulbs | Grimy corridor concrete |
| Stage03_FakeMorgue | Metal cabinets, covered mannequin props, medical junk, Thai sign, cold fluorescent light | Dirty clinical tiles |
| Stage04_ServiceCorridor | Wooden stair, stored props, shelves, tools, wiring | Worn backstage work floor |
| Stage05_HauntedMaze | Doorways, theatrical ghost flats, cloth, eerie colored lights | Continuous indoor pathway |
| Stage06_RecoveryShrine | Buddha altar, candles, offerings, first aid supplies, water | Calm shrine-room floor |
| Stage07_WhiteGhostBossChamber | Cursed shrine, portraits, curtains, candles, ritual dressing | Open arena with flat ritual markings |
| Stage08_EscapeLane | Exit sign, rear fences, lights, distant temple fair | Quiet wet exit pavement |

Every folder has `Background/<Stem>_Background.png`, `Floor/<Stem>_Floor.png`, `<Stem>_Preview.png`, `Source/<Stem>_Source.png`, `manifest.json`, and `Unity/<Stem>_ArtPreview.prefab`. The two gameplay plates are separate files and separate SpriteRenderers. `Gallery.html` displays assembled views or individual layers; `StageOverview.png` compares all eight stages in sequence, left to right and top to bottom.

## Import and placement

Gameplay sprites use 100 PPU, Point filtering, no mipmaps, no texture compression, Clamp wrapping, and centered pivots. Backgrounds are 720 × 160; floors are 720 × 240. Together they form 720 × 400 scenery with an upper 40% background and lower 60% floor. These opaque rectangular plates fill their respective bands; transparency is unnecessary. Original generated plates remain in Source. Exporting uses nearest-neighbor sampling and splits at each drawing's actual rear floor edge before normalizing the two bands.

At scale 1, each segment is 7.2 world units wide and 4 high, matching the existing camera's orthographic size 2 and approximately 16:9 view. Preview prefabs place Background at `(0, 2.56, 0)` and Floor at `(0, 0.56, 0)`, giving a shared seam at Y 1.76 and total vertical range [-0.64, 3.36]. Sorting orders -100 and -90 keep scenery behind actors. The art preview prefabs contain no camera, enemies, collision, navigation, pickups, or scene transitions.

Use the preview prefab as an authoring starting point. The existing camera `StageFraming.floor` field resizes its assigned placeholder renderer at runtime: keep these scenery renderers independent of that field to avoid stretching the authored floor. Existing gameplay scenes and that camera component were preserved. A visual floor is not a collision surface; retain the motor's existing lane bounds or author a level-specific range.

These are authored room plates, not seamless tiles. Longer scrolling stages require additional adjoining segments or custom overlap/seam work. Wider aspect ratios and camera movement can expose horizontal plate edges; extend the scene art or constrain a room camera rather than stretching pixels. Upward air-combat camera expansion may also require extra scenery above the plate. The provided assets prioritize the standard rest composition.

## Props, reuse, and polish

No new independently placeable props were drawn in this task; furnishings are baked into Background. Existing enemy-package cursed totem Idle/Damaged/Destroyed sprites can be placed independently in the boss room. The player/enemy artwork was used to guide resolution and world scale; no existing character sprite was changed. The existing sprite import convention was reused. There was no existing drawn stage plate set to extend; the combat demo used a flat placeholder floor.

Before final production, proofread/redraw practical Thai sign lettering, simplify any noisy reflected-light clusters, refine perspective seams, and hand-polish wall/floor joins if extending a room. The generated stage images are consistent prototype assets, not a complete modular tileset. No optional foreground occluders were added, keeping the fight lane clear.

## Validation and provenance

`ArtValidation.json` records the eight room manifests and 16 plate dimensions. `UnityImportValidationResults.txt` records actual Unity import settings, SpriteRenderer references, exact plate seam/framing, and eight prefab checks. Rebuild preview prefabs with **Beat Em Up → Art → Build and validate haunted stage previews**. Generation prompts and original output provenance are in `Tools/HauntedStages/generation.json`; the poster and request are preserved in that tool folder's Reference directory. Re-export with `Tools/HauntedStages/package.cjs` using Node and Sharp; original outputs are retained locally as a fallback.
